//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples.RequestResponse
{
    using System;

    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    class Program
    {
        #region Fields
        static string serviceBusNamespace;
        static string serviceBusKeyName;
        static string serviceBusKey;
        static string ClientId;

        const double ResponseMessageTimeout = 20.0;
        #endregion

        static void Main(string[] args)
        {
            ParseArgs(args);
            Console.Title = "Client";

            // Send request messages to request queue
            TopicClient topicClient = CreateTopicClient(SampleManager.TopicPath);
            SubscriptionClient responseClient = CreateSubscriptionClient(
                SampleManager.TopicPath, SampleManager.ResponseSubName);

            Console.WriteLine("Preparing to send request messages to {0}...", topicClient.Path);
            SendMessages(topicClient, responseClient);

            // All messages sent
            Console.WriteLine("\nClient finished sending requests.");
            Console.ReadLine();
        }

        static void SendMessages(TopicClient topicClient, SubscriptionClient responseClient)
        {
            // Send request messages to queue:
            Console.WriteLine("Sending request messages to topic {0}", topicClient.Path);
            Console.WriteLine("Receiving response messages on subscription {0}", responseClient.Name);

            MessageSession session = responseClient.AcceptMessageSession(ClientId);

            for (int i = 0; i < SampleManager.NumMessages; ++i)
            {
                // send request message
                BrokeredMessage message = new BrokeredMessage
                    {
                        ReplyToSessionId = ClientId,
                        MessageId = i.ToString(),
                        CorrelationId = SampleManager.RequestSubName
                    };
                
                topicClient.Send(message);
                SampleManager.OutputMessageInfo("REQUEST: ", message);

                // start asynchronous receive operation
                session.BeginReceive(TimeSpan.FromSeconds(ResponseMessageTimeout), ProcessResponse, session);
            }


            Console.WriteLine();
        }

        public static TopicClient CreateTopicClient(string topicPath)
        {
            TokenProvider tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(serviceBusKeyName, serviceBusKey);

            Uri uri = ServiceBusEnvironment.CreateServiceUri("sb", serviceBusNamespace, string.Empty);
            MessagingFactory messagingFactory = MessagingFactory.Create(uri, tokenProvider);

            return messagingFactory.CreateTopicClient(topicPath);
        }

        public static SubscriptionClient CreateSubscriptionClient(string topicPath, string subName)
        {
            TokenProvider tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(serviceBusKeyName, serviceBusKey);

            Uri uri = ServiceBusEnvironment.CreateServiceUri("sb", serviceBusNamespace, string.Empty);
            MessagingFactory messagingFactory = MessagingFactory.Create(uri, tokenProvider);

            return messagingFactory.CreateSubscriptionClient(topicPath, subName, ReceiveMode.ReceiveAndDelete);
        }

        static void ParseArgs(string[] args)
        {
            if (args.Length != 4)
            {
                throw new ArgumentException("Incorrect number of arguments. args = {0}", args.ToString());
            }

            serviceBusNamespace = args[0];
            serviceBusKeyName = args[1];
            serviceBusKey = args[2];
            ClientId = args[3];
        }

        static void ProcessResponse(IAsyncResult result)
        {
            MessageSession session = result.AsyncState as MessageSession;
            BrokeredMessage message = session.EndReceive(result);

            if (message == null)
            {
                Console.WriteLine("ERROR: Message Receive Timeout.");
            }
            else
            {
                SampleManager.OutputMessageInfo("RESPONSE: ", message);
            }
        }
    }
}
