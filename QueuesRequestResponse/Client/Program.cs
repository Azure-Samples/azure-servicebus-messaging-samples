//---------------------------------------------------------------------------------
// Copyright (c) 2011, Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//---------------------------------------------------------------------------------

namespace MessagingSamples
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    class Program : IDualBasicQueueSampleWithKeys
    {

        Dictionary<string, TaskCompletionSource<BrokeredMessage>> pendingRequests = new Dictionary<string, TaskCompletionSource<BrokeredMessage>>();

        public async Task Run(
           string namespaceAddress,
           string basicQueueName,
           string basicQueue2Name,
           string sendKeyName,
           string sendKey,
           string receiveKeyName,
           string receiveKey)
        {
            var senderFactory = MessagingFactory.Create(
               namespaceAddress,
               new MessagingFactorySettings
               {
                   TransportType = TransportType.Amqp,
                   TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(sendKeyName, sendKey)
               });
            senderFactory.RetryPolicy = new RetryExponential(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), 10);

            var receiverFactory = MessagingFactory.Create(
               namespaceAddress,
               new MessagingFactorySettings
               {
                   TransportType = TransportType.Amqp,
                   TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(receiveKeyName, receiveKey)
               });
            receiverFactory.RetryPolicy = new RetryExponential(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), 10);


            Console.Title = "Client";


            var sender = senderFactory.CreateQueueClient(basicQueueName);
            var receiver = receiverFactory.CreateQueueClient(basicQueue2Name);
            receiver.OnMessageAsync(
                async m =>
                {
                    TaskCompletionSource<BrokeredMessage> tc;
                    if (pendingRequests.TryGetValue(m.CorrelationId, out tc))
                    {
                        tc.SetResult(m);
                    }
                    else
                    {
                        // can't correlate, toss out
                        await m.DeadLetterAsync();
                    }
                });


            var replyTokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(sendKeyName, sendKey);
            var replyTo = new Uri(new Uri(namespaceAddress), basicQueue2Name);

            for (int i = 0; i < 10; i++)
            {
                var requestMessage = new BrokeredMessage()
                {
                    TimeToLive = TimeSpan.FromMinutes(5),
                    MessageId = Guid.NewGuid().ToString(),
                    ReplyTo = new UriBuilder(replyTo)
                    {
                      Query = string.Format("tk={0}", 
                         Uri.EscapeDataString(await replyTokenProvider.GetWebTokenAsync(replyTo.AbsoluteUri, string.Empty, false, TimeSpan.FromMinutes(1))))   
                    }.ToString()
                };

                var tcs = new TaskCompletionSource<BrokeredMessage>();
                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                cts.Token.Register(tcs.SetCanceled);

                pendingRequests.Add(requestMessage.MessageId, tcs);
                await sender.SendAsync(requestMessage);

                await tcs.Task;

            }
            
            // All messages sent
            Console.WriteLine("\nClient complete.");
            Console.ReadLine();
        }


    }
}
