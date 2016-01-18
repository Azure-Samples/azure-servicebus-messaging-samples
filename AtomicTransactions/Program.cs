//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure AppFabric SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples.Transactions
{
    using System;
    using System.Transactions;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    public class Program
    {
        private const string QueueName = "TransactionsSampleQueue";

        public static void Main()
        {
            string serviceBusConnectionString;

            // Read user credentials
            Console.Write("Please provide a connection string to Service Bus (/? for help):\n ");
            serviceBusConnectionString = Console.ReadLine();

            if ((String.Compare(serviceBusConnectionString, "/?") == 0) || (serviceBusConnectionString.Length == 0))
            {
                Console.Write("To connect to the Service Bus cloud service, go to the Windows Azure portal and select 'View Connection String'.\n");
                Console.Write("To connect to the Service Bus for Windows Server, use the get-sbClientConfiguration PowerShell cmdlet.\n\n");
                Console.Write("A Service Bus connection string has the following format: \nEndpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<keyName>;SharedAccessKey=<key>");

                serviceBusConnectionString = Console.ReadLine();
                Environment.Exit(0);
            }


            // Using a ServiceBusNamespaceClient, create a queue for incoming messages and a queue for outgoing ones.
            NamespaceManager namespaceManager =  NamespaceManager.CreateFromConnectionString(serviceBusConnectionString);

            // Create a queue with a relatively short PeekLock timeout
            Console.WriteLine("Creating Queues...");
            if (namespaceManager.QueueExists(QueueName))
            {
                namespaceManager.DeleteQueue(QueueName);
            }
            QueueDescription queueDescription = namespaceManager.CreateQueue(new QueueDescription(QueueName){LockDuration = TimeSpan.FromSeconds(15)});

            // Create a MessagingFactory to send and receive messages
            MessagingFactory messagingFactory = MessagingFactory.CreateFromConnectionString(serviceBusConnectionString);

            // Create communication objects to send and receive on the queue
            MessageSender sender = messagingFactory.CreateMessageSender(queueDescription.Path);
            MessageReceiver receiver = messagingFactory.CreateMessageReceiver(queueDescription.Path, ReceiveMode.PeekLock);

            //-------------------------------------------------------------------------------------
            // 1: Send/Complete in a Transaction and Complete
            Console.WriteLine("\nScenario 1: Send/Complete in a Transaction and then Complete");
            //-------------------------------------------------------------------------------------
            SendAndCompleteInTransactionAndCommit(sender, receiver);

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to move to the next scenario.");
            Console.ReadLine();

            //-------------------------------------------------------------------------------------
            // 2: Send/Complete in a Transaction and Abort
            Console.WriteLine("\nScenario 2: Send/Complete in a Transaction and do not Complete");
            //-------------------------------------------------------------------------------------
            SendAndCompleteInTransactionAndRollback(sender, receiver);

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();

            // Cleanup:
            receiver.Close();
            sender.Close();
            messagingFactory.Close();
            namespaceManager.DeleteQueue(QueueName);
        }

        private static void SendAndCompleteInTransactionAndRollback(MessageSender sender, MessageReceiver receiver)
        {
            // Seed the queue with a message - we'll transactionally complete this message and send a response
            Console.WriteLine("Sending Message 'Message 2'");
            BrokeredMessage requestMessage = new BrokeredMessage("Message 2");
            sender.Send(requestMessage);

            // Both Send and Complete are supported as part of a local transaction, but PeekLock or
            // ReceiveAndDelete are not. We'll receive a message outside of a transaction scope,
            // and both Complete it and Send a reply within a transaction scope.
            Console.Write("Peek-Lock the Message... ");
            BrokeredMessage receivedMessage = receiver.Receive();
            string receivedMessageBody = receivedMessage.GetBody<string>();
            Console.WriteLine(receivedMessageBody);

            // Create a new global transaction scope
            using (TransactionScope scope = new TransactionScope())
            {
                Console.WriteLine("Inside Transaction {0}", Transaction.Current.TransactionInformation.LocalIdentifier);

                BrokeredMessage replyMessage = new BrokeredMessage("Reply To - " + receivedMessageBody);

                // This call to Send(BrokeredMessage) takes part in the local transaction and will 
                // not persist until the transaction Commits; if the transaction is not committed, 
                // the operation will Rollback
                Console.WriteLine("Sending Reply in a Transaction");
                sender.Send(replyMessage);

                // This call to Complete() also takes part in the local transaction and will not 
                // persist until the transaction Commits; if the transaction is not committed, the 
                // operation will Rollback
                Console.WriteLine("Completing message in a Transaction");
                receivedMessage.Complete();

                // Do not complete the transaction scope. When it disposes, the transaction will
                // rollback causing the message send and message complete not to be persisted.
                // Typically, a transaction would fail to complete because either an exception is
                // thrown within the transaction timeout, or the transaction times out because it
                // lasts for more than one minute (the maximum permitted duration of a transaction
                // service side).
                Console.WriteLine("Exiting the transaction scope without committing...");
            }

            // Since the transaction aborted, the reply message was not sent and the request
            // message was not completed. Once the message's peek lock expires, we will be able to
            // receive it again.
            Console.Write("Receive the request again (this can take a while, because we're waiting for the PeekLock to timeout)... ");
            BrokeredMessage receivedReplyMessage = receiver.Receive();
            Console.WriteLine(receivedReplyMessage.GetBody<string>());
            receivedReplyMessage.Complete();
        }

        private static void SendAndCompleteInTransactionAndCommit(MessageSender sender, MessageReceiver receiver)
        {
            // Seed the queue with a message - we'll transactionally complete this message and send a response
            Console.WriteLine("Sending Message 'Message 1'");
            BrokeredMessage requestMessage = new BrokeredMessage("Message 1");
            sender.Send(requestMessage);

            // Both Send and Complete are supported as part of a local transaction, but PeekLock or
            // ReceiveAndDelete are not. We'll receive a message outside of a transaction scope,
            // and both Complete it and Send a reply within a transaction scope.
            Console.Write("Peek-Lock the Message... ");
            BrokeredMessage receivedMessage = receiver.Receive();
            string receivedMessageBody = receivedMessage.GetBody<string>();
            Console.WriteLine(receivedMessageBody);

            // Create a new global transaction scope
            using (TransactionScope scope = new TransactionScope())
            {
                Console.WriteLine("Inside Transaction {0}", Transaction.Current.TransactionInformation.LocalIdentifier);

                BrokeredMessage replyMessage = new BrokeredMessage("Reply To - " + receivedMessageBody);

                // This call to Send(BrokeredMessage) takes part in the local transaction and will 
                // not persist until the transaction Commits; if the transaction is not committed, 
                // the operation will Rollback
                Console.WriteLine("Sending Reply in a Transaction");
                sender.Send(replyMessage);

                // This call to Complete() also takes part in the local transaction and will not 
                // persist until the transaction Commits; if the transaction is not committed, the 
                // operation will Rollback
                Console.WriteLine("Completing message in a Transaction");
                receivedMessage.Complete();

                // Complete the transaction scope, as the transaction commits, the reply message is 
                // sent and the request message is completed as a single, atomic unit of work
                Console.WriteLine("Marking the Transaction Scope as Completed");
                scope.Complete();
            }

            // Receive the reply message
            Console.Write("Receive the reply... ");
            BrokeredMessage receivedReplyMessage = receiver.Receive();
            Console.WriteLine(receivedReplyMessage.GetBody<string>());
            receivedReplyMessage.Complete();
        }
    }
}
