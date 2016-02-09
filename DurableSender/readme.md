#Introduction
This sample demonstrates how an application that is temporarily disconnected from the network can continue to send messages to Service Bus. The durable message sender library stores all messages in a local MSMQ queue until connectivity is restored. At the same time, the library allows the application to send message to Service Bus as part of a distributed transaction.
##Building the Sample
Install Azure SDK 2.0 or later.
Create a Service Bus namespace.
Build the sample. Open the solution in Visual Studio 2010 or later, add your namespace name and SAS key, and press F6.
To demonstrate the use of DTC, assign a valid SQL connection string to private const string SqlConnectionString in file Client.cs.
To disable the fault injector, set const bool enableFaultInjection to false.
Run the client. When prompted, supply the namespace name, issuer name and issuer key of your Service Bus namespace.
##Description
To allow an application to send brokered messages to a Service Bus queue or topic in the absence of network connectivity, these messages need to be stored locally and be transmitted to Service Bus in the background after connectivity has been restored. This functionality is implemented by the durable message sender library. Just as it calls methods of the Service Bus client library, the application calls the Send() method of the durable message sender library.
```C#
// Create a MessagingFactory. 
MessagingFactory messagingFactory = MessagingFactory.Create(namespaceUri, tokenProvider); 
 
// Create a durable sender. 
DurableMessageSender durableMessageSender = new DurableMessageSender(messagingFactory, SbusQueueName); 
 
// Send message. 
BrokeredMessage msg = new BrokeredMessage("This is a message."); 
 
durableMessageSender.Send(msg);
 ```
 
The durable message sender library enqueues all of the application’s messages into a local transactional MSMQ queue. In the background, the durable message sender library reads these messages from the MSMQ queue and sends them to the Service Bus queue or topic. The durable message sender library maintains one MSMQ queue per Service Bus queue or topic that the application wants to send to.

```C#
public void Send(BrokeredMessage sbusMessage) 
{ 
    Message msmqMessage = MsmqHelper.PackSbusMessageIntoMsmqMessage(sbusMessage); 
    SendtoMsmq(this.msmqQueue, msmqMessage); 
} 
 
void SendtoMsmq(MessageQueue msmqQueue, Message msmqMessage) 
{ 
    if (Transaction.Current == null) 
    { 
        msmqQueue.Send(msmqMessage, MessageQueueTransactionType.Single); 
    } 
    else 
    { 
        msmqQueue.Send(msmqMessage, MessageQueueTransactionType.Automatic); 
    } 
} 
``` 
 

If the durable message sender library experiences a temporary failure when sending a message to Service Bus, the durable message sender library waits some time and then tries again. The wait time increases exponentially with every failure. The maximum wait time is 60 seconds. After a successful transmission to Service Bus, the wait time is reset to its initial value of 50ms.
If the durable message sender library experiences a permanent failure when sending a message to Service Bus, the message is moved to a MSMQ deadletter queue.

```C#
Message msmqMessage = null; 
try 
{ 
    msmqMessage = this.msmqQueue.EndPeek(result); 
} 
catch (MessageQueueException ex) 
{ 
    if (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout) 
    { 
        MsmqPeekBegin(); 
        return; 
    } 
} 
 
if (msmqMessage != null) 
{ 
    BrokeredMessage sbusMessage = MsmqHelper.UnpackSbusMessageFromMsmqMessage(msmqMessage); 
    // Clone Service Bus message in case we need to deadletter it. 
    BrokeredMessage sbusDeadletterMessage = CloneBrokeredMessage(sbusMessage); 
 
    switch (SendMessageToServiceBus(sbusMessage)) 
    { 
        case SendResult.Success: // Message was successfully sent to Service Bus. Remove MSMQ message from MSMQ queue. 
            this.msmqQueue.BeginReceive(TimeSpan.FromSeconds(60), null, MsmqOnReceiveComplete); 
            break; 
        case SendResult.WaitAndRetry: // Service Bus is temporarily unavailable. Wait. 
            waitAfterErrorTimer = new Timer(ResumeSendingMessagesToServiceBus, null, timerWaitTimeInMilliseconds, Timeout.Infinite); 
            break; 
        case SendResult.PermanentFailure: // Permanent error. Deadletter MSMQ message. 
            DeadletterMessage(this.clonedMessage); 
            this.msmqQueue.BeginReceive(TimeSpan.FromSeconds(60), null, MsmqOnReceiveComplete); 
            break; 
        } 
    } 
}
``` 
  

Unlike sending messages directly to Service Bus, sending messages to a transactional MSMQ queue can be done as part of a distributed transaction. As such, the durable message sender library allows an application to send messages to a Service Bus queue or topic as part of a regular or a distributed transaction.
The durable message sender library maintains message ordering. This means that the durable message sender library sends messages to Service Bus in the same order with which the application sends the messages to the durable message sender library. The durable message sender library maintains ordering in the presence of temporary failures. In order to avoid message duplication, the Service Bus queue or topic has to have duplicate detection enabled (QueueDescription.RequiresDuplicateDetection = true;).
Note that the durable message sender library does not honor transactional guarantees of message batches. If the application sends multiple messages to the durable message sender library within a single transaction, and then Service Bus returns a permanent error the durable message sender library sends one of these messages to Service Bus, then this message will not be enqueued into the Service Bus queue or topic whereas the other messages will.
Also note that messages don’t expire while they are stored in the MSMQ queue. This implementation of the durable message sender library sets the TimeToBeReceived property of the MSMQ message to infinite.
##Source Code Files
Client.cs: Implements a Service Bus client that sends and receives messages to Service Bus using the durable sender library.
DurableMessageSender.cs: Implements the durable message sender API. Implements code that converts Service Bus brokered messages to and from MSMQ messages and sends and receives MSMQ messages.
MsmqHelper.cs: Implements queue management and message conversion methods.
FaultInjector.cs: Injects various faults that simulate transient and permanent Service Bus faults.
##More Information
For more information on Service Bus, see http://msdn.microsoft.com/en-us/library/windowsazure/jj656647.aspx.