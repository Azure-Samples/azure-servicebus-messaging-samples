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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Transactions;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.ServiceBus.Samples.AsyncTransactions
{
    class Application
    {
        private const string QueueName = "AsyncTransactionTestQueue";
        const bool FaultInjectorActive = false; // TESTING ONLY: Set to "true" to induce faults.

        // Connection string of your Azure Service Bus or Windows Server Service Bus namespace.
        // For Azure Service Bus namespaces, go to the Azure portal, mark your namespace, click Connection Information
        // button on the bottom of the page. Then copy the SAS RootManageSharedAccessKey.
        // For Windows Server Service Bus namespace, run the cmdlet Get-SBClientConfiguration on the server. 
        // 
        // If yu are using Windows Server Service Bus and this client runs on a different machine than Service Bus, 
        // import the server certificate to the client machine as described in 
        // http://msdn.microsoft.com/en-us/library/windowsazure/jj192993.aspx.
        // 
        // BE AWARE THAT HARDCODING YOUR CONNECTION STRING IS A SECURITY RISK IF YOU SHARE THIS CODE. 
        const string SasConnectionString = "Endpoint=sb://YOUR_NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";


        public static void Main()
        {
            ServiceBusConnectionStringBuilder connBuilder = new ServiceBusConnectionStringBuilder(SasConnectionString);

            // Create namespace manager and create Service Bus queue if it does not exist already.
            NamespaceManager namespaceManager = NamespaceManager.CreateFromConnectionString(connBuilder.ToString());
            if (!namespaceManager.QueueExists(QueueName))
            {
                namespaceManager.CreateQueue(QueueName);
                Console.WriteLine("Created Service Bus queue \"{0}\".", QueueName);
            }
            else
            {
                Console.WriteLine("Service Bus queue \"{0}\" already exists.", QueueName);
            }

            // Create a MessagingFactory and QueueClient.
            MessagingFactory messagingFactory = MessagingFactory.CreateFromConnectionString(connBuilder.ToString());
            QueueClient queueClient = messagingFactory.CreateQueueClient(QueueName, ReceiveMode.ReceiveAndDelete);

            // Create transaction and callback state. Initialize synchronization counter.
            CallbackState state = new CallbackState(queueClient);
            state.faultInjector = new FaultInjector(FaultInjectorActive);

            // Within a transaction scope, call BeginSend() for multiple messages.
            TransactionScope txScope = new TransactionScope(state.tx);
            try
            {
                // Send three messages using BeginSend().
                for (int i = 1; i <= 3; i++)
                {
                    BrokeredMessage msg = CreateBrokeredMessage(i);
                    Console.WriteLine("Begin sending message {0}.", msg.Label);
                    Interlocked.Increment(ref state.synchronizationCounter); // Increment counter once for every BeginSend().

                    // TESTING ONLY: Simulate a fault that causes BeginSend() to return an error.
                    state.faultInjector.InjectFaultAtBeingSend();

                    queueClient.BeginSend(msg, ProcessSendCallback, state);
                }

                // Sending two messages using BeginSendBatch().
                List<BrokeredMessage> messageBatch = new List<BrokeredMessage>();
                messageBatch.Add(CreateBrokeredMessage(4));
                messageBatch.Add(CreateBrokeredMessage(5));
                Console.WriteLine("Begin sending message batch");
                Interlocked.Increment(ref state.synchronizationCounter); // Increment counter once for every BeginSendBatch().

                // TESTING ONLY: Simulate a fault that causes BeginSendBatch() to return an error.
                state.faultInjector.InjectFaultAtBeingSend();
                queueClient.BeginSendBatch(messageBatch, ProcessSendBatchCallback, state);
            }
            catch (Exception ex)
            {
                Console.WriteLine("BeginSend returns {0}: {1}", ex.GetType(), ex.Message);
                Interlocked.Decrement(ref state.synchronizationCounter); // Decrement synchronization counter because EndSend() won't be called for this BeginSend().
                Interlocked.Increment(ref state.exceptionCounter); // Increment exception counter to indicate that transaction needs to be aborted.
            }
            txScope.Complete();
            txScope.Dispose();

            // Commit transaction if all EndSend() calls have been made.
            CommitTransactionIfReady(state);

            // Receive multiple messages.
            for (int i = 1; i <= 5; i++)
            {
                BrokeredMessage msgRecv = queueClient.Receive(TimeSpan.FromSeconds(1)); // Use a short timeout to reduce completion of sample in case we induce faults.
                PrintBrokeredMessage(msgRecv);
            }

            // Cleanup.
            Console.WriteLine("\nPress ENTER to exit\n");
            Console.ReadLine();
            queueClient.Close();
            messagingFactory.Close();
            namespaceManager.DeleteQueue(QueueName);
        }

        // Call EndSend(). Commit transaction if transaction scope has been closed.
        static void ProcessSendCallback(IAsyncResult result)
        {
            CallbackState state = result.AsyncState as CallbackState;

            try
            {
                state.qc.EndSend(result);

                // TESTING ONLY: Simulate a fault that causes EndSend() to return an error.
                state.faultInjector.InjectFaultAtEndSend();

                Console.WriteLine("Send completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("EndSend returns {0}: {1}", ex.GetType(), ex.Message);
                Interlocked.Increment(ref state.exceptionCounter); // Increment exception counter to indicate that transaction needs to be aborted.
            }

            CommitTransactionIfReady(state);
        }

        // Call EndSendBatch(). Commit transaction if transaction scope has been closed.
        static void ProcessSendBatchCallback(IAsyncResult result)
        {
            CallbackState state = result.AsyncState as CallbackState;

            try
            {
                state.qc.EndSendBatch(result);

                // TESTING ONLY: Simulate a fault that causes EndSend() to return an error.
                state.faultInjector.InjectFaultAtEndSend();

                Console.WriteLine("SendBatch completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("EndSendBatch returns {0}: {1}", ex.GetType(), ex.Message);
                Interlocked.Increment(ref state.exceptionCounter); // Increment exception counter to indicate that transaction needs to be aborted.
            }

            CommitTransactionIfReady(state);
        }

        // Commit transaction if transaction scope is closed and EndSend() has been called for all operations.
        static void CommitTransactionIfReady(CallbackState state)
        {
            if (Interlocked.Decrement(ref state.synchronizationCounter) == 0)
            {
                if (state.exceptionCounter == 0)
                {
                    state.tx.BeginCommit(CommitTransactionCallback, state);
                }
                else
                {
                    state.tx.Rollback();
                    Console.WriteLine("Transaction is: " + state.tx.TransactionInformation.Status);
                }
            }
        }

        static void CommitTransactionCallback(IAsyncResult result)
        {
            CallbackState state = result.AsyncState as CallbackState;
            state.tx.EndCommit(result);
            Console.WriteLine("Transaction is: " + state.tx.TransactionInformation.Status);
            state.tx.Dispose();
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
            if (msg != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Received message:");
                Console.WriteLine("   Label:    " + msg.Label);
                Console.WriteLine("   Body:     " + msg.GetBody<string>());
                Console.WriteLine("   Sent at:  " + msg.EnqueuedTimeUtc + " UTC");
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
}