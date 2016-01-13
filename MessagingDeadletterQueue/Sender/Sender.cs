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

    public class Sender
    {
        private static string serviceBusConnectionString;

        public static void Main()
        {
            // Get credentials and set up management and runtime messaging entities:

            GetUserCredentials();
            NamespaceManager namespaceClient = CreateNamespaceManager();
            MessagingFactory messagingFactory = CreateMessagingFactory();

            Console.WriteLine("Creating queue 'OrdersService'...");

            if(namespaceClient.QueueExists("OrdersService"))
                namespaceClient.DeleteQueue("OrdersService");
            
            QueueDescription queue = namespaceClient.CreateQueue("OrdersService");

            QueueClient queueClient = messagingFactory.CreateQueueClient(queue.Path, ReceiveMode.ReceiveAndDelete);
            
            // Send messages to queue, of different order types:
            Console.WriteLine("Sending messages to queue...");
                CreateAndSendOrderMessage("DeliveryOrder", 1, 10, 15, queueClient);
                CreateAndSendOrderMessage("StayInOrder", 2, 15, 500, queueClient);
                CreateAndSendOrderMessage("TakeOutOrder", 3, 1, 25, queueClient);
                CreateAndSendOrderMessage("TakeOutOrder", 5, 3, 25, queueClient);
                CreateAndSendOrderMessage("DeliveryOrder", 4, 100, 100000, queueClient);
            

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to delete queue and exit.");
            Console.ReadLine();

            // Cleanup:
            messagingFactory.Close();
            namespaceClient.DeleteQueue(queue.Path);
        }

        static void GetUserCredentials()
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

        static NamespaceManager CreateNamespaceManager()
        {
            return NamespaceManager.CreateFromConnectionString(serviceBusConnectionString);
        }

        static MessagingFactory CreateMessagingFactory()
        {
            return MessagingFactory.CreateFromConnectionString(serviceBusConnectionString);
        }

        private static void CreateAndSendOrderMessage(string orderType, int? orderNumber, int numberOfItems, int orderTotal, QueueClient sender)
        {
            var message = new BrokeredMessage(Guid.NewGuid().ToString());
            message.Properties.Add("OrderType", orderType);
            message.Properties.Add("OrderNumber", orderNumber);
            message.Properties.Add("NumberOfItems", numberOfItems);
            message.Properties.Add("OrderTotal", orderTotal);

            Console.WriteLine("Sending message of order type {0}.", message.Properties["OrderType"]);
            sender.Send(message);
        }
    }
}
