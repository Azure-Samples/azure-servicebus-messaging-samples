#Introduction
This sample demonstrates how to use the Windows Azure Service Bus duplicate message detection with queues. See the Service Bus documentation for more information about the Service Bus before exploring the samples.

This sample creates two queues, one with duplicate detection enabled and other one without duplicate detection.

 

##Prerequisites
If you haven't already done so, please read the release notes document that explains how to sign up for a Windows Azure account and how to configure your environment.


##Sample Flow
The sample flows in the following manner:

1. Sample creates a queue called “DefaultQueue” without duplicate
detection enabled:
    1. Sends a message with MessageId “MessageId123”;
    2. Sends another message with the same MessageId i.e. a duplicate
message
    3. Receives the messages. It receives both the messages as the queue
does not detect duplicate messages.
1. Sample creates another queue called “RemoveDuplicatesQueue” with
duplicate detection enabled:
    1. Sends a message with MessageId “MessageId123”;
    2. Sends another message with the same MessageId i.e. a duplicate
message
    3. Receives the messages. This time it receives only one message
since the duplicate messages are detected and dropped by the queue itself.
3. Sample deletes both the queues
 

##Running the Sample
To run the sample:

Build the solution in Visual Studio and run the sample project.
When prompted enter the Service Bus connection string.

##Expected Output

Please provide a connection string to ServiceBus (/? for help): <connection string>
 
Creating DefaultQueue ...
Created DefaultQueue
        Sending messages to DefaultQueue ...
        => Sent a message with messageId ABC123
        => Sent a duplicate message with messageId ABC123
 
        Waiting for messages from DefaultQueue ...
        <= Received a message with messageId ABC123
        <= Received a message with messageId ABC123
                RECEIVED a DUPLICATE MESSAGE
        Done receiving messages from DefaultQueue
 
Creating RemoveDuplicatesQueue ...
Created RemoveDuplicatesQueue
        Sending messages to RemoveDuplicatesQueue ...
        => Sent a message with messageId ABC123
        => Sent a duplicate message with messageId ABC123
 
        Waiting for messages from RemoveDuplicatesQueue ...
        <= Received a message with messageId ABC123
        Done receiving messages from RemoveDuplicatesQueue
 
Press [Enter] to exit.
