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

namespace Microsoft.Samples.SessionMessages
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Text;
    using System.Threading;

    public class Client
    {
        #region Fields
        static string senderId;
        static int numberOfMessages;
        static Random random = new Random();
        #endregion

        static void Main(string[] args)
        {
            try
            {
                ParseArgs(args);

                // Send messages to queue which does not require session
                Console.Title = "Ping Client";

                // Create sender to Order Service
                ChannelFactory<IPingServiceContract> factory = new ChannelFactory<IPingServiceContract>(SampleManager.PingClientConfigName);
                IPingServiceContract clientChannel = factory.CreateChannel();
                ((IChannel)clientChannel).Open();

                // Send messages
                numberOfMessages = random.Next(10, 30);
                Console.WriteLine("[Client{0}] Sending {1} messages to {2}...", senderId, numberOfMessages, SampleManager.PingQueueName);
                SendMessages(clientChannel);

                // Close sender
                ((IChannel)clientChannel).Close();
                factory.Close();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Exception occurred: {0}", exception);
                SampleManager.ExceptionOccurred = true;
            }

            Console.WriteLine("\nSender complete.");
            Console.WriteLine("\nPress [Enter] to exit.");
            Console.ReadLine();
        }

        static void SendMessages(IPingServiceContract clientChannel)
        {
            // Send messages to queue which requires session:
            for (int i = 0; i < numberOfMessages; i++)
            {
                // Send message 
                PingData message = CreatePingData();
                clientChannel.Ping(message);
                SampleManager.OutputMessageInfo("Send", message);
                Thread.Sleep(200);
            }
        }

        private static PingData CreatePingData()
        {
            // Generating a random message
            return new PingData(RandomString(), senderId);
        }

        /// <summary>
        /// Creates a random string
        /// </summary>
        /// <returns></returns>
        public static string RandomString()
        {
            StringBuilder builder = new StringBuilder();
            int size = random.Next(5, 15);
            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                builder.Append(ch);
            }

            return builder.ToString();
        }

        static void ParseArgs(string[] args)
        {
            if (args.Length != 1)
            {
                // SenderId is required for identifying sender
                senderId = new Random().Next(1, 7).ToString();
            }
            else
            {
                senderId = args[0];
            }
        }
    }
}
