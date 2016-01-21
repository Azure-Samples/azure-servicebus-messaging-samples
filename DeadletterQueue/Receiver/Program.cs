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

    public class Program : IBasicQueueReceiveSample
    {
        const int MaxRetryCount = 5;
        readonly System.Collections.Hashtable hashTable = new System.Collections.Hashtable();

        public async Task Run(string namespaceAddress, string queueName, string receiveToken)
        {
            var messagingFactory = MessagingFactory.Create(
                namespaceAddress,
                TokenProvider.CreateSharedAccessSignatureTokenProvider(receiveToken));
            var queueClient = messagingFactory.CreateQueueClient(queueName);

            // Read messages from queue until it is empty:
            Console.WriteLine("Reading messages from queue...");

            BrokeredMessage receivedMessage;
            while ((receivedMessage = await queueClient.ReceiveAsync(TimeSpan.FromSeconds(10))) != null)
            {
                var retryCount = 0;
                while (retryCount < MaxRetryCount)
                {
                    if (ProcessOrder(receivedMessage))
                    {
                        break;
                    }
                    retryCount++;
                }

                if (retryCount == MaxRetryCount)
                {
                    Console.WriteLine(
                        "Adding Order {0} with {1} number of items and {2} total to DeadLetter queue",
                        receivedMessage.Properties["OrderNumber"],
                        receivedMessage.Properties["NumberOfItems"],
                        receivedMessage.Properties["OrderTotal"]);
                    await receivedMessage.DeadLetterAsync("UnableToProcess", "Failed to process in reasonable attempts");
                }
            }

            Console.WriteLine();
            Console.WriteLine("No more messages left in queue. Logging dead lettered messages...");

            // Log the dead-lettered messages that could not be processed:
            var deadLetterClient = messagingFactory.CreateQueueClient(
                QueueClient.FormatDeadLetterPath(queueClient.Path),
                ReceiveMode.ReceiveAndDelete);
            BrokeredMessage receivedDeadLetterMessage;
            while ((receivedDeadLetterMessage = deadLetterClient.Receive(TimeSpan.FromSeconds(10))) != null)
            {
                LogOrder(receivedDeadLetterMessage);
            }

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();
        }

        /// <summary>
        ///     This method simulates the random failure behavior which happens in real world.
        ///     We will randomly select a message to fail based on some random number value. To make sure the message processing
        ///     fails
        ///     all the times during subsequent retries, we add the result to the hashtable and retrieve it from there.
        /// </summary>
        /// <param name="receivedMessage"></param>
        /// <returns></returns>
        bool ProcessOrder(BrokeredMessage receivedMessage)
        {
            if (hashTable.ContainsKey(receivedMessage.Properties["OrderNumber"]))
            {
                return false;
            }

            if (new Random().Next()%2 == 0 ? true : false)
            {
                Console.WriteLine(
                    "Received Order {0} with {1} number of items and {2} total",
                    receivedMessage.Properties["OrderNumber"],
                    receivedMessage.Properties["NumberOfItems"],
                    receivedMessage.Properties["OrderTotal"]);
                return true;
            }
            hashTable.Add(receivedMessage.Properties["OrderNumber"], false);
            return false;
        }

        void LogOrder(BrokeredMessage message)
        {
            Console.WriteLine(
                "Order {0} with {1} number of items and {2} total logged from DeadLetter queue. DeadLettering Reason is \"{3}\" and Deadlettering error description is \"{4}\"",
                message.Properties["OrderNumber"],
                message.Properties["NumberOfItems"],
                message.Properties["OrderTotal"],
                message.Properties["DeadLetterReason"],
                message.Properties["DeadLetterErrorDescription"]);
        }

    }
}