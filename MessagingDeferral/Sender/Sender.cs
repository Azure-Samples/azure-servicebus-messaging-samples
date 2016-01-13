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
    
    public class Sender
    {
        private static NamespaceManager namespaceManager;
        private static MessagingFactory messagingFactory;

        private static string serviceBusNamespace;
        private static string serviceBusKeyName;
        private static string serviceBusKey;

        public static void Main()
        {
            // Setup:
            Sender.GetUserCredentials();
            QueueDescription queueDescription = CreateQueue();
            QueueClient queueClient = CreateQueueClient(queueDescription);

            // Send messages to queue:
            Console.WriteLine("Sending messages to queue...");

            BrokeredMessage message1 = CreateOrderMessage("High");
            queueClient.Send(message1);
            Console.WriteLine("Sent message {0} with high priority.", message1.MessageId);

            BrokeredMessage message2 = CreateOrderMessage("Low");
            queueClient.Send(message2);
            Console.WriteLine("Sent message {0} with low priority.", message2.MessageId);

            BrokeredMessage message3 = CreateOrderMessage("High");
            queueClient.Send(message3);
            Console.WriteLine("Sent message {0} with high priority.", message3.MessageId);

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to delete queue and exit.");
            Console.ReadLine();

            // Cleanup:
            messagingFactory.Close();
            namespaceManager.DeleteQueue(queueDescription.Path);
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

        private static QueueDescription CreateQueue()
        {
            Uri managementAddress = ServiceBusEnvironment.CreateServiceUri("https", Sender.serviceBusNamespace, string.Empty);
            namespaceManager = new NamespaceManager(
                managementAddress,
                TokenProvider.CreateSharedAccessSignatureTokenProvider(serviceBusKeyName, serviceBusKey));

            Console.WriteLine("Creating queue \"OrdersQueue\".");

            if (namespaceManager.QueueExists("OrdersQueue"))
            {
                namespaceManager.DeleteQueue("OrdersQueue");
            }

            return namespaceManager.CreateQueue("OrdersQueue");
        }

        private static QueueClient CreateQueueClient(QueueDescription queueDescription)
        {
            Uri runtimeUri = ServiceBusEnvironment.CreateServiceUri("sb", Sender.serviceBusNamespace, string.Empty);
            messagingFactory = MessagingFactory.Create(
                runtimeUri,
                TokenProvider.CreateSharedAccessSignatureTokenProvider(serviceBusKeyName, serviceBusKey));

            return messagingFactory.CreateQueueClient(queueDescription.Path);
        }

        private static BrokeredMessage CreateOrderMessage(string priority)
        {
            BrokeredMessage message = new BrokeredMessage();
            message.MessageId = "Order" + Guid.NewGuid().ToString();
            message.Properties.Add("Priority", priority);
            return message;
        }
    }
}
