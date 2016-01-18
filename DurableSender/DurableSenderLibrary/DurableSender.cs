//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure Platform AppFabric SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples.DurableSender
{
    using System;
    using System.Messaging;
    using System.Threading;
    using System.Transactions;
    using Microsoft.ServiceBus.Messaging;

    public class DurableSender : IDisposable
    {
        const long WaitTimeAfterServiceBusReturnsAnIntermittentErrorInSeconds = 5;
        const bool enableFaultInjection = true;

        MessagingFactory messagingFactory;
        QueueClient queueClient;
        MessageQueue msmqQueue;
        MessageQueue msmqDeadletterQueue;
        string sbusQueueName;
        string msmqQueueName;
        string msmqDeadletterQueueName;
        Timer waitAfterErrorTimer;

        // FOR TESTING PURPOSE ONLY.
        FaultInjector faultInjector;

        enum SendResult {Success, WaitAndRetry, PermanentFailure};

        public DurableSender(MessagingFactory messagingFactory, string sbusQueueName)
        {
            this.messagingFactory = messagingFactory;
            this.sbusQueueName = sbusQueueName;

            // Create a Service Bus queue client to send messages to the Service Bus queue.
            this.queueClient = this.messagingFactory.CreateQueueClient(this.sbusQueueName);

            // Create MSMQ queue if it doesn't exit. If it does, open the existing MSMQ queue.
            this.msmqQueueName = MsmqHelper.CreateMsmqQueueName(sbusQueueName, "SEND");
            this.msmqQueue = MsmqHelper.GetMsmqQueue(this.msmqQueueName);

            // Create MSMQ deadletter queue if it doesn't exit. If it does, open the existing MSMQ deadletter queue.
            this.msmqDeadletterQueueName = MsmqHelper.CreateMsmqQueueName(sbusQueueName, "SEND_DEADLETTER");
            this.msmqDeadletterQueue = MsmqHelper.GetMsmqQueue(this.msmqDeadletterQueueName);

            // FOR TESTING PURPOSE ONLY.
            this.faultInjector = new FaultInjector(enableFaultInjection);

            // Start receiving messages from the MSMQ queue.
            MsmqPeekBegin();
        }

        public void Dispose()
        {
            // Don't delete MSMQ queues in production. We want to preserve messages across process restarts.
            Console.WriteLine("DurableSender: Deleting MSMQ queue {0} ...\n", this.msmqQueueName);
            MessageQueue.Delete(this.msmqQueueName);
            Console.WriteLine("DurableSender: Deleting MSMQ queue {0} ...\n", this.msmqDeadletterQueueName);
            MessageQueue.Delete(this.msmqDeadletterQueueName);
            this.queueClient.Close();
            GC.SuppressFinalize(this);
        }

        public void Send(BrokeredMessage sbusMessage)
        {
            Message msmqMessage = MsmqHelper.PackSbusMessageIntoMsmqMessage(sbusMessage);
            SendtoMsmq(this.msmqQueue, msmqMessage);
        }

        void SendtoMsmq(MessageQueue msmqQueue, Message msmqMessage)
        {
            if (Transaction.Current == null)
            {
                msmqQueue.Send(msmqMessage, MessageQueueTransactionType.Single);
            }
            else
            {
                msmqQueue.Send(msmqMessage, MessageQueueTransactionType.Automatic);
            }
        }

        void MsmqPeekBegin()
        {
            this.msmqQueue.BeginPeek(TimeSpan.FromSeconds(60), null, MsmqOnPeekComplete);
        }

        void MsmqOnPeekComplete(IAsyncResult result)
        {
            // Complete the MSMQ peek operation. If a timeout occured, peek again.
            Message msmqMessage = null;
            try
            {
                msmqMessage = this.msmqQueue.EndPeek(result);
            }
            catch (MessageQueueException ex)
            {
                if (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                {
                    MsmqPeekBegin();
                    return;
                }
            }

            if (msmqMessage != null)
            {
                BrokeredMessage sbusMessage = MsmqHelper.UnpackSbusMessageFromMsmqMessage(msmqMessage);
                // Clone Service Bus message in case we need to deadletter it.
                BrokeredMessage sbusDeadletterMessage = CloneBrokeredMessage(sbusMessage);

                Console.WriteLine("DurableSender: Enqueue message {0} into Service Bus.", msmqMessage.Label);
                switch (SendMessageToServiceBus(sbusMessage))
                {
                    case SendResult.Success: // Message was successfully sent to Service Bus. Remove MSMQ message from MSMQ queue.
                        Console.WriteLine("DurableSender: Service Bus send operation completed.");
                        this.msmqQueue.BeginReceive(TimeSpan.FromSeconds(60), null, MsmqOnReceiveComplete);
                        break;
                    case SendResult.WaitAndRetry: // Service Bus is temporarily unavailable. Wait.
                        Console.WriteLine("DurableSender: Service Bus is temporarily unavailable.");
                        waitAfterErrorTimer = new Timer(ResumeSendingMessagesToServiceBus, null, WaitTimeAfterServiceBusReturnsAnIntermittentErrorInSeconds * 1000, Timeout.Infinite);
                        break;
                    case SendResult.PermanentFailure: // Permanent error. Deadletter MSMQ message.
                        Console.WriteLine("DurableSender: Permanent error when sending message to Service Bus. Deadletter message.");
                        Message msmqDeadletterMessage = MsmqHelper.PackSbusMessageIntoMsmqMessage(sbusDeadletterMessage);
                        try
                        {
                            SendtoMsmq(this.msmqDeadletterQueue, msmqDeadletterMessage);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("DurableSender: Failure when sending message {0} to deadletter queue {1}: {2} {3}",
                                msmqDeadletterMessage.Label, msmqDeadletterQueue.FormatName, ex.GetType(), ex.Message);
                        }
                        this.msmqQueue.BeginReceive(TimeSpan.FromSeconds(60), null, MsmqOnReceiveComplete);
                        break;
                }
            }
        }

        void MsmqOnReceiveComplete(IAsyncResult result)
        {
            this.msmqQueue.EndReceive(result);
            Console.WriteLine("DurableSender: MSMQ receive operation completed.");
            MsmqPeekBegin();
        }
        
        // Send message to Service Bus.
        SendResult SendMessageToServiceBus(BrokeredMessage sbusMessage)
        {
            try
            {
                // FOR TESTING PURPOSE ONLY: Inject Service Bus error. 
                faultInjector.InjectFaultBeforeSendingMessageToServiceBus();

                this.queueClient.Send(sbusMessage); // Use synchonous send to preserve message ordering.

                // FOR TESTING PURPOSE ONLY: Inject Service Bus error. 
                faultInjector.InjectFaultAfterSendingMessageToServiceBus();

                return SendResult.Success;
            }
            catch (MessagingException ex)
            {
                if (ex.IsTransient)
                {
                    Console.WriteLine("DurableSender: Transient exception when sending message {0}: {1} {2}", sbusMessage.Label, ex.GetType(), ex.Message);
                    return SendResult.WaitAndRetry;
                }
                else
                {
                    Console.WriteLine("DurableSender: Permanent exception when sending message {0}: {1} {2}", sbusMessage.Label, ex.GetType(), ex.Message);
                    return SendResult.PermanentFailure;
                }
            }

            catch (Exception ex)
            {
                Type exceptionType = ex.GetType();
                if (exceptionType == typeof(TimeoutException))
                {
                    Console.WriteLine("DurableSender: Exception: {0}", exceptionType);
                    return SendResult.WaitAndRetry;
                }
                else
                {
                    // Indicate a permanent failure in case of:
                    //  - ArgumentException
                    //  - ArgumentNullException
                    //  - ArgumentOutOfRangeException
                    //  - InvalidOperationException
                    //  - OperationCanceledException
                    //  - TransactionException
                    //  - TransactionInDoubtException
                    //  - TransactionSizeExceededException
                    //  - UnauthorizedAccessException
                    Console.WriteLine("DurableSender: Exception: {0}", exceptionType);
                    return SendResult.PermanentFailure;
                }
            }
        }

        // This method is called when timer expires.
        void ResumeSendingMessagesToServiceBus(Object stateInfo)
        {
            Console.WriteLine("DurableSender: Resume peeking MSMQ messages.");
            MsmqPeekBegin();
        }

        // Clone BrokeredMessage including system properties. Older versions of Microsoft.ServiceBus.dll
        // only clone the body of the message. Newer versions clone all message properties.
        BrokeredMessage CloneBrokeredMessage(BrokeredMessage source)
        {
            BrokeredMessage destination = source.Clone();
            //destination.ContentType = source.ContentType;
            //destination.CorrelationId = source.CorrelationId;
            //destination.Label = source.Label;
            //destination.MessageId = source.MessageId;
            //destination.ReplyTo = source.ReplyTo;
            //destination.ReplyToSessionId = source.ReplyToSessionId;
            //destination.ScheduledEnqueueTimeUtc = source.ScheduledEnqueueTimeUtc;
            //destination.SessionId = source.SessionId;
            //destination.TimeToLive = source.TimeToLive;
            //destination.To = source.To;

            return destination;
        }
    }
}
