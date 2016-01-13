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
    using System.Threading;
    using Microsoft.ServiceBus.Messaging;
    
    public class Client
    {
        #region Fields
        static string customerId;
        static int orderQuantity;
        #endregion

        static void Main(string[] args)
        {
            try
            {
                ParseArgs(args);

                // Send messages to queue which does not require session
                Console.Title = "Order Client";

                // Create sender to Order Service
                ChannelFactory<IOrderServiceContract> sendChannelFactory = new ChannelFactory<IOrderServiceContract>(SampleManager.OrderSendClientConfigName);
                IOrderServiceContract clientChannel = sendChannelFactory.CreateChannel();
                ((IChannel)clientChannel).Open();

                // Send messages
                orderQuantity = new Random().Next(10, 30);
                Console.WriteLine("Sending {0} messages to {1}...", orderQuantity, SampleManager.OrderQueueName);
                PlaceOrder(clientChannel);

                // Close sender
                ((IChannel)clientChannel).Close();
                sendChannelFactory.Close();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Exception occurred: {0}", exception);
                SampleManager.ExceptionOccurred = true;
            }

            Console.WriteLine("\nSender complete.");
            Console.WriteLine("\nPress [Enter] to exit.");
            Console.ReadLine();
        }

        static void PlaceOrder(IOrderServiceContract clientChannel)
        {
            // Send messages to queue which requires session:
            for(int i = 0; i < orderQuantity; i++)
            {
                using (OperationContextScope scope = new OperationContextScope((IContextChannel)clientChannel))
                {
                    OrderItem orderItem = RandomizeOrder();

                    // Assigning the session name
                    BrokeredMessageProperty property = new BrokeredMessageProperty();

                    // Correlating ServiceBus SessionId to CustomerId 
                    property.SessionId = customerId;

                    // Add BrokeredMessageProperty to the OutgoingMessageProperties bag to pass on the session information 
                    OperationContext.Current.OutgoingMessageProperties.Add(BrokeredMessageProperty.Name, property);
                    clientChannel.Order(orderItem);
                    SampleManager.OutputMessageInfo("Order", string.Format("{0} [{1}]", orderItem.ProductId, orderItem.Quantity), customerId);
                    Thread.Sleep(200);
                }
            }
        }

        static OrderItem RandomizeOrder()
        {
            // Generating a random order
            string productId = SampleManager.Products[new Random().Next(0, 6)];
            int quantity = new Random().Next(1, 100);
            return new OrderItem(productId, quantity);
        }

        static void ParseArgs(string[] args)
        {
            if (args.Length != 1)
            {
                // Customer Id is needed to identify the sender
                customerId = new Random().Next(1, 7).ToString();
            }
            else
            {
                customerId = args[0];
            }
        }
    }
}
