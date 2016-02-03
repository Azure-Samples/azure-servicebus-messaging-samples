#Getting Started with Service Bus Queues

This sample shows how to interact with the essential API elemnts for interacting with a Service Bus Queue.
You will learn how to establish a connection, and to send and receive messages.  The sample also 
shows the most important properties of Service Bus messages. 

## Prerequisites and Setup

All samples share the same basic setup, explained in the main [README](../README.md) file. There are no extra setup steps for this sample.
The application entry points are in [Main.cs](../common/Main.md), which is shared across all samples. The sample implementations generally 
reside in *Program.cs*, starting with Run().    

You can build the sample from the command line with the [build.bat](build.bat) or [build.ps1](build.ps1) scripts. This assumes that you 
have the .NET Build tools in the path. You can also open up the [QueuesGettingStarted.sln](QueuesGettingStarted.sln) solution file with Visual Studio and build.
With either option, the NuGet package manager should download and install the **WindowsAzure.ServiceBus** package containing the 
Microsoft.ServiceBus.dll assembly, including dependencies.

## The Program

To keep things reasonably simple, the sample program keeps message sender and message receiver code within a single hosting application,
even though these roles are often spread across applications, services, or at least across independently deployed and run tiers of applications
or services. For clarity, the send and receive activities are kept as separate as if they were different apps and share no API object instances.

### Sending Messages          

Sending messages requires a connection to Service Bus, which is managed by a *MessagingFactory*. The *MessagingFactory* serves as an anchor for connection 
management and as a factory for the various client objects that can interact with Service Bus entities. Connections to Service Bus are established 
"just in time" as soon as required (for instance when the first send or receive operation is initiated) and the connection is shared across all 
client objects created from the same *MessagingFactory*, each having a separate link inside that connection. When the *MessagingFactory* is closed or
aborted, all client operations across all client objects are aborted as well.

