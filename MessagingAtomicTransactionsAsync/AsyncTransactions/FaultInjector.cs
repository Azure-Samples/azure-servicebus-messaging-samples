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

using System;

namespace Microsoft.ServiceBus.Samples.AsyncTransactions
{
    class FaultInjector
    {
        bool active;
        long countBeginSend;
        long countEndSend;

        public FaultInjector(bool active)
        {
            this.active = active;
            this.countBeginSend = 0;
            this.countEndSend = 0;
        }

        public void InjectFaultAtBeingSend()
        {
            countBeginSend++;
            if (active && countBeginSend == -1)
            {
                throw (new InvalidOperationException("Fault injector simulates fault at BeginSend operation."));
            }
        }

        public void InjectFaultAtEndSend()
        {
            countEndSend++;
            if (active && countEndSend == 3)
            {
                throw (new InvalidOperationException("Fault injector simulates fault at EndSend operation."));
            }
        }
    }
}
