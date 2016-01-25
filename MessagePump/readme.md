#Introduction
This sample implements a message pump that pumps messages from a source queue or subscription into a destination queue or topic. The source and the destination entity may reside in different namespaces, which may be hosted in different datacenters or regions.

##Building the Sample
Install Windows Azure SDK or upload the Microsoft.ServiceBus.dll from NuGet. 
Create a Service Bus namespace with the Azure Portal.
Open the solution in Visual Studio 2010 or later.
Modify the connection string in the App.config file and press F6. The connection string is formatted for a SAS key.
To enable the fault injector, set const bool EnableFaultInjection to true in file MessagePump.cs.
Press F5 to run the client. When prompted, supply the namespace name, issuer name and issuer key of your Service Bus namespaces.
Description
This sample demonstrates the implementation of a message pump that is able to pump messages from a source queue or subscription into a destination queue or topic. The source and the destination entity may reside in different namespaces, which may be hosted in different datacenters or regions.

The message pump is designed to pump messages at a large rate. The pump can be part of an application or can be hosted in a dedicated process that is deployed on-premise or in Azure.

To achieve high throughput, the message pump is implemented asynchronously. It utilizes the CLR’s IO threads. At the same time, the message pump uses multiple messaging factories. Since multiple threads concurrently receive messages from the source entity and send messages to the destination entity, the message pump does not preserve the message order.

If the message pump experiences a temporary failure when receiving or sending a message, it waits for a little while and then tries again. With every fault the wait time increases exponentially up to a maximum of 60 seconds.

If the message pump experiences a permanent failure when sending a message to the destination entity, the message pump moves the message into source entity’s deadletter queue.

To emulate transient and permanent faults, the sample comes with a fault injector. To enable fault injection, set const bool EnableFaultInjection to true in file MessagePump.cs.

The file client.cs implements the client that sends messages to a source queue and then starts the pump to pump all messages into a destination queue. For simplicity, both queues reside in the same namespace. After sleeping for a short while the client receives the messages from the destination queue and checks that all messages have been received exactly once. The sleep is required only to get accurate performance numbers of the message pump’s throughput.

##Source Code Files
App.config: Contains application configuration data and the Servcie Bus connection string.
AsyncArguments.cs: Implements a set of properties that are passed to callback methods.
Client.cs: Implements the client that sends messages to the source queue, starts the message pump, and then receives messages from the destination queue.
FaultInjector.cs: Implements a mechanism that simulates various Service Bus failures.
MessagePump.cs: Implements the message pump.
PerfMeasure.cs: Implements the infrastructure required to perform throughput measurements.
TimerWaitTime.cs: Maintains current wait times in case of transient failures.
##More Information
For more information on Service Bus, see http://msdn.microsoft.com/en-us/library/windowsazure/jj656647.aspx.