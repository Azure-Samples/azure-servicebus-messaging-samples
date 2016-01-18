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

namespace Microsoft.ServiceBus.Samples.AutoForward
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ServiceBus.Messaging;

    class Client
    {
        private const string SourceTopicName = "SourceTopic";
        private const string SourceTopicSubscriptionName1 = "Sub1";
        private const string SourceTopicSubscriptionName2 = "Sub2";
        private const string DestinationQueueName = "DestinationQueue";
        private const string TransferQueueName = "TransferQueue";

        public static void Main()
        {
            // Get the SAS key of your Azure Service Bus namespaces by going to the Azure portal, mark your namespace,
            // and click Connection Information button on the bottom of the page. Then copy the SAS RootManageSharedAccessKey
            // connection string. The key is defined in the SharedAccessKey property of the connection string.
            // BE AWARE THAT HARDCODING YOUR CONNECTION STRING IS A SECURITY RISK IF YOU SHARE THIS CODE. 
            string serviceNamespace = "YOUR-NAMESPACE";
            string namespaceManageKeyName = "RootManageSharedAccessKey";
            string namespaceManageKey = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";

            // Create SAS token provider with Manage rights on the namespace. This right is required to create new entities.
            Uri namespaceUri = ServiceBusEnvironment.CreateServiceUri("sb", serviceNamespace, string.Empty);
            Console.WriteLine("Namespace URI: " + namespaceUri.ToString());
            TokenProvider namespaceManageTokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(namespaceManageKeyName, namespaceManageKey);

            // Create namespace manager and create destination queue with a SAS rule that allows sending to that queue.
            NamespaceManager namespaceManager = new NamespaceManager(namespaceUri, namespaceManageTokenProvider);
            if (namespaceManager.QueueExists(DestinationQueueName))
            {
                namespaceManager.DeleteQueue(DestinationQueueName);
            }
            QueueDescription destinationQueueDescription = new QueueDescription(DestinationQueueName);
            string destinationQueueSendKeyName = "DestinationQueueSendKey";
            string destinationQueueSendKey = SharedAccessAuthorizationRule.GenerateRandomKey();
            SharedAccessAuthorizationRule destinationQueueSendRule = new SharedAccessAuthorizationRule(destinationQueueSendKeyName, destinationQueueSendKey, new[] { AccessRights.Send });
            destinationQueueDescription.Authorization.Add(destinationQueueSendRule);
            QueueDescription destinationQueue = namespaceManager.CreateQueue(destinationQueueDescription);
            Console.WriteLine("Created Service Bus queue \"{0}\"", DestinationQueueName);

            // Create message pump for destination queue.
            MessagingFactory namespaceManageMessagingFactory = MessagingFactory.Create(namespaceUri, namespaceManageTokenProvider);
            QueueClient destinationQueueClient = namespaceManageMessagingFactory.CreateQueueClient(DestinationQueueName);
            OnMessageOptions options = new OnMessageOptions() { AutoComplete = true };
            destinationQueueClient.OnMessage(receivedMessage => PrintBrokeredMessage(receivedMessage), options);; 


            /*
            ** Part 1: Use auto-forwarding from Topic1 to Topic2.
            */

            // Create source topic with a SAS rule that allows sending to that topic.
            if (namespaceManager.TopicExists(SourceTopicName))
            {
                namespaceManager.DeleteTopic(SourceTopicName);
            }
            TopicDescription sourceTopicDescription = new TopicDescription(SourceTopicName);
            string sourceTopicSendKeyName = "SourceTopicSendKey";
            string sourceTopicSendKey = SharedAccessAuthorizationRule.GenerateRandomKey();
            SharedAccessAuthorizationRule sourceTopicSendRule = new SharedAccessAuthorizationRule(sourceTopicSendKeyName, sourceTopicSendKey, new[] { AccessRights.Send });
            sourceTopicDescription.Authorization.Add(sourceTopicSendRule);
            TopicDescription sourceTopic = namespaceManager.CreateTopic(sourceTopicDescription);
            Console.WriteLine("Created Service Bus topic \"{0}\"", SourceTopicName);

            // Create subscription on source topic. Configure subscription such that it forwards messages to destination queue.
            // Note that the destination queue must aleady exist at the time we are creating this subscription.
            SubscriptionDescription subscripitonDescription = new SubscriptionDescription(SourceTopicName, SourceTopicSubscriptionName1);
            subscripitonDescription.ForwardTo = DestinationQueueName;
            namespaceManager.CreateSubscription(subscripitonDescription);
            Console.WriteLine("Created Service Bus subscription \"{0}\" on topic \"{1}\"", SourceTopicSubscriptionName1, SourceTopicName);

            // Create a messaging factory and topicClient with send permissions on the source topic. Then send message M1
            // to sourceTopic. The message is forwarded without the user requiring send permissions on the destination queue.
            TokenProvider sourceTopicSendTokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(sourceTopicSendKeyName, sourceTopicSendKey);
            MessagingFactory sourceTopicSendMessagingFactory = MessagingFactory.Create(namespaceUri, sourceTopicSendTokenProvider);
            TopicClient topicClient1 = sourceTopicSendMessagingFactory.CreateTopicClient(SourceTopicName);
            topicClient1.Send(CreateBrokeredMessage("M1"));


            /*
            ** Part 2: Send message to destination queue via transfer queue.
            */

            // Create transfer queue with a SAS rule that allows sending to that queue.
            // Use the same key and key name that is used to authorize send permissions on the destination queue.
            if (namespaceManager.QueueExists(TransferQueueName))
            {
                namespaceManager.DeleteQueue(TransferQueueName);
            }
            QueueDescription transferQueueDescription = new QueueDescription(TransferQueueName);
            SharedAccessAuthorizationRule transferQueueSendRule = new SharedAccessAuthorizationRule(destinationQueueSendKeyName, destinationQueueSendKey, new[] { AccessRights.Send });
            transferQueueDescription.Authorization.Add(transferQueueSendRule);
            namespaceManager.CreateQueue(transferQueueDescription);
            Console.WriteLine("Created Service Bus queue \"{0}\"", TransferQueueName);

            // Create a sender that send message M2 to the destination queue via a transfer queue.
            // The sender needs send permissions on the transfer queue and on the destination queue.
            // Since the token provider can handle only a single key, the key and key name of
            // transferQueue and destinationQueue must be identical. Alternatively, you can use a root-level key.
            TokenProvider transferQueueSendTokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(destinationQueueSendKeyName, destinationQueueSendKey);
            MessagingFactory transferQueueSendMessagingFactory = MessagingFactory.Create(namespaceUri, transferQueueSendTokenProvider);
            MessageSender sender = transferQueueSendMessagingFactory.CreateMessageSender(DestinationQueueName, TransferQueueName);
            sender.Send(CreateBrokeredMessage("M2"));


            /*
            ** Part 3: Create an autoforward on the deadletter queue of subscription 1.
            ** Autoforwarding from deadletter queues was introduced in SDK 2.3.
            */

            SubscriptionDescription sd2 = new SubscriptionDescription(sourceTopic.Path, SourceTopicSubscriptionName2) { ForwardDeadLetteredMessagesTo = destinationQueue.Path };
            namespaceManager.CreateSubscription(sd2);
            Console.WriteLine("Created subscription \"{0}\" with a deadletter forward to \"{1}\"", SourceTopicSubscriptionName2, DestinationQueueName);

            // Create a client and send message to source topic.
            TopicClient topicClient2 = sourceTopicSendMessagingFactory.CreateTopicClient(SourceTopicName);
            topicClient2.Send(CreateBrokeredMessage("M3"));

            // Deadletter message.
            SubscriptionClient subscriptionClient = namespaceManageMessagingFactory.CreateSubscriptionClient(sourceTopic.Path, SourceTopicSubscriptionName2);
            BrokeredMessage msg3 = subscriptionClient.Receive();
            msg3.DeadLetter("Deadlettered for demonstration purposes", "MyErrorText");
            Console.WriteLine("Deadlettered message \"" + msg3.Label + "\"");
            

            /*
            ** Close messaging factory and delete queues and topics.
            */
            Console.WriteLine("\nPress ENTER to delete topics and exit\n");
            Console.ReadLine();
            namespaceManageMessagingFactory.Close();
            namespaceManager.DeleteQueue(DestinationQueueName);
            namespaceManager.DeleteQueue(TransferQueueName);
            namespaceManager.DeleteTopic(SourceTopicName);
        }

        // Create a new Service Bus message.
        public static BrokeredMessage CreateBrokeredMessage(string label)
        {
            // Create a Service Bus message.
            BrokeredMessage msg = new BrokeredMessage("This is the body of message \"" + label + "\".");
            msg.Properties.Add("Priority", 1);
            msg.Properties.Add("Importance", "High");
            msg.Label = label;
            msg.TimeToLive = TimeSpan.FromSeconds(90);
            return msg;
        }

        // Print the Service Bus message.
        public static void PrintBrokeredMessage(BrokeredMessage msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Received message:");
            Console.WriteLine("   Label:    " + msg.Label);
            Console.WriteLine("   Body:     " + msg.GetBody<string>());
            foreach (KeyValuePair<string, object> p in msg.Properties)
            {
                Console.WriteLine("   Property: " + p.Key.ToString() + " = " + p.Value.ToString());
            }
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}
