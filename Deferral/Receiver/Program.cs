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
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    public class Program : IBasicQueueReceiveSample
    {
        public async Task Run(string namespaceAddress, string queueName, string receiveToken)
        {
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(receiveToken);
            var messagingFactory = MessagingFactory.Create(namespaceAddress, tokenProvider);

            var queueClient = messagingFactory.CreateQueueClient(queueName);

            // Read messages from queue until queue is empty:
            Console.WriteLine("Reading messages from queue...");

            var deferredSequenceNumbers = new List<long>();

            while (true)
            {
                var receivedMessage = await queueClient.ReceiveAsync(TimeSpan.FromSeconds(10));

                if (receivedMessage == null)
                {
                    break;
                }
                // Low-priority messages will be dealt with later:
                if (receivedMessage.Properties["Priority"].ToString() == "Low")
                {
                    await receivedMessage.DeferAsync();
                    Console.WriteLine("Deferred message with id {0}.", receivedMessage.MessageId);
                    // Deferred messages can only be retrieved by message receipt. Here, keeping track of the
                    // message receipt for a later retrieval:
                    deferredSequenceNumbers.Add(receivedMessage.SequenceNumber);
                }
                else
                {
                    Console.WriteLine("Processed {0}-priority order {1}.", receivedMessage.Properties["Priority"], receivedMessage.MessageId);
                    await receivedMessage.CompleteAsync();
                }
            }

            Console.WriteLine();
            Console.WriteLine("No more messages left in queue. Moving onto deferred messages...");

            // Process the low-priority messages:
            foreach (var sequenceNumber in deferredSequenceNumbers)
            {
                var message = await queueClient.ReceiveAsync(sequenceNumber);
                Console.WriteLine("Processed {0}-priority order {1}.", message.Properties["Priority"], message.MessageId);
                await message.CompleteAsync();
            }

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();
        }
    }
}