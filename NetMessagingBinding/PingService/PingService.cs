//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.Samples.SessionMessages
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using Microsoft.ServiceBus.Messaging;

    [ServiceContract]
    public interface IPingServiceContract
    {
        [OperationContract(IsOneWay = true)]
        [ReceiveContextEnabled(ManualControl = true)]
        void Ping(PingData pingData);
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode=ConcurrencyMode.Single)]
    public class PingService : IPingServiceContract
    {
        [OperationBehavior]
        public void Ping(PingData pingData)
        {
            // Get the message properties
            var incomingProperties = OperationContext.Current.IncomingMessageProperties;
            BrokeredMessageProperty property = (BrokeredMessageProperty)incomingProperties[BrokeredMessageProperty.Name];

            // Print message
            SampleManager.OutputMessageInfo("Receive", pingData);

             //Complete the Message
            ReceiveContext receiveContext;
            if (ReceiveContext.TryGet(incomingProperties, out receiveContext))
            {
                receiveContext.Complete(TimeSpan.FromSeconds(10.0d));
            }
            else
            {
                throw new InvalidOperationException("Receiver is in peek lock mode but receive context is not available!");
            }
        }
    }
}

