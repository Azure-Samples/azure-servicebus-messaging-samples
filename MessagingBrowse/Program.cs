    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus.Messaging;
    using System.IO;
    using System.ServiceModel.Channels;

namespace BrowseMessages
{
        class Program
        {
            #region Fields
            static string ServiceBusConnectionString;
            static string ServiceBusentityPath;
            #endregion

            static void Main(string[] args)
            {
                // ***************************************************************************************
                // This sample demonstrates how to use MessagePeek feature to look into the content of 
                // Service bus entities (Queues , Subscriptions).
                // ***************************************************************************************
                Program.GetNamespaceAndCredentials();

                MessagingFactory messagingFactory = MessagingFactory.CreateFromConnectionString(ServiceBusConnectionString);
                MessageReceiver messageReciever = messagingFactory.CreateMessageReceiver(Program.ServiceBusentityPath);

                BrokeredMessage msg;
                while (true)
                {
                    msg = messageReciever.Peek();
                    if (msg != null)
                    {
                        Console.WriteLine("{0} {1} - {2} - {3}", msg.EnqueuedTimeUtc.ToLocalTime().ToShortDateString(), msg.EnqueuedTimeUtc.ToLocalTime().ToLongTimeString(), msg.SequenceNumber, msg.Label);
                        var listViewItems = msg.Properties.Select(p => new[] { p.Key, p.Value.ToString() }).ToArray();
                        for (int propIndex = 0; propIndex < listViewItems.Length; propIndex++)
                        {
                            Console.Write("{0}: {1}\t", listViewItems[propIndex][0], listViewItems[propIndex][1]);
                        }

                        Stream stream = msg.GetBody<Stream>();
                        if (stream != null)
                        {
                            StreamReader reader = new StreamReader(stream);
                            string text = reader.ReadToEnd();
                            if (text != null)
                            {
                                Console.WriteLine("\n{0}\n", text);
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                messagingFactory.Close();
                Console.WriteLine("Press [Enter] to quit...");
                Console.ReadLine();
            }


            static void GetNamespaceAndCredentials()
            {
                Console.Write("Please provide a connection string to Service Bus (/? for help):\n ");
                Program.ServiceBusConnectionString = Console.ReadLine();

                if ((String.Compare(Program.ServiceBusConnectionString, "/?") == 0) || (Program.ServiceBusConnectionString.Length == 0))
                {
                    Console.Write("To connect to the Service Bus cloud service, go to the Windows Azure portal and select 'View Connection String'.\n");
                    Console.Write("To connect to the Service Bus for Windows Server, use the get-sbClientConfiguration PowerShell cmdlet.\n\n");
                    Console.Write("A Service Bus connection string has the following format: \nEndpoint=sb://<namespace>.servicebus.windows.net/;SharedSecretIssuer=<issuer>;SharedSecretValue=<secret>\n");

                    Program.ServiceBusConnectionString = Console.ReadLine();
                    Environment.Exit(0);
                }

                Console.Write("Please provide an entity path to peek messages from (/? for help):\n ");
                Program.ServiceBusentityPath = Console.ReadLine();

                if ((String.Compare(Program.ServiceBusentityPath, "/?") == 0) || (Program.ServiceBusentityPath.Length == 0))
                {
                    Console.Write("Entity path include the relative path of you Service Bus entity.\n");
                    Console.Write("Examples: MyQueue; MyTopic/subscriptions/MySubscription .\n\n");

                    Program.ServiceBusentityPath = Console.ReadLine();
                    Environment.Exit(0);
                }
            }
        }
    }
