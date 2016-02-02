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

    public class Program : IBasicQueueSendReceiveSample
    {
        public async Task Run(string namespaceAddress, string queueName, string sendToken, string receiveToken)
        {
            // Create communication objects to send and receive on the queue
            var senderMessagingFactory = await MessagingFactory.CreateAsync(namespaceAddress, TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken));
            var sender = await senderMessagingFactory.CreateMessageSenderAsync(queueName);

            var receiverMessagingFactory = await MessagingFactory.CreateAsync(namespaceAddress, TokenProvider.CreateSharedAccessSignatureTokenProvider(receiveToken));
            var receiver = await receiverMessagingFactory.CreateMessageReceiverAsync(queueName, ReceiveMode.PeekLock);

            //-------------------------------------------------------------------------------------
            // 1: Send/Complete in a Transaction and Complete
            Console.WriteLine("\nScenario 1: Send/Complete in a Transaction and then Complete");
            //-------------------------------------------------------------------------------------
            await this.SendAndCompleteInTransactionAndCommit(sender, receiver);

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to move to the next scenario.");
            Console.ReadLine();

            //-------------------------------------------------------------------------------------
            // 2: Send/Complete in a Transaction and Abort
            Console.WriteLine("\nScenario 2: Send/Complete in a Transaction and do not Complete");
            //-------------------------------------------------------------------------------------
            await this.SendAndCompleteInTransactionAndRollback(sender, receiver);

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();

            // Cleanup:
            await receiver.CloseAsync();
            await sender.CloseAsync();
            await senderMessagingFactory.CloseAsync();
            await receiverMessagingFactory.CloseAsync();
        }


        async Task SendAndCompleteInTransactionAndCommit(MessageSender sender, MessageReceiver receiver)
        {
            // Seed the queue with a message - we'll transactionally complete this message and send a response
            Console.WriteLine("Sending Message 'Message 1'");
            var requestMessage = new BrokeredMessage("Message 1");
            await sender.SendAsync(requestMessage);

            // Both Send and Complete are supported as part of a local transaction, but PeekLock or
            // ReceiveAndDelete are not. We'll receive a message outside of a transaction scope,
            // and both Complete it and Send a reply within a transaction scope.
            Console.Write("Peek-Lock the Message... ");
            var receivedMessage = await receiver.ReceiveAsync();
            var receivedMessageBody = receivedMessage.GetBody<string>();
            Console.WriteLine(receivedMessageBody);

            // Create a new global transaction scope
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                Console.WriteLine("Inside Transaction {0}", Transaction.Current.TransactionInformation.LocalIdentifier);

                var replyMessage = new BrokeredMessage("Reply To - " + receivedMessageBody);

                // This call to Send(BrokeredMessage) takes part in the local transaction and will 
                // not persist until the transaction Commits; if the transaction is not committed, 
                // the operation will Rollback
                Console.WriteLine("Sending Reply in a Transaction");
                await sender.SendAsync(replyMessage);

                // This call to Complete() also takes part in the local transaction and will not 
                // persist until the transaction Commits; if the transaction is not committed, the 
                // operation will Rollback
                Console.WriteLine("Completing message in a Transaction");
                await receivedMessage.CompleteAsync();

                // Complete the transaction scope, as the transaction commits, the reply message is 
                // sent and the request message is completed as a single, atomic unit of work
                Console.WriteLine("Marking the Transaction Scope as Completed");
                scope.Complete();
            }

            // Receive the reply message
            Console.Write("Receive the reply... ");
            var receivedReplyMessage = await receiver.ReceiveAsync();
            Console.WriteLine(receivedReplyMessage.GetBody<string>());
            await receivedReplyMessage.CompleteAsync();
        }

        async Task SendAndCompleteInTransactionAndRollback(MessageSender sender, MessageReceiver receiver)
        {
            // Seed the queue with a message - we'll transactionally complete this message and send a response
            Console.WriteLine("Sending Message 'Message 2'");
            var requestMessage = new BrokeredMessage("Message 2");
            await sender.SendAsync(requestMessage);

            // Both Send and Complete are supported as part of a local transaction, but PeekLock or
            // ReceiveAndDelete are not. We'll receive a message outside of a transaction scope,
            // and both Complete it and Send a reply within a transaction scope.
            Console.Write("Peek-Lock the Message... ");
            var receivedMessage = await receiver.ReceiveAsync();
            var receivedMessageBody = receivedMessage.GetBody<string>();
            Console.WriteLine(receivedMessageBody);

            // Create a new global transaction scope
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                Console.WriteLine("Inside Transaction {0}", Transaction.Current.TransactionInformation.LocalIdentifier);

                var replyMessage = new BrokeredMessage("Reply To - " + receivedMessageBody);

                // This call to Send(BrokeredMessage) takes part in the local transaction and will 
                // not persist until the transaction Commits; if the transaction is not committed, 
                // the operation will Rollback
                Console.WriteLine("Sending Reply in a Transaction");
                await sender.SendAsync(replyMessage);

                // This call to Complete() also takes part in the local transaction and will not 
                // persist until the transaction Commits; if the transaction is not committed, the 
                // operation will Rollback
                Console.WriteLine("Completing message in a Transaction");
                await receivedMessage.CompleteAsync();

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
            var receivedReplyMessage = await receiver.ReceiveAsync();
            Console.WriteLine(receivedReplyMessage.GetBody<string>());
            await receivedReplyMessage.CompleteAsync();
        }

        
    }
}