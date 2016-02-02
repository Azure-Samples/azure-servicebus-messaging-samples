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
        public async Task Run(string namespaceAddress, string manageToken)
        {
            // This sample demonstrates how to use advanced filters with ServiceBus topics and subscriptions.
            // The sample creates a topic and 3 subscriptions with different filter definitions.
            // Each receiver will receive matching messages depending on the filter associated with a subscription.
            
            // Create messaging factory and ServiceBus namespace client.
            var sharedAccessSignatureTokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(manageToken);
            var messagingFactory = MessagingFactory.Create(namespaceAddress, sharedAccessSignatureTokenProvider);
            var namespaceManager = new NamespaceManager(namespaceAddress, sharedAccessSignatureTokenProvider);

            // Delete the topic from last run.
            DeleteTopicsAndSubscriptions(namespaceManager);

            // Create topic and subscriptions that'll be using through the sample.
            CreateTopicsAndSubscriptions(namespaceManager);

            // Send sample messages.
            SendMessagesToTopic(messagingFactory);

            // Receive messages from subscriptions.
            ReceiveAllMessagesFromSubscripions(messagingFactory);

            messagingFactory.Close();

            Console.WriteLine("Press [Enter] to quit...");
            Console.ReadLine();
        }

        const string TopicName = "MyTopic";
        const string SubsNameAllMessages = "AllOrders";
        const string SubsNameColorBlueSize10Orders = "ColorBlueSize10Orders";
        const string SubsNameHighPriorityOrders = "HighPriorityOrders";

     
        static void SendMessagesToTopic(MessagingFactory messagingFactory)
        {
            // Create client for the topic.
            var topicClient = messagingFactory.CreateTopicClient(TopicName);

            // Create a message sender from the topic client.

            Console.WriteLine("\nSending orders to topic.");

            // Now we can start sending orders.
            SendOrder(topicClient, new Order());
            SendOrder(topicClient, new Order { Color = "blue", Quantity = 5, Priority = "low" });
            SendOrder(topicClient, new Order { Color = "red", Quantity = 10, Priority = "high" });
            SendOrder(topicClient, new Order { Color = "yellow", Quantity = 5, Priority = "low" });
            SendOrder(topicClient, new Order { Color = "blue", Quantity = 10, Priority = "low" });
            SendOrder(topicClient, new Order { Color = "blue", Quantity = 5, Priority = "high" });
            SendOrder(topicClient, new Order { Color = "blue", Quantity = 10, Priority = "low" });
            SendOrder(topicClient, new Order { Color = "red", Quantity = 5, Priority = "low" });
            SendOrder(topicClient, new Order { Color = "red", Quantity = 10, Priority = "low" });
            SendOrder(topicClient, new Order { Color = "red", Quantity = 5, Priority = "low" });
            SendOrder(topicClient, new Order { Color = "yellow", Quantity = 10, Priority = "high" });
            SendOrder(topicClient, new Order { Color = "yellow", Quantity = 5, Priority = "low" });
            SendOrder(topicClient, new Order { Color = "yellow", Quantity = 10, Priority = "low" });

            Console.WriteLine("All messages sent.");
        }

        static void SendOrder(TopicClient topicClient, Order order)
        {
            using (var message = new BrokeredMessage())
            {
                message.CorrelationId = order.Priority;
                message.Properties.Add("color", order.Color);
                message.Properties.Add("quantity", order.Quantity);

                topicClient.Send(message);
            }

            Console.WriteLine("Sent order with Color={0}, Quantity={1}, Priority={2}", order.Color, order.Quantity, order.Priority);
        }

        static void ReceiveAllMessagesFromSubscripions(MessagingFactory messagingFactory)
        {
            // Receive message from 3 subscriptions.
            ReceiveAllMessageFromSubscription(messagingFactory, SubsNameAllMessages);
            ReceiveAllMessageFromSubscription(messagingFactory, SubsNameColorBlueSize10Orders);
            ReceiveAllMessageFromSubscription(messagingFactory, SubsNameHighPriorityOrders);
        }

        static void ReceiveAllMessageFromSubscription(MessagingFactory messagingFactory, string subsName)
        {
            var receivedMessages = 0;

            // Create subscription client.
            var subsClient =
                messagingFactory.CreateSubscriptionClient(TopicName, subsName, ReceiveMode.ReceiveAndDelete);

            // Create a receiver from the subscription client and receive all messages.
            Console.WriteLine("\nReceiving messages from subscription {0}.", subsName);

            while (true)
            {
                var receivedMessage = subsClient.Receive(TimeSpan.FromSeconds(1));

                if (receivedMessage != null)
                {
                    receivedMessage.Dispose();
                    receivedMessages++;
                }
                else
                {
                    // No more messages to receive.
                    break;
                }
            }

            Console.WriteLine("Received {0} messages from subscription {1}.", receivedMessages, subsClient.Name);
        }

        static void CreateTopicsAndSubscriptions(NamespaceManager namespaceManager)
        {
            Console.WriteLine("\nCreating a topic and 3 subscriptions.");

            // Create a topic and 3 subscriptions.
            var topicDescription = namespaceManager.CreateTopic(TopicName);
            Console.WriteLine("Topic created.");

            // Create a subscription for all messages sent to topic.
            namespaceManager.CreateSubscription(topicDescription.Path, SubsNameAllMessages, new TrueFilter());
            Console.WriteLine("Subscription {0} added with filter definition set to TrueFilter.", SubsNameAllMessages);

            // Create a subscription that'll receive all orders which have color "blue" and quantity 10.
            namespaceManager.CreateSubscription(
                topicDescription.Path,
                SubsNameColorBlueSize10Orders,
                new SqlFilter("color = 'blue' AND quantity = 10"));
            Console.WriteLine("Subscription {0} added with filter definition \"color = 'blue' AND quantity = 10\".", SubsNameColorBlueSize10Orders);

            // Create a subscription that'll receive all high priority orders.
            namespaceManager.CreateSubscription(topicDescription.Path, SubsNameHighPriorityOrders, new CorrelationFilter("high"));
            Console.WriteLine("Subscription {0} added with correlation filter definition \"high\".", SubsNameHighPriorityOrders);

            Console.WriteLine("Create completed.");
        }

        static void DeleteTopicsAndSubscriptions(NamespaceManager namespaceManager)
        {
            Console.WriteLine("\nDeleting topic and subscriptions from previous run if any.");

            try
            {
                namespaceManager.DeleteTopic(TopicName);
            }
            catch (MessagingEntityNotFoundException)
            {
                Console.WriteLine("No topic found to delete.");
            }

            Console.WriteLine("Delete completed.");
        }
      
    }
}