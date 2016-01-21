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


namespace MessagingSamples
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    public class Program : IBasicQueueSendReceiveSample
    {

        public async Task Run(string namespaceAddress, string queueName, string sendToken, string receiveToken)
        {

            // Create communication objects to send and receive on the queue
            var senderMessagingFactory = await MessagingFactory.CreateAsync(namespaceAddress, TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken));
            var sender = await senderMessagingFactory.CreateMessageSenderAsync(queueName);

            var receiverMessagingFactory = await MessagingFactory.CreateAsync(namespaceAddress, TokenProvider.CreateSharedAccessSignatureTokenProvider(receiveToken));
            var receiver = await receiverMessagingFactory.CreateMessageReceiverAsync(queueName, ReceiveMode.PeekLock);

            // Initialize message pump options
            var options = new OnMessageOptions
            {
                AutoComplete = true, // Indicates if the message-pump should call complete on messages after the callback has completed processing.
                MaxConcurrentCalls = 2 // Indicates the maximum number of concurrent calls to the callback the pump should initiate 
            };
            
            
            options.ExceptionReceived += LogErrors; // Allows users to get notified of any errors encountered by the message pump

            Console.WriteLine("Starting message processing ...");
            // Start receiveing messages
            receiver.OnMessageAsync(async (receivedMessage) =>
            // Initiates the message pump and callback is invoked for each message that is received, calling close on the client will stop the pump.
            {
                // Process the message
                await Console.Out.WriteLineAsync(string.Format("Processing received Message: Id = {0}, Body = {1}", receivedMessage.MessageId, receivedMessage.GetBody<string>()));
            }, options);

            Console.WriteLine("Press any key to exit.");

            await this.SendMessages(sender);

            Console.ReadKey();
        }

        Task SendMessages(MessageSender sender)
        {
            return sender.SendBatchAsync(new List<BrokeredMessage>
            {
                new BrokeredMessage("First message information") {MessageId = "1"},
                new BrokeredMessage("Second message information") {MessageId = "2"},
                new BrokeredMessage("Third message information") {MessageId = "3"},
                new BrokeredMessage("Fourth message information") {MessageId = "4"},
                new BrokeredMessage("Fifth message information") {MessageId = "5"},
                new BrokeredMessage("Sixth message information") {MessageId = "6"},
                new BrokeredMessage("Seventh message information") {MessageId = "7"},
                new BrokeredMessage("Eighth message information") {MessageId = "8"},
                new BrokeredMessage("Ninth message information") {MessageId = "9"},
                new BrokeredMessage("Tenth message information") {MessageId = "10"}
            });
        }

        private static void LogErrors(object sender, ExceptionReceivedEventArgs e)
        {
            if (e.Exception != null)
            {
                Console.WriteLine("Error: " + e.Exception.Message);
            }
        }

    }
}
