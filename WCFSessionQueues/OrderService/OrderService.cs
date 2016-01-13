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
    using System.Collections.Generic;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using Microsoft.ServiceBus.Messaging;

    // ServiceBus does not support IOutputSessionChannel.
    // All senders sending messages to sessionful queue must use a contract which does not enforce SessionMode.Required.
    // Sessionful messages are sent by setting the SessionId property of the BrokeredMessageProperty object.
    [ServiceContract]
    public interface IOrderServiceContract
    {
        [OperationContract(IsOneWay = true)]
        [ReceiveContextEnabled(ManualControl = true)]
        void Order(OrderItem orderItem);
    }

    // ServiceBus supports both IInputChannel and IInputSessionChannel. 
    // A sessionful service listening to a sessionful queue must have SessionMode.Required in its contract.
    [ServiceContract(SessionMode = SessionMode.Required)]
    public interface IOrderServiceContractSessionful : IOrderServiceContract
    {
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession, ConcurrencyMode = ConcurrencyMode.Single)]
    public class OrderService : IOrderServiceContractSessionful, IDisposable
    {
        #region Service variables
        List<OrderItem> orderItems;
        int messageCounter;
        string sessionId;
        #endregion

        public OrderService()
        {
            this.orderItems = new List<OrderItem>();
            this.sessionId = string.Empty;
        }

        public void Dispose()
        {
            SampleManager.OutputMessageInfo("Process Order", string.Format("Finished processing order. Total {0} items", orderItems.Count), this.sessionId);
        }

        [OperationBehavior]
        public void Order(OrderItem orderItem)
        {
            // Get the BrokeredMessageProperty from OperationContext
            var incomingProperties = OperationContext.Current.IncomingMessageProperties;
            BrokeredMessageProperty property = (BrokeredMessageProperty)incomingProperties[BrokeredMessageProperty.Name];
            
            // Get the current ServiceBus SessionId
            if (this.sessionId == string.Empty)
            {
                this.sessionId = property.SessionId;
            }

            // Print message
            if (this.messageCounter == 0)
            {
                SampleManager.OutputMessageInfo("Process Order", "Started processing order.", this.sessionId);
            }

            //Complete the Message
            ReceiveContext receiveContext;
            if (ReceiveContext.TryGet(incomingProperties, out receiveContext))
            {
                receiveContext.Complete(TimeSpan.FromSeconds(10.0d));
                this.orderItems.Add(orderItem);
                this.messageCounter++;
            }
            else
            {
                throw new InvalidOperationException("Receiver is in peek lock mode but receive context is not available!");
            }
        }
    }
}

