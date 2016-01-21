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
    using System.Collections.Generic;
    using Microsoft.ServiceBus.Messaging;

    public class Helper
    {
        static List<Type> intermittentFailureExceptions = new List<Type>();

        public static void SendMessage(QueueClient queueClient, BrokeredMessage message, string replica)
        {
            intermittentFailureExceptions.Add(typeof(ServerBusyException));
            intermittentFailureExceptions.Add(typeof(MessagingCommunicationException));
            intermittentFailureExceptions.Add(typeof(TimeoutException));

            var attemptCounter = 1;
            while(true)
            {
                try
                {
                    // Simulate exceptions.
                    // Throw ServerBusyException for first attempt to send message 2 to primary queue.
                    //if (String.Compare(message.MessageId, "2") == 0 && attemptCounter == 1 && String.Compare(replica, "primary") == 0)
                    //{
                    //    throw new ServerBusyException("Simulating a busy server.");
                    //}
                    // Throw TimeoutException for any attempt to send message 4 to primary queue.
                    //if (String.Compare(message.MessageId, "4") == 0 && String.Compare(replica, "primary") == 0)
                    //{
                    //    throw new TimeoutException("Simulate a timeout.");
                    //}
                    // Throw MessagingEntityNotFoundException for any attempt to send message 2 to primary queue.
                    if (String.Compare(message.MessageId, "2") == 0 && String.Compare(replica, "primary") == 0)
                    {
                        throw new MessagingEntityNotFoundException("primary queue");
                    }
                    // Throw MessagingEntityNotFoundException for any attempt to send message 4 to secondary queue.
                    if (String.Compare(message.MessageId, "4") == 0 && String.Compare(replica, "secondary") == 0)
                    {
                        throw new MessagingEntityNotFoundException("secondary queue");
                    }
                    // Throw InvalidOperationException for any attempt to send message 6.
                    if (String.Compare(message.MessageId, "6") == 0)
                    {
                        throw new InvalidOperationException("Simulating an invalid operation");
                    }
                  
                    // Send message.
                    queueClient.Send(message);
                    return;
                }
                catch (Exception e)
                {
                    // If the send operation failed due to an intermittent failure, increment attemptCounter and try again.
                    // If we tried 3 times already, give up and bubble up exception.
                    var intermittent = false;
                    foreach (var t in intermittentFailureExceptions)
                    {
                        if (e.GetType() == t)
                        {
                            Console.WriteLine("Intermittent failure on attempt {0} to send message {1} to {2} queue: {3}", attemptCounter, message.MessageId, replica, t.ToString());
                            intermittent = true;
                            if (attemptCounter > 2)
                            {
                                throw e;
                            }    
                            attemptCounter++;
                            break;
                        }
                    }

                    if (intermittent == true)
                    {
                        continue; // Do next iternation of while(true) loop.
                    }

                    // If the send operation failed due to any non-intermittent failure, bubble up exception.
                    throw e;
                }
            }
        }

        public static BrokeredMessage CreateSampleMessage(int i)
        {
            var body = "Message" + i.ToString();
            var message = new BrokeredMessage(body);
            message.MessageId = i.ToString();
            message.TimeToLive = TimeSpan.FromMinutes(2.0);

            return message;
        }
    }
}
