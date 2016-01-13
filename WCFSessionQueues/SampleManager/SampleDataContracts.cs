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
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract(Name="OrderDataContract", Namespace="Microsoft.Samples.SessionMessages")]
    public class OrderItem
    {
        [DataMember]
        public string ProductId;

        [DataMember]
        public int Quantity;

        public OrderItem(string productId)
            : this(productId, 1)
        {
        }

        public OrderItem(string productId, int quantity)
        {
            this.ProductId = productId;
            this.Quantity = quantity;
        }
    }
}
