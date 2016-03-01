# Atomic Transactions with Service Bus

This sample illustrates how to use Azure Service Bus atomic transaction support by implementing a 
travel booking scenario using the [Saga pattern](http://kellabyte.com/2012/05/30/clarifying-the-saga-pattern/)
first formulated by [Hector Garcia Molina and Kenneth Salem [PDF]](http://www.cs.cornell.edu/andru/cs711/2002fa/reading/sagas.pdf) 
in 1987 as a form of a long-lived transaction.     

Mind that the sample is of substantial complexity and primarily aimed at developers building frameworks leaning 
on Azure Service Bus for creating robust foundations for business applications in the cloud. Therefore, the sample 
code is very intentionally not "frameworked-over" with a smooth abstraction for hosting the simulated business logic,
since the focus is on showing the interactions with the platform. 

You can most certainly use the presented capabilities directly in a business application if you wish.

In this document we will discuss the transactional capabilities of Service Bus first, then briefly discuss Sagas (you 
are encouraged to review the blog article and the paper linked above for more depth) and how we project the concept
onto Service Bus, and then we'll take a look at the code.  

## What are Transactions?

"Transactions" are execution scopes in the context of [transaction processing](https://en.wikipedia.org/wiki/Transaction_processing).
A transaction groups two or more operations together. The goal of a transaction is that the result of this group of operations 
is consistent. A transaction coordinator or a transaction framework therefore ensures that the operations belonging to the group 
either jointly fail or jointly succeed and in this respect "act as one" - which is referred to as atomicity. 

Transaction theory is rich and defines further properties that relate to how the participants in the transaction ought to handle 
their resources during a transaction and after the transaction succeeds or fails. We'll touch on somne of those below, but diving 
into the details is well beyond the scope of this document, but there are a several excellent books and many papers for exploring 
theory and history of transaction processing. These two will be worth your time:

* Jim Gray, Andreas Reuter, Transaction Processing — Concepts and Techniques, 1993, Morgan Kaufmann, ISBN 1-55860-190-2
* Philip A. Bernstein, Eric Newcomer, Principles of Transaction Processing, 1997, Morgan Kaufmann, ISBN 1-55860-415-4
  
## How does Service Bus support transactions?

Azure Service Bus is a transactional message broker and ensures transactional integrity for all internal operations 
against its message stores and the respective indices. All transfers of messages inside of Service Bus, such as moving 
messages to a deadletter queue or automatic forwarding of messaging between entities are transactional. What that means is 
that if Service Bus reports a message as accepted it has already been stored and labeled with a sequence number, and from 
there onwards, any transfers inside of Service Bus will not lead to loss or duplication of the message. 

From a consumer perspective, Service Bus supports grouping of certain operations against individual entities in the scope 
of a transaction. You can, for instance, send several messages from within a transaction scope, and the messages will only 
be committed to the broker when the transaction successfully completes.

The operations that can be performed within a transaction scope are:
* QueueClient, MessageSender, TopicClient: 
    * Send, SendAsync
    * SendBatch, SendBatchAsync
* BrokeredMessage:  
    * Complete, CompleteAsync
    * Abandon, AbandonAsync
    * Deadletter, DeadletterAsync
    * Defer, DeferAsync
    * RenewLock, RenewLockAsync
    
Quite apparently missing are all receive operations. The assumption made for Service Bus transactions is that the application
acquires messages, in ReceiveMode.PeekLock, via a receive loop or the OnMessage callback, and then opens a transaction 
for processing the message. The disposition of the message (complete, abandon, deadletter, defer) then occurs within the 
scope of and dependent on the overall outcome of the transaction.

> Client transactions are currently only supported over the *NetMessaging* protocol and using the .NET Framework client.  

Service Bus does **not** support enlistment into distributed 2-phase-commit transactions via MS DTC or other transaction 
coordinators, so you cannot perform an operation against SQL Server and Service Bus from within the same transaction scope.

Service Bus *does* support .NET Framework transactions [which enlist volatile participants](https://msdn.microsoft.com/en-us/library/ms172153(v=vs.85).aspx) 
into a transaction scope, so you can make the outcome of a transaction and therefore whether Service Bus operations 
will be committed dependent on the outcome of independently enlisted, parallel local work.

Also not supported are transactions that span multiple Service Bus entities, so you cannot receive/complete a 
message from one queue and send to a different queue from within a single transaction scope ... (no reason to be  
disappointed, keep reading) ... but:

## Queue -> Work -> Queue

To allow transactional handover of data from a queue to a processor and then onward to another queue, Service Bus supports 
"transfers". 
```
        /---\         +-----+        +-----+
       |  P  | =====> |  T  | =====> |  Q  |
        \---/         +-----+        +-----+
``` 

In a transfer operation, a sender (or transaction processor, P) first sends a message to a "transfer queue" (T) and the 
transfer queue immediately proceeds to move the message to the actual destination queue (Q) using the same robust transfer 
implementation that the auto-forward capability relies on. The message is never committed to the transfer queue's log in 
a way that it becomes visible for the transfer queue's consumers.

This transfer model becomes a powerful tool for transactional applications, when the transfer queue is the exact entity 
from which the processor is receiving its input messages:  

```
       +-----+         /---\         +-----+        +-----+
       |  T  | =====> |  P  | =====> |  T  | =====> |  Q  |
       +-----+         \---/         +-----+        +-----+
```
   
or, illustrated differently:

```
        /---\  <===== +-----+        +-----+
       |  P  |        |  T  |        |  Q  |
        \---/  =====> +-----+ =====> +-----+
```

It may initially look a bit odd to post a message back to the queue from which your process receives, but it enables 
Service Bus to execute the operation to complete (or defer or deadletter) the input message and the operation to 
capture the resulting output message on the same message log in an atomic operation.

```
        /---\  [M1] == Complete() ===> +-----+                  +-----+
       |  P  |                         |  T  |                  |  Q  |
        \---/  [M2] == Send() =======> +-----+ [M2] == Fwd ===> +-----+
```

The way you sent of a transfer is by creating a message sender that targets the destination queue "via" the 
transfer queue. You will obviously also have a receiver that pulls messages from that same queue:  

```C#
    var sender = this.messagingFactory.CreateMessageSender(destinationQueue, myQueueName);
    var receiver = this.messagingFactory.CreateMessageReceiver(myQueueName);
```   

A simple transaction then uses these elements as follows:

```C#
   var msg = reciever.Receive();
   
   using ( scope = new TransactionScope() )
   {
       // do whatever work is required 
       
       var newmsg = ... package the result ... 
        
       msg.Complete(); // mark the message as done
       sender.Send( newmsg ); // forward the result
       
       scope.Complete(); // declare the transaction done
   } 
   
```



.... more to come here ...