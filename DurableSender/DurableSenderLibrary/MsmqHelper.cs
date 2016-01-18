//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure Platform AppFabric SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples.DurableSender
{
    using System;
    using System.Messaging;
    using Microsoft.ServiceBus.Messaging;

    public class MsmqHelper
    {
        // Create the specified transactional MSMQ queue if it doesn't exist.
        // If it exists, open existing queue. Return the queue handle.
        public static MessageQueue GetMsmqQueue(string queueName)
        {
            MessageQueue msmqQueue = new MessageQueue(queueName, true);
            if (!MessageQueue.Exists(queueName))
            {
                MessageQueue.Create(queueName, true);
                Console.WriteLine("Created MSMQ queue " + queueName);
            }
            else
            {
                msmqQueue.Refresh();
            }
            msmqQueue.MessageReadPropertyFilter.SetAll();
            msmqQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(BrokeredMessage) });
            return msmqQueue;
        }

        // Create an MSMQ queue.
        public static string CreateMsmqQueueName(string sbusQueueName, string suffix)
        {
            return (".\\private$\\" + sbusQueueName.Replace("/", "_") + "_" + suffix);
        }

        // Pack a single brokered message into an MSMQ message.
        public static Message PackSbusMessageIntoMsmqMessage(BrokeredMessage sbusMessage)
        {
            Message msmqMessage = new Message(sbusMessage);
            msmqMessage.Label = sbusMessage.Label;
            return msmqMessage;
        }

        // Extract a single brokered message from an MSMQ message.
        public static BrokeredMessage UnpackSbusMessageFromMsmqMessage(Message msmqMessage)
        {
            BrokeredMessage brokeredMessage = (BrokeredMessage)msmqMessage.Body;
            return brokeredMessage;
        }
    }
}
