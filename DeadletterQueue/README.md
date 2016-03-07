# Deadletter Queues

This sample shows how to move messages to the *Deadletter* queue, how to retrieve messages from it, and resubmit corrected message back into the main queue.

## What is a Deadletter Queue?

All Service Bus Queues and Subscriptions have a secondary sub-queue, called the *Deadletter Queue*. This sub-queue does not need to be explicitly 
created and cannot be deleted or otherwise managed independent of the main entity.

The purpose of the Deadletter queue is accept and hold messages that cannot be delivered to any receiver or messages that could not be processed.
Messages can then be taken out of the Deadletter queue and inspected. An application might, potentially with help of an operator, correct issues and 
resubmit the message, log the fact that there was an error, or take corrective action. (The latter is shown in the [AtomicTransactions](../AtomicTransactions) 
sample where Deadletter queues are used to initiate compensating work of a Saga)

From an API and protocol perspective, the Deadletter queue is mostly like any other queue, except that messages can only be submitted via the 
deadletter-gesture of the parent entity, that time-to-live is not observed, and that you can't deadletter from a deadletter queue. The deadletter
queue fully supports peek-lock delivery and transactional operations.

**Important:** There is no automatic cleanup of the Deadletter queue. Messages remain in the Deadletter queue until they are   

## How do messages get into the Deadletter Queue?

There are several activities in Service Bus that cause messages to get pushed to the Deadletter queue from within the messaging engine itself. The
application can also push messages to the deadletter queue explicitly. 

As the message gets moved by the broker, two properties are added to the message as the broker calls its internal version of the 
```void DeadLetter( string deadLetterReason, string deadLetterErrorDescription)``` method on the message.  

| Property Name              | Description                                               |
|----------------------------|-----------------------------------------------------------|
| DeadLetterReason           | System-defined or application-defined text code declaring |
|                            | why the message has been deadlettered. System-defined     |
|                            | codes are:                                                | 
|                            | * MaxDeliveryCountExceeded - max delivery count reached   |
|                            | * TTLExpiredException - time-to-live expired              |
| DeadLetterErrorDescription | Human readable description of the reason code             |

Applications can define their own codes for the ```DeadLetterReason``` property.

### Exceeding MaxDeliveryCount 

Queues and subscriptions have a ```QueueDescription.MaxDeliveryCount```/```SubscriptionDescription.MaxDeliveryCount``` setting; the default value is 10. 
Whenever a message has been delivered under a lock (ReceiveMode.PeekLock), but has been either explicitly abandoned or the lock has expired, the message's
```BrokeredMessage.DeliveryCount``` is incremented. When the DeliveryCount exceeds the ```MaxDeliveryCount```, the message gets moved to the Deadletter queue 
specifying the ``MaxDeliveryCountExceeded``` reason code.

This behavior cannot be turned off, but the ```MaxDeliveryCount``` can set to a very large number. 

### Exceeding TimeToLive

When the ```QueueDescription.EnableDeadLetteringOnMessageExpiration```/```SubscriptionDescription.EnableDeadLetteringOnMessageExpiration``` property is
set to *true* (the default is *false*), all expiring messages are moved to the deadletter queue, specifying the ``TTLExpiredException``` reason code.

Mind that expired messages are only purged and therefore moved to the Deadletter queue when there is at least one active receiver pulling on the 
main Queue or Subscription; that behavior is by design.

### Errors while processing Subscription rules 

When ```SubscriptionDescriptionEnableDeadLetteringOnFilterEvaluationExceptions```is turned on for a subscription, any errors that occur while a
subscription's SQL filter rule executes are being captured in the Deadletter queue along with the offending message.

### Application-Level Deadlettering

In addition to these system-provided deadlettering features, applications can use the Deadletter queue explicitly to reject unacceptable messages. 
This may include messages that cannot be properly processed due to any sort of system issue, messages that hold malformed payloads, or messages that fail 
authentication when some message-level security scheme is used.

##The Sample

[TBD]         

    



    


 
    
 