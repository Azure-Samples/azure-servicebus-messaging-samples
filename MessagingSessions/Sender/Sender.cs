//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure AppFabric SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples.SessionMessages
{
    using System;
    using System.Threading;
    using Microsoft.ServiceBus.Messaging;

    class Program
    {
        #region Fields
        static string ServiceBusConnectionString;

        // Delay to simulate processing time
        static int senderDelay = 100;
        #endregion

        static void Main(string[] args)
        {
            ParseArgs(args);
            Console.Title = "MessageSender";

            // Send messages to queue which does not require session
            QueueClient queueClient = CreateQueueClient(SampleManager.SessionlessQueueName);
            Console.WriteLine("Preparing to send messages to {0}...", queueClient.Path);
            Thread.Sleep(3000);

            SendMessages(queueClient);

            // Send messages to queue requiring session
            queueClient = CreateQueueClient(SampleManager.SessionQueueName);
            Console.WriteLine("Preparing to send messages to {0}...", queueClient.Path);
            SendMessages(queueClient);

            // All messages sent
            Console.WriteLine("\nSender complete.");
            Console.ReadLine();
        }

        static void SendMessages(QueueClient queueClient)
        {
            // Send messages to queue:
            Console.WriteLine("Sending messages to queue {0}", queueClient.Path);

            System.Random rand = new Random();
            for (int i = 0; i < SampleManager.NumMessages; ++i)
            {
                string sessionName = rand.Next(SampleManager.NumSessions).ToString();
                BrokeredMessage message = CreateSessionMessage(sessionName);
                queueClient.Send(message);
                SampleManager.OutputMessageInfo("SEND: ", message);
                Thread.Sleep(senderDelay);
            }

            Console.WriteLine();
        }

        // Create the runtime entities (queue client)
        static QueueClient CreateQueueClient(string queueName)
        {
            return MessagingFactory.CreateFromConnectionString(ServiceBusConnectionString).CreateQueueClient(queueName);
        }

        static BrokeredMessage CreateSessionMessage(string sessionId)
        {
            BrokeredMessage message = new BrokeredMessage();
            message.SessionId = sessionId;
            message.MessageId = "Order_" + Guid.NewGuid().ToString().Substring(0,5);
            return message;
        }

        static void ParseArgs(string[] args)
        {
            if (args.Length != 1)
            {
                throw new ArgumentException("Incorrect number of arguments. args = {0}", args.ToString());
            }

            ServiceBusConnectionString = args[0];            
        }
    }
}
