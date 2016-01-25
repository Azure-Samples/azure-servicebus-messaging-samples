#Introduction
This sample demonstrates how to use Service Bus partitioned queues and topics. It illustrates the use of partitioned entitites with sessions, transaction, and transfer queues.
##Building and Running the Sample
To build the samle, perform the following steps:

* Get Service Bus SDK 2.2 or later via NuGet.
* Create a Service Bus namespace.
* In file Client.cs, replace the string ConnectionString with the connection string of your namespace. Obtain the connection string from the Azure portal by marking your Service Bus namespace and pressing the Connection Information button at the bottom of the page.
* Build the sample. Open the solution in Visual Studio 2010 or later and press F6.
* Press F5 to run the client.

This sample implements a client that sends messages and receive messages to and from a partitioned Service Bus queue. It performs the following tasks:

1. Create a partitioned queue. Send and receive multiple messages outside and inside a transaction. Observe that the sequence numbers of the non-transactional messages are non-consequtive, which indicates that they are stored in different message stores. The transactional messages have consequtive sequence numbers, which indicates that they are stored in the same messaging store.
2. Create a partitioned session-aware queue. Send and receive multiple messages outside and inside a transaction. Observe that all messages have consequtive sequence numbers, which indicates that they are stored in the same message store. This is independent on whether the nmessages were sent inside or outside a transaction.
3. Create a partitioned queue, a partitioned topic, and a subscription on the partitioned topic. Configure auto-forwarding from the subscription into the queue. Send and receive multiple messages inside a transaction. The SessionId property is used as a partition key for the partitioned source topic as well as for the partitioned destination queue.
4. Create a non-session-aware partitioned queue and a session-aware partitioned queue. Send and receive multiple messages inside a transaction to the session-aware queue via the non-session-aware queue. The ViaPartitionKey is used as a partition key for the partitioned transfer queue whereas the SessionId is used as a partition key for the target queue.

##Description
To provide higher message throughput and better availability, Service Bus implements partitioning of queues and topics. Whereas a conventional queue or topic is handled by a single message broker and stored in one messaging store, a partitioned queue or topic is handled by multiple message brokers and stored in multiple messaging stores. This means that the overall throughput of a partitioned queue or topic is no longer limited by the performance of a single message broker or messaging store. In addition, a temporary outage of a messaging store does not render a partitioned queue or topic unavailable.

A partitioned queue or topic works as follows: Each partitioned queue or topic consists of multiple fragments. Each fragment is stored in a different messaging store and handled by a different message broker. When a message is sent to a partitioned queue or topic, Service Bus assigns the message to one of the fragments. The selection is done randomly by Service Bus or by a partition key that can be specified by the sender. When a client wants to receive a message from a partitioned queue or a subscription of a partitioned topic, Service Bus checks all fragments for messages. If it finds any, it picks one and passes that message to the receiver.

You create a partitioned queue or topic via the Azure portal, in Visual Studio or via a CreateQueue or CreateTopic API. Set the QueueDescription.EnablePartitioning or TopicDescription.EnablePartitioning property to true. These flags must be set at the time the queue or topic is created. It is not possible to change this property on an existing queue or topic.

```C#
QueueDescription qd = new QueueDescription(QueueName); 
qd.EnablePartitioning = true; 
if (!nm.QueueExists(QueueName)) 
{ 
    nm.CreateQueue(qd); 
} 
``` 

###Sessions

If you want to send a message to a session-aware topic or queue, the message must have the SessionId property set. The SessionId serves as the partition key. The PartitionKey property must be identical to the SessionId property or must be null. It is not possible to use a single transaction to send multiple messages to different sessions. If attempted, Service Bus returns an InvalidOperationException.

```C#
// Send messages within a transaction. 
CommittableTransaction committableTransaction = new CommittableTransaction(); 
using (TransactionScope ts = new TransactionScope(committableTransaction)) 
{ 
    BrokeredMessage msg = new BrokeredMessage("This is a message"); 
    msg.SessionId = "MySessionId"; 
    queueClient.Send(msg); 
    ts.Complete(); 
} 
committableTransaction.Commit(); 
```

###Auto-forwarding

Service Bus supports automatic message forwarding from a partitioned source queue or topic into a partitioned destination queue or topic by setting the ForwardTo property on the source queue or subscription. If the message contains a partition key, the key is used for the source as well as for the destination entity.
```C#
// Create partitioned destination queue. 
QueueDescription dqd = new QueueDescription(DestQueueName) { EnablePartitioning = true, RequiresSession = true }; 
namespaceManager.CreateQueue(dqd); 
 
// Create partitioned source queue. 
QueueDescription sqd = new QueueDescription(SrcQueueName) { EnablePartitioning = true, ForwardTo = DestQueueName }; 
namespaceManager.CreateQueue(sqd); 
 
// Send message within a transaction. 
MessageSender sender = mf.CreateMessageSender(SrcQueueName); 
CommittableTransaction committableTransaction = new CommittableTransaction(); 
using (TransactionScope ts = new TransactionScope(committableTransaction)) 
{ 
    BrokeredMessage msg = new BokeredMessage("This is a message"); 
    msg.SessionId = "MySessionId"; 
    sender.Send(msg); 
    ts.Complete(); 
} 
committableTransaction.Commit();
 ```
 
###Send via transfer queue

Service Bus supports sending messages to a partitioned queue or topic (target) via a partitioned transfer queue. If the message contains a partition key, the key is used for the destination entity only. The partitioning at the transfer queue is controlled by the ViaPartitionKey property if set. If you use a transaction to send messages to a destination queue or topic via a partitioned transfer queue, the message must have the ViaPartitionKey property set. This property is used as a partition key for the transfer queue. If a single transaction is used to send multiple messages via a same transfer queue, the ViaPartitionKey property of all messages must be set to the same value.

```C#
// Create partitioned target queue. 
QueueDescription targetQd = new QueueDescription(TargetQueueName) { EnablePartitioning = true, RequiresSession = true }; 
namespaceManager.CreateQueue(targetQd); 
 
// Create partitioned transfer queue. 
QueueDescription transferQd = new QueueDescription(TransferQueueName) { EnablePartitioning = true }; 
namespaceManager.CreateQueue(transferQd); 
 
// Send message within a transaction. 
MessageSender sender = mf.CreateMessageSender(transferDestinationEntityPath: targetQd.Path, viaEntityPath: transferQd.Path); 
CommittableTransaction committableTransaction = new CommittableTransaction(); 
using (TransactionScope ts = new TransactionScope(committableTransaction)) 
{ 
    BrokeredMessage msg = new BrokeredMessage("This is a message"); 
    msg.SessionId = "MySessionId"; // Used as the partition key for the target queue. 
    msg.ViaPartitionKey = "MyViaPartitionKey"; // Used as the partition key for the transfer queue. 
    sender.Send(msg); 
    ts.Complete(); 
} 
committableTransaction.Commit();
```
 
##More Information
For more information on partitioned entities, see http://msdn.microsoft.com/en-us/library/dn520246.aspx.