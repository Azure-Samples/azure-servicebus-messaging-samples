//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure Platform SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples.MsmqServiceBusBridge
{
    using System;
    using System.Diagnostics;
    using System.Messaging;
    using System.Threading;
    using System.Transactions;
    using Microsoft.ServiceBus.Messaging;

    public class Bridge
    {
        static MessagingFactory messagingFactory = null;

        public static void Main()
        {
            Console.WriteLine("Process name: " + Process.GetCurrentProcess().ProcessName);

            messagingFactory = Helper.GetMessagingFactory();

            var sendMsmqToSbusThreadStart = new ThreadStart(SendMsmqToSbus);
            var sendMsmqToSbusThread = new Thread(sendMsmqToSbusThreadStart);
            sendMsmqToSbusThread.Start();

            var sendSbusToMsmqThreadStart = new ThreadStart(SendSbusToMsmq);
            var sendSbusToMsmqThread = new Thread(sendSbusToMsmqThreadStart);
            sendSbusToMsmqThread.Start();

            sendMsmqToSbusThread.Join();
            sendSbusToMsmqThread.Join();
        }

        private static void SendMsmqToSbus()
        {
            // Create Service Bus communication object to send to Service Bus Queue
            QueueClient sbSendToQueueClient = Helper.GetServiceBusQueueClient(messagingFactory, Constants.ServiceBusSendQueue);

            // Create System.Messaging communication object to receive from Msmq Queue
            MessageQueue receiveFromMsmqQueue = Helper.OpenMsmqQueue(Constants.MsmqSendQueue, true);

            while (true)
            {
                try
                {
                    using (MessageQueueTransaction msmqTransaction = new MessageQueueTransaction())
                    {
                        msmqTransaction.Begin();

                        // Receive message from MSMQ.
                        System.Messaging.Message msmqMessage = receiveFromMsmqQueue.Receive(msmqTransaction);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(
                            string.Format("BRIDGE -- Received message from {0}: {1}", Constants.MsmqSendQueue, msmqMessage.Label));
                        Console.ResetColor();

                        // Creating Service Bus message.
                        var sbusMessage = new BrokeredMessage((string)msmqMessage.Body);
                        sbusMessage.Label = msmqMessage.Label;
                        sbusMessage.TimeToLive = TimeSpan.FromSeconds(60);

                        // Send Service Bus message.
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(
                            string.Format("BRIDGE -- Sending message to {0}: {1}", Constants.ServiceBusSendQueue, sbusMessage.Label));
                        Console.ResetColor();
                        sbSendToQueueClient.Send(sbusMessage);

                        msmqTransaction.Commit();
                    }
                }
                catch (MessageQueueException)
                {
                    // In this scenario (infinite receive), we can receive a MessageQueueException when the MSMQ queues are deleted during cleanup
                    return;
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Exception received: " + exception.ToString());
                    throw;
                }
            }
        }

        private static void SendSbusToMsmq()
        {
            // Create Service Bus communication object to receive to Service Bus Queue
            QueueClient sbReceiveFromQueueClient = Helper.GetServiceBusQueueClient(messagingFactory, Constants.ServiceBusReceiveQueue, ReceiveMode.PeekLock);

            // Create System.Messaging communication object to send to Msmq Queue
            MessageQueue sendToMsmqQueue = Helper.OpenMsmqQueue(Constants.MsmqReceiveQueue, true);
            
            while (true)
            {
                System.Messaging.Message msmqmessage = null;
                try
                {
                    // Receive the BrokeredMessage from Service Bus Queue
                    using (TransactionScope scope = new TransactionScope())
                    {
                        BrokeredMessage message = sbReceiveFromQueueClient.Receive();
                        if (message != null)
                        {
                            // Complete the peek-locked message
                            message.Complete();

                            string brokeredMessageLabel = message.Label;
                            string brokeredMessageBody = message.GetBody<string>();

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(
                                string.Format("BRIDGE -- Received message from {0}: {1}:{2}", 
                                Constants.ServiceBusReceiveQueue, brokeredMessageLabel, brokeredMessageBody));
                            Console.ResetColor();

                            msmqmessage = new System.Messaging.Message();
                            msmqmessage.Body = brokeredMessageBody;
                            msmqmessage.Label = brokeredMessageLabel;
                            msmqmessage.Formatter = new BinaryMessageFormatter();

                            // Send the Brokered-to-MSMQ message created above to an MSMQ queue
                            if (msmqmessage != null)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine(
                                    string.Format("BRIDGE -- Sending message to {0}: {1}:{2}", 
                                    Constants.MsmqReceiveQueue, msmqmessage.Label, msmqmessage.Body));
                                Console.ResetColor();

                                using (var tx = new MessageQueueTransaction())
                                {
                                    tx.Begin();

                                    try
                                    {
                                        sendToMsmqQueue.Send(msmqmessage, tx);
                                        tx.Commit();
                                    }
                                    catch (MessageQueueException messageQueueException)
                                    {
                                        Console.WriteLine("Msmq exception received: " + messageQueueException.ToString());
                                        throw;
                                    }
                                }
                            }
                        }

                        scope.Complete();
                    }
                }
                catch (MessagingException)
                {
                    // In this scenario (infinite receive), we can receive a MessagingException when the Service Bus queues are deleted during cleanup
                    return;
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Exception received: " + exception.ToString());
                    throw;
                }
            }
        }
    }
}
