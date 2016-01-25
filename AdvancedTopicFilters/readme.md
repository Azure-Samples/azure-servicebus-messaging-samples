#Introduction
This sample demonstrates how to use the Windows Azure Service Bus publish/subscribe advanced filters. See the Service Bus documentation for more information about the Service Bus before exploring the samples.

This sample creates a topic and 3 subscriptions with different filter definitions, sends messages to the topic, and receives all messages from subscriptions.

##Prerequisites
If you haven't already done so, please read the release notes document that explains how to sign up for a Windows Azure account and how to configure your environment.

 

##Sample Flow
The sample flows in the following manner:

1. Sample creates a topic and 3 subscriptions:
    1. Subscription A to receive all messages;
    2. Subscription B to receive only messages which matches filter
expression "color = 'blue' AND quantity = 10";
    3. And subscription C to receive only messages which have a
correlation value of “high”
2. A message sender sends bunch of messages.
3. A message receiver is created for each subscription and all
messages are received. We expect:
    1. Receiver of subscription A receives all messages;
    2. Receiver of subscription B receives a single message;
    3. And receiver of subscription C receives a single message.

##Running the Sample
To run the sample:

 

Build the solution in Visual Studio and run the sample project.
When prompted enter the Service Bus connection string.

##Expected Output

                       Please provide a connection string to Service Bus (/? for help): 
                        <connection string>
                         
                        Deleting topic and subscriptions from previous run if any.
                        Delete completed.

                        Creating a topic and 3 subscriptions.
                        Topic created.
                        Subscription AllOrders added with filter definition set to TrueFilter.
                        Subscription ColorBlueSize10Orders added with filter definition "color = 'blue' AND quantity = 10".
                        Subscription HighPriorityOrders added with correlation filter definition "high".
                        Create completed.

                        Sending orders to topic.
                        Sent order with Color=, Quantity=0, Priority=
                        Sent order with Color=blue, Quantity=5, Priority=low
                        Sent order with Color=red, Quantity=10, Priority=high
                        Sent order with Color=yellow, Quantity=5, Priority=low
                        Sent order with Color=blue, Quantity=10, Priority=low
                        Sent order with Color=blue, Quantity=5, Priority=high
                        Sent order with Color=blue, Quantity=10, Priority=low
                        Sent order with Color=red, Quantity=5, Priority=low
                        Sent order with Color=red, Quantity=10, Priority=low
                        Sent order with Color=red, Quantity=5, Priority=low
                        Sent order with Color=yellow, Quantity=10, Priority=high
                        Sent order with Color=yellow, Quantity=5, Priority=low
                        Sent order with Color=yellow, Quantity=10, Priority=low


                        All messages sent.

                        Receiving messages from subscription AllOrders.
                        Received 13 messages from subscription AllOrders.

                        Receiving messages from subscription ColorBlueSize10Orders.
                        Received 2 messages from subscription ColorBlueSize10Orders.

                        Receiving messages from subscription HighPriorityOrders.
                        Received 3 messages from subscription HighPriorityOrders.


                        Press [Enter] to quit...