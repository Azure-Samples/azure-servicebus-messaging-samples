#Getting Started with Service Bus Topics

This sample shows how to interact with the essential API elements for interacting with a Service Bus Topic.

Good news: The sample is nearly identical to the [QueuesGettingStarted](../QueuesGettingStarted) sample since 
the API gestures for interacting with queues and topics are indeed the same ones. In this document we will 
therefore focus on the few differences between the samples. 

## Prerequisites and Setup

All samples share the same basic setup, explained in the main [README](../README.md) file. There are no extra setup steps for this sample.
The application entry points are in [Main.cs](../common/Main.md), which is shared across all samples. The sample implementations generally
reside in *Program.cs*, starting with *Run()*.

You can build the sample from the command line with the [build.bat](build.bat) or [build.ps1](build.ps1) scripts. This assumes that you
have the .NET Build tools in the path. You can also open up the [QueuesGettingStarted.sln](QueuesGettingStarted.sln) solution file with Visual Studio and build.
With either option, the NuGet package manager should download and install the **WindowsAzure.ServiceBus** package containing the
Microsoft.ServiceBus.dll assembly, including dependencies.

## What is a Topic?

Topics very similar to queues. A topic has one input, exactly like a queue, and it has zero or more named, user-configurable, durably created, 
service-side outputs, called *subscriptions*, which each act like independent queues. 

Conceptually, every existing subscription receives a copy of each message that is sent into the topic, so that each subscriber can independently 
consume the complete message stream. Whether a message is selected into the subscription is determined by a filter condition; the default filter 
condition allows any message. Filters are further illustrated in the [TopicFilters](../TopicFilters) sample.      

> The factual implementation is more efficient than this conceptual idea. The message bodies are stored just once and only a subset of the 
> message properties are copied for each subscription. If you are sending many messges with larger messages bodies, each of those
> therefore only counts once towards the topic's size quota.

## The Program

The send-side of the sample is identical to the [QueuesGettingStarted](../QueuesGettingStarted) sample and therefore shows that
queues and topics can be used interchangeably, and that an application's messaging topology can indeed be adjusted as needed 
while limiting or avoiding code churn.   

The *only* difference in the sender portion of this sample is that we're passing the name of a topic instead the name of a queue:

```C#
    var sender = await senderFactory.CreateMessageSenderAsync(topicName);
```

The receive side is also nearly identical. The *Run()* function passes the name of the subscription in addition to the topic name, 
and also gets to pass a different console color option for displaying the messages received on that subscription. 

´´´ C#

    async Task ReceiveMessagesAsync(string namespaceAddress, string topicName, string subscriptionName, string receiveToken, 
                                    CancellationToken cancellationToken, ConsoleColor color)
    {
        ... create factory ...

```

The only truly noteworthy difference is that we are constructing the *MessageReceiver* not over the path of the main entity 
as we do with queues, but first format a path to the subscription from the topic and subscription names, and then construct 
the *MessageReceiver* using the composite path. For the *MessageReceiver*, that path is completely interchangeable with any 
queue's path.

The static helper method SubscriptionClient.FormatSubscriptionPath returns a path of the form ```{topic-name}/Subscription/{subscription-name}```

``` C#
>       var subscriptionPath = SubscriptionClient.FormatSubscriptionPath(topicName, subscriptionName);
        var receiver = await receiverFactory.CreateMessageReceiverAsync(subscriptionPath, ReceiveMode.PeekLock);
```

In an application you would commonly not use the above two lines together. If you want to retain flexibility for 
your application's messaging topology, you will manage the path from which you receive messages for a component 
or service separately, likely in configuration. 
   
You can obviously also easily create a *SubscriptionClient* through the *MessagingFactory* as follows and in a 
single line:

```C#
    var receiver = receiverFactory.CreateSubscriptionClient(topicName, subscriptionName);
``` 

The *SubscriptionClient* class differs from the regular receiver in that it has specific support for managing 
subscription rules at runtime. More on this in the [TopicFilters](../TopicFilters) sample. 

## Run()

The Run() method that is invoked by the common sample entrypoint first sends a few messages and kicks off the receivers for 
three subscriptions in parallel. The messages received from the subscriptions will differ in color depending on which
subscription tzhey were received from. 

 The cancellation token passed to the receiver method is being triggered when the 
user presses any key sometime after sender and receiver have been kicked off. 

```C#
    public async Task Run(string namespaceAddress, string topicName, string sendToken, string receiveToken)
    {
        var cts = new CancellationTokenSource();

        await this.SendMessagesAsync(namespaceAddress, topicName, sendToken);

        var allReceives = Task.WhenAll(
            this.ReceiveMessagesAsync(namespaceAddress, topicName, "Subscription1", receiveToken, cts.Token, ConsoleColor.Cyan),
            this.ReceiveMessagesAsync(namespaceAddress, topicName, "Subscription2", receiveToken, cts.Token, ConsoleColor.Green),
            this.ReceiveMessagesAsync(namespaceAddress, topicName, "Subscription3", receiveToken, cts.Token, ConsoleColor.Yellow));
        Console.WriteLine("\nEnd of scenario, press any key to exit.");
        Console.ReadKey();

        cts.Cancel();
        await allReceives;
    }
```

##Running the sample

You can run the application from Visual Studio or on the command line from the sample's root directory by starting <code>bin/debug/sample.exe</code>
