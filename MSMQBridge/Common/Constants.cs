//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure Platform SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------
namespace Microsoft.ServiceBus.Samples.MsmqServiceBusBridge
{
    public static class Constants
    {
        public const string MsmqSendQueue = ".\\private$\\MsmqSendQueue";
        public const string MsmqReceiveQueue = ".\\private$\\MsmqReceiveQueue";

        public const string ServiceBusSendQueue = "ServiceBusSendQueue";
        public const string ServiceBusReceiveQueue = "ServiceBusReceiveQueue";
    }
}
