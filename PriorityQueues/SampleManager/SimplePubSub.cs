namespace Microsoft.ServiceBus.Samples.SimplePubSub
{
    using System;
    using System.Configuration;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
    

    public class SimplePubSub
    {
        #region Fields

        static string ServiceBusConnectionString;

        static NamespaceManager namespaceManager;

        static int numPriorities = 4
            ;
        static int numMessages = 20;

        static string baseTopicName = "MyTopic";
        static string priorityPropName = "Priority";

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
            namespaceManager = NamespaceManager.CreateFromConnectionString(ServiceBusConnectionString);
            TopicDescription topicDescription = new TopicDescription(baseTopicName);

            // Delete the queue if already exists before creation. 
            if (namespaceManager.TopicExists(topicDescription.Path))
            {
                namespaceManager.DeleteTopic(topicDescription.Path);
            }

            Console.WriteLine("\nCreating Topic...");
            TopicDescription mainTopic = namespaceManager.CreateTopic(topicDescription);

            // this sub recieves messages for Priority = 1
            RuleDescription rulePri1 = new RuleDescription(new SqlFilter(PriorityPropName + " = 1"));
            SubscriptionDescription pri1Sub = new SubscriptionDescription(TopicName, "Priority1Subscription");
            Priority1Sub = namespaceManager.CreateSubscription(pri1Sub, rulePri1);

            // this sub recieves messages for Priority = 2
            RuleDescription rulePri2 = new RuleDescription(new SqlFilter(PriorityPropName + " = 2"));
            SubscriptionDescription pri2Sub = new SubscriptionDescription(TopicName, "Priority2Subscription");
            Priority2Sub = namespaceManager.CreateSubscription(pri2Sub, rulePri2);

            // this sub recieves messages for Priority Less than 2
            RuleDescription rulePriNot1 = new RuleDescription(new SqlFilter(PriorityPropName + " > 2"));
            SubscriptionDescription priNot1or2Sub = new SubscriptionDescription(TopicName, "PriorityLessThan2Subscription");
            PriorityLessThan2Sub = namespaceManager.CreateSubscription(priNot1or2Sub, rulePriNot1);

            // Start senders and receivers:
            Console.WriteLine("\nLaunching senders and receivers...");

            //send messages to topic            
            MessagingFactory messagingFactory = MessagingFactory.CreateFromConnectionString(ServiceBusConnectionString);

            TopicClient topicClient = messagingFactory.CreateTopicClient(SimplePubSub.TopicName);

            Console.WriteLine("Preparing to send messages to {0}...", topicClient.Path);

            SendMessages(topicClient);


            // All messages sent
            Console.WriteLine("\nSender complete. Press ENTER");
            Console.ReadLine();

            // start recieve
            Console.WriteLine("Receiving messages by priority ...");
            SubscriptionClient subClient1 = messagingFactory.CreateSubscriptionClient(SimplePubSub.TopicName, pri1Sub.Name, ReceiveMode.ReceiveAndDelete);
            SubscriptionClient subClient2 = messagingFactory.CreateSubscriptionClient(SimplePubSub.TopicName, pri2Sub.Name, ReceiveMode.ReceiveAndDelete);
            SubscriptionClient subClient3 = messagingFactory.CreateSubscriptionClient(SimplePubSub.TopicName, priNot1or2Sub.Name, ReceiveMode.ReceiveAndDelete);

            while (true)
            {
                try
                {
                    BrokeredMessage message = subClient1.Receive(TimeSpan.FromSeconds(0));

                    if (message == null)
                    {
                        message = subClient2.Receive(TimeSpan.FromSeconds(0));

                        if (message == null)
                        {
                            message = subClient3.Receive(TimeSpan.FromSeconds(0));
                        }
                    }
                    if (message != null)
                    {
                        SimplePubSub.OutputMessageInfo("RECV: ", message);
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
                catch (MessagingException e)
                {
                    Console.WriteLine("Error: " + e.Message);
                }
            }

            Console.WriteLine("\nReceiver complete. press ENTER");
            Console.ReadLine();

            // Cleanup:
            namespaceManager.DeleteTopic(baseTopicName);
        }

        #region HelperFunctions
        static void GetUserCredentials()
        {
            ServiceBusConnectionString = ConfigurationSettings.AppSettings["Microsoft.ServiceBus.ConnectionString"];
            if (!string.IsNullOrEmpty(ServiceBusConnectionString))
            {
                return;
            }

            // User connection string
            Console.WriteLine("Please provide the connection string to use:");
            ServiceBusConnectionString = Console.ReadLine();
        }

        static void SendMessages(TopicClient topicClient)
        {
            // Send messages to queue:
            Console.WriteLine("Sending messages to topic {0}", topicClient.Path);

            System.Random rand = new Random();
            for (int i = 0; i < SimplePubSub.NumMessages; ++i)
            {
                BrokeredMessage message = new BrokeredMessage();
                message.Properties.Add(SimplePubSub.PriorityPropName, rand.Next(1, SimplePubSub.NumCategories));
                message.MessageId = "Order_" + DateTime.Now.ToLongTimeString();
                try
                {
                    topicClient.Send(message);
                }
                catch (Exception)
                {
                    break;
                }
                SimplePubSub.OutputMessageInfo("SEND: ", message);
            }

            Console.WriteLine();
        }
        #endregion

        #region PublicHelpers
        // Public helper functions and accessors

        public static int NumCategories
        {
            get { return numPriorities; }
            set { numPriorities = value; }
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

        public static SubscriptionDescription Priority1Sub
        { get; set; }

        public static SubscriptionDescription Priority2Sub
        { get; set; }

        public static SubscriptionDescription PriorityLessThan2Sub
        { get; set; }

        public static string PriorityPropName
        { get { return priorityPropName; } }


        public static void OutputMessageInfo(string action, BrokeredMessage message, string additionalText = "")
        {
            if (message == null)
            {
                return;
            }
            object prop = message.Properties[priorityPropName];
            if (prop != null)
            {
                Console.ForegroundColor = colors[int.Parse(prop.ToString()) % colors.Length];
                Console.WriteLine("{0}{1} - Priority {2}. {3}", action, message.MessageId, message.Properties[priorityPropName], additionalText);
                Console.ResetColor();
            }
        }
        #endregion
    }
}
