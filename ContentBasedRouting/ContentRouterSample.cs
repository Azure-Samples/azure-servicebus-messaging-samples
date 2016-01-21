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

    public class Program : IDynamicSample
    {
        const int NumCategories = 6;
        const int NumMessages = 20;
        const string TopicName = "MyTopic";
        const string CategoryPropName = "Category";

        public async Task Run(string namespaceAddress, string manageToken)
        {
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(manageToken);
            var namespaceManager = new NamespaceManager(
                namespaceAddress,
                tokenProvider);

            var topicDescription = new TopicDescription(TopicName);

            // Delete the queue if already exists before creation. 
            if (await namespaceManager.TopicExistsAsync(topicDescription.Path))
            {
                await namespaceManager.DeleteTopicAsync(topicDescription.Path);
            }

            Console.WriteLine("\nCreating Topic...");
            var mainTopic = await namespaceManager.CreateTopicAsync(topicDescription);

            // this sub receives all messages
            await namespaceManager.CreateSubscriptionAsync(mainTopic.Path, "AuditSubscription");

            // this sub receives messages for Category = 1
            var ruleCat1 = new RuleDescription(new SqlFilter(CategoryPropName + " = 1"));
            var cat1Sub = new SubscriptionDescription(TopicName, "Category1Subscription");
            namespaceManager.CreateSubscription(cat1Sub, ruleCat1);

            // this sub receives messages for Category <> 1
            var ruleCatNot1 = new RuleDescription(new SqlFilter(CategoryPropName + " <> 1"));
            var catNot1Sub = new SubscriptionDescription(TopicName, "CategoryNot1Subscription");
            namespaceManager.CreateSubscription(catNot1Sub, ruleCatNot1);

            // Start senders and receivers:
            Console.WriteLine("\nLaunching senders and receivers...");

            //send messages to topic            
            MessagingFactory messagingFactory = MessagingFactory.Create(
                namespaceAddress,
                tokenProvider);

            var topicClient = messagingFactory.CreateTopicClient(TopicName);

            Console.WriteLine("Preparing to send messages to {0}...", topicClient.Path);

            SendMessages(topicClient);

            // All messages sent
            Console.WriteLine("\nSender complete. Press ENTER");
            Console.ReadLine();

            // start receive
            for (var ctr = 0; ctr < 3; ctr++)
            {
                var subscriptionName = string.Empty;

                switch (ctr)
                {
                    case 0:
                    {
                        Console.Title = "Audit Subscription Receiver";
                        subscriptionName = "AuditSubscription";
                        break;
                    }
                    case 1:
                    {
                        Console.Title = "Category 1 Subscription Receiver";
                        subscriptionName = "Category1Subscription";
                        break;
                    }
                    case 2:
                    {
                        Console.Title = "Category Not 1 Subscription Receiver";
                        subscriptionName = "CategoryNot1Subscription";
                        break;
                    }
                    default:
                    {
                        Console.Title = "Unknown";
                        break;
                    }
                }

                Console.WriteLine("Selecting {0}...", subscriptionName);
                var subClient = messagingFactory.CreateSubscriptionClient(TopicName, subscriptionName, ReceiveMode.ReceiveAndDelete);
                Console.WriteLine("Ready to receive messages from {0}...", subClient.Name);

                while (true)
                {
                    try
                    {
                        var message = subClient.Receive(TimeSpan.FromSeconds(5));

                        if (message != null)
                        {
                            OutputMessageInfo("RECV: ", message);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (MessageNotFoundException)
                    {
                        Console.WriteLine("Got MessageNotFoundException, waiting for messages to be available");
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine("Got TimeoutException, no more messages available");
                        break;
                    }
                    catch (MessagingException e)
                    {
                        Console.WriteLine("Error: " + e.Message);
                    }
                }

                Console.WriteLine("\nReceiver complete. press ENTER");
                Console.ReadLine();
            }
            Console.WriteLine("\nPress [Enter] to exit.");
            Console.ReadLine();

            // Cleanup:
            namespaceManager.DeleteTopic(TopicName);
        }

        void SendMessages(TopicClient topicClient)
        {
            // Send messages to queue:
            Console.WriteLine("Sending messages to topic {0}", topicClient.Path);

            var rand = new Random();
            for (var i = 0; i < NumMessages; ++i)
            {
                var message = new BrokeredMessage();
                message.Properties.Add(CategoryPropName, rand.Next(NumCategories));
                message.MessageId = "Order_" + DateTime.Now.ToLongTimeString();
                try
                {
                    topicClient.Send(message);
                }
                catch (Exception)
                {
                    break;
                }
                OutputMessageInfo("SEND: ", message);
            }

            Console.WriteLine();
        }

        void OutputMessageInfo(string action, BrokeredMessage message, string additionalText = "")
        {
            ConsoleColor[] colors =
            {
                ConsoleColor.Red,
                ConsoleColor.Green,
                ConsoleColor.Yellow,
                ConsoleColor.Cyan,
                ConsoleColor.Magenta,
                ConsoleColor.Blue,
                ConsoleColor.White
            };

            var prop = message?.Properties[CategoryPropName];

            if (prop != null)
            {
                Console.ForegroundColor = colors[int.Parse(prop.ToString())%colors.Length];
                Console.WriteLine("{0}{1} - Category {2}. {3}", action, message.MessageId, message.Properties[CategoryPropName], additionalText);
                Console.ResetColor();
            }
        }
    }
}