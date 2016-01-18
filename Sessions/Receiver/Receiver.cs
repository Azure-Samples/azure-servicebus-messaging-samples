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
    using System.IO;
    using System.Threading;
    using Microsoft.ServiceBus.Messaging;

    class Program
    {
        #region Fields
        static string ServiceBusConnectionString;       

        // Delay to simulate processing time
        static int receiveMessageTimeout = 10;
        static int receiveSessionMessageTimeout = 10;
        static int acceptSessionReceiverTimeout = 10;

        static int receiverDelay = 200;
        static DateTime lastReceive;
        #endregion

        static void Main(string[] args)
        {
            ParseArgs(args);
            
            // Create MessageReceiver for queue which does not require session
            Console.Title = "MessageReceiver";
            QueueClient sessionlessQueueClient = CreateQueueClient(SampleManager.SessionlessQueueName);
            Console.WriteLine("Ready to receive messages from {0}...", sessionlessQueueClient.Path);

            lastReceive = DateTime.Now;
            ReceiveMessages(sessionlessQueueClient);
                        
            // Create SessionReceiver for queue requiring session
            Console.Title = "SessionReceiver";
            QueueClient sessionQueueClient = CreateQueueClient(SampleManager.SessionQueueName);
            Console.Clear();
            Console.WriteLine("Ready to receive messages from {0}...", sessionQueueClient.Path);

            bool allSessionsAccepted = false;
            while (!allSessionsAccepted)
            {
                try
                {   
                    // Please note that AcceptMessageSession(sessionId) can be used if sessionId is already known. This sample
                    // demonstrates where it is unknown.
                    Console.WriteLine("Checking for session...");                    
                    MessageSession sessionReceiver = sessionQueueClient.AcceptMessageSession(TimeSpan.FromSeconds(acceptSessionReceiverTimeout));

                    ReceiveSessionMessages(sessionReceiver);
                    Console.WriteLine("All received on this session");
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Got TimeoutException, no more sessions available");
                    allSessionsAccepted = true;
                }
            }

            Console.WriteLine("\nReceiver complete.");
            Console.ReadLine();
        }

        static void ReceiveMessages(QueueClient queueClient)
        {
            // Read messages from queue until queue is empty:
            Console.WriteLine("Reading messages from queue {0}", queueClient.Path);
            // Console.WriteLine("Receiver Type: " + receiver.GetType().Name);
            
            BrokeredMessage receivedMessage;
            while ((receivedMessage = queueClient.Receive(TimeSpan.FromSeconds(receiveMessageTimeout))) != null)
            {
                ProcessMessage(receivedMessage);
            }
        }

        static void ReceiveSessionMessages(MessageSession receiver)
        {
            // Read messages from queue until queue is empty:
            Console.WriteLine("Reading messages from queue {0}", receiver.Path);
            Console.WriteLine("Receiver Type:" + receiver.GetType().Name);
            Console.WriteLine("Receiver.SessionId = " + receiver.SessionId);

            BrokeredMessage receivedMessage;
            while ((receivedMessage = receiver.Receive(TimeSpan.FromSeconds(receiveSessionMessageTimeout))) != null)
            {
                string sessionId = receiver.SessionId;
                ProcessMessage(receivedMessage, receiver);
            }

            receiver.Close();
        }      

        static void ProcessMessage(BrokeredMessage message, MessageSession session = null)
        {
            DateTime startProcessingNewMessage = DateTime.Now;
            TimeSpan elapsed = startProcessingNewMessage - lastReceive;
            lastReceive = startProcessingNewMessage;

            // Using the Session State to track how much processing time was spent on a group.
            // This value will persist even if a receiver process is killed and the remaining 
            // messages are picked up by another receiver.
            string readState = null;
            if (session != null)
            {
                TimeSpan totalElapsed = elapsed;

                readState = GetState(session);
                if (readState != null)
                {
                    TimeSpan prevElapsed = TimeSpan.FromSeconds(Double.Parse(readState));
                    totalElapsed = elapsed + prevElapsed;
                }

                SetState(session, totalElapsed.TotalSeconds.ToString());                
            }

            SampleManager.OutputMessageInfo("RECV: ", message, "State: " + readState);

            Thread.Sleep(receiverDelay);
        }

        static string GetState(MessageSession session)
        {
            string state = null;
            Stream stream = session.GetState();

            if (stream != null)
            {
                using (stream)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        state = reader.ReadToEnd();
                    }
                }
            }

            return state;
        }

        static void SetState(MessageSession session, string state)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(state);
                    writer.Flush();

                    stream.Position = 0;
                    session.SetState(stream);
                }
            }
        }

        static QueueClient CreateQueueClient(string queueName)
        {
            return MessagingFactory.CreateFromConnectionString(ServiceBusConnectionString).CreateQueueClient(queueName, ReceiveMode.ReceiveAndDelete);
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
