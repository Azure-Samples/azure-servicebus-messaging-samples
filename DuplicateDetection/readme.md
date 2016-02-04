#Duplicate Detection

This sample illustrates the "duplicate detection" feature of the Service Bus client.

The sample is specifically crafted to demonstrate the effect of duplicate detection when
enabled on a queue or topic. The default setting is for duplicate detection to be turned off. 

## What is duplicate detection?

Enabling duplicate detection will keep track of the *MessageId* of all messages sent into 
a queue or topic during a defined time window. If any new message is sent that carries a 
*MessageId* that has already been logged during the time window, the message will be reported
as being accepted (the send operation succeeds), but the newly sent message will be instantly 
ignored and dropped. No other parts of the message are being considered.

## Why would I need this?

Simply stated, if a client wanted to send a message, and believes it couldn't send the message, 
but factually did send the message, a retry will cause the same message to end up in the system 
twice. It is quite possible that a message gets committed into the queue and acknowledgment can't 
be returned to the sender. Duplicate detection takes the doubt out of this situation by letting
the sender re-send the same message and tossing out any duplicate copy.

## Transactions  

Duplicate detection is a key element in creating reliable business applications in environments,
like the cloud, where the foundational assumptions for supporting distributed "atomic" transactions 
are no longer true.

Service Bus, like most other cloud platform services, including practically all databases, cannot 
be enlisted into a 2-phase-commit transaction governed by a transaction coordinator. 

That means that the classic model of having a receive operation from a queue, an insert into a database, 
and the execution of some logic either succeed together or fail together, and do so in isolation such that 
intermediate results of a failed or ongoing transaction will not impact any parallel work, is not an 
option for (scalable) cloud solutions. 

Since applications do need such composite operations and consistent outcomes, what do we do?

A Service Bus Queue is a great start to communicate "jobs" that must be executed reliably. The 
queue consumer picks up the job input, performs the desired work, and reports the outcome. If anything 
happens during execution of the work, including an outright crash, the message lock will eventually
expire, and the job will be picked up again and can again be executed.

Many cloud resources, like storage or databases have some notion of batch commits or local 
transactions that allow composite work on that resource to either summarily succeed or summarily 
fail, which limits (not eliminates) the risk of leaving orphaned intermediate results sitting around. 
If the overall operation fails, you abandon the job message for a re-run or or [Deadletter](../Deadletter) it. 
If it succeeds, you report the job outcome to output queue. And only once you have done that successfully,
you report the job as complete.  

What is important for such a strategy is that the system can cope with executing the same job or parts 
of the same job multiple times with predictable results. (That is not always easy.) It's also a considerable 
source of confusion when the same job gets reported as being completed two or more times.

So imagine now that we're performing a composite activity successfully, report it as done, and then 
get stuck or crash while completing the job message. Another consumer might pick up the job, do the work 
again, and report it as done once again and then complete the job message successfully. We've done the 
work twice due to a local error condition, but now we also leaked the fact that we did the work twice to all 
downstream consumers.        

Duplicate detection to the rescue!

When you ensure that the message reporting the job outcome will always carry the same message-id (like the 
job's identifier), the second, sadly superfluous execution of the job won't cause downstream confusion 
as it is being absorbed and dropped.      
      
   
## How do I turn it on?

The feature can be turned on setting [QueueDescription.RequiresDuplicateDetection](https://msdn.microsoft.com/library/azure/microsoft.servicebus.messaging.queuedescription.requiresduplicatedetection.aspx) or
[TopicDescription.RequiresDuplicateDetection]() to **true** when creating a queue or topic.  

The setup script creates a queue with this property turned on and this sample uses that queue.

You can configure the size of the duplicate detection window during which message-ids are being
retained with the expressively named [QueueDescription.DuplicateDetectionHistoryTimeWindow](https://msdn.microsoft.com/en-us/library/azure/microsoft.servicebus.messaging.queuedescription.duplicatedetectionhistorytimewindow.aspx) property. The default
value is 10 minutes. 

Mind that the enabling duplicate detection and size of the window will directly impacts a queues' (and a topic's) throughgput.
Keeping the window small, means that fewer message-ids must be retained and matched and throughput is impacted less. For 
high throughput entities that require duplicate detection, you should keep the window as small as feasible for the use-case.     
   
## Prerequisites and Setup

All samples share the same basic setup, explained in the main [README](../README.md) file. There are no extra setup steps for this sample.
The application entry points are in [Main.cs](../common/Main.md), which is shared across all samples. The sample implementations generally
reside in *Program.cs*, starting with *Run()*.

You can build the sample from the command line with the [build.bat](build.bat) or [build.ps1](build.ps1) scripts. This assumes that you
have the .NET Build tools in the path. You can also open up the [QueuesGettingStarted.sln](QueuesGettingStarted.sln) solution file with Visual Studio and build.
With either option, the NuGet package manager should download and install the **WindowsAzure.ServiceBus** package containing the
Microsoft.ServiceBus.dll assembly, including dependencies.

## Sample Code

Having worked through [QueuesGettingStarted](../QueuesGettingStarted) and [ReceiveLoop](../ReceiveLoop), you
will be familiar with the majority of API gestures we use here, so we're not going to through all of 
those again.

The sample really just sends two messages that have the same MessageId set:  

``` C#
    // Send messages to queue
    Console.WriteLine("\tSending messages to {0} ...", queueName);
    var message = new BrokeredMessage
    {
        MessageId = "ABC123",
        TimeToLive = TimeSpan.FromMinutes(1)
    };
    await sender.SendAsync(message);
    Console.WriteLine("\t=> Sent a message with messageId {0}", message.MessageId);

    var message2 = new BrokeredMessage
    {
        MessageId = "ABC123",
        TimeToLive = TimeSpan.FromMinutes(1)
    };
    await sender.SendAsync(message2);
```

Following that is a simple loop that receives messages until the queue is empty:

``` C#
    while (true)
    {
        var receivedMessage = await receiver.ReceiveAsync(TimeSpan.FromSeconds(10));

        if (receivedMessage == null)
        {
            break;
        }
        Console.WriteLine("\t<= Received a message with messageId {0}", receivedMessage.MessageId);
        await receivedMessage.CompleteAsync();
        if (receivedMessageId.Equals(receivedMessage.MessageId, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("\t\tRECEIVED a DUPLICATE MESSAGE");
        }

        receivedMessageId = receivedMessage.MessageId;
    }
``` 

When you execute the sample you will find that the second message is not being received. As expected.