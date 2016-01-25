
#Deferred Messages Sample
This sample demonstrates how to use the message deferral feature of the Windows Azure Service Bus.

The sample shows a simple sender and receiver communicating via a Service Bus queue. Both sender and receiver prompt for service namespace credentials. These are used to authenticate with the Access Control service, and acquire an access token that proves to the Service Bus insfrastructure that the client is authorized to access the queue. The sender creates the queue, and sends messages of different priorities into it. The receiver reads until the queue is empty, immediately processing the high-priority messages, and deferring the low-priority messages. The receiver processes the low-priority messages once the queue is empty and all high-priority messages have been taken care of.

Message deferral capability is also available for messages received from a subscription. A receiver on a subscription can defer messages in exactly the same way as it would for a queue, and can similarly retrieve messages by message receipt.

##Prerequisites

If you haven't already done so, please read the release notes document that explains how to sign up for a Windows Azure account and how to configure your environment.

##Sender

The sender obtains user credentials and creates a ServiceBusNamespaceClient (namespaceClient). This entity holds the credentials and is used for all messaging management operations - in this case, to create a durable queue with a well-known name for communication with the receiver.

C# 
                        private static TransportClientCredentialBase GetUserCredentials()
                        {
                            Console.Write("Your Service Namespace: ");
                            serviceNamespace = Console.ReadLine();
                            Console.Write("Your Issuer Name: ");
                            issuerName = Console.ReadLine();
                            Console.Write("Your Issuer Secret: ");
                            issuerKey = Console.ReadLine();
                            Console.WriteLine();

                            return TransportClientCredentialBase.CreateSharedSecretCredential(
                                issuerName, issuerKey);
                        }

                        private static Queue CreateQueue(TransportClientCredentialBase credentials)
                        {
                            Uri managementUri = ServiceBusEnvironment.CreateServiceUri("sb", serviceNamespace, string.Empty);
                            namespaceClient = new ServiceBusNamespaceClient(managementUri, credentials);
                            
                            Console.WriteLine("Creating queue \"OrdersQueue\".");
                            return namespaceClient.CreateQueue("OrdersQueue");
                        }
                      
The preceding code prompts for the issuer credential and then constructs the listening URI using that information. The static ServiceBusEnvironment.CreateServiceUri function is provided to help construct the URI with the correct format and domain name. It is strongly recommended that you use this function instead of building the URI from scratch because the URI construction logic and format might change in future releases.

The sender then creates a QueueClient, an entity used to create senders/receivers on a queue:
C# 
                        private static QueueClient CreateQueueClient(Queue q, TransportClientCredentialBase credentials)
                        {
                            Uri runtimeUri = ServiceBusEnvironment.CreateServiceUri("sb", serviceNamespace, string.Empty);
                            messagingFactory = MessagingFactory.Create(runtimeUri, credentials);

                            return messagingFactory.CreateQueueClient(q);
                        }
                      
The sender opens a MessageSender using the QueueClient, generates a few messages of different priorities, and sends them into the queue. The sender waits for user input to close, and deletes the queue (queue messages automatically deleted) to clean up.

C# 
                      public static void Main()
                      {
                        ...

                        // Send messages to queue:
                        Console.WriteLine("Sending messages to queue...");
                        using (MessageSender sender = queueClient.CreateSender())
                        {
                            BrokeredMessage message1 = CreateOrderMessage("High");
                            sender.Send(message1);
                            Console.WriteLine("Sent message {0} with high priority.", message1.MessageId);

                            BrokeredMessage message2 = CreateOrderMessage("Low");
                            sender.Send(message2);
                            Console.WriteLine("Sent message {0} with low priority.", message2.MessageId);

                            BrokeredMessage message3 = CreateOrderMessage("High");
                            sender.Send(message3);
                            Console.WriteLine("Sent message {0} with high priority.", message3.MessageId);
                        }

                        Console.WriteLine();
                        Console.WriteLine("Press [Enter] to delete queue and exit.");
                        Console.ReadLine();

                        // Cleanup:
                        messagingFactory.Close();
                        namespaceClient.DeleteQueue(queue.Path);
                      }

                      private static BrokeredMessage CreateOrderMessage(string priority)
                      {
                        BrokeredMessage message = BrokeredMessage.CreateMessage();
                        message.MessageId = "Order" + Guid.NewGuid().ToString();
                        message.Properties.Add("Priority", priority);
                        return message;
                      }
                    
##Receiver

The receiver also prompts for credentials. Since the queue was created by the sender, the receiver only needs the runtime QueueClient in order to create queue receivers:

C# 
                        private static TransportClientCredentialBase GetUserCredentials()
                        {
                            Console.Write("Your Service Namespace: ");
                            serviceNamespace = Console.ReadLine();
                            Console.Write("Your Issuer Name: ");
                            issuerName = Console.ReadLine();
                            Console.Write("Your Issuer Secret: ");
                            issuerKey = Console.ReadLine();
                            Console.WriteLine();

                            return TransportClientCredentialBase.CreateSharedSecretCredential(issuerName, issuerKey);
                        }

                        // Create the runtime entities (queue client)
                        private static QueueClient CreateQueueClient(string queueName, TransportClientCredentialBase credentials)
                        {
                            Uri runtimeUri = ServiceBusEnvironment.CreateServiceUri("sb", serviceNamespace, string.Empty);
                            MessagingFactory messagingFactory = MessagingFactory.Create(runtimeUri, credentials);

                            return messagingFactory.CreateQueueClient(queueName);
                        }
                      
The receiver opens a MessageReceiver on the queue and keeps on receiving until the queue is empty. Any high-priority messages are immeditately processed. Low-priority messages are deferred, and their message receipts are tracked. (NOTE: deferred messages can only be retrieved by message receipt, so it is important to keep track of those receipts.) Once the queue is empty and all high-priority messages have been processed, the receiver returns to the deferred messages, retrieves them by message receipt, and processes them.

C# 
                             public static void Main()
                            {
                                ...

                                // Read messages from queue until queue is empty:
                                Console.WriteLine("Reading messages from queue...");

                                MessageReceiver receiver = queueClient.CreateReceiver();
                                List<MessageReceipt> deferredMessageReceipts = new List<MessageReceipt>();

                                BrokeredMessage receivedMessage;
                                while (receiver.TryReceive(TimeSpan.FromSeconds(10), out receivedMessage))
                                {
                                    // Low-priority messages will be dealt with later:
                                    if (receivedMessage.Properties["Priority"].ToString() == "Low")
                                    {
                                        receivedMessage.Defer();
                                        Console.WriteLine("Deferred message with id {0}.", receivedMessage.MessageId);
                                        // Deferred messages can only be retrieved by message receipt. Here, keeping track of the
                                        // message receipt for a later retrieval:
                                        deferredMessageReceipts.Add(receivedMessage.MessageReceipt);
                                    }
                                    else
                                    {
                                        ProcessMessage(receivedMessage);
                                    }
                                }

                                Console.WriteLine();
                                Console.WriteLine("No more messages left in queue. Moving onto deferred messages...");

                                // Process the low-priority messages:
                                foreach (MessageReceipt receipt in deferredMessageReceipts)
                                {
                                    ProcessMessage(receiver.Receive(receipt));
                                }

                                Console.WriteLine();
                                Console.WriteLine("Press [Enter] to exit.");
                                Console.ReadLine();

                                receiver.Close();
                            }

                            private static void ProcessMessage(BrokeredMessage message)
                            {
                                Console.WriteLine("Processed {0}-priority order {1}.", message.Properties["Priority"], message.MessageId);
                                message.Complete();
                            }
                    
Running the Sample

To run the sample, build the solution in Visual Studio or from the command line, then run the two resulting executable files. Start the sender first, then start the receiver. Once the receiver has completed, close the sender to clean up the messaging entities. Both programs prompt for your Service Bus namespace and the issuer credentials. For the issuer secret, be sure to use the "Default Key" value from the Azure portal, rather than one of the management keys.

Expected Output - Sender

                         Please provide the namespace to use:
                       ...
                       Please provide the Issuer name to use:
                       ...
                       Please provide the Issuer key to use:
                       ...
                       Creating queue "OrdersQueue".
                       Sending messages to queue...
                       Sent message Order60f16940-ca49-4620-b93a-a770e1566c89 with high priority.
                       Sent message Order6f94cf34-c476-4489-acaf-a6924979f5ab with low priority.
                       Sent message Order0211ceea-b8ff-4bdc-927b-1e2a657401bd with high priority.

                       Press [Enter] to delete queue and exit.
                    
Expected Output - Receiver

                         Please provide the namespace to use:
                       ...
                       Please provide the Issuer name to use:
                       ...
                       Please provide the Issuer key to use:
                       ...
                       Reading messages from queue...
                       Processed High-priority order Order60f16940-ca49-4620-b93a-a770e1566c89.
                       Deferred message with id Order6f94cf34-c476-4489-acaf-a6924979f5ab.
                       Processed High-priority order Order0211ceea-b8ff-4bdc-927b-1e2a657401bd.

                       No more messages left in queue. Moving onto deferred messages...
                       Processed Low-priority order Order6f94cf34-c476-4489-acaf-a6924979f5ab.

                       Press [Enter] to exit.
                    
Did you find this information useful? Please send your suggestions and comments about the documentation.