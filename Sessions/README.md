# Sessions

This sample illustrates the Session handling feature of Azure Service Bus. 

## What are Service Bus Sessions?

Service Bus Sessions, also called "Groups" in the AMQP 1.0 protocol, are unbounded sequences of related 
messages. Service Bus is not prescriptive about the nature of the relationship, and also doesn't 
define a particular model for telling where a message sequence starts or ends.

Any sender can "create" a session when submitting messages into a Topic or Queue by setting the 
```BrokeredMessage.SessionId``` property to some application-defined identifier that is unique to 
the session. At the AMQP 1.0 protocol level, this value maps to the ```group-id``` property. 

Sessions come into existence when there is at least one message with the session's ```SessionId``` 
in the Queue or Topic subscription. Once a Session exists, there is no defined moment or gesture 
for when the session expires or disappears.  

Theoretically, a message can be received for a session today, and the next message in a year's time, 
and if the ```SessionId``` matches, the session is the same from the Service Bus perspective.

We say "theoretically", because an application usually has a notion of where a set of related 
messages starts and ends; Service Bus simply doesn't set any specific rules. In the sample we 
show a set of related messages for which there is a clear rule of where the session ends.

The Session *feature* in Service Bus enables a specific kind of receive gesture in form of 
the ```MessageSession```. You turn the feature on by setting ```QueueDescription.RequiresSession``` or
```SubscriptionDescription.RequiresSession``` to *true*, meaning that the entity must be correctly 
configured before you attempt to use the related API gestures. 

The foundational API gestures for Sessions exist on both the ```QueueClient``` and the 
```SubscriptionClient```. There is an imperative model where you control when sessions and messages 
are received, and a callback-based model, very similar to ```OnMessage``` that hides the 
complexity of managing the receive loop. The callback model is what this sample shows.

## Session Features

The Session feature provides concurrent demultiplexing of interleaved message streams while
preserving and guaranteeing ordered delivery.

To illustrate this, let's look at a picture:

```
Queue
-----------------------
12321423112312133213321
-----------------------
          |
Streams   V                              +--- MessageSession.Receive() -> 1 1 1 1
-----------------------                  |
1   1   11  1 1   1   1  > SessionId=1 --+  
 2 2  2   2  2   2   2   > SessionId=2 ------ MessageSession.Receive() -> 2 2 2 2
  3    3   3   33  33    > SessionId=3 --+
     4                   > SessionId=4   |
-----------------------                  +--- MessageSession.Receive() -> 3 3 3 3
```

A ```MessageSession``` receiver is created by the client accepting a session.
Imperatively, the client calls ```QueueClient.AcceptMessageSession```/
```QueueClient.AcceptMessageSessionAsync```, in the callback model is registers a session handler 
as we'll show below.

When the session is accepted and while it is held by a client, that client holds an exclusive lock on
*all* messages with that session's ``´SessionId``` that exist in the Queue or Subscription, and also 
on all messages that will arrive with that ``SessionId`` while the session is held.

When multiple concurrent receivers now pull from the queue, the messages belonging to a particular 
session are dispatched to the specific receiver that currently holds the lock for that session.
With that, an interleaved message stream residing in one Queue or Subscription gets cleanly 
de-multiplexed to different receivers and those receivers can also sit on different client machines,
since the lock management happens inside Service Bus.

The Queue is, however, still a queue. There is no random access. The illustration above shows
three concurrent ```MessageSession`` receivers, which all *must* actively take messages off the 
Queue for every receiver to make progress. The Session with ```SessionId=4``` above has no 
active, owning client, which means that no messages will be delivered to anyone until that 
message has been taken by a newly created owning session receiver.  

While that might appear very constraining, a single receiver process can indeed handle very many 
concurrent sessions easily, especially when they are written with strictly asynchronous code; 
juggling several dozen concurrent sessions effectively automatic with the callback model.

The strategy for handling very many concurrent sessions, whereby each session only sporadically 
receives messages is for the handler to drop the session after some idle time and pick up
processing again when the session is accepted as the next session arrives.

The session lock held by the session receiver is an umbrella for the message locks used by the
```ReceiveMode.PeekLock``` mode. A receiver cannot have two messages concurrently "in flight",
but the messages *must* be processed in order. A new message can only be obtained when the prior
message has been completed. 

### The sample

   

  

        

  
   


   