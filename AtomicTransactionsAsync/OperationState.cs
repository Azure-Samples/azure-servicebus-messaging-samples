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
using System.Transactions;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.ServiceBus.Samples.AsyncTransactions
{
    class OperationState
    {
        public QueueClient qc;
        public CommittableTransaction tx;
        public Int32 synchronizationCounter;
        public Int32 exceptionCounter;
        public FaultInjector faultInjector; // TESTING ONLY.

        public OperationState(QueueClient qc)
        {
            this.qc = qc;
            this.tx = new CommittableTransaction();
            this.synchronizationCounter = 1;
            this.exceptionCounter = 0;
        }
    }
}
