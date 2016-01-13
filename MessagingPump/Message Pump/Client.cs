//---------------------------------------------------------------------------------
// Copyright (c) 2013, Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//---------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.ServiceBus.Samples.MessagePump
{

    class Client
    {
        private const string SourceQueueOrSubscriptionName = "MessagePumpSampleSourceQueue";
        private const string DestinationQueueOrTopicName = "MessagePumpSampleDestinationQueue";

        const int NumOfMessages = 10000;

        public static void Main()
        {
            string connectionString = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("Please provide ServiceBus Connection string in App.Config.");
            }
            ServiceBusConnectionStringBuilder connectionStringBuilder = new ServiceBusConnectionStringBuilder(connectionString);
            NamespaceManager namespaceManager = NamespaceManager.CreateFromConnectionString(connectionStringBuilder.ToString());
            
            // Create namespace manager and create Service Bus queues if they don't exist already.
            if (!namespaceManager.QueueExists(SourceQueueOrSubscriptionName))
            {
                namespaceManager.CreateQueue(SourceQueueOrSubscriptionName);
                Console.WriteLine("Created Service Bus queue \"{0}\".", SourceQueueOrSubscriptionName);
            }
            QueueDescription destinationQueueDescription = new QueueDescription(DestinationQueueOrTopicName);
            destinationQueueDescription.RequiresDuplicateDetection = true;
            if (!namespaceManager.QueueExists(DestinationQueueOrTopicName))
            {
                namespaceManager.CreateQueue(destinationQueueDescription);
                Console.WriteLine("Created Service Bus queue \"{0}\".", DestinationQueueOrTopicName);
            }

            // Create a MessagingFactory.
            MessagingFactory messagingFactory = MessagingFactory.CreateFromConnectionString(connectionString);

            // Send messages to source queue.
            SendMessages(messagingFactory, NumOfMessages);

            // Create message pump. For simplicity, the same namespace is used for source and destination entity.
            TokenProvider tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionStringBuilder.SharedAccessKeyName, connectionStringBuilder.SharedAccessKey);
            Uri namespaceUri = connectionStringBuilder.Endpoints.First();
            MessagePump messagePump = new MessagePump(namespaceUri, tokenProvider, SourceQueueOrSubscriptionName, namespaceUri, tokenProvider, DestinationQueueOrTopicName);

            // Receive messages from destination queue.
            System.Threading.Thread.Sleep(30000);  // Sleep until all messages have been pumped. Sleep is needed only for performance testing.
            ReceiveMessages(messagingFactory, NumOfMessages);
            ReceiveDeadLetterMessages(messagingFactory);

            // Cleanup
            Console.WriteLine("\nPress ENTER to exit\n");
            Console.ReadLine();

            messagingFactory.Close();
            messagePump.Dispose();
            namespaceManager.DeleteQueue(SourceQueueOrSubscriptionName);
            namespaceManager.DeleteQueue(DestinationQueueOrTopicName);
        }

        static void SendMessages(MessagingFactory messagingFactory, int numOfMessages)
        {
            MessageSender messageSender = messagingFactory.CreateMessageSender(SourceQueueOrSubscriptionName);

            Console.WriteLine("Sending {0} messages...", numOfMessages);
            int numOfBatches = (numOfMessages + 99) / 100;
            for (int i = 0; i < numOfBatches; i++)
            {
                // Create batch of messages.
                List<BrokeredMessage> batch = new List<BrokeredMessage>();
                for (int j = 1; j <= 100; j++)
                {
                    batch.Add(CreateBrokeredMessage(i * 100 + j));
                }

                // Send batch.
                try
                {
                    messageSender.SendBatch(batch);
                }
                catch (ServerBusyException)
                {
                    Thread.Sleep(1000);
                    i--;
                }
            }
            Console.WriteLine("{0} messages sent", numOfMessages);
            messageSender.Close();
        }

        static void ReceiveMessages(MessagingFactory messagingFactory, int numOfMessages)
        {
            MessageReceiver messageReceiver = messagingFactory.CreateMessageReceiver(DestinationQueueOrTopicName, ReceiveMode.ReceiveAndDelete);
            messageReceiver.PrefetchCount = 100;

            Console.WriteLine("Receiving {0} messages...", numOfMessages);
            bool[] messagesReceived = new bool[numOfMessages + 1];
            messagesReceived[0] = true;
            for (int i = 1; i <= numOfMessages; i++)
            {
                try
                {
                    BrokeredMessage msg = messageReceiver.Receive(TimeSpan.FromSeconds(5));  // In production, don't specify timeout.
                    if (msg != null)
                    {
                        int index = Convert.ToInt32(msg.Label.Substring(1));
                        if (index > numOfMessages)
                        {
                            Console.WriteLine("Error: Message {0} is out of bound (index = {1}). Is there an old message is the source queue?", msg.Label, index);
                        }
                        else
                        {
                            if (messagesReceived[index])
                            {
                                Console.WriteLine("Message {0} was received multiple times.", msg.Label);
                            }
                            messagesReceived[index] = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Receiver: " + ex.Message);
                }
            }

            messageReceiver.Close();

            // Check if all messages have been received.
            bool allMessagesReceived = true;
            for (int i = 1; i <= numOfMessages; i++)
            {
                if (!messagesReceived[i])
                {
                    if (allMessagesReceived)
                    {
                        Console.Write("The following messages were not received:");
                        allMessagesReceived = false;
                    }
                    Console.Write(" M" + i);
                }
            }
            if (allMessagesReceived)
            {
                Console.WriteLine("All messages have been received.");
            }
            else
            {
                Console.WriteLine();
            }
        }

        static void ReceiveDeadLetterMessages(MessagingFactory messagingFactory)
        {
            MessageReceiver deadletterReceiver = messagingFactory.CreateMessageReceiver(SourceQueueOrSubscriptionName + @"/$DeadLetterQueue");

            bool deadletterMessagesFound = false;
            while (true)
            {
                BrokeredMessage deadletterMessage = deadletterReceiver.Receive(TimeSpan.FromSeconds(5));
                if (deadletterMessage == null)
                {
                    break;
                }
                if (!deadletterMessagesFound)
                {
                    Console.Write("The following messages were deadlettered:");
                }
                deadletterMessagesFound = true;
                Console.Write(" " + deadletterMessage.Label);
            }
            if (deadletterMessagesFound)
            {
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("No deadletter messages found.");
            }

            deadletterReceiver.Close();
        }

        // Create a new Service Bus message.
        static BrokeredMessage CreateBrokeredMessage(int i)
        {
            // Create a Service Bus message.
            Int32[] payload = new Int32[256];
            BrokeredMessage msg = new BrokeredMessage(payload);
            msg.Label = "M" + i.ToString();
            msg.TimeToLive = TimeSpan.FromSeconds(90);
            return msg;
        }
    }
}
