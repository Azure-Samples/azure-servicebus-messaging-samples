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
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
    using Newtonsoft.Json;

    public class Program : ISessionQueueSendReceiveSample
    {
        public async Task Run(string namespaceAddress, string queueName, string sendToken, string receiveToken)
        {
            Console.WriteLine("Press any key to exit the scenario");

            CancellationTokenSource cts = new CancellationTokenSource();

            await Task.WhenAll(
                this.SendMessagesAsync(Guid.NewGuid().ToString(), namespaceAddress, queueName, sendToken),
                this.SendMessagesAsync(Guid.NewGuid().ToString(), namespaceAddress, queueName, sendToken),
                this.SendMessagesAsync(Guid.NewGuid().ToString(), namespaceAddress, queueName, sendToken),
                this.SendMessagesAsync(Guid.NewGuid().ToString(), namespaceAddress, queueName, sendToken));

            var receiveTask = this.ReceiveMessagesAsync(namespaceAddress, queueName, receiveToken, cts.Token);
            Console.ReadKey();
            cts.Cancel();

            await receiveTask;
        }

        async Task SendMessagesAsync(string session, string namespaceAddress, string queueName, string sendToken)
        {
            var senderFactory = MessagingFactory.Create(
                namespaceAddress,
                new MessagingFactorySettings
                {
                    TransportType = TransportType.Amqp,
                    TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken)
                });
            senderFactory.RetryPolicy = new RetryExponential(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), 10);

            var sender = await senderFactory.CreateMessageSenderAsync(queueName);

            dynamic data = new[]
            {
                new {step = 1, title = "Shop"},
                new {step = 2, title = "Unpack"},
                new {step = 3, title = "Prepare"},
                new {step = 4, title = "Cook"},
                new {step = 5, title = "Eat"},
            };

            for (int i = 0; i < data.Length; i++)
            {
                var message = new BrokeredMessage(new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data[i]))))
                {
                    SessionId = session,
                    ContentType = "application/json",
                    Label = "RecipeStep",
                    MessageId = i.ToString(),
                    TimeToLive = TimeSpan.FromMinutes(2)
                };
                await sender.SendAsync(message);
                lock (Console.Out)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Message sent: Session {0}, MessageId = {1}", message.SessionId, message.MessageId);
                    Console.ResetColor();
                }
            }

        }

        async Task ReceiveMessagesAsync(string namespaceAddress, string queueName, string receiveToken, CancellationToken ct)
        {
            var receiverFactory = MessagingFactory.Create(
                namespaceAddress,
                new MessagingFactorySettings
                {
                    TransportType = TransportType.NetMessaging, // deferral not yet supported on AMQP 
                    TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(receiveToken)
                });
            receiverFactory.RetryPolicy = new RetryExponential(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), 10);

            ct.Register(() => receiverFactory.Close());

            var client = receiverFactory.CreateQueueClient(queueName, ReceiveMode.PeekLock);
            while (!ct.IsCancellationRequested)
            {
                var session = await client.AcceptMessageSessionAsync();
                lock (Console.Out)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        "\t\t\t\tSession:\tSessionId = {0}",
                        session.SessionId);
                    Console.ResetColor();
                }
                while (true)
                {
                    try
                    {
                        //receive messages from Queue
                        var message = await session.ReceiveAsync(TimeSpan.FromSeconds(5));
                        if (message != null)
                        {
                            if (message.Label != null &&
                                message.ContentType != null &&
                                message.Label.Equals("RecipeStep", StringComparison.InvariantCultureIgnoreCase) &&
                                message.ContentType.Equals("application/json", StringComparison.InvariantCultureIgnoreCase))
                            {
                                var body = message.GetBody<Stream>();

                                dynamic recipeStep = JsonConvert.DeserializeObject(new StreamReader(body, true).ReadToEnd());
                                lock (Console.Out)
                                {
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    Console.WriteLine(
                                        "\t\t\t\tMessage received:  \n\t\t\t\t\t\tSessionId = {0}, \n\t\t\t\t\t\tMessageId = {1}, \n\t\t\t\t\t\tSequenceNumber = {2}," +
                                        "\n\t\t\t\t\t\tContent: [ step = {3}, title = {4} ]",
                                        message.SessionId,
                                        message.MessageId,
                                        message.SequenceNumber,
                                        recipeStep.step,
                                        recipeStep.title);
                                    Console.ResetColor();
                                }
                                await message.CompleteAsync();
                            }
                            else
                            {
                                await message.DeadLetterAsync("ProcessingError", "Don't know what to do with this message");
                            }
                        }
                        else
                        {
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
            }
            await receiverFactory.CloseAsync();
        }
    }
}