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
    using System.Transactions;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    public class Program : IDynamicSample
    {
        // Partitioned entities is only available in Azure Service Bus. It is not available in Service Bus Server v1.1.
       
        const string QueueName = "PartitionedQueue1";
        const string TransferQueueName = "PartitionedTransferQueue";
        const string TransferTopicName = "PartitionedTransferTopic";
        const string TransferSubscriptionName = "Sub1";

        public async Task Run(string namespaceAddress, string manageToken)
        {
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(manageToken);
            var namespaceManager = new NamespaceManager(namespaceAddress, tokenProvider);
            var messagingFactory = MessagingFactory.Create(namespaceAddress, tokenProvider);

            Console.WriteLine("Non-session-aware queue");
            await this.RunSampleAsync(namespaceManager, messagingFactory);

            Console.WriteLine("Session-aware queue");
            await this.RunSessionSampleAsync(namespaceManager, messagingFactory);

            Console.WriteLine("Autoforwarding");
            await this.RunAutoForwardSampleAsync(namespaceManager, messagingFactory);

            Console.WriteLine("Send via queue");
            await this.RunTransferSampleAsync(namespaceManager, messagingFactory);

            messagingFactory.Close();
        }

        async Task RunSampleAsync(NamespaceManager namespaceManager, MessagingFactory messagingFactory)
        {
            // Create partitioned non-session-aware queue.
            await this.CreatePartitionedQueueAsync(namespaceManager, QueueName, false);

            // Send messages without a transaction.
            var qc = messagingFactory.CreateQueueClient(QueueName, ReceiveMode.ReceiveAndDelete);
            for (var i = 1; i <= 2; i++)
            {
                // Create a Service Bus message.
                var msg = new BrokeredMessage("This is the body of message " + i)
                {
                    Label = "M" + i,
                    TimeToLive = TimeSpan.FromSeconds(90)
                };
                qc.Send(msg);
            }

            // Send messages within a transaction.
            var committableTransaction = new CommittableTransaction();
            using (var ts = new TransactionScope(committableTransaction, TransactionScopeAsyncFlowOption.Enabled))
            {
                for (var i = 3; i <= 4; i++)
                {
                    // Create a Service Bus message.
                    await qc.SendAsync(new BrokeredMessage("This is the body of message " + i)
                    {
                        Label = "M" + i,
                        TimeToLive = TimeSpan.FromSeconds(90),
                        PartitionKey = "myPartitionKey"
                    });
                }
                ts.Complete();
            }
            committableTransaction.Commit();

            // Receive messages.
            for (var i = 1; i <= 4; i++)
            {
                var msg = await qc.ReceiveAsync();
                ProcessMessage(msg);
            }

            // Cleanup.
            Console.WriteLine("\nPress ENTER to continue.");
            Console.ReadLine();
            await qc.CloseAsync();
            await namespaceManager.DeleteQueueAsync(QueueName);
        }

        async Task RunSessionSampleAsync(NamespaceManager nm, MessagingFactory mf)
        {
            // Create partitioned session-aware queue.
            await this.CreatePartitionedQueueAsync(nm, QueueName, true);

            // Send messages without a transaction.
            var qc = mf.CreateQueueClient(QueueName, ReceiveMode.ReceiveAndDelete);
            for (var i = 1; i <= 2; i++)
            {
                // Create a Service Bus message.
                qc.Send(new BrokeredMessage("This is the body of message " + i)
                {
                    Label = "M" + i,
                    TimeToLive = TimeSpan.FromSeconds(90),
                    SessionId = "MySessionId"
                });
            }

            // Send messages within a transaction.
            var committableTransaction = new CommittableTransaction();
            using (var ts = new TransactionScope(committableTransaction, TransactionScopeAsyncFlowOption.Enabled))
            {
                for (var i = 3; i <= 4; i++)
                {
                    // Create a Service Bus message.
                    // This line is optional and can be omitted.
                    qc.Send(new BrokeredMessage("This is the body of message " + i)
                    {
                        Label = "M" + i,
                        TimeToLive = TimeSpan.FromSeconds(90),
                        SessionId = "MySessionId",
                        PartitionKey = "MySessionId"
                    });
                }
                ts.Complete();
            }
            committableTransaction.Commit();

            // Receive messages.
            var session = await qc.AcceptMessageSessionAsync("MySessionId");
            for (var i = 1; i <= 4; i++)
            {
                var msg = await session.ReceiveAsync();
                ProcessMessage(msg);
            }

            // Cleanup.
            Console.WriteLine("\nPress ENTER to continue.");
            Console.ReadLine();
            await qc.CloseAsync();
            await nm.DeleteQueueAsync(QueueName);
        }

        async Task RunAutoForwardSampleAsync(NamespaceManager nm, MessagingFactory messagingFactory)
        {
            // Create partitioned target queue. The target queue is session-aware.
            await this.CreatePartitionedQueueAsync(nm, QueueName, true);

            // Create partitioned transfer topic.
            var transferTopic = await this.CreatePartitionedTopicAsync(nm, TransferTopicName);
            var transferSubscription = new SubscriptionDescription(transferTopic.Path, TransferSubscriptionName) {ForwardTo = QueueName};
            await nm.CreateSubscriptionAsync(transferSubscription);

            // Send message within a transaction. The transaction spans the sending to the transfer queue.
            var sender = await messagingFactory.CreateMessageSenderAsync(TransferTopicName);
            var committableTransaction = new CommittableTransaction();
            using (var ts = new TransactionScope(committableTransaction, TransactionScopeAsyncFlowOption.Enabled))
            {
                for (var i = 1; i <= 2; i++)
                {
                    // Create a Service Bus message.
                    // Used as the partition key for the target queue.
                    await sender.SendAsync(new BrokeredMessage("This is the body of message " + i)
                    {
                        Label = "M" + i,
                        TimeToLive = TimeSpan.FromSeconds(90),
                        SessionId = "MySessionId"
                    });
                }
                ts.Complete();
            }
            committableTransaction.Commit();

            // Reeiver messages.
            var qc = messagingFactory.CreateQueueClient(QueueName, ReceiveMode.ReceiveAndDelete);
            var session = await qc.AcceptMessageSessionAsync("MySessionId");
            for (var i = 1; i <= 2; i++)
            {
                var msg = await session.ReceiveAsync();
                ProcessMessage(msg);
            }

            // Cleanup.
            Console.WriteLine("\nPress ENTER to continue.");
            Console.ReadLine();
            await sender.CloseAsync();
            await qc.CloseAsync();
            await nm.DeleteQueueAsync(QueueName);
            await nm.DeleteQueueAsync(TransferTopicName);
        }

        async Task RunTransferSampleAsync(NamespaceManager nm, MessagingFactory mf)
        {
            // Create partitioned target queue and transfer queue. The target queue is session-aware.
            var targetQueue = await this.CreatePartitionedQueueAsync(nm, QueueName, true);
            var transferQueue = await this.CreatePartitionedQueueAsync(nm, TransferQueueName, false);

            // Send message within a transaction. The transaction spans the sending to the transfer queue.
            var sender = mf.CreateMessageSender(targetQueue.Path, transferQueue.Path);
            var committableTransaction = new CommittableTransaction();
            using (var ts = new TransactionScope(committableTransaction, TransactionScopeAsyncFlowOption.Enabled))
            {
                for (var i = 1; i <= 2; i++)
                {
                    // Create a Service Bus message.
                    var msg = new BrokeredMessage("This is the body of message " + i)
                    {
                        Label = "M" + i,
                        TimeToLive = TimeSpan.FromSeconds(90),
                        SessionId = "MySessionId",
                        ViaPartitionKey = "MyViaPartitionKey"
                    };
                    // Used as the partition key for the target queue.
                    // Used as the partition key for the transfer queue.
                    await sender.SendAsync(msg);
                }
                ts.Complete();
            }
            committableTransaction.Commit();

            // Reeiver messages.
            var qc = mf.CreateQueueClient(targetQueue.Path, ReceiveMode.ReceiveAndDelete);
            var session = await qc.AcceptMessageSessionAsync("MySessionId");
            for (var i = 1; i <= 2; i++)
            {
                var msg = await session.ReceiveAsync();
                ProcessMessage(msg);
            }

            // Cleanup.
            Console.WriteLine("\nPress ENTER to exit.");
            Console.ReadLine();
            await sender.CloseAsync();
            await qc.CloseAsync();
            await nm.DeleteQueueAsync(QueueName);
            await nm.DeleteQueueAsync(TransferQueueName);
        }

        async Task<QueueDescription> CreatePartitionedQueueAsync(NamespaceManager nm, string queueName, bool requiresSession)
        {
            var qd = new QueueDescription(queueName) {EnablePartitioning = true, RequiresSession = requiresSession};
            if (await nm.QueueExistsAsync(queueName))
            {
                await nm.DeleteQueueAsync(queueName);
            }
            await nm.CreateQueueAsync(qd);
            qd = await nm.GetQueueAsync(queueName);
            Console.WriteLine("Queue \"" + queueName + "\" created. Queue size: " + qd.MaxSizeInMegabytes + " MByte.");
            return qd;
        }

        async Task<TopicDescription> CreatePartitionedTopicAsync(NamespaceManager nm, string topicName)
        {
            var td = new TopicDescription(topicName) {EnablePartitioning = true};
            if (await nm.TopicExistsAsync(topicName))
            {
                await nm.DeleteTopicAsync(topicName);
            }
            await nm.CreateTopicAsync(td);
            td = await nm.GetTopicAsync(topicName);
            Console.WriteLine("Topic \"" + topicName + "\" created. Topic size: " + td.MaxSizeInMegabytes + " MByte.");
            return td;
        }

        // Create a new Service Bus message.

        // Process received message. Message pump will auto-complete message when this method returns.
        static void ProcessMessage(BrokeredMessage msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Received message:");
            Console.WriteLine("   Label:    " + msg.Label);
            Console.WriteLine("   Body:     " + msg.GetBody<string>());
            Console.WriteLine("   SeqNum:   " + msg.SequenceNumber);
            Console.WriteLine("   MsgID:    " + msg.MessageId);
            Console.WriteLine("   Session:  " + msg.SessionId);
            Console.WriteLine("   ParKey:   " + msg.PartitionKey);
            Console.WriteLine("   ViaParKey:" + msg.ViaPartitionKey);
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}