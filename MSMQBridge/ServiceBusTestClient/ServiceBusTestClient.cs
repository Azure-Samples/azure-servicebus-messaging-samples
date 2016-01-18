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
    using System.Threading;
    using System.Transactions;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    public class ServiceBusTestClient
    {
        public static void Main()
        {
            Console.WriteLine("Process name: " + Process.GetCurrentProcess().ProcessName);

            MessagingFactory messagingFactory = Helper.GetMessagingFactory();

            // Create service bus communication objects to send/receive from the service bus queues.
            QueueClient sbReceiveFromQueueClient = Helper.GetServiceBusQueueClient(messagingFactory, Constants.ServiceBusSendQueue, ReceiveMode.PeekLock);
            QueueClient sbSendToQueueClient = Helper.GetServiceBusQueueClient(messagingFactory, Constants.ServiceBusReceiveQueue);

            while (true)
            {
                BrokeredMessage message = null;
                string messageBody = null;
                try
                {
                    using (TransactionScope scope = new TransactionScope())
                    {
                        message = sbReceiveFromQueueClient.Receive();
                        if (message != null)
                        {
                            message.Complete();
                            messageBody = message.GetBody<string>();

                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            Console.WriteLine(
                                string.Format("Received message from {0}: {1}:{2}", Constants.ServiceBusSendQueue, message.Label, messageBody));
                            Console.ResetColor();

                            scope.Complete();
                        }
                    }

                    using (TransactionScope scope = new TransactionScope())
                    {
                        if (message != null)
                        {
                            BrokeredMessage resendMessage = new BrokeredMessage(messageBody);
                            resendMessage.Label = "Response to " + message.Label;
                            resendMessage.TimeToLive = new TimeSpan(0, 0, 60);

                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine(
                                string.Format("Sending message to {0}: {1}:{2}", Constants.ServiceBusReceiveQueue, resendMessage.Label, messageBody));
                            Console.ResetColor();

                            sbSendToQueueClient.Send(resendMessage);
                            scope.Complete();
                        }
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

