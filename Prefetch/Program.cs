//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
//----------------------------------------------------------------
using System;
using System.Diagnostics;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Samples.MessagesPrefetchSample
{
    class Program
    {
        #region Fields
        static string serviceBusConnectionString;

        const string QueueName = "MyQueue";
        #endregion

        static void Main(string[] args)
        {
            // ***************************************************************************************
            // This sample demonstrates how to use the messages prefetch feature upon receive
            // The sample creates a Queue, sends messages to it and receives all messages
            // using 2 receivers one with prefetchCount = 0 (disabled) and the other with 
            // prefetCount = 100. For each case, it calculates the time taken to receive and complete
            // all messages and at the end, it prints the difference between both times.
            // ***************************************************************************************

            // Get ServiceBus namespace and credentials from the user.
            Program.GetNamespaceAndCredentials();

            // Create mesasging factory and ServiceBus namespace manager.
            MessagingFactory messagingFactory = Program.CreateMessagingFactory();
            NamespaceManager namespaceManager = Program.CreateNamespaceManager();

            // Create queue that will be used through the sample.
            Program.CreateQueue(namespaceManager);

            // Send and Receive messages with prefetch OFF
            long timeTaken1 = Program.SendAndReceiveMessages(messagingFactory, 0);

            // Send and Receive messages with prefetch ON
            long timeTaken2 = Program.SendAndReceiveMessages(messagingFactory, 100);

            // Calculate the time difference
            long timeDifference = timeTaken1 - timeTaken2;

            Console.WriteLine("\nTime difference = {0} milliseconds", timeDifference);

            Console.WriteLine();
            Console.WriteLine("Press [Enter] to quit...");
            Console.ReadLine();

            // Cleanup:
            messagingFactory.Close();
            namespaceManager.DeleteQueue(Program.QueueName);
        }


        static long SendAndReceiveMessages(MessagingFactory messagingFactory, int prefetchCount)
        {
            // Create client for the queue.
            QueueClient queueClient = messagingFactory.CreateQueueClient(Program.QueueName, ReceiveMode.PeekLock);

            // Now we can start sending messages.
            int messageCount = 1000;

            Console.WriteLine("\nSending {0} messages to the queue", messageCount);

            for (int i = 0; i < messageCount; i++)
            {
                queueClient.Send(new BrokeredMessage());
            }

            Console.WriteLine("Send completed");

            // Set the prefetchCount on the queueClient
            queueClient.PrefetchCount = prefetchCount;

            // Start stopwatch
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            // Receive the messages
            Console.WriteLine("Receiving messages from queue using prefetchCount = {0}", prefetchCount);

            BrokeredMessage receivedMessage = queueClient.Receive(TimeSpan.FromSeconds(10));

            while (receivedMessage != null)
            {
                receivedMessage.Complete();
                receivedMessage = queueClient.Receive(TimeSpan.FromSeconds(10));
            }

            Console.WriteLine("Receive completed");

            // Stop the stopwatch
            stopWatch.Stop();

            long timeTaken = stopWatch.ElapsedMilliseconds;
            Console.WriteLine("Time to receive and complete all messages = {0} milliseconds", timeTaken);

            // Close the QueueClient
            queueClient.Close();

            return timeTaken;
        }

        static void CreateQueue(NamespaceManager namespaceManager)
        {
            Console.WriteLine("\nCreating a queue.");
            
            // Create a queue.
            if(namespaceManager.QueueExists(Program.QueueName))
            {
                namespaceManager.DeleteQueue(Program.QueueName);
            }

            QueueDescription description = new QueueDescription(Program.QueueName);
            description.LockDuration = TimeSpan.FromMinutes(3);

            QueueDescription queueDescription = namespaceManager.CreateQueue(description);

            Console.WriteLine("Queue created.");
        }

        static NamespaceManager CreateNamespaceManager()
        {
            return NamespaceManager.CreateFromConnectionString(serviceBusConnectionString);
        }

        static MessagingFactory CreateMessagingFactory()
        {
            return MessagingFactory.CreateFromConnectionString(serviceBusConnectionString);
        }


        static void GetNamespaceAndCredentials()
        {
            Console.Write("Please provide a connection string to Service Bus (/? for help):\n ");
            Program.serviceBusConnectionString = Console.ReadLine();

            if ((String.Compare(Program.serviceBusConnectionString, "/?") == 0) || (Program.serviceBusConnectionString.Length == 0))
            {
                Console.Write("To connect to the Service Bus cloud service, go to the Windows Azure portal and select 'View Connection String'.\n");
                Console.Write("To connect to the Service Bus for Windows Server, use the get-sbClientConfiguration PowerShell cmdlet.\n\n");
                Console.Write("A Service Bus connection string has the following format: \nEndpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<keyName>;SharedAccessKey=<key>");

                Program.serviceBusConnectionString = Console.ReadLine();
                Environment.Exit(0);
            }
        }
    }
}
