//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure SDK
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
    using System.Collections.Generic;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Description;
    using Microsoft.ServiceBus.Messaging;

    public class Receiver
    {
        private static string serviceBusNamespace;
        private static string serviceBusKeyName;
        private static string serviceBusKey;

        public static void Main()
        {
            // Setup:
            GetUserCredentials();
            QueueClient queueClient = CreateQueueClient("OrdersQueue");

            // Read messages from queue until queue is empty:
            Console.WriteLine("Reading messages from queue...");

            List<long> deferredSequenceNumbers = new List<long>();
         
            while (true)
            {
                BrokeredMessage receivedMessage = queueClient.Receive(TimeSpan.FromSeconds(10));

                if (receivedMessage == null)
                {
                    break;
                }
                else
                {
                    // Low-priority messages will be dealt with later:
                    if (receivedMessage.Properties["Priority"].ToString() == "Low")
                    {
                        receivedMessage.Defer();
                        Console.WriteLine("Deferred message with id {0}.", receivedMessage.MessageId);
                        // Deferred messages can only be retrieved by message receipt. Here, keeping track of the
                        // message receipt for a later retrieval:
                        deferredSequenceNumbers.Add(receivedMessage.SequenceNumber);
                    }
                    else
                    {
                        ProcessMessage(receivedMessage);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("No more messages left in queue. Moving onto deferred messages...");

            // Process the low-priority messages:
            foreach (long sequenceNumber in deferredSequenceNumbers)
            {
                ProcessMessage(queueClient.Receive(sequenceNumber));
            }

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();
        }

        private static void GetUserCredentials()
        {
            // User namespace
            Console.Write("Please provide the namespace: ");
            serviceBusNamespace = Console.ReadLine();

            // Issuer name
            Console.Write("Please provide the key name (e.g., \"RootManageSharedAccessKey\"): ");
            serviceBusKeyName = Console.ReadLine();

            // Issuer key
            Console.Write("Please provide the key: ");
            serviceBusKey = Console.ReadLine();
        }


        // Create the runtime entities (queue client)
        private static QueueClient CreateQueueClient(string queueName)
        {
            Uri runtimeUri = ServiceBusEnvironment.CreateServiceUri("sb", Receiver.serviceBusNamespace, string.Empty);
            
            MessagingFactory messagingFactory = MessagingFactory.Create(
                runtimeUri,
                TokenProvider.CreateSharedAccessSignatureTokenProvider(serviceBusKeyName, serviceBusKey));

            return messagingFactory.CreateQueueClient(queueName);
        }

        private static void ProcessMessage(BrokeredMessage message)
        {
            Console.WriteLine("Processed {0}-priority order {1}.", message.Properties["Priority"], message.MessageId);
            message.Complete();
        }
    }
}
