# Deferral

# What is Deferral

When a Queue or Subscription client receives a message that it is willing to process, but for which processing is 
not currently possible, it has the option of "deferring" retrieval of the message to a later point. The 
API gesture is ```BrokeredMessage.Defer```/```BrokeredMessage.DeferAsync```.

Deferred messages remain in the main queue along with all other active messages (unlike [Deadletter](../Deadletter)
messages that sit in a sub-queue), but they can no longer be received using the regular ```Receive```/```ReceiveAsync``` 
functions.   

Instead, the "owner" of a deferred message is responsible for remembering the ```SequenceNumber``` of the deferred 
message and can then, at the appropriate time, receive this message explicitly with ```Receive(sequenceNumber)```.

Deferring messages does not impact message's expiration, meaning that deferred messages can still expire. 

## Why would I use it?
 
Deferral is a feature specifically created for workflow processing scenarios. Workflow frameworks may 
require certain operations to complete in a particular order, and postpone processing of some received
messages until prescribed prior work informred by other messages has been completed.

Ultimately, the features aids in re-sorting messages while leaving those message safely in the message store.

The [SessionState](../SessionState) concept and sample builds on this sample and shows how to use 
the broker's session state feature to keep track of which messages were deferred in a particular context.

 ##The Sample

     

