//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure Platform SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples.MsmqServiceBusBridge
{
    using System;
    using System.Diagnostics;
    using System.Messaging;
    using System.Threading;
    using System.Transactions;

    class MsmqTestClient
    {
        static void Main()
        {
            Console.WriteLine("Process name: " + Process.GetCurrentProcess().ProcessName);

            Helper.CreateMsmqQueue(Constants.MsmqSendQueue);
            Helper.CreateMsmqQueue(Constants.MsmqReceiveQueue);
            Helper.CreateServiceBusQueue(Constants.ServiceBusSendQueue);
            Helper.CreateServiceBusQueue(Constants.ServiceBusReceiveQueue);

            // Start subthread that monitors admin queue.
            var sendQueueThreadStart = new ThreadStart(SendToMsmq);
            var sendQueueThread = new Thread(sendQueueThreadStart);
            sendQueueThread.Start();


            var receiveQueueThreadStart = new ThreadStart(ReceiveFromMsmq);
            var receiveQueueThread = new Thread(receiveQueueThreadStart);
            receiveQueueThread.Start();

            sendQueueThread.Join();
            receiveQueueThread.Join();

            Console.WriteLine();
            Console.WriteLine("Please press [Enter] to clean up the queues and exit");
            Console.ReadLine();
            Exit();
        }

        private static void SendToMsmq()
        {
            // Open transactional MSMQ queue. Define filter to retrieve all message properties.
            MessageQueue msmqQueue = Helper.OpenMsmqQueue(Constants.MsmqSendQueue, true);

            Console.WriteLine("We will wait for 10 seconds between each 'Send' to clearly show the bridge functionality...");
            Console.WriteLine();

            for (int i = 1; i <= 3; i++)
            {
                var message = new System.Messaging.Message();
                message.Body = "Message body " + i + ".";
                message.Formatter = new BinaryMessageFormatter();
                message.Label = "Message" + i;

                // Send a message.
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    string.Format("Sending Message to {0}: {1}", Constants.MsmqSendQueue, message.Label));
                Console.ResetColor();

                using (var tx = new MessageQueueTransaction())
                {
                    tx.Begin();
                    try
                    {
                        msmqQueue.Send(message, tx);
                        tx.Commit();
                    }
                    catch (MessageQueueException exception)
                    {
                        Console.WriteLine("MessageQueueException received: " + exception.ToString());
                        throw;
                    }
                }

                // For the purpose of clearly showing the functionality of the bridge sample, sleep for 30seconds 
                // so that there is sufficient time between each message output 
                Thread.Sleep(10000);
            }
        }

        private static void ReceiveFromMsmq()
        {
            // Open transactional MSMQ queue. Define filter to retrieve all message properties.
            MessageQueue msmqQueue = Helper.OpenMsmqQueue(Constants.MsmqReceiveQueue, true);

            for (int i = 1; i <= 3; i++)
            {
                try
                {
                    using (MessageQueueTransaction msmqTransaction = new MessageQueueTransaction())
                    {
                        // Receive message from MSMQ.
                        msmqTransaction.Begin();
                        System.Messaging.Message msmqMessage = msmqQueue.Receive(msmqTransaction);
                        Console.WriteLine(
                            string.Format("Received message from {0}: {1}", Constants.MsmqReceiveQueue, msmqMessage.Label));
                        msmqTransaction.Commit();
                    }
                }
                catch (MessageQueueException)
                {
                    // In this scenario (infinite receive), we can receive a MessageQueueException when
                    // the MSMQ queues are deleted during cleanup
                    return;
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Exception received: " + exception.ToString());
                    throw;
                }
            }
        }

        private static void Exit()
        {
            Helper.DeleteMsmqQueue(Constants.MsmqSendQueue);
            Helper.DeleteMsmqQueue(Constants.MsmqReceiveQueue);
            Helper.DeleteQueue(Constants.ServiceBusSendQueue);
            Helper.DeleteQueue(Constants.ServiceBusReceiveQueue);
        }
    }
}
