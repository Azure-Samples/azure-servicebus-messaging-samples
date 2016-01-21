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
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
    using Microsoft.ServiceBus.Samples.AsyncTransactions;

    class Program : IDynamicSample
    {
        const string QueueName = "AsyncTransactionTestQueue";
        const bool FaultInjectorActive = false; // TESTING ONLY: Set to "true" to induce faults.


        // Connection string of your Azure Service Bus or Windows Server Service Bus namespace.
        // For Azure Service Bus namespaces, go to the Azure portal, mark your namespace, click Connection Information
        // button on the bottom of the page. Then copy the SAS RootManageSharedAccessKey.
        // For Windows Server Service Bus namespace, run the cmdlet Get-SBClientConfiguration on the server. 
        // 
        // If yu are using Windows Server Service Bus and this client runs on a different machine than Service Bus, 
        // import the server certificate to the client machine as described in 
        // http://msdn.microsoft.com/en-us/library/windowsazure/jj192993.aspx.


        public async Task Run(string namespaceAddress, string manageToken)
        {
            // Create namespace manager and create Service Bus queue if it does not exist already.
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(manageToken);
            var namespaceManager = new NamespaceManager(namespaceAddress, tokenProvider);
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
            var messagingFactory = MessagingFactory.Create(namespaceAddress, tokenProvider);
            var queueClient = messagingFactory.CreateQueueClient(QueueName, ReceiveMode.ReceiveAndDelete);

            // Create transaction and callback state. Initialize synchronization counter.
            var operationState = new OperationState(queueClient);
            operationState.faultInjector = new FaultInjector(FaultInjectorActive);

            // Within a transaction scope, call BeginSend() for multiple messages.
            var txScope = new TransactionScope(operationState.tx);
            try
            {
                // Send three messages using BeginSend().
                for (var i = 1; i <= 3; i++)
                {
                    var msg = CreateBrokeredMessage(i);
                    Console.WriteLine("Begin sending message {0}.", msg.Label);
                    Interlocked.Increment(ref operationState.synchronizationCounter); // Increment counter once for every BeginSend().

                    // TESTING ONLY: Simulate a fault that causes BeginSend() to return an error.
                    operationState.faultInjector.InjectFaultAtBeingSend();

#pragma warning disable 4014
                    queueClient.SendAsync(msg).ContinueWith(
                        async t =>
                        {
                            try
                            {
                                if (t.IsFaulted && t.Exception != null)
                                {
                                    throw t.Exception;
                                }
                                operationState.faultInjector.InjectFaultAtEndSend();
                                Console.WriteLine("Send completed.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Send returns {0}: {1}", ex.GetType(), ex.Message);
                                Interlocked.Increment(ref operationState.exceptionCounter);
                                // Increment exception counter to indicate that transaction needs to be aborted.
                            }
                            await CommitTransactionIfReadyAsync(operationState);
                        });
#pragma warning restore 4014
                }


                // Sending two messages using BeginSendBatch().
                var messageBatch = new List<BrokeredMessage>
                {
                    CreateBrokeredMessage(4),
                    CreateBrokeredMessage(5)
                };
                Console.WriteLine("Begin sending message batch");
                Interlocked.Increment(ref operationState.synchronizationCounter); // Increment counter once for every BeginSendBatch().

                // TESTING ONLY: Simulate a fault that causes BeginSendBatch() to return an error.
                operationState.faultInjector.InjectFaultAtBeingSend();

#pragma warning disable 4014
                queueClient.SendBatchAsync(messageBatch).ContinueWith(
                    async t =>
                    {
                        try
                        {
                            if (t.IsFaulted && t.Exception != null)
                            {
                                throw t.Exception;
                            }

                            // TESTING ONLY: Simulate a fault that causes EndSend() to return an error.
                            operationState.faultInjector.InjectFaultAtEndSend();

                            Console.WriteLine("SendBatch completed.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("EndSendBatch returns {0}: {1}", ex.GetType(), ex.Message);
                            Interlocked.Increment(ref operationState.exceptionCounter);
                            // Increment exception counter to indicate that transaction needs to be aborted.
                        }

                        await CommitTransactionIfReadyAsync(operationState);
                    });
#pragma warning restore 4014

                Console.WriteLine("SendBatch completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("BeginSend returns {0}: {1}", ex.GetType(), ex.Message);
                Interlocked.Decrement(ref operationState.synchronizationCounter);
                    // Decrement synchronization counter because EndSend() won't be called for this BeginSend().
                Interlocked.Increment(ref operationState.exceptionCounter); // Increment exception counter to indicate that transaction needs to be aborted.
            }
            txScope.Complete();
            txScope.Dispose();

            // Commit transaction if all EndSend() calls have been made.
            await this.CommitTransactionIfReadyAsync(operationState);

            // Receive multiple messages.
            for (var i = 1; i <= 5; i++)
            {
                var msgRecv = await queueClient.ReceiveAsync(TimeSpan.FromSeconds(1));
                    // Use a short timeout to reduce completion of sample in case we induce faults.
                PrintBrokeredMessage(msgRecv);
            }

            //// Cleanup.
            Console.WriteLine("\nPress ENTER to exit\n");
            Console.ReadLine();
            queueClient.Close();
            messagingFactory.Close();
            namespaceManager.DeleteQueue(QueueName);
        }

        // Commit transaction if transaction scope is closed and EndSend() has been called for all operations.
        async Task CommitTransactionIfReadyAsync(OperationState state)
        {
            if (Interlocked.Decrement(ref state.synchronizationCounter) == 0)
            {
                if (state.exceptionCounter == 0)
                {
                    await Task.Factory.FromAsync(state.tx.BeginCommit, state.tx.EndCommit, null);
                }
                else
                {
                    state.tx.Rollback();
                    Console.WriteLine("Transaction is: " + state.tx.TransactionInformation.Status);
                }
            }
        }

        // Create a new Service Bus message.
        public static BrokeredMessage CreateBrokeredMessage(int i)
        {
            // Create a Service Bus message.
            var msg = new BrokeredMessage("This is the body of message " + i);
            msg.Properties.Add("Priority", 1);
            msg.Properties.Add("Importance", "High");
            msg.Label = "M" + i;
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
                foreach (var p in msg.Properties)
                {
                    Console.WriteLine("   Property: " + p.Key + " = " + p.Value);
                }
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
    }
}