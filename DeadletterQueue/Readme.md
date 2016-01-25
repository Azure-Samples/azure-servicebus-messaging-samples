
#Dead Letter Queue Sample
This sample demonstrates how to use the Service Bus and the messaging "dead letter queue" functionality.

The sample shows a simple sender and receiver communicating via a Service Bus queue. Both sender and receiver prompt for service namespace credentials. (These are used to authenticate with the Access Control service, and acquire an access token that proves to the Service Bus insfrastructure that the client is authorized to access the queue.) The sender creates the queue, and sends messages simulating different orders into it. The receiver reads orders until the queue is empty, simulating failure on processing some messages. The failing messages are dead-lettered. At the end of the samples, the dead-lettered messages are received and logged.

It is also possible to create a separate receiver application for reading the messages in the dead letter queue, and performing additional actions for each message (such as updating order types to include these unknown orders).

 
Note: Dead-lettering also applies to topics and subscriptions, where each subscription has its own dead letter subqueue. It can be accessed in a similar way to a subscription's dead letter subqueue: subscriptionClient.CreateReceiver("$DeadLetterQueue").
Prerequisites

If you haven't already done so, please read the release notes document that explains how to sign up for a account and how to configure your environment.

##Sender

The sender's flow:

Obtains user credentials and creates a NamespaceManager (namespaceClient) and a MessagingFactory (messagingFactory). These entities hold the credentials and are used for all messaging management and runtime operations.
Creates queue using the namespaceClient
Sends messages to queue
Waits for user input to delete queue
 
Note: The static ServiceBusEnvironment.CreateServiceUri function is provided to help construct the URI with the correct format and domain name. It is strongly recommended that you use this function instead of building the URI from scratch because the URI construction logic and format might change in future releases.
##Receiver

The receiver's flow:

Gets user credentials, but only creates a MessagingFactory and a QueueClient (for runtime operations), since the queue was created by the sender
Receives messages from the queue and processes them.
Processing simulates an error by failing to process random messages for MaxRetryCount times. Once a message cannot be processed MaxRetryCount times, the message is dead-lettered.
Reads and logs messages from the dead-letter queue (separate dead-letter queue message receiver created)
 
Note: Messages can only be dead-lettered if they have been received using the PeekLock receive mode. In comparison to the simpler ReceiveAndDeleted mode (message deleted on receiving), the PeekLock mode requires a message to either be completed (Complete()) to take the message out of the queue. If a PeekLock receiver calls Abandon() on a message, or does not Abandon() or Complete() with the message lock timeout, the message will be made available on the queue, for processing by any receiver.
Running the Sample

To run the sample, build the solution in Visual Studio or from the command line, then run the two resulting executable files. Start the sender first, then start the receiver. Once the receiver has completed, close the sender to clean up the messaging entities. Both programs prompt for your AppFabric service namespace and the issuer credentials. For the issuer secret, be sure to use the "Default Key" value from the AppFabric portal, rather than one of the management keys.

Note that the expected output below is a sample only - it may not exactly match your run because the sample randomly decides which messages should be dead-lettered.

Expected Output - Sender

                         Please provide the namespace to use:
                        <service namespace>
                        Please provide the Issuer name to use:
                        <issuer name>
                        Please provide the Issuer key to use:
                        <issuer secret>
                        Creating queue 'OrdersService'...
                        Sending messages to queue...
                        Sending message of order type DeliveryOrder.
                        Sending message of order type StayInOrder.
                        Sending message of order type TakeOutOrder.
                        Sending message of order type TakeOutOrder.
                        Sending message of order type DeliveryOrder.

                        Press [Enter] to delete queue and exit.
                    
Expected Output - Receiver

                         Please provide the namespace to use:
                        <service namespace>
                        Please provide the Issuer name to use:
                        <issuer name>
                        Please provide the Issuer key to use:
                        <issuer secret>
                        Reading messages from queue...
                        Adding Order 1 with 10 number of items and 15 total to DeadLetter queue
                        Received Order 2 with 15 number of items and 500 total
                        Adding Order 3 with 1 number of items and 25 total to DeadLetter queue
                        Adding Order 5 with 3 number of items and 25 total to DeadLetter queue
                        Received Order 4 with 100 number of items and 100000 total

                        No more messages left in queue. Logging dead lettered messages...
                        Order 1 with 10 number of items and 15 total logged from DeadLetter queue. DeadLettering Reason is "UnableToProcess" and
                         Deadlettering error description is "Failed to process in reasonable attempts"
                        Order 3 with 1 number of items and 25 total logged from DeadLetter queue. DeadLettering Reason is "UnableToProcess" and
                        Deadlettering error description is "Failed to process in reasonable attempts"
                        Order 5 with 3 number of items and 25 total logged from DeadLetter queue. DeadLettering Reason is "UnableToProcess" and
                        Deadlettering error description is "Failed to process in reasonable attempts"

                        Press [Enter] to exit.
                    
Did you find this information useful? Please send your suggestions and comments about the documentation.