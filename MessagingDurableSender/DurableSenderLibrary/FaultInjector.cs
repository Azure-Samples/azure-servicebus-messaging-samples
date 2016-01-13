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
    using Microsoft.ServiceBus.Messaging;

    class FaultInjector
    {
        int beforeSendingMessageCount;
        int afterSendingMessageCount;
        bool active;

        public FaultInjector(bool active)
        {
            this.active = active;
        }

        public void InjectFaultBeforeSendingMessageToServiceBus()
        {
            beforeSendingMessageCount++;

            // Inject a permanent fault. This simulates a scenario in which Service Bus
            // cannot handle the message (e.g., queue does not exist). This fault causes the durable sender to remove
            // the message from the MSMQ send queue and move it into the MSMQ sned deadletter queue.
            if (active && beforeSendingMessageCount == -1) // Don't simulate any permanent failures.
            {
                throw (new InvalidOperationException("Fault injector simulates a permanent Service Bus error"));
            }
        }

        public void InjectFaultAfterSendingMessageToServiceBus()
        {
            afterSendingMessageCount++;

            // Inject a transient fault after message M2 has been sent. This simulates a scenario in which
            // Service Bus receives the message but then the client returns an exception (e.g., due to a timeout).
            // This fault causes the durable sender to wait and send this message again. Service Bus will detect
            // that the second copy of M2 is a duplicate of the first copy of M2. Service Bus will suppress the
            // second copy of M2.
            if (active && afterSendingMessageCount == 2)
            {
                throw (new ServerBusyException("Fault injector simulates a transient Service Bus error"));
            }
        }
    }
}
