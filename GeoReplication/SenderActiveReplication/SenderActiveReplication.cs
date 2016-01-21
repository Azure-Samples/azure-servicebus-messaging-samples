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

namespace MessagingSamples
{
    using System;
    using Microsoft.ServiceBus.Messaging;

    public class SenderActiveReplication
    {
        const string QueueName = "ReplicatedQueue";

        static void Main(string[] args)
        {
          MessagingFactory primaryFactory = null;
            MessagingFactory secondaryFactory = null;

            try
            {
                // Create a primary and secondary messaging factory.
                primaryFactory = primaryManagementObjects.GetMessagingFactory();
                secondaryFactory = secondaryManagementObjects.GetMessagingFactory();

                // Create the primary and secondary queue.
                Console.WriteLine("\nCreating primary Queue '{0}'...", SenderActiveReplication.QueueName);
                primaryManagementObjects.CreateQueue(SenderActiveReplication.QueueName);
                Console.WriteLine("\nCreating secondary Queue '{0}'...", SenderActiveReplication.QueueName);
                secondaryManagementObjects.CreateQueue(SenderActiveReplication.QueueName);

                // Create a primary and secondary queue client.
                var primaryQueueClient = primaryFactory.CreateQueueClient(SenderActiveReplication.QueueName);
                var secondaryQueueClient = secondaryFactory.CreateQueueClient(SenderActiveReplication.QueueName);

                //*****************************************************************************************************
                //                                   Sending messages
                //*****************************************************************************************************
                Console.WriteLine("\nSending messages to primary and secondary queues...\n");

                for (var i=1; i<=5; i++)
                {
                    // Create brokered message.
                    var m1 = Helper.CreateSampleMessage(i);

                    // Clone message so we can send clone to secondary in case sending to the primary fails.
                    var m2 = m1.Clone();

                    Exception ex;
                    var exceptionCount = 0;

                    // Send message to primary queue.
                    try
                    {
                        // Emulating unavailabilty of the primary queue after fourth message has been sent.
                        if (i > 4)
                        {
                            throw new MessagingEntityNotFoundException(SenderActiveReplication.QueueName);
                        }

                        Helper.SendMessage(primaryQueueClient, m1, "primary");
                        Console.WriteLine(string.Format("Message {0} sent to primary queue: Body = {1}", m1.MessageId, m1.GetBody<string>()));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unable to send message {0} to primary queue: Exception {1}", m1.MessageId, e.ToString());
                        ex = e;
                        exceptionCount++;
                    }

                    // Send message to secondary queue.
                    try
                    {
                        Helper.SendMessage(secondaryQueueClient, m2, "secondary");
                        Console.WriteLine(string.Format("Message {0} sent to secondary queue: Body = {1}", m2.MessageId, m2.GetBody<string>()));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unable to send message {0} to secondary queue: Exception {0}", m2.MessageId, e.ToString());
                        ex = e;
                        exceptionCount++;
                    }

                    // Throw exception if send operation on both queues failed.
                    if (exceptionCount > 1)
                    {
                        throw new Exception("Send Failure");
                    }
                }

                Console.WriteLine("\nAfter running the entire sample, press ENTER to clean up and exit.");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception {0}", e.ToString());
                throw e;
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
                secondaryManagementObjects.DeleteQueue(QueueName);
            }
        }

    }
}
