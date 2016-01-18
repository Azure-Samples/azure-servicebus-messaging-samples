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

namespace Microsoft.ServiceBus.Samples.DurableSender
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Transactions;
    using Microsoft.ServiceBus.Messaging;

    class Client
    {
        private const string SbusQueueName = "DurableClientQueue";
        private const string SqlConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=SBGatewayDatabase;Integrated Security=true";

        public static void Main()
        {
            string serviceBusNamespace = "YOUR-NAMESPACE";
            string sasKeyName = "RootManageSharedAccessKey";
            string sasKeyValue = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";

            // Create token provider.
            Uri namespaceUri = ServiceBusEnvironment.CreateServiceUri("sb", serviceBusNamespace, string.Empty);
            Console.WriteLine("Namespace URI: " + namespaceUri.ToString());
            TokenProvider tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(sasKeyName, sasKeyValue);

            // Create namespace manager and create Service Bus queue if it does not exist already.
            NamespaceManager namespaceManager = new NamespaceManager(namespaceUri, tokenProvider);
            QueueDescription queueDescription = new QueueDescription(SbusQueueName);
            queueDescription.RequiresDuplicateDetection = true;
            if (!namespaceManager.QueueExists(SbusQueueName))
            {
                namespaceManager.CreateQueue(queueDescription);
                Console.WriteLine("Created Service Bus queue \"{0}\".", SbusQueueName);
            }

            // Create a MessagingFactory.
            MessagingFactory messagingFactory = MessagingFactory.Create(namespaceUri, tokenProvider);

            // Create a durable sender.
            DurableSender durableSender = new DurableSender(messagingFactory, SbusQueueName);

            /*
            ** Send messages.
            */

            // Example 1:
            // Send a message outside a transaction scope. If a transactional MSMQ send queue
            // is used, (Transactional = true) an internal MSMQ transaction is created.
            BrokeredMessage nonTxMsg = CreateBrokeredMessage(1);
            Console.WriteLine("Sending message {0} outside of a transaction.", nonTxMsg.Label);
            durableSender.Send(nonTxMsg);

            // Example 2:
            // Send a message inside a transaction scope.
            BrokeredMessage txMsg = CreateBrokeredMessage(2);
            Console.WriteLine("Sending message {0} within a transaction.", txMsg.Label);
            using (TransactionScope scope = new TransactionScope())
            {
                durableSender.Send(txMsg);
                scope.Complete();
            }

            // Example 3:
            // Send two messages inside a transaction scope. If another resource manager is used
            // (e.g., SQL server), the transaction is automatically promoted to a distributed
            // transaction. If a non-transactional MSMQ send queue is used, (TransactionalSend = false),
            // sending the message is not part of the transaction.
            for (int i = 3; i <= 4; i++)
            {
                BrokeredMessage dtcMsg = CreateBrokeredMessage(i);
                Console.WriteLine("Sending message {0} within a distributed transaction.", dtcMsg.Label);
                try
                {
                    using (TransactionScope scope = new TransactionScope())
                    {
                        // Perform a SQL Server operation to force a distributed transaction.
                        if (!SqlConnectionString.Equals(""))
                        {
                            SqlConnection sqlConnection = new SqlConnection(SqlConnectionString);
                            string commandText = "SELECT * FROM dbo.ContainersTable";
                            SqlCommand sqlCommand = new SqlCommand(commandText, sqlConnection);
                            sqlConnection.Open();
                            sqlCommand.ExecuteNonQuery();

                            // FOR TESTING PURPOSE ONLY: Throw exception to cause the transaction to be aborted. This should
                            // cause message M3 and M4 to not get enqueued.
                            //if (i == 4)
                            //{
                            //    throw new Exception("SqlFailedException");
                            //}
                        }

                        // Send message.
                        durableSender.Send(dtcMsg);
                        scope.Complete();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Sender: " + ex.Message);
                }
            }

            /*
            ** Receive messages.
            */

            QueueClient queueClient = messagingFactory.CreateQueueClient(SbusQueueName, ReceiveMode.ReceiveAndDelete);
            for (int i = 1; i <= 4; i++)
            {
                try
                {
                    BrokeredMessage msg = queueClient.Receive();
                    if (msg != null)
                    {
                        PrintBrokeredMessage(msg);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Receiver: " + ex.Message);
                }
            }

            /*
            ** Cleanup
            */

            Console.WriteLine("\nPress ENTER to exit\n");
            Console.ReadLine();

            durableSender.Dispose();
            queueClient.Close();
            messagingFactory.Close();
            namespaceManager.DeleteQueue(SbusQueueName);
        }

        // Create a new Service Bus message.
        public static BrokeredMessage CreateBrokeredMessage(int i)
        {
            // Create a Service Bus message.
            BrokeredMessage msg = new BrokeredMessage("This is the body of message " + i.ToString());
            msg.Properties.Add("Priority", 1);
            msg.Properties.Add("Importance", "High");
            msg.Label = "M" + i.ToString();
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
                Console.WriteLine("   Sent at:  " + msg.EnqueuedTimeUtc);
                Console.WriteLine("   ID:       " + msg.MessageId);
                Console.WriteLine("   SeqNum:   " + msg.SequenceNumber);
                foreach (KeyValuePair<string, object> p in msg.Properties)
                {
                    Console.WriteLine("   Property: " + p.Key.ToString() + " = " + p.Value.ToString());
                }
                Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}