For the send operation, we create a new MessagingFactory and pass the namespace base address (typically *sb://{namespace-name}.servicebus.windows.net*)
and a set of *MessagingFactorySettings*. The settings object is configured with the transport protocol type (AMQP 1.0) and with a token provider object 
that wraps the SAS send token passed to the sample by the [entry point](../common/Main.md).   

Once the factory is constructed, we set the *RetryPolicy* property to a new instance of the *RetryExponential* policy class that implements an 
exporantial backoff strategy for retries. When the policy property is set, all client objects created through this factory will be initialized with
the given policy and will automatically perform retries following the rules of the policy when transient errors occur.   

```C#
async Task SendMessagesAsync(string namespaceAddress, string queueName, string sendToken)
{
    var senderFactory = MessagingFactory.Create(
        namespaceAddress,
        new MessagingFactorySettings
        {
            TransportType = TransportType.Amqp,
            TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken)
        });
    senderFactory.RetryPolicy = new RetryExponential(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), 10);
```

From the factory we next create a *MessageSender*, which can send messages to Queues and Topics. We could also create a *QueueClient*, 
which is the client object specialized for Queues, but the generic message sender is the more flexible option.
  
     
```C#
    var sender = await senderFactory.CreateMessageSenderAsync(queueName);
```

With the message sender in hands, we proceed to create a few messages (snippet below is abridged) and send them. 

In this example we use the Newtonsoft JSON.NET serializer to turn a dynamic object into JSON format, then encode the resulting 
text as UTF-8, and pass the resulting byte stream into the body of the message. We then set the message's ContentType property 
to "application/json" to inform the receiver of the message body format.

We could also pass a serializable .NET object (marked as [Serializable] or [DataContract]) as the message body object to the 
*BrokeredMessage* constructor here. When sending with AMQP as we do here, the object would be serialized in AMQP encoding by default. 
When sending with the NetMessaging transport type, the object would be serialized with the binary .NET data contract serializer. 
A further overload of the *BrokeredMessage* constructor lets you pass an XmlObjectSerializer of your own choice.

The example also sets 

* the *Label* property, which gives the receiver a hint about the purpose of the message and allows for 
  dispatching to a handler method without first touching the message body.        
* the *MessageId* property, which uniquely identifies this particular message and enables features like correlation 
  and duplicate detection.       
* the *TimeToLive* property, which causes the message to expire and be automatically garbage collected from the Queue
  when expired. We set this here so that we don't accumulate many stale messages in the demo queue as you experiment. 

```C#
    dynamic data = new[]
    {
        new {name = "Einstein", firstName = "Albert"},
        ...
        new {name = "Kopernikus", firstName = "Nikolaus"}
    };


    for (int i = 0; i < data.Length; i++)
    {
        var message = new BrokeredMessage(new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data[i]))))
        {
            ContentType = "application/json",
            Label = "Scientist",
            MessageId = i.ToString(),
            TimeToLive = TimeSpan.FromMinutes(2)
        };
```

And once we have composed a message, we send it into the queue. With the *RetryPolicy* we've set on the *MessagingFactory*, 
the asynchronous send operation will automatically retry the send operation should transient errors occur. We therefore 
deliberately don't wrap this operation into a try/catch block here as we would do in a production application that might 
then surface an explicit error message into the log or display it to the user as all retries have been exhausted.    

```C#
        await sender.SendAsync(message);
    }
```     
     
## Receiving Messages

The message receiver side also requires a *MessagingFactory* that we construct just like for the sender. The only difference,
not even really apparent, is that we pass a token that confers receive ("Listen") permission on the Queue. Everything else, 
except names, is teh same as on the send side. 

```C#
async Task ReceiveMessagesAsync(string namespaceAddress, string queueName, string receiveToken)
    {
        var receiverFactory = MessagingFactory.Create(
            namespaceAddress,
            new MessagingFactorySettings
            {
                TransportType = TransportType.Amqp,
                TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(receiveToken)
            });
        receiverFactory.RetryPolicy = new RetryExponential(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), 10);
``` 
     
As you might expect, we'll now create a receiver object. Like with send, we could use the *QueueClient*, but we create a generic
*MessageReceiver* that could also receive from a Topic Subscription when given the correct path. Note that the reciver is created 
with the *PeekLock* receive mode. This mode will pass the message to the receiver while the broker maintains a lock on 
the message and hold on to the message. If the message has not been completed, deferred, deadlettered, or abandoned during the
lock timeout period, the message will again appear in the queue (or the Topic Subscription) for retrieval. This is different 
from the *ReceiveAndDelete* alternative where the message has been deleted as it arrives at the receiver. In this example, the 
message is either completed or deadlettered as you will see further below.    

```C#
       var receiver = await receiverFactory.CreateMessageReceiverAsync(queueName, ReceiveMode.PeekLock);
```  

With the receiver set up, we then enter into a simple receive loop that terminates when the queue is empty (and stay empty for at least 5 seconds).
The loop will go on "forever" until we break out of it, which is a common pattern for message receive loops. We could also make continuation of the 
loop dependent on a termination flag or cancellation token.  

``` C#
    while (true)
    {
        try
        {
            //receive messages from Queue
            var message = await receiver.ReceiveAsync(TimeSpan.FromSeconds(5));
```

Invoking the Receive or ReceiveAsync operation with a timeout argument will return **null** when the timeout expires without a
message being available for retrieval. You can force the operation to return immediately when there is no message in the entity 
by passing TimeSpan.Zero, but that should only be done in rare cases, such as during controlled application shutdown, as it causes 
excessive network traffic when used in a loop. 

We use 5 seconds here to have the sample exit cleanly once the sample messages have been consumed and to show the timeout behavior.
  
For a production receive loop, the more common strategy will be to call *Receive*/*ReceiveAsync* without a timeout 
argument, ([see Alternate Loop section](#alternate-loop)).

If we have obntained a valid message, we'll first check whether it is a message that we can handle. For this example, we check 
the Label and ContentType properties for whether they contain the expected values indiocating that we can successfully 
decode and process the message body. If they do, we acquire the body stream and deserialize it:      

``` C#            
            if (message != null)
            {
                if (message.Label != null &&
                    message.ContentType != null &&
                    message.Label.Equals("Scientist", StringComparison.InvariantCultureIgnoreCase) &&
                    message.ContentType.Equals("application/json", StringComparison.InvariantCultureIgnoreCase))
                {
                    var body = message.GetBody<Stream>();

                    dynamic scientist = JsonConvert.DeserializeObject(new StreamReader(body, true).ReadToEnd());
```

Instead of processing the message, the sample code writes out the message properties to the console. Of particular interest are 
those propertzies that the broker sets or modifies as the message passes through:

* the *SequenceNumnber* property is a monotonically increasing and gapless sequence number assigned to each message 
  as it is processed by the broker. The sequence number is authoritiative for determining order of arrival. For partitioned
  entities, the lower 48 bits hold the per-partition sequence number, the upper 16 bits hold the partition number.           
* the *EnqueuedTimeUtc* property reflects the time at which the message has been committed by the processing 
  broker node. There may be clock skew from UTC and also between different broker nodes. If you need to determine order 
  of arrival refer to the SequenceNumber.           
* the *Size* property holds the size of the message body, in bytes.
* the *ExpiredAtUtc* property holds the absolute instant at which this message will expire (EnqueuedTimeUtc+TimeToLive)  

 ``` C#
                    lock (Console.Out)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine(
                            "\t\t\t\tMessage received: \n\t\t\t\t\t\tMessageId = {0}, \n\t\t\t\t\t\tSequenceNumber = {1}, \n\t\t\t\t\t\tEnqueuedTimeUtc = {2}," +
                            "\n\t\t\t\t\t\tExpiresAtUtc = {5}, \n\t\t\t\t\t\tContentType = \"{3}\", \n\t\t\t\t\t\tSize = {4},  \n\t\t\t\t\t\tContent: [ firstName = {6}, name = {7} ]",
                            message.MessageId,
                            message.SequenceNumber,
                            message.EnqueuedTimeUtc,
                            message.ContentType,
                            message.Size,
                            message.ExpiresAtUtc,
                            scientist.firstName,
                            scientist.name);
                        Console.ResetColor();
                    }
```
Now that we're done with "processing" the message, we tell the broker about that being the case. The *Complete(Async)* 
operation will settle the message transfer with the broker and remove it from the broker. If the message does not 
meet our processing criteria, we will deadletter it, meaning it is put into a special queue for handling defective
messages. The broker will automatically deadletter the message if delivery has been attempted too many times. 
You can find out more about this in the [Deadletter](../Deadletter) sample.

``` C#                    
                    await message.CompleteAsync();
                }
                else
                {
                    await message.DeadLetterAsync("ProcessingError", "Don't know what to do with this message");
                }
            }
``` 

If the message has come back as **null** we break out of the loop.
 
```C#            
            else
            {
                //no more messages in the queue
                break;
            }
        }
```

And finally, when any kind of messaging exception occurs and that exception is not transient, meaning things 
will not get better if we retry the operation, then we "log" and rethrow for external handling. Otherwise we'll 
absorb the exception (you might want to log it for monitoring purposes) and keep going.   

```C#        
        catch (MessagingException e)
        {
            if (!e.IsTransient)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }
    }
``` 

###Alternate Loop

A receive loop with a receive operation that is not time-boxed will look like the snippet below. The assumption here is that an operation external 
to the loop (on a different thread) will terminate the loop by calling *Close* on the *MessageReceiver* instance *receiver*. Closing the receiver
will cause the pending receive to return **null** and calling *ReceiveAsync* again will fail out with an OperationCanceledException that will 
terminate the loop.   

``` C#
    while (true)
    {
        try
        {
            //receive messages from Queue
            var message = await receiver.ReceiveAsync();
            if ( message == null ) break;
            
            ...
            await message.CompleteAsync();
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (MessagingException e)
        {
            if (!e.IsTransient)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }
    }
``` 
     
##Running the sample

You can run the application from Visual Studio or on the command line from the sample's root directory by starting <code>bin/debug/sample.exe</code>
