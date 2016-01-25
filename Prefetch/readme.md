#Introduction
This sample demonstrates how to use the Windows Azure Service Bus messages prefetch feature. See the Service Bus documentation for more information about the Service Bus before exploring the samples.

This sample demonstrates how to use the messages prefetch feature upon receive. The sample creates a queue, sends messages to it and receives all messages using 2 receivers one with prefetchCount = 0 (disabled) and the other with prefetCount = 100. For each case, it calculates the time taken to receive and complete all messages and at the end, it prints the difference between both times.

 

##Prerequisites
If you haven't already done so, please read the release notes document that explains how to sign up for a Windows Azure account and how to configure your environment.

##Sample Flow

The sample flows in the following manner:

1. Sample creates a queue
2. A QueueClient is created to send and receive messages.
    1. The QueueClient sends 1000 messages;
    2. The PrefetchCount property of the QueueClient is set to 0 (disabled);
    3. The QueueClient receives all the messages;
    4. The receive time is calculated: t1.
3. Another QueueClient is created to send and receive messages.
    1. The QueueClient sends 1000 messages;
    2. The PrefetchCount property of the QueueClient is set to 100;
    3. The QueueClient receives all the messages;
    4. The receive time is calculated: t2.
4. Time difference is calculated = t1 - t2.
 

##Running the Sample
To run the sample:

Build the solution in Visual Studio and run the sample project.
When prompted enter a Service Bus connection string.

##Expected Output

 

                    Please provide a connection string to Service Bus (/? for help): 
                    <connection string>

                    Creating a queue.
                    Queue created.

                    Sending 1000 messages to the queue
                    Send completed
                    Receiving messages from queue using prefetchCount = 0
                    Receive completed
                    Time to receive and complete all messages = ...

                    Sending 1000 messages to the queue
                    Send completed
                    Receiving messages from queue using prefetchCount = 100
                    Receive completed
                    Time to receive and complete all messages = ...

                    Time difference = ...

                    Press [Enter] to quit...