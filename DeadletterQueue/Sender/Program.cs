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
            // Get credentials and set up management and runtime messaging entities:

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken);
            var messagingFactory = MessagingFactory.Create(namespaceAddress, tokenProvider);

            var queueClient = messagingFactory.CreateQueueClient(queueName, ReceiveMode.ReceiveAndDelete);

            // Send messages to queue, of different order types:
            Console.WriteLine("Sending messages to queue...");
            await this.CreateAndSendOrderMessage("DeliveryOrder", 1, 10, 15, queueClient);
            await this.CreateAndSendOrderMessage("StayInOrder", 2, 15, 500, queueClient);
            await this.CreateAndSendOrderMessage("TakeOutOrder", 3, 1, 25, queueClient);
            await this.CreateAndSendOrderMessage("TakeOutOrder", 5, 3, 25, queueClient);
            await this.CreateAndSendOrderMessage("DeliveryOrder", 4, 100, 100000, queueClient);


            Console.WriteLine();
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();

            // Cleanup:
            messagingFactory.Close();
        }

        Task CreateAndSendOrderMessage(string orderType, int? orderNumber, int numberOfItems, int orderTotal, QueueClient sender)
        {
            var message = new BrokeredMessage()
            {
                TimeToLive = TimeSpan.FromMinutes(1),
                Properties =
                {
                    { "OrderType", orderType},
                    { "OrderNumber", orderNumber},
                    { "NumberOfItems", numberOfItems},
                    { "OrderTotal", orderTotal}
                }
            };

            Console.WriteLine("Sending message of order type {0}.", message.Properties["OrderType"]);
            return sender.SendAsync(message);
        }
    }
}