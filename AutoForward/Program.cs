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

namespace MessagingSamples
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    class Program : IDynamicSample
    {
        const string SourceTopicName = "SourceTopic";
        const string SourceTopicSubscriptionName1 = "Sub1";
        const string SourceTopicSubscriptionName2 = "Sub2";
        const string DestinationQueueName = "DestinationQueue";
        const string TransferQueueName = "TransferQueue";
        const string DestinationQueueSendKeyName = "DestinationQueueSendKey";
        const string KeyName = "SourceTopicSendKey";

        public async Task Run(string namespaceAddress, string manageToken)
        {
            // Get the 
            var namespaceManageTokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(manageToken);

            // Create namespace manager and create destination queue with a SAS rule that allows sending to that queue.
            var namespaceManager = new NamespaceManager(namespaceAddress, namespaceManageTokenProvider);
            if (namespaceManager.QueueExists(DestinationQueueName))
            {
                namespaceManager.DeleteQueue(DestinationQueueName);
            }
            var destinationQueueDescription = new QueueDescription(DestinationQueueName);
            var destinationQueueSendKey = SharedAccessAuthorizationRule.GenerateRandomKey();
            var destinationQueueSendRule = new SharedAccessAuthorizationRule(
                DestinationQueueSendKeyName,
                destinationQueueSendKey,
                new[] {AccessRights.Send});
            destinationQueueDescription.Authorization.Add(destinationQueueSendRule);
            var destinationQueue = namespaceManager.CreateQueue(destinationQueueDescription);
            Console.WriteLine("Created Service Bus queue \"{0}\"", DestinationQueueName);

            // Create message pump for destination queue.
            var namespaceManageMessagingFactory = MessagingFactory.Create(namespaceAddress, namespaceManageTokenProvider);
            var destinationQueueClient = namespaceManageMessagingFactory.CreateQueueClient(DestinationQueueName);
            var options = new OnMessageOptions {AutoComplete = true};
            destinationQueueClient.OnMessage(receivedMessage => PrintMessage(receivedMessage), options);
            ;


            /*
            ** Part 1: Use auto-forwarding from Topic1 to Topic2.
            */

            // Create source topic with a SAS rule that allows sending to that topic.
            if (namespaceManager.TopicExists(SourceTopicName))
            {
                namespaceManager.DeleteTopic(SourceTopicName);
            }
            var sourceTopicDescription = new TopicDescription(SourceTopicName);
            var sourceTopicSendKey = SharedAccessAuthorizationRule.GenerateRandomKey();
            var sourceTopicSendRule = new SharedAccessAuthorizationRule(
                KeyName,
                sourceTopicSendKey,
                new[] {AccessRights.Send});
            sourceTopicDescription.Authorization.Add(sourceTopicSendRule);
            var sourceTopic = namespaceManager.CreateTopic(sourceTopicDescription);
            Console.WriteLine("Created Service Bus topic \"{0}\"", SourceTopicName);

            // Create subscription on source topic. Configure subscription such that it forwards messages to destination queue.
            // Note that the destination queue must aleady exist at the time we are creating this subscription.
            var subscriptionDescription = new SubscriptionDescription(SourceTopicName, SourceTopicSubscriptionName1);
            subscriptionDescription.ForwardTo = DestinationQueueName;
            namespaceManager.CreateSubscription(subscriptionDescription);
            Console.WriteLine("Created Service Bus subscription \"{0}\" on topic \"{1}\"", SourceTopicSubscriptionName1, SourceTopicName);

            // Create a messaging factory and topicClient with send permissions on the source topic. Then send message M1
            // to sourceTopic. The message is forwarded without the user requiring send permissions on the destination queue.
            var sourceTopicSendTokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                KeyName,
                sourceTopicSendKey);
            var sourceTopicSendMessagingFactory = MessagingFactory.Create(namespaceAddress, sourceTopicSendTokenProvider);
            var topicClient1 = sourceTopicSendMessagingFactory.CreateTopicClient(SourceTopicName);
            topicClient1.Send(CreateMessage("M1"));


            /*
            ** Part 2: Send message to destination queue via transfer queue.
            */

            // Create transfer queue with a SAS rule that allows sending to that queue.
            // Use the same key and key name that is used to authorize send permissions on the destination queue.
            if (namespaceManager.QueueExists(TransferQueueName))
            {
                namespaceManager.DeleteQueue(TransferQueueName);
            }
            var transferQueueDescription = new QueueDescription(TransferQueueName);
            var transferQueueSendRule = new SharedAccessAuthorizationRule(
                DestinationQueueSendKeyName,
                destinationQueueSendKey,
                new[] {AccessRights.Send});
            transferQueueDescription.Authorization.Add(transferQueueSendRule);
            namespaceManager.CreateQueue(transferQueueDescription);
            Console.WriteLine("Created Service Bus queue \"{0}\"", TransferQueueName);

            // Create a sender that send message M2 to the destination queue via a transfer queue.
            // The sender needs send permissions on the transfer queue and on the destination queue.
            // Since the token provider can handle only a single key, the key and key name of
            // transferQueue and destinationQueue must be identical. Alternatively, you can use a root-level key.
            var transferQueueSendTokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                DestinationQueueSendKeyName,
                destinationQueueSendKey);
            var transferQueueSendMessagingFactory = MessagingFactory.Create(namespaceAddress, transferQueueSendTokenProvider);
            var sender = transferQueueSendMessagingFactory.CreateMessageSender(DestinationQueueName, TransferQueueName);
            sender.Send(CreateMessage("M2"));


            /*
            ** Part 3: Create an autoforward on the deadletter queue of subscription 1.
            ** Autoforwarding from deadletter queues was introduced in SDK 2.3.
            */

            var sd2 = new SubscriptionDescription(sourceTopic.Path, SourceTopicSubscriptionName2)
            {
                ForwardDeadLetteredMessagesTo = destinationQueue.Path
            };
            namespaceManager.CreateSubscription(sd2);
            Console.WriteLine("Created subscription \"{0}\" with a deadletter forward to \"{1}\"", SourceTopicSubscriptionName2, DestinationQueueName);

            // Create a client and send message to source topic.
            var topicClient2 = sourceTopicSendMessagingFactory.CreateTopicClient(SourceTopicName);
            topicClient2.Send(CreateMessage("M3"));

            // Deadletter message.
            var subscriptionClient = namespaceManageMessagingFactory.CreateSubscriptionClient(
                sourceTopic.Path,
                SourceTopicSubscriptionName2);
            var msg3 = subscriptionClient.Receive();
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
        public static BrokeredMessage CreateMessage(string label)
        {
            // Create a Service Bus message.
            var msg = new BrokeredMessage("This is the body of message \"" + label + "\".");
            msg.Properties.Add("Priority", 1);
            msg.Properties.Add("Importance", "High");
            msg.Label = label;
            msg.TimeToLive = TimeSpan.FromSeconds(90);
            return msg;
        }

        // Print the Service Bus message.
        public static void PrintMessage(BrokeredMessage msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Received message:");
            Console.WriteLine("   Label:    " + msg.Label);
            Console.WriteLine("   Body:     " + msg.GetBody<string>());
            foreach (var p in msg.Properties)
            {
                Console.WriteLine("   Property: " + p.Key + " = " + p.Value);
            }
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}