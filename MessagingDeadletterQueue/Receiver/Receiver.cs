//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure AppFabric SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples
{
    using System;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    public class Receiver
    {
        private static string serviceBusConnectionString;
        private const int MaxRetryCount = 5;

        private static System.Collections.Hashtable hashTable = new System.Collections.Hashtable();
        private enum OrderType
        {
            StayInOrder,
            TakeOutOrder,
            DeliveryOrder
        }

        public static void Main()
        {
            // Get credentials and create a client for receiving messages:
            GetUserCredentials();
            MessagingFactory messagingFactory = CreateMessagingFactory();
            QueueClient queueClient = messagingFactory.CreateQueueClient("OrdersService");

            // Read messages from queue until it is empty:
            Console.WriteLine("Reading messages from queue...");

            BrokeredMessage receivedMessage;
            while ((receivedMessage = queueClient.Receive(TimeSpan.FromSeconds(10))) != null)
            {
                int retryCount = 0;
                while (retryCount < MaxRetryCount)
                {
                    if (ProcessOrder(receivedMessage))
                        break;
                    else
                        retryCount++;
                }

                if (retryCount == MaxRetryCount)
                {
                    Console.WriteLine("Adding Order {0} with {1} number of items and {2} total to DeadLetter queue", receivedMessage.Properties["OrderNumber"],
                                receivedMessage.Properties["NumberOfItems"], receivedMessage.Properties["OrderTotal"]);
                    receivedMessage.DeadLetter("UnableToProcess", "Failed to process in reasonable attempts");
                }
            }

            Console.WriteLine();
            Console.WriteLine("No more messages left in queue. Logging dead lettered messages...");

            // Log the dead-lettered messages that could not be processed:
            QueueClient deadLetterClient = messagingFactory.CreateQueueClient(QueueClient.FormatDeadLetterPath(queueClient.Path), ReceiveMode.ReceiveAndDelete);
            BrokeredMessage receivedDeadLetterMessage;
            while ((receivedDeadLetterMessage = deadLetterClient.Receive(TimeSpan.FromSeconds(10))) != null)
            {
                LogOrder(receivedDeadLetterMessage);
            }
            
            Console.WriteLine();
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();
        }

        private static void GetUserCredentials()
        {
            Console.Write("Please provide a connection string to Service Bus (/? for help): ");
            serviceBusConnectionString = Console.ReadLine();

            if ((String.Compare(serviceBusConnectionString, "/?") == 0) || (serviceBusConnectionString.Length == 0))
            {
                Console.WriteLine("\nTo connect to the Service Bus cloud service, go to the Windows Azure portal and select 'View Connection String'.");
                Console.WriteLine("To connect to the Service Bus for Windows Server, use the get-sbClientConfiguration PowerShell cmdlet.");
                Console.WriteLine("A Service Bus connection string has the following format: \nEndpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<keyName>;SharedAccessKey=<key>\n");

                serviceBusConnectionString = Console.ReadLine();
                Environment.Exit(0);
            }
        }
         
        static MessagingFactory CreateMessagingFactory()
        {
            return MessagingFactory.CreateFromConnectionString(serviceBusConnectionString);
        }
        /// <summary>
        /// This method simulates the random failure behavior which happens in real world. 
        /// We will randomly select a message to fail based on some random number value. To make sure the message processing fails 
        /// all the times during subsequent retries, we add the result to the hashtable and retrieve it from there. 
        /// </summary>
        /// <param name="receivedMessage"></param>
        /// <returns></returns>
        private static bool ProcessOrder(BrokeredMessage receivedMessage)
        {
            if (hashTable.ContainsKey(receivedMessage.Properties["OrderNumber"]))
            {
                return false;
            }

            if (new Random().Next() % 2 == 0 ? true : false)
            {
                Console.WriteLine("Received Order {0} with {1} number of items and {2} total", receivedMessage.Properties["OrderNumber"],
                                 receivedMessage.Properties["NumberOfItems"], receivedMessage.Properties["OrderTotal"]);
                return true;
            }
            else
            {
                hashTable.Add(receivedMessage.Properties["OrderNumber"], false);
                return false;
            }
        }

        private static void LogOrder(BrokeredMessage message)
        {
            Console.WriteLine("Order {0} with {1} number of items and {2} total logged from DeadLetter queue. DeadLettering Reason is \"{3}\" and Deadlettering error description is \"{4}\"", message.Properties["OrderNumber"],
                                  message.Properties["NumberOfItems"], message.Properties["OrderTotal"], message.Properties["DeadLetterReason"], message.Properties["DeadLetterErrorDescription"]);
        }
    }
}
