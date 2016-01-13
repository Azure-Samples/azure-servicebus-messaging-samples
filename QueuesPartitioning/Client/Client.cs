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
using System.Transactions;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Samples.PartitionedEntity
{
    public class Client
    {
        // Partitioned entities is only available in Azure Service Bus. It is not available in Service Bus Server v1.1.
        //
        // Connection string of your Azure Service Bus. Get the connection string from the Azure Portal:
        // Mark your Service Bus namespace and press the Connection Information button at the bottom of the page.
        //
        // BE AWARE THAT HARDCODING YOUR CONNECTION STRING IS A SECURITY RISK IF YOU SHARE THIS CODE.
        public const string ConnectionString = "Endpoint=sb://YOUR-NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";

        const string QueueName = "PartitionedQueue";
        const string TransferQueueName = "PartitionedTransferQueue";
        const string TransferTopicName = "PartitionedTransferTopic";
        const string TransferSubscriptionName = "Sub1";

        static void Main(string[] args)
        {
            NamespaceManager nm = NamespaceManager.CreateFromConnectionString(ConnectionString);
            MessagingFactory mf = MessagingFactory.CreateFromConnectionString(ConnectionString);

            Console.WriteLine("Non-session-aware queue");
            RunSample(nm, mf);

            Console.WriteLine("Session-aware queue");
            RunSessionSample(nm, mf);

            Console.WriteLine("Autoforwarding");
            RunAutoForwardSample(nm, mf);

            Console.WriteLine("Send via queue");
            RunTransferSample(nm, mf);

            mf.Close();
        }

        static void RunSample(NamespaceManager nm, MessagingFactory mf)
        {
            // Create partitioned non-session-aware queue.
            CreatePartitionedQueue(nm, QueueName, false);

            // Send messages without a transaction.
            QueueClient qc = mf.CreateQueueClient(QueueName, ReceiveMode.ReceiveAndDelete);
            for (int i = 1; i <= 2; i++)
            {
                BrokeredMessage msg = CreateBrokeredMessage(i);
                qc.Send(msg);
            }

            // Send messages within a transaction.
            CommittableTransaction committableTransaction = new CommittableTransaction();
            using (TransactionScope ts = new TransactionScope(committableTransaction))
            {
                for (int i = 3; i <= 4; i++)
                {
                    BrokeredMessage msg = CreateBrokeredMessage(i);
                    msg.PartitionKey = "myPartitionKey";
                    qc.Send(msg);
                }
                ts.Complete();
            }
            committableTransaction.Commit();

            // Receive messages.
            for (int i = 1; i <= 4; i++)
            {
                BrokeredMessage msg = qc.Receive();
                ProcessMessage(msg);
            }

            // Cleanup.
            Console.WriteLine("\nPress ENTER to continue.");
            Console.ReadLine();
            qc.Close();
            nm.DeleteQueue(QueueName);
        }

        static void RunSessionSample(NamespaceManager nm, MessagingFactory mf)
        {
            // Create partitioned session-aware queue.
            CreatePartitionedQueue(nm, QueueName, true);

            // Send messages without a transaction.
            QueueClient qc = mf.CreateQueueClient(QueueName, ReceiveMode.ReceiveAndDelete);
            for (int i = 1; i <= 2; i++)
            {
                BrokeredMessage msg = CreateBrokeredMessage(i);
                msg.SessionId = "MySessionId";
                qc.Send(msg);
            }

            // Send messages within a transaction.
            CommittableTransaction committableTransaction = new CommittableTransaction();
            using (TransactionScope ts = new TransactionScope(committableTransaction))
            {
                for (int i = 3; i <= 4; i++)
                {
                    BrokeredMessage msg = CreateBrokeredMessage(i);
                    msg.SessionId = "MySessionId";
                    msg.PartitionKey = "MySessionId"; // This line is optional and can be omitted.
                    qc.Send(msg);
                }
                ts.Complete();
            }
            committableTransaction.Commit();

            // Receive messages.
            MessageSession session = qc.AcceptMessageSession("MySessionId");
            for (int i = 1; i <= 4; i++)
            {
                BrokeredMessage msg = session.Receive();
                ProcessMessage(msg);
            }

            // Cleanup.
            Console.WriteLine("\nPress ENTER to continue.");
            Console.ReadLine();
            qc.Close();
            nm.DeleteQueue(QueueName);
        }

        static void RunAutoForwardSample(NamespaceManager nm, MessagingFactory mf)
        {
            // Create partitioned target queue. The target queue is session-aware.
            CreatePartitionedQueue(nm, QueueName, true);

            // Create partitioned transfer topic.
            TopicDescription transferTopic = CreatePartitionedTopic(nm, TransferTopicName);
            SubscriptionDescription transferSubscription = new SubscriptionDescription(transferTopic.Path, TransferSubscriptionName) { ForwardTo = QueueName };
            nm.CreateSubscription(transferSubscription);

            // Send message within a transaction. The transaction spans the sending to the transfer queue.
            MessageSender sender = mf.CreateMessageSender(TransferTopicName);
            CommittableTransaction committableTransaction = new CommittableTransaction();
            using (TransactionScope ts = new TransactionScope(committableTransaction))
            {
                for (int i = 1; i <= 2; i++)
                {
                    BrokeredMessage msg = CreateBrokeredMessage(i);
                    msg.SessionId = "MySessionId"; // Used as the partition key for the target queue.
                    sender.Send(msg);
                }
                ts.Complete();
            }
            committableTransaction.Commit();

            // Reeiver messages.
            QueueClient qc = mf.CreateQueueClient(QueueName, ReceiveMode.ReceiveAndDelete);
            MessageSession session = qc.AcceptMessageSession("MySessionId");
            for (int i = 1; i <= 2; i++)
            {
                BrokeredMessage msg = session.Receive();
                ProcessMessage(msg);
            }

            // Cleanup.
            Console.WriteLine("\nPress ENTER to continue.");
            Console.ReadLine();
            sender.Close();
            qc.Close();
            nm.DeleteQueue(QueueName);
            nm.DeleteQueue(TransferTopicName);
        }

        static void RunTransferSample(NamespaceManager nm, MessagingFactory mf)
        {
            // Create partitioned target queue and transfer queue. The target queue is session-aware.
            QueueDescription targetQueue = CreatePartitionedQueue(nm, QueueName, true);
            QueueDescription transferQueue = CreatePartitionedQueue(nm, TransferQueueName, false);

            // Send message within a transaction. The transaction spans the sending to the transfer queue.
            MessageSender sender = mf.CreateMessageSender(transferDestinationEntityPath: targetQueue.Path, viaEntityPath: transferQueue.Path);
            CommittableTransaction committableTransaction = new CommittableTransaction();
            using (TransactionScope ts = new TransactionScope(committableTransaction))
            {
                for (int i = 1; i <= 2; i++)
                {
                    BrokeredMessage msg = CreateBrokeredMessage(i);
                    msg.SessionId = "MySessionId"; // Used as the partition key for the target queue.
                    msg.ViaPartitionKey = "MyViaPartitionKey"; // Used as the partition key for the transfer queue.
                    sender.Send(msg);
                }
                ts.Complete();
            }
            committableTransaction.Commit();

            // Reeiver messages.
            QueueClient qc = mf.CreateQueueClient(targetQueue.Path, ReceiveMode.ReceiveAndDelete);
            MessageSession session = qc.AcceptMessageSession("MySessionId");
            for (int i = 1; i <= 2; i++)
            {
                BrokeredMessage msg = session.Receive();
                ProcessMessage(msg);
            }

            // Cleanup.
            Console.WriteLine("\nPress ENTER to exit.");
            Console.ReadLine();
            sender.Close();
            qc.Close();
            nm.DeleteQueue(QueueName);
            nm.DeleteQueue(TransferQueueName);
        }

        static QueueDescription CreatePartitionedQueue(NamespaceManager nm, string queueName, bool requiresSession)
        {
            QueueDescription qd = new QueueDescription(queueName) { EnablePartitioning = true, RequiresSession = requiresSession };
            if (nm.QueueExists(queueName))
            {
                nm.DeleteQueue(queueName);
            }
            nm.CreateQueue(qd);
            qd = nm.GetQueue(queueName);
            Console.WriteLine("Queue \"" + queueName + "\" created. Queue size: " + qd.MaxSizeInMegabytes + " MByte.");
            return qd;
        }

        static TopicDescription CreatePartitionedTopic(NamespaceManager nm, string topicName)
        {
            TopicDescription td = new TopicDescription(topicName) { EnablePartitioning = true };
            if (nm.TopicExists(topicName))
            {
                nm.DeleteTopic(topicName);
            }
            nm.CreateTopic(td);
            td = nm.GetTopic(topicName);
            Console.WriteLine("Topic \"" + topicName + "\" created. Topic size: " + td.MaxSizeInMegabytes + " MByte.");
            return td;
        }

        // Create a new Service Bus message.
        static BrokeredMessage CreateBrokeredMessage(int i)
        {
            // Create a Service Bus message.
            BrokeredMessage msg = new BrokeredMessage("This is the body of message " + i.ToString());
            msg.Label = "M" + i.ToString();
            msg.TimeToLive = TimeSpan.FromSeconds(90);
            return msg;
        }

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
