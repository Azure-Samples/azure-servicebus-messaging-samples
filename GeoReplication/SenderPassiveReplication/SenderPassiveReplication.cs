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
    using Microsoft.ServiceBus.Messaging;

    public class SenderPassiveReplication
    {
        const string QueueName = "ReplicatedQueue";
        static bool primaryIsActive = true;

        static void Main(string[] args)
        {
            ManagementObjects primaryManagementObjects = new ManagementObjects("primary");
            ManagementObjects secondaryManagementObjects = new ManagementObjects("secondary");

            MessagingFactory primaryFactory = null;
            MessagingFactory secondaryFactory = null;

            try
            {
                // Create a primary and secondary messaging factory.
                primaryFactory = primaryManagementObjects.GetMessagingFactory();
                secondaryFactory = secondaryManagementObjects.GetMessagingFactory();

                // Create the primary and secondary queue.
                Console.WriteLine("\nCreating primary Queue '{0}'...", QueueName);
                primaryManagementObjects.CreateQueue(QueueName);
                Console.WriteLine("\nCreating secondary Queue '{0}'...", QueueName);
                secondaryManagementObjects.CreateQueue(QueueName);

                // Create a primary and secondary queue client.
                QueueClient primaryQueueClient = primaryFactory.CreateQueueClient(QueueName);
                QueueClient secondaryQueueClient = secondaryFactory.CreateQueueClient(QueueName);

                //*****************************************************************************************************
                //                                   Sending messages
                //*****************************************************************************************************
                Console.WriteLine("\nSending messages to primary or secondary queue...\n");

                for (int i = 1; i <= 8; i++)
                {
                    // Create brokered message.
                    BrokeredMessage m1 = Helper.CreateSampleMessage(i);

                    // Send message to primary or secondary queue.
                    BrokeredMessage m2 = m1.Clone();
                    try
                    {
                        if (primaryIsActive == true)
                        {
                            SendMessage(primaryQueueClient, secondaryQueueClient, m1, m2, "primary", "secondary");
                        }
                        else
                        {
                            SendMessage(secondaryQueueClient, primaryQueueClient, m2, m1, "secondary", "primary");
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unable to send to primary or secondary queue: Exception {0}", e.ToString());
                    }
                }

                Console.WriteLine("\nAfter running the entire sample, press ENTER to clean up and exit.");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception {0}", e.ToString());
                throw;
            }
            finally
            {
                // Closing factories closes all entities created from these factories.
                if (primaryFactory != null)
                {
                    primaryFactory.Close();
                }
                if (secondaryFactory != null)
                {
                    secondaryFactory.Close();
                }

                // Delete primary and secondary queue.
                primaryManagementObjects.DeleteQueue(QueueName);
                primaryManagementObjects.DeleteQueue(QueueName);
            }
        }

        // Send message to active queue. If this fails, send message to backup queue.
        // If the send operation to the backup queue succeeds, make the backup queue the new active queue.
        static void SendMessage(QueueClient activeQueueClient, QueueClient backupQueueClient, BrokeredMessage m1, BrokeredMessage m2, string activeReplica, string backupReplica)
        {
            List<Type> serverFailureExceptions = new List<Type>();
            serverFailureExceptions.Add(typeof(MessagingEntityNotFoundException));
            serverFailureExceptions.Add(typeof(UnauthorizedAccessException));
            serverFailureExceptions.Add(typeof(ServerBusyException));
            serverFailureExceptions.Add(typeof(MessagingCommunicationException));
            serverFailureExceptions.Add(typeof(TimeoutException));

            // Send message to active queue. This block throws an exception if:
            //   - sending to the active queue returns an exception other than a serverFailureException.
            //   - sending to the backup queue returns any exception.
            try
            {
                Helper.SendMessage(activeQueueClient, m1, activeReplica);
                Console.WriteLine(string.Format("Message {0} sent to {1} queue: Body = {2}", m1.MessageId, activeReplica, m1.GetBody<string>()));
            }
            catch (Exception activeQueueException)
            {
                bool serverFailure = false;
                foreach (Type t in serverFailureExceptions)
                {
                    if (activeQueueException.GetType() == t)
                    {
                        serverFailure = true;
                        Console.WriteLine("{0} when sending message {1} to {2} queue. Attempting to send to {3} queue.", t.ToString(), m1.MessageId, activeReplica, backupReplica);
                        try
                        {
                            Helper.SendMessage(backupQueueClient, m2, backupReplica);
                            Console.WriteLine(string.Format("Message {0} sent to {1} queue: Body = {2}", m2.MessageId, backupReplica, m1.GetBody<string>()));

                            // Sending to backup queue succeeded. Toggle primaryIsActive flag.
                            primaryIsActive = !primaryIsActive;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Failure when sending message {0} to {1} queue. Giving up", m2.MessageId, backupReplica);
                            throw e;
                        }
                        break;
                    }
                }

                // If send operation to the active queue failed with a non-server failure, bubble up exception.
                if (serverFailure == false)
                {
                    throw activeQueueException;
                }
            }
        }

    }
}
