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
    using System.Runtime.Serialization;

    [DataContract(Name="PingDataContract", Namespace="Microsoft.Samples.SessionMessages")]
    public class PingData
    {
        [DataMember]
        public string Message;

        [DataMember]
        public string SenderId;

        public PingData()
            : this(string.Empty, string.Empty)
        {
        }

        public PingData(string message, string senderId)
        {
            this.Message = message;
            this.SenderId = senderId;
        }
    }
}
