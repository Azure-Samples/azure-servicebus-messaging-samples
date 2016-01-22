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
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    class Program : IBasicQueueReceiveSample
    {
        class FactoryTokenProviderTuple
        {
            public MessagingFactory Factory { get; set; }
            public DelegatingTokenProvider TokenProvider { get; set; }
        }

        const double ReceiveMessageTimeout = 20.0;

        readonly Dictionary<Uri, FactoryTokenProviderTuple> responderFactories = new Dictionary<Uri, FactoryTokenProviderTuple>();
        readonly object responderFactoriesMutex = new object();
        Uri namespaceUri;

        public async Task Run(string namespaceAddress, string queueName, string receiveToken)
        {
            this.namespaceUri = new Uri(namespaceAddress);
            var receiverFactory = MessagingFactory.Create(
                namespaceAddress,
                new MessagingFactorySettings
                {
                    TransportType = TransportType.Amqp,
                    TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(receiveToken)
                });
            try
            {
                receiverFactory.RetryPolicy = new RetryExponential(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), 10);
                var receiver = receiverFactory.CreateQueueClient(queueName, ReceiveMode.PeekLock);
                try
                {
                    receiver.OnMessageAsync(rq =>
                    {
                        return this.Respond(rq,
                            async m => new BrokeredMessage("Reply"));
                    });

                    Console.WriteLine("Pres ENTER to stop procssing requests.");
                    Console.ReadLine();
                }
                finally
                {
                    receiver.Close();
                }
            }
            finally
            {
                receiverFactory.Close();
            }
            foreach (var rpf in this.responderFactories.Values)
            {
                rpf.Factory.Close();
            }
        }

        async Task Respond(BrokeredMessage request, Func<BrokeredMessage, Task<BrokeredMessage>> handleRequest)
        {
            // evaluate ReplyTo
            if (!string.IsNullOrEmpty(request.ReplyTo))
            {
                Uri targetUri;

                if (Uri.TryCreate(request.ReplyTo, UriKind.RelativeOrAbsolute, out targetUri))
                {
                    string replyToken = null;
                    if (!targetUri.IsAbsoluteUri)
                    {
                        targetUri = new Uri(this.namespaceUri, targetUri);
                    }
                    var queryPortion = targetUri.Query;
                    if (!string.IsNullOrEmpty(queryPortion) && queryPortion.Length > 1)
                    {
                        var nvm = HttpUtility.ParseQueryString(queryPortion.Substring(1));
                        var tokenString = nvm["tk"];
                        if (tokenString != null)
                        {
                            replyToken = tokenString;
                        }
                    }
                    if (replyToken == null)
                    {
                        await request.DeadLetterAsync("NoReplyToToken", "No 'tk' query parameter in ReplyTo field found");
                        return;
                    }
                    targetUri = new Uri(targetUri.GetLeftPart(UriPartial.Path));


                    // now we're reasonably confident that the input message can be
                    // replied to, so let's execute the message processing

                    try
                    {
                        var reply = await handleRequest(request);
                        reply.CorrelationId = request.MessageId;

                        FactoryTokenProviderTuple factory;

                        lock (this.responderFactoriesMutex)
                        {
                            var signatureTokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(replyToken);
                            if (this.responderFactories.TryGetValue(targetUri, out factory))
                            {
                                factory.TokenProvider.TokenProvider =
                                    signatureTokenProvider;
                            }
                            else
                            {
                                var tokenProvider = new DelegatingTokenProvider(
                                    signatureTokenProvider
                                    );
                                var receiverFactory = MessagingFactory.Create(
                                    targetUri.GetLeftPart(UriPartial.Authority),
                                    new MessagingFactorySettings
                                    {
                                        TransportType = TransportType.Amqp,
                                        TokenProvider = tokenProvider
                                    });
                                factory = new FactoryTokenProviderTuple { Factory = receiverFactory, TokenProvider = tokenProvider };
                                this.responderFactories.Add(targetUri, factory);
                            }
                        }

                        var sender = await factory.Factory.CreateMessageSenderAsync(targetUri.AbsolutePath.Substring(1));
                        await sender.SendAsync(reply);
                        await request.CompleteAsync();
                    }
                    catch
                    {
                        await request.DeadLetterAsync();
                    }
                }
                else
                {
                    await request.DeadLetterAsync("NoReplyTo", "No ReplyTo field found");
                }
            }
        }
    }
}
