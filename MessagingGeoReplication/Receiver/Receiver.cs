//---------------------------------------------------------------------------------
// Copyright (c) 2012, Microsoft Corporation
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

namespace Microsoft.Samples.BrokeredMessagingGeoReplication
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.ServiceBus.Messaging;

    public class ReceiverThreadParameters
    {
        public QueueClient queueClient;
        public string replica;
    }

    public class Receiver
    {
        const string QueueName = "ReplicatedQueue";

        static volatile List<string> receivedMessageList = new List<string>();
        static readonly object receivedMessageListLock = new object();

        static void Main(string[] args)
        {
            ManagementObjects primaryManagementObjects = new ManagementObjects("primary");
            ManagementObjects secondaryManagementObjects = new ManagementObjects("secondary");

            MessagingFactory primaryFactory = null;
            MessagingFactory secondaryFactory = null;

            Thread primaryReceiverThread = new Thread(Receive);
            Thread secondaryReceiverThread = new Thread(Receive);

            try
            {
                // Create a primary and secondary messaging factory.
                primaryFactory = primaryManagementObjects.GetMessagingFactory();
                secondaryFactory = secondaryManagementObjects.GetMessagingFactory();

                // Create a primary and secondary queue client.
                QueueClient primaryQueueClient = primaryFactory.CreateQueueClient(Receiver.QueueName);
                QueueClient secondaryQueueClient = secondaryFactory.CreateQueueClient(Receiver.QueueName);

                // Start thread that receives messages from the primary queue.
                ReceiverThreadParameters primaryReceiverThreadParameters = new ReceiverThreadParameters();
                primaryReceiverThreadParameters.queueClient = primaryQueueClient;
                primaryReceiverThreadParameters.replica = "primary";
                primaryReceiverThread.Start(primaryReceiverThreadParameters);

                // Start thread that receives messages from the secondary queue.
                ReceiverThreadParameters secondaryReceiverThreadParameters = new ReceiverThreadParameters();
                secondaryReceiverThreadParameters.queueClient = secondaryQueueClient;
                secondaryReceiverThreadParameters.replica = "secondary";
                secondaryReceiverThread.Start(secondaryReceiverThreadParameters);

                Console.WriteLine("End of scenario, press ENTER to exit.\n");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception {0}", e.ToString());
                throw;
            }
            finally
            {
                // Kill primary and secondary receiver thread.
                primaryReceiverThread.Abort();
                secondaryReceiverThread.Abort();

                // Closing factories closes all entities created from these factories.
                if (primaryFactory != null)
                {
                    primaryFactory.Close();
                }
                if (secondaryFactory != null)
                {
                    secondaryFactory.Close();
                }
            }
        }

        static void Receive(object args)
        {
            ReceiverThreadParameters arguments = (ReceiverThreadParameters)args;
            QueueClient queueClient = arguments.queueClient;
            string replica = arguments.replica;

            Console.WriteLine("Receiving messages from {0} queue '{1}'...\n", replica, Receiver.QueueName);

            for (; ; )
            {
                try
                {
                    BrokeredMessage message = queueClient.Receive(TimeSpan.FromSeconds(50));

                    if (message != null)
                    {
                        // Detect if a message with an identical ID has been received through the other queue.
                        bool duplicate;
                        lock (receivedMessageListLock)
                        {
                            duplicate = receivedMessageList.Remove(message.MessageId);
                            if (duplicate == false)
                            {
                                receivedMessageList.Add(message.MessageId);
                            }
                        }
                        if (duplicate == false)
                        {
                            // Message has not been received yet through the other queue. Process message.
                            Console.WriteLine(string.Format("Message received from {0} queue: Id = {1}, Body = {2}", replica, message.MessageId, message.GetBody<string>()));
                            // Further custom message processing could go here...
                        }
                        else
                        {
                            // Message has already been received through the other queue. Ignore message.
                            Console.WriteLine(string.Format("Duplicate message received from {0} queue: Id = {1}, Body = {2}", replica, message.MessageId, message.GetBody<string>()));
                        }
                        message.Complete();
                    }

                }
                catch (MessagingEntityNotFoundException e)
                {
                    Console.WriteLine("MessagingEntityNotFoundException when receiving from {0} queue. Datacenter might be down. Exception: {1}", replica, e.ToString());
                    Thread.Sleep(60 * 1000);
                }
                catch (TimeoutException e)
                {
                    Console.WriteLine("TimeoutException when receiving from {0} queue. Datacenter might be down. Exception: {1}", replica, e.ToString());
                    Thread.Sleep(60 * 1000);
                }
                catch (ThreadAbortException)
                {
                    Console.WriteLine("\nExiting {0} receive thread.", replica);
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unexpected exception when receiving from {0} queue {1}", replica, e.ToString());
                    throw;
                }
            }
        }

    }
}
