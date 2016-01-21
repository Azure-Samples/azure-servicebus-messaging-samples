//   
//   Copyright © Microsoft Corporation, All Rights Reserved
// 
//   Licensed under the Apache License, Version 2.0 (the "License"); 
//   you may not use this file except in compliance with the License. 
//   You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0 
// 
//   THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
//   OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION
//   ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A
//   PARTICULAR PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
// 
//   See the Apache License, Version 2.0 for the specific language
//   governing permissions and limitations under the License. 

namespace Microsoft.ServiceBus.Samples.DurableSender
{
    using System;
    using System.Messaging;
    using System.Threading;
    using System.Transactions;
    using MessagingSamples;
    using Microsoft.ServiceBus.Messaging;

    public class DurableSender : IDisposable
    {
        const long WaitTimeAfterServiceBusReturnsAnIntermittentErrorInSeconds = 5;
        readonly MessagingFactory messagingFactory;
        readonly MessageQueue msmqDeadletterQueue;
        readonly string msmqDeadletterQueueName;
        readonly MessageQueue msmqQueue;
        readonly string msmqQueueName;
        readonly QueueClient queueClient;
        readonly string serviceBusQueueName;
        Timer waitAfterErrorTimer;

        public DurableSender(MessagingFactory messagingFactory, string serviceBusQueueName)
        {
            this.messagingFactory = messagingFactory;
            this.serviceBusQueueName = serviceBusQueueName;

            // Create a Service Bus queue client to send messages to the Service Bus queue.
            this.queueClient = this.messagingFactory.CreateQueueClient(this.serviceBusQueueName);

            // Create MSMQ queue if it doesn't exit. If it does, open the existing MSMQ queue.
            this.msmqQueueName = MsmqHelper.CreateMsmqQueueName(serviceBusQueueName, "SEND");
            this.msmqQueue = MsmqHelper.GetMsmqQueue(this.msmqQueueName);

            // Create MSMQ deadletter queue if it doesn't exit. If it does, open the existing MSMQ deadletter queue.
            this.msmqDeadletterQueueName = MsmqHelper.CreateMsmqQueueName(serviceBusQueueName, "SEND_DEADLETTER");
            this.msmqDeadletterQueue = MsmqHelper.GetMsmqQueue(this.msmqDeadletterQueueName);

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

        public void Send(BrokeredMessage brokeredMessage)
        {
            var msmqMessage = MsmqHelper.PackServiceBusMessageIntoMsmqMessage(brokeredMessage);
            this.SendtoMsmq(this.msmqQueue, msmqMessage);
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
            this.msmqQueue.BeginPeek(TimeSpan.FromSeconds(60), null, this.MsmqOnPeekComplete);
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
                    this.MsmqPeekBegin();
                    return;
                }
            }

            if (msmqMessage != null)
            {
                var brokeredMessage = MsmqHelper.UnpackServiceBusMessageFromMsmqMessage(msmqMessage);
                // Clone Service Bus message in case we need to deadletter it.
                var serviceBusDeadletterMessage = brokeredMessage.Clone();

                Console.WriteLine("DurableSender: Enqueue message {0} into Service Bus.", msmqMessage.Label);
                switch (SendMessageToServiceBus(brokeredMessage))
                {
                    case SendResult.Success: // Message was successfully sent to Service Bus. Remove MSMQ message from MSMQ queue.
                        Console.WriteLine("DurableSender: Service Bus send operation completed.");
                        this.msmqQueue.BeginReceive(TimeSpan.FromSeconds(60), null, this.MsmqOnReceiveComplete);
                        break;
                    case SendResult.WaitAndRetry: // Service Bus is temporarily unavailable. Wait.
                        Console.WriteLine("DurableSender: Service Bus is temporarily unavailable.");
                        this.waitAfterErrorTimer = new Timer(
                            this.ResumeSendingMessagesToServiceBus,
                            null,
                            WaitTimeAfterServiceBusReturnsAnIntermittentErrorInSeconds*1000,
                            Timeout.Infinite);
                        break;
                    case SendResult.PermanentFailure: // Permanent error. Deadletter MSMQ message.
                        Console.WriteLine("DurableSender: Permanent error when sending message to Service Bus. Deadletter message.");
                        var msmqDeadletterMessage = MsmqHelper.PackServiceBusMessageIntoMsmqMessage(serviceBusDeadletterMessage);
                        try
                        {
                            SendtoMsmq(this.msmqDeadletterQueue, msmqDeadletterMessage);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                "DurableSender: Failure when sending message {0} to deadletter queue {1}: {2} {3}",
                                msmqDeadletterMessage.Label,
                                msmqDeadletterQueue.FormatName,
                                ex.GetType(),
                                ex.Message);
                        }
                        this.msmqQueue.BeginReceive(TimeSpan.FromSeconds(60), null, this.MsmqOnReceiveComplete);
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
        SendResult SendMessageToServiceBus(BrokeredMessage brokeredMessage)
        {
            try
            {
                this.queueClient.Send(brokeredMessage); // Use synchonous send to preserve message ordering.

                return SendResult.Success;
            }
            catch (MessagingException ex)
            {
                if (ex.IsTransient)
                {
                    Console.WriteLine(
                        "DurableSender: Transient exception when sending message {0}: {1} {2}",
                        brokeredMessage.Label,
                        ex.GetType(),
                        ex.Message);
                    return SendResult.WaitAndRetry;
                }
                Console.WriteLine(
                    "DurableSender: Permanent exception when sending message {0}: {1} {2}",
                    brokeredMessage.Label,
                    ex.GetType(),
                    ex.Message);
                return SendResult.PermanentFailure;
            }

            catch (Exception ex)
            {
                var exceptionType = ex.GetType();
                if (exceptionType == typeof (TimeoutException))
                {
                    Console.WriteLine("DurableSender: Exception: {0}", exceptionType);
                    return SendResult.WaitAndRetry;
                }
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

        // This method is called when timer expires.
        void ResumeSendingMessagesToServiceBus(Object stateInfo)
        {
            Console.WriteLine("DurableSender: Resume peeking MSMQ messages.");
            this.MsmqPeekBegin();
        }

        enum SendResult
        {
            Success,
            WaitAndRetry,
            PermanentFailure
        };
    }
}