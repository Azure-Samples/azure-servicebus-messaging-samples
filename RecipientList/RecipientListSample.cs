namespace Microsoft.ServiceBus.Samples.SimplePubSub
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows.Forms;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Description;
    using Microsoft.ServiceBus.Messaging;
    

    public class RecipientListSample
    {
        #region Fields

        static string ServiceBusNamespace;
        static string ServiceBusIssuerName;
        static string ServiceBusIssuerKey;

        static NamespaceManager namespaceManager;

        static int numCategories = 6;
        static int numMessages = 20;

        static string baseTopicName = "MyTopic";
        static string addressPropName = "Address";

        static ConsoleColor[] colors = new ConsoleColor[] { 
            ConsoleColor.Red, 
            ConsoleColor.Green, 
            ConsoleColor.Yellow, 
            ConsoleColor.Cyan,
            ConsoleColor.Magenta,
            ConsoleColor.Blue,             
            ConsoleColor.White};

        #endregion

        static void Main(string[] args)
        {
            // Setup:
            GetUserCredentials();

            // Create the Topic / Subscription entities 
            Uri managementUri = ServiceBusEnvironment.CreateServiceUri("sb", ServiceBusNamespace, string.Empty);

            namespaceManager = new NamespaceManager(
                managementUri,
                TokenProvider.CreateSharedSecretTokenProvider(ServiceBusIssuerName, ServiceBusIssuerKey));
            TopicDescription topicDescription = new TopicDescription(baseTopicName);

            // Delete the queue if already exists before creation. 
            if (namespaceManager.TopicExists(topicDescription.Path))
            {
                namespaceManager.DeleteTopic(topicDescription.Path);
            }

            Console.WriteLine("\nCreating Topic...");
            TopicDescription mainTopic = namespaceManager.CreateTopic(topicDescription);

            // this sub receives all messages
            AuditSub = namespaceManager.CreateSubscription(baseTopicName, "AuditSubscription");

            // thus sub receives messages addressed to First
            FirstSub = namespaceManager.CreateSubscription(baseTopicName, "FirstSubscription", 
	            new SqlFilter("Address LIKE '%First%'"));

            // thus sub receives messages addressed to Second
            SecondSub = namespaceManager.CreateSubscription(baseTopicName, "SecondSubscription", 
	            new SqlFilter("Address LIKE '%Second%'"));

            // Start senders and receivers:
            Console.WriteLine("\nLaunching senders and receivers...");

            Uri runtimeUri = ServiceBusEnvironment.CreateServiceUri("sb", ServiceBusNamespace, string.Empty);

            //send messages to topic            
            MessagingFactory messagingFactory = MessagingFactory.Create(
                runtimeUri,
                TokenProvider.CreateSharedSecretTokenProvider(ServiceBusIssuerName, ServiceBusIssuerKey));

            TopicClient topicClient = messagingFactory.CreateTopicClient(RecipientListSample.TopicName);

            Console.WriteLine("Preparing to send messages to {0}...", topicClient.Path);
        
            SendMessages(topicClient);

            // All messages sent
            Console.WriteLine("\nSender complete. Press ENTER");
            Console.ReadLine();
            
            // start receive
            for (int ctr = 0; ctr < 3; ctr++)
            {
                string subscriptionName = string.Empty;

                switch (ctr)
                {
                    case 0:
                        {
                            Console.Title = "Audit Subscription Receiver";
                            subscriptionName = "AuditSubscription";
                            break;
                        }
                    case 1:
                        {
                            Console.Title = "First Subscription Receiver";
                            subscriptionName = "FirstSubscription";
                            break;
                        }
                    case 2:
                        {
                            Console.Title = "Second Subscription Receiver";
                            subscriptionName = "SecondSubscription";
                            break;
                        }
                    default:
                        {
                            Console.Title = "Unknown";
                            break;
                        }
                }

                Console.WriteLine("Selecting {0}...", subscriptionName);
                SubscriptionClient subClient = messagingFactory.CreateSubscriptionClient(RecipientListSample.TopicName, subscriptionName, ReceiveMode.ReceiveAndDelete); 
                Console.WriteLine("Ready to receive messages from {0}...", subClient.Name);

                while (true)
                {
                    try
                    {
                        BrokeredMessage message = subClient.Receive(TimeSpan.FromSeconds(5));

                        if (message != null)
                        {
                            RecipientListSample.OutputMessageInfo("RECV: ", message);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (MessageNotFoundException)
                    {
                        Console.WriteLine("Got MessageNotFoundException, waiting for messages to be available");
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine("Got TimeoutException, no more messages available");
                        break;
                    }
                    catch (MessagingException e)
                    {
                        Console.WriteLine("Error: " + e.Message);
                    }

                }

                Console.WriteLine("\nReceiver complete. press ENTER");
                Console.ReadLine();
            }
            Console.WriteLine("\nPress [Enter] to exit.");
            Console.ReadLine();

            // Cleanup:
            namespaceManager.DeleteTopic(baseTopicName);
        }

        #region HelperFunctions
        static void GetUserCredentials()
        {
            // User namespace
            Console.WriteLine("Please provide the namespace to use:");
            ServiceBusNamespace = Console.ReadLine();


            // Issuer name
            Console.WriteLine("Please provide the Issuer name to use:");
            ServiceBusIssuerName = Console.ReadLine();
            
            // Issuer key
            Console.WriteLine("Please provide the Issuer key to use:");
            ServiceBusIssuerKey = Console.ReadLine();

        }

        static void SendMessages(TopicClient topicClient)
        {
            // Send messages to queue:
            Console.WriteLine("Sending messages to topic {0}", topicClient.Path);

            System.Random rand = new Random();
            for (int i = 0; i < RecipientListSample.NumMessages; ++i)
            {
                BrokeredMessage message = new BrokeredMessage();
                int categoryId = rand.Next(RecipientListSample.NumAddresses);
                message.Properties.Add(RecipientListSample.AddressPropName, categoryId == 1 ? "First" : (categoryId == 0) ? "First,Second" : "Second");
                message.MessageId = "Order_" + DateTime.Now.ToLongTimeString();
                try
                {
                    topicClient.Send(message);
                }
                catch (Exception)
                {
                    break;
                }
                RecipientListSample.OutputMessageInfo("SEND: ", message);
            }

            Console.WriteLine();
        }
        #endregion

        #region PublicHelpers
        // Public helper functions and accessors

        public static int NumAddresses
        {
            get { return numCategories; }
            set { numCategories = value; }
        }

        public static int NumMessages
        {
            get { return numMessages; }
            set { numMessages = value; }
        }

        public static string TopicName
        {
            get { return baseTopicName; }
            set { baseTopicName = value; }
        }

        public static SubscriptionDescription AuditSub
        { get; set; }

        public static SubscriptionDescription SecondSub
        { get; set; }

        public static SubscriptionDescription FirstSub
        { get; set; }

        public static string AddressPropName
        { get { return addressPropName; } }


        public static void OutputMessageInfo(string action, BrokeredMessage message, string additionalText = "")
        {
            if (message == null)
            {
                return;
            }
            object prop = message.Properties[addressPropName];
            if (prop != null)
            {
                Console.ForegroundColor = colors[int.Parse(prop.ToString().Equals("First") ? "1" : prop.ToString().Equals("Second") ? "2" : "0") % colors.Length];
                Console.WriteLine("{0}{1} - Address {2}. {3}", action, message.MessageId, message.Properties[addressPropName], additionalText);
                Console.ResetColor();
            }
        }
        #endregion
    }
}
