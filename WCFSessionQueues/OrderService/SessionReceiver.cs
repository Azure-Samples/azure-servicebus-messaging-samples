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

    public class SessionReceiver
    {
        static void Main(string[] args)
        {
            try
            {
                // Create MessageReceiver for queue which requires session
                Console.Title = "Order Service";
                Console.WriteLine("Ready to receive messages from {0}...", SampleManager.OrderQueueName);

                // Creating the service host object as defined in config
                ServiceHost serviceHost = new ServiceHost(typeof(OrderService));

                // Add ErrorServiceBehavior for handling errors encounter by servicehost during execution.
                serviceHost.Description.Behaviors.Add(new ErrorServiceBehavior());

                // Subscribe to the faulted event.
                serviceHost.Faulted += new EventHandler(serviceHost_Faulted);

                // Start service
                serviceHost.Open();

                Console.WriteLine("\nPress [Enter] to close ServiceHost.");
                Console.ReadLine();

                // Close the service
                serviceHost.Close();
            }
            catch (Exception exception)
            {
                SampleManager.ExceptionOccurred = true;
                Console.WriteLine("Exception occurred: {0}", exception);
                Console.WriteLine("\nPress [Enter] to exit.");
                Console.ReadLine();
            }
        }

        static void serviceHost_Faulted(object sender, EventArgs e)
        {
            Console.WriteLine("Fault occurred. Aborting the service host object ...");
            ((ServiceHost)sender).Abort();
        }
    }
}
