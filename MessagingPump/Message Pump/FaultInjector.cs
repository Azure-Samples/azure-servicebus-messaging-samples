//---------------------------------------------------------------------------------
// Copyright (c) 2013, Microsoft Corporation
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

using System;
using System.Threading;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.ServiceBus.Samples.MessagePump
{
    class FaultInjector
    {
        Int32 beforeReceivingMessageCount;
        Int32 afterReceivingMessageCount;
        Int32 beforeSendingMessageCount;
        Int32 afterSendingMessageCount;
        Int32 beforeCompletingMessageCount;
        Int32 afterCompletingMessageCount;
        Int32 beforeDeadLetteringMessageCount;
        Int32 afterDeadLetteringMessageCount;
        bool active;

        public FaultInjector(bool active)
        {
            this.active = active;
        }

        public void InjectFaultBeforeReceivingMessage()
        {
            Int32 count = Interlocked.Increment(ref beforeReceivingMessageCount);
            if (active && count == -1)
            {
                throw (new InvalidOperationException("Fault injector simulates a permanent Service Bus error."));
            }
        }

        public void InjectFaultAfterReceivingMessage()
        {
            Int32 count = Interlocked.Increment(ref afterReceivingMessageCount);
            if (active && count == -1)
            {
                throw (new ServerBusyException("Fault injector simulates a transient Service Bus error."));
            }
            if (active && count == -1)
            {
                throw (new InvalidOperationException("Fault injector simulates a permanent Service Bus error."));
            }
        }

        public void InjectFaultBeforeSendingMessage()
        {
            Int32 count = Interlocked.Increment(ref beforeSendingMessageCount);
            if (active && count == 22)
            {
                throw (new InvalidOperationException("Fault injector simulates a permanent Service Bus error."));
            }
        }

        public void InjectFaultAfterSendingMessage()
        {
            Int32 count = Interlocked.Increment(ref afterSendingMessageCount);
            if (active && count > 55 && count < 70)
            {
                throw (new ServerBusyException("Fault injector simulates a transient Service Bus error."));
            }
            if (active && count == -1)
            {
                throw (new InvalidOperationException("Fault injector simulates a permanent Service Bus error."));
            }
        }

        public void InjectFaultBeforeCompletingMessage()
        {
            Int32 count = Interlocked.Increment(ref beforeCompletingMessageCount);
            if (active && count == -1)
            {
                throw (new InvalidOperationException("Fault injector simulates a permanent Service Bus error."));
            }
        }

        public void InjectFaultAfterCompletingMessage()
        {
            Int32 count = Interlocked.Increment(ref afterCompletingMessageCount);
            if (active && count == -1)
            {
                throw (new ServerBusyException("Fault injector simulates a transient Service Bus error."));
            }
            if (active && count == -1)
            {
                throw (new InvalidOperationException("Fault injector simulates a permanent Service Bus error."));
            }
        }

        public void InjectFaultBeforeDeadLetteringMessage()
        {
            Int32 count = Interlocked.Increment(ref beforeDeadLetteringMessageCount);
            if (active && count == -1)
            {
                throw (new InvalidOperationException("Fault injector simulates a permanent Service Bus error."));
            }
        }

        public void InjectFaultAfterDeadLetteringMessage()
        {
            Int32 count = Interlocked.Increment(ref afterDeadLetteringMessageCount);
            if (active && count == -1)
            {
                throw (new ServerBusyException("Fault injector simulates a transient Service Bus error."));
            }
            if (active && count == -1)
            {
                throw (new InvalidOperationException("Fault injector simulates a permanent Service Bus error."));
            }
        }
    }
}
