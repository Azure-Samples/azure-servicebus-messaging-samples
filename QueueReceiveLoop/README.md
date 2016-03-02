#Receive Loops

This sample is a variation of the [QueuesGettingStarted](../QueuesGettingStarted) sample. This sample does not use the ```OnMessage``` API,
but rather implemens an explicit receive loop.  

## Prerequisites and Setup

All samples share the same basic setup, explained in the main [README](../README.md) file. There are no extra setup steps for this sample.
The application entry points are in [Main.cs](../common/Main.md), which is shared across all samples. The sample implementations generally 
reside in *Program.cs*, starting with Run().    

You can build the sample from the command line with the [build.bat](build.bat) or [build.ps1](build.ps1) scripts. This assumes that you 
have the .NET Build tools in the path. You can also open up the [ReceiveLoop.sln](ReceiveLoop.sln) solution file with Visual Studio and build.
With either option, the NuGet package manager should download and install the **WindowsAzure.ServiceBus** package containing the 
Microsoft.ServiceBus.dll assembly, including dependencies.

## The Program

To keep things reasonably simple, the sample program keeps message sender and message receiver code within a single hosting application,
even though these roles are often spread across applications, services, or at least across independently deployed and run tiers of applications
or services. For clarity, the send and receive activities are kept as separate as if they were different apps and share no API object instances.

### Sending Messages          

Sending messages is identical to the [QueuesGettingStarted](../QueuesGettingStarted/README.md) sample and discussed there. 
     
## Receiving Messages

The setup of the ```MessagingFactory``` and ```MessageReceiver``` also echoes the base sample.

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
       var receiver = await receiverFactory.CreateMessageReceiverAsync(queueName, ReceiveMode.PeekLock);
```  

With the receiver set up, we then enter into a simple receive loop that terminates when the queue is empty (and stays empty for at least 5 seconds).
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

Invoking the Receive or ReceiveAsync operation with a timeout argument will return ```null``` when the timeout expires without a
message being available for retrieval. You can force the operation to return immediately when there is no message in the entity 
by passing ```TimeSpan.Zero```, but that should only be done in rare cases, such as during controlled application shutdown, as it causes 
excessive network traffic when used in a loop. 

We use a 5 seconds wait to have the sample exit cleanly once the sample messages have been consumed and to show the timeout behavior.
  
**For a production receive loop**, the more common strategy will be to call ```Receive```/```ReceiveAsync``` without a timeout 
argument, ([also see the Alternate Loop section below](#alternate-loop)).

If we have obtained a valid message, we'll first check whether it is a message that we can handle. For this example, we check 
the ```Label``` and ```ContentType``` properties for whether they contain the expected values indicating that we can successfully 
decode and process the message body. If they do, we acquire the body stream and deserialize it just like in the base sample; 
the entire message handling is identical to what you would do inside an ```OnMessage``` callback:      

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
                    await message.CompleteAsync();
                }
                else
                {
                    await message.DeadLetterAsync("ProcessingError", "Don't know what to do with this message");
                }
            }
``` 

If the message has come back as ```null```, there is no further message available and we break out of the loop.
 
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
to the loop (on a different thread) will terminate the loop by calling ```Close``` on the ```MessageReceiver``` instance ```receiver```. Closing the receiver
will cause the pending receive to return ```null``` and calling ```ReceiveAsync``` again will fail out with an ```OperationCanceledException``` that will 
also terminate the loop.   

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
