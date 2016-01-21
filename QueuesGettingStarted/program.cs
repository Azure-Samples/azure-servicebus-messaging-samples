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

    public class Program : IBasicQueueSendReceiveSample
    {
        public async Task Run(string namespaceAddress, string queueName, string sendToken, string receiveToken)
        {
            await this.SendMessages(namespaceAddress, queueName, sendToken);
            Console.WriteLine("Press any key to start receiving messages that you just sent ...");
            Console.ReadKey();
            await this.ReceiveMessages(namespaceAddress, queueName, receiveToken);
            Console.WriteLine("\nEnd of scenario, press any key to exit.");
            Console.ReadKey();
        }

        async Task SendMessages(string namespaceAddress, string queueName, string sendToken)
        {
            var senderFactory = MessagingFactory.Create(
                namespaceAddress,
                new MessagingFactorySettings
                {
                    TransportType = TransportType.Amqp,
                    TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken)
                });
            senderFactory.RetryPolicy = new RetryExponential(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), 10);

            var sender = senderFactory.CreateQueueClient(queueName);

            var messageList = new List<BrokeredMessage>
            {
                new BrokeredMessage("First message information") {MessageId = "1", TimeToLive = TimeSpan.FromMinutes(2)},
                new BrokeredMessage("Second message information") {MessageId = "2", TimeToLive = TimeSpan.FromMinutes(2)},
                new BrokeredMessage("Third message information") {MessageId = "3", TimeToLive = TimeSpan.FromMinutes(2)}
            };


            Console.WriteLine("\nSending messages to Queue...");

            foreach (var message in messageList)
            {
                while (true)
                {
                    try
                    {
                        await sender.SendAsync(message);
                    }
                    catch (MessagingException e)
                    {
                        if (!e.IsTransient)
                        {
                            Console.WriteLine(e.Message);
                            throw;
                        }
                    }
                    Console.WriteLine("Message sent: Id = {0}, Body = {1}", message.MessageId, message.GetBody<string>());
                    break;
                }
            }
        }

        async Task ReceiveMessages(string namespaceAddress, string queueName, string receiveToken)
        {
            var receiverFactory = MessagingFactory.Create(
                namespaceAddress,
                new MessagingFactorySettings
                {
                    TransportType = TransportType.Amqp,
                    TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(receiveToken)
                });
            receiverFactory.RetryPolicy = new RetryExponential(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), 10);

            var receiver = receiverFactory.CreateQueueClient(queueName, ReceiveMode.PeekLock);

            Console.WriteLine("\nReceiving message from Queue...");
            BrokeredMessage message = null;
            while (true)
            {
                try
                {
                    //receive messages from Queue
                    message = await receiver.ReceiveAsync(TimeSpan.FromSeconds(5));
                    if (message != null)
                    {
                        Console.WriteLine("Message received: Id = {0}, Body = {1}", message.MessageId, message.GetBody<string>());
                        // Further custom message processing could go here...
                        await message.CompleteAsync();
                    }
                    else
                    {
                        //no more messages in the queue
                        break;
                    }
                }
                catch (MessagingException e)
                {
                    if (!e.IsTransient)
                    {
                        Console.WriteLine(e.Message);
                        throw;
                    }
                }
            }
            await receiver.CloseAsync();
            await receiverFactory.CloseAsync();
        }
    }
}