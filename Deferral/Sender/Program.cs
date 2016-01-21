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

    public class Program : IBasicQueueSendSample
    {
        public async Task Run(string namespaceAddress, string queueName, string sendToken)
        {
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken);
            var messagingFactory = MessagingFactory.Create(namespaceAddress, tokenProvider);

            var queueClient = messagingFactory.CreateQueueClient(queueName);

            // Send messages to queue:
            Console.WriteLine("Sending messages to queue...");

            var message1 = this.CreateOrderMessage("High");
            await queueClient.SendAsync(message1);
            Console.WriteLine("Sent message {0} with high priority.", message1.MessageId);

            var message2 = this.CreateOrderMessage("Low");
            await queueClient.SendAsync(message2);
            Console.WriteLine("Sent message {0} with low priority.", message2.MessageId);

            var message3 = this.CreateOrderMessage("High");
            await queueClient.SendAsync(message3);
            Console.WriteLine("Sent message {0} with high priority.", message3.MessageId);

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to delete queue and exit.");
            Console.ReadLine();

            // Cleanup:
            await queueClient.CloseAsync();
            await messagingFactory.CloseAsync();
        }

        BrokeredMessage CreateOrderMessage(string priority)
        {
            return new BrokeredMessage
            {
                MessageId = "Order" + Guid.NewGuid(),
                TimeToLive = TimeSpan.FromMinutes(1),
                Properties =
                { {"Priority", priority} }
            };
        }
    }
}