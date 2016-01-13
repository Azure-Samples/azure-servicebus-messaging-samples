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


namespace Microsoft.Samples.MessagingWithQueues
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
    using System.Configuration;
    using System.Threading;

    public class program
    {
        static string QueueName = "OnMessageSampleQueue";
        static QueueClient Client;

        static void Main(string[] args)
        {
            // Please see http://go.microsoft.com/fwlink/?LinkID=249089 for getting Service Bus connection string and adding to app.config

            Console.WriteLine("Creating a Queue");
            CreateQueue();
            Console.WriteLine("Sending messages ...");
            SendMessages();

            // Initialize message pump options
            OnMessageOptions options = new OnMessageOptions();
            options.AutoComplete = true; // Indicates if the message-pump should call complete on messages after the callback has completed processing.
            options.MaxConcurrentCalls = 1; // Indicates the maximum number of concurrent calls to the callback the pump should initiate 
            options.ExceptionReceived += LogErrors; // Allows users to get notified of any errors encountered by the message pump

            Console.WriteLine("Starting message processing ...");
            // Start receiveing messages
            Client.OnMessage((receivedMessage) => // Initiates the message pump and callback is invoked for each message that is recieved, calling close on the client will stop the pump.
            {
                // Process the message
                Console.WriteLine(string.Format("Processing recived Message: Id = {0}, Body = {1}", receivedMessage.MessageId, receivedMessage.GetBody<string>()));
            }, options);

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        private static void CreateQueue()
        {
            NamespaceManager namespaceManager = NamespaceManager.Create();

            Console.WriteLine("\nCreating Queue '{0}'...", QueueName);

            // Delete if exists
            if (namespaceManager.QueueExists(QueueName))
            {
                namespaceManager.DeleteQueue(QueueName);
            }

            namespaceManager.CreateQueue(QueueName);
        }

        private static void SendMessages()
        {
            Client = QueueClient.Create(QueueName);

            List<BrokeredMessage> messageList = new List<BrokeredMessage>();

            messageList.Add(CreateSampleMessage("1", "First message information"));
            messageList.Add(CreateSampleMessage("2", "Second message information"));
            messageList.Add(CreateSampleMessage("3", "Third message information"));
            messageList.Add(CreateSampleMessage("4", "Fourth message information"));
            messageList.Add(CreateSampleMessage("5", "Fifth message information"));
            messageList.Add(CreateSampleMessage("6", "Sixth message information"));
            messageList.Add(CreateSampleMessage("7", "Seventh message information"));
            messageList.Add(CreateSampleMessage("8", "Eighth message information"));
            messageList.Add(CreateSampleMessage("9", "Ninth message information"));
            messageList.Add(CreateSampleMessage("10", "Tenth message information"));

            Console.WriteLine("\nSending messages to Queue...");

            foreach (BrokeredMessage message in messageList)
            {
                while (true)
                {
                    try
                    {
                        Client.Send(message);
                    }
                    catch (MessagingException e)
                    {
                        if (!e.IsTransient)
                        {
                            Console.WriteLine(e.Message);
                            throw;
                        }
                        else
                        {
                            Thread.Sleep(2000);
                        }
                    }
                    Console.WriteLine(string.Format("Message sent: Id = {0}, Body = {1}", message.MessageId, message.GetBody<string>()));
                    break;
                }
            }

        }

        private static BrokeredMessage CreateSampleMessage(string messageId, string messageBody)
        {
            BrokeredMessage message = new BrokeredMessage(messageBody);
            message.MessageId = messageId;
            return message;
        }

        private static void LogErrors(object sender, ExceptionReceivedEventArgs e)
        {
            if (e.Exception != null)
            {
                Console.WriteLine("Error: " + e.Exception.Message);
                Client.Close();
            }
        }
    }
}
