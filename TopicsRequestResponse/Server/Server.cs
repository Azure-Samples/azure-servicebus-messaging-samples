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

        const double ReceiveMessageTimeout = 20.0;
        #endregion

        static void Main(string[] args)
        {
            ParseArgs(args);

            // Receive request messages from request queue
            Console.Title = "Server";
            TopicClient topicClient = CreateTopicClient(SampleManager.TopicPath);
            SubscriptionClient requestClient = CreateSubscriptionClient(
                SampleManager.TopicPath, SampleManager.RequestSubName);

            Console.WriteLine("Ready to receive messages from {0}/{1}...", requestClient.TopicPath, requestClient.Name);
            ReceiveMessages(topicClient, requestClient);

            Console.WriteLine("\nServer complete.");
            Console.ReadLine();
        }

        static void ReceiveMessages(TopicClient topicClient, SubscriptionClient requestClient)
        {
            // Read all the messages from subscription:
            BrokeredMessage request;
            while ((request = requestClient.Receive(TimeSpan.FromSeconds(ReceiveMessageTimeout))) != null)
            {
                SampleManager.OutputMessageInfo("REQUEST: ", request);

                BrokeredMessage response = new BrokeredMessage
                    {
                        SessionId = request.ReplyToSessionId,
                        MessageId = request.MessageId,
                        CorrelationId = SampleManager.ResponseSubName
                    };

                topicClient.Send(response);
                SampleManager.OutputMessageInfo("RESPONSE: ", response);
            }
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
            if (args.Length != 3)
            {
                throw new ArgumentException("Incorrect number of arguments. args = {0}", args.ToString());
            }

            serviceBusNamespace = args[0];
            serviceBusKeyName = args[1];
            serviceBusKey = args[2];
        }
    }
}
