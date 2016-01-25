#Introduction
This sample demonstrates how to automatically forward messages from a queue, subscription, or deadletter queue into another queue or topic. It also demonstrates how to send a message into a queue or topic via a transfer queue.
##Building the Sample
* Install Windows Azure SDK 2.3 or later.
* Create a Service Bus namespace.
* In files Program.cs, replace strings serviceNamespace, sasKeyName and sasKey with the information of your namespace.
* Build the sample. Open the solution in Visual Studio 2010 or later and press F6.
* Press F5 to run the application.

The sample generates 3 messages: M1, M2 and M3. M1 is sent to a source topic with one subscription, from which it is fowarded to a destination queue. M2 is sent to the destination queue via a transfer queue. M3 is sent to a source topic with two subscriptions. One subscription forwards M3 to the destination queue. The second subscription deadletters M3. Service Bus forwards this copy of M3 to the destination queue.
##Description
The auto-forwarding feature enables you to chain a subscription or queue to another queue or topic that is part of the same service namespace. When auto-forwarding is enabled, Service Bus automatically removes messages that are placed in the first queue or subscription (source) and puts them in the second queue or topic (destination). Note that it is still possible to send a message to the destination entity directly. Also note that it is not possible to chain a subqueue, such as a deadletter queue, to another queue or topic.
To forward messages from a source queue or subscription to a destination queue or topic, set the QueueDescription.ForwardTo property to the name of destination queue or topic. Note that the destination queue/topic must aleady exist at the time you create the source queue/subscription. Service Bus requires the sender to attach a token that indicates that the sender has send permissions on the source topic. The sender does not need any permissions on the destination queue.

```C#
QueueDescription sourceQueueDescription = new QueueDescription(SourceQueueName); 
sourceQueueDescription.ForwardTo = DestinationQueueName; 
namespaceManager.CreateQueue(sourceQueueDescription);
```

To forward messages from a deadletter queue, set the QueueDescription.ForwardDeadLetteredMessagesTo property to the path of destination queue or topic. Again, Service Bus requires the sender to attach a token that indicates that the sender has send permissions on the source topic. The sender does not need any permissions on the destination queue.
``` C#
QueueDescription sourceQueueDescription = new QueueDescription(SourceQueueName);  
sourceQueueDescription.ForwardDeadLetteredMessagesTo = DestinationQueue.Path;  
namespaceManager.CreateQueue(sourceQueueDescription);
```

Sending a message to a destination queue or topic via a transfer queue can be used to send multiple messages to different queues/topics in a single transaction. Service Bus does not support to directly send multiple messages to different queues in a single transaction. To achieve this, set up a transfer queue and send all messages via that transfer queue. The transaction spans the sending into the transfer queue. From there, Service Bus automatically pumps each of the messages into their destination queues. To send messages to a destination queue via a transfer queue, the sender must have permissions to send to both queues. This can be achieved by giving the sender namespace-level send permission or use the same key and key name for the send rule for both entities.
```C#
namespaceManager.CreateQueue(DestinationQueueName); 
namespaceManager.CreateQueue(TransferQueueName); 
MessageSender sender = messagingFactory.CreateMessageSender(DestinationQueueName, TransferQueueName); 
sender.Send(new BrokeredMessage("my message"));
```
##More Information
For more information on Autoforwarding, see Chaining Service Bus Entities with Auto-forwarding.