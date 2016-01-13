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
    using System.Messaging;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    public class Helper
    {
        private static string serviceNamespace, issuerName, issuerSecret;

        public static void GetUserCredentials()
        {
            //Read user credentials.
            Console.Write("Service Namespace: ");
            serviceNamespace = Console.ReadLine();

            Console.Write("Issuer Name: ");
            issuerName = Console.ReadLine();

            Console.Write("Issuer Secret: ");
            issuerSecret = Console.ReadLine();
            Console.WriteLine();
        }

        public static Uri GetNamespace()
        {
            if (string.IsNullOrEmpty(serviceNamespace))
            {
                GetUserCredentials();
            }

            Uri namespaceUri = ServiceBusEnvironment.CreateServiceUri("sb", serviceNamespace, string.Empty);
            return namespaceUri;
        }

        public static TokenProvider GetTokenProvider()
        {
            if (string.IsNullOrEmpty(issuerName) || string.IsNullOrEmpty(issuerSecret))
            {
                GetUserCredentials();
            }

            return TokenProvider.CreateSharedSecretTokenProvider(issuerName, issuerSecret);
        }

        public static QueueDescription CreateServiceBusQueue(string queueName)
        {
            QueueDescription queueDesc; 
            NamespaceManager namespaceClient = GetNamespaceManager();
            if (namespaceClient.QueueExists(queueName))
            {
                queueDesc = namespaceClient.GetQueue(queueName);
            }
            else
            {
                queueDesc = namespaceClient.CreateQueue(queueName);
            }

            return queueDesc;
        }

        public static void DeleteQueue(string queueName)
        {
            NamespaceManager namespaceClient = GetNamespaceManager();
            if (namespaceClient.QueueExists(queueName))
            {
                namespaceClient.DeleteQueue(queueName);
            }
        }

        public static NamespaceManager GetNamespaceManager()
        {
            Uri nameSpaceUri = GetNamespace();
            TokenProvider clientToken = GetTokenProvider();
            return new NamespaceManager(nameSpaceUri, clientToken);
        }

        public static MessagingFactory GetMessagingFactory()
        {
            // Create a MessagingFactory to receive messages.
            Uri nameSpaceUri = GetNamespace();
            TokenProvider clientToken = GetTokenProvider();
            return MessagingFactory.Create(nameSpaceUri, clientToken);
        }

        public static QueueClient GetServiceBusQueueClient(MessagingFactory messagingFactory, string queueName, ReceiveMode receiveMode = ReceiveMode.ReceiveAndDelete)
        {
            QueueClient client =  messagingFactory.CreateQueueClient(queueName, receiveMode);
            return client;
        }

        public static void CreateMsmqQueue(string queueName)
        {
            if (!MessageQueue.Exists(queueName))
            {
                MessageQueue.Create(queueName, true);
            }
        }

        public static void DeleteMsmqQueue(string queueName)
        {
            if (MessageQueue.Exists(queueName))
            {
                MessageQueue.Delete(queueName);
            }
        }

        public static MessageQueue OpenMsmqQueue(string queueName, bool sharedModeDenyReceive = false)
        {
            var msmqQueue = new System.Messaging.MessageQueue(queueName, sharedModeDenyReceive);
            msmqQueue.MessageReadPropertyFilter.SetAll();
            msmqQueue.Refresh();
            msmqQueue.Formatter = new BinaryMessageFormatter();
            return msmqQueue;
        }
    }
}
