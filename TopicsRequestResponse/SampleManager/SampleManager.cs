//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples.RequestResponse
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows.Forms;

    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
    using Microsoft.ServiceBus.Messaging.Filters;


    public class SampleManager
    {
        #region Fields

        static string serviceBusNamespace;
        static string serviceBusKeyName;
        static string serviceBusKey;

        static NamespaceManager namespaceManager;

        static List<Process> serverProcs = new List<Process>();
        static List<Process> clientProcs = new List<Process>();
        static int numClients = 4;
        static int numServers = 1;
        static int numMessages = 10;

        static bool displayVertical = true;

        static string topicPath = "RequestResponseTopic";
        private static string requestSubName = "Request";
        private static string responseSubName = "Response";


        static ConsoleColor[] colors = new ConsoleColor[] { 
            ConsoleColor.Red, 
            ConsoleColor.Green, 
            ConsoleColor.Yellow, 
            ConsoleColor.Cyan,
            ConsoleColor.Magenta,
            ConsoleColor.Blue,             
            ConsoleColor.White};

        // constants for imported Win32 functions
        private static IntPtr HWND_TOP = new IntPtr(0);
        #endregion

        #region Imports
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        #endregion

        static void Main(string[] args)
        {
            // Setup:
            ParseArgs(args);
            GetUserCredentials();
            CreateNamespaceClient();

            // Create topic
            Console.WriteLine("\nCreating Topic...");
            TopicDescription description = CreateTopic(topicPath);
            Console.WriteLine(
                "Created {0}", description.Path);

            // Create request subscription
            Console.WriteLine("\nCreating Subscriptions...");
            SubscriptionDescription requestSub = CreateSubscription(description.Path, requestSubName, false);
            Console.WriteLine(
                "Created {0}/{1}, RequiresSession = {2}",
                requestSub.TopicPath,
                requestSub.Name,
                requestSub.RequiresSession);
            SubscriptionDescription responseSub = CreateSubscription(description.Path, responseSubName, true);
            Console.WriteLine(
                "Created {0}/{1}, RequiresSession = {2}",
                responseSub.TopicPath,
                responseSub.Name,
                responseSub.RequiresSession);

            // Start clients and servers:
            Console.WriteLine("\nLaunching clients and servers...");
            StartClients();
            StartServers();

            Console.WriteLine("\nPress [Enter] to exit.");
            Console.ReadLine();

            // Cleanup:
            namespaceManager.DeleteSubscription(requestSub.TopicPath, requestSub.Name);
            namespaceManager.DeleteSubscription(responseSub.TopicPath, responseSub.Name);
            namespaceManager.DeleteTopic(description.Path);
            StopClients();
            StopServers();
        }

        #region HelperFunctions
        static void GetUserCredentials()
        {
            // User namespace
            Console.Write("Please provide the namespace: ");
            serviceBusNamespace = Console.ReadLine();

            // Issuer name
            Console.Write("Please provide the key name (e.g., \"RootManageSharedAccessKey\"): ");
            serviceBusKeyName = Console.ReadLine();

            // Issuer key
            Console.Write("Please provide the key: ");
            serviceBusKey = Console.ReadLine();
        }

        // Create the management entities (queue)
        static void CreateNamespaceClient()
        {
            Uri uri = ServiceBusEnvironment.CreateServiceUri("sb", serviceBusNamespace, string.Empty);
            namespaceManager = new NamespaceManager(uri, TokenProvider.CreateSharedAccessSignatureTokenProvider(serviceBusKeyName, serviceBusKey));
        }

        static TopicDescription CreateTopic(string path)
        {
            TopicDescription description = new TopicDescription(path);

            // Delete the topic if it already exists
            if (namespaceManager.TopicExists(path))
            {
                namespaceManager.DeleteTopic(path);
            }

            return namespaceManager.CreateTopic(description);
        }

        static SubscriptionDescription CreateSubscription(string path, string name, bool sessions)
        {
            SubscriptionDescription description = new SubscriptionDescription(path, name) { RequiresSession = sessions };

            // Delete the subscription if it already exists
            if (namespaceManager.SubscriptionExists(description.TopicPath, description.Name))
            {
                namespaceManager.DeleteSubscription(description.TopicPath, description.Name);
            }

            return namespaceManager.CreateSubscription(description, new CorrelationFilter(name));
        }

        static void StartClients()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "RequestResponseSampleClient.exe";
            for (int i = 0; i < numClients; ++i)
            {
                startInfo.Arguments = CreateArgs(i.ToString());
                Process process = Process.Start(startInfo);
                clientProcs.Add(process);
            }
            Thread.Sleep(500);
            ArrangeWindows();
        }

        static void StopClients()
        {
            foreach (Process proc in clientProcs)
            {
                proc.CloseMainWindow();
            }
        }

        static void StartServers()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "RequestResponseSampleServer.exe";
            startInfo.Arguments = CreateArgs();
            for (int i = 0; i < numServers; ++i)
            {
                Process process = Process.Start(startInfo);
                serverProcs.Add(process);
            }
            Thread.Sleep(500);
            ArrangeWindows();
        }

        static void StopServers()
        {
            foreach (Process proc in serverProcs)
            {
                proc.CloseMainWindow();
            }
        }

        static string CreateArgs(string responseToSessionId = null)
        {
            string args = serviceBusNamespace + " " + serviceBusKeyName + " " + serviceBusKey;
            if (responseToSessionId != null)
            {
                args += " " + responseToSessionId;
            }

            return args;
        }

        static void ArrangeWindows()
        {
            int screenWidth = Screen.PrimaryScreen.WorkingArea.Width;
            int screenHeight = Screen.PrimaryScreen.WorkingArea.Height;

            int maxHeight = screenHeight / 3;
            int maxWidth = screenWidth / 2;


            int senderWidth = screenWidth / (numClients + 1);
            int senderHeight = maxHeight;
            int managerWidth = senderWidth;
            int managerHeight = senderHeight;
            int receiverWidth = screenWidth / (numServers);
            int receiverHeight = screenHeight / 2;
            if (displayVertical)
            {
                senderWidth = screenWidth / 3;
                senderHeight = Math.Min(maxHeight, screenHeight / (numClients + 1));
                managerWidth = maxWidth;
                managerHeight = senderHeight;
                receiverWidth = screenWidth / 3;
                receiverHeight = Math.Min(maxHeight, screenHeight / (numServers));
            }

            Console.Title = "Manager";
            IntPtr mainHandle = Process.GetCurrentProcess().MainWindowHandle;
            SetWindowPos(mainHandle, HWND_TOP, 0, 0, managerWidth, managerHeight, 0);

            for (int i = 0; i < clientProcs.Count; ++i)
            {
                IntPtr handle = clientProcs[i].MainWindowHandle;
                if (displayVertical)
                {
                    SetWindowPos(handle, HWND_TOP, 0, senderHeight * (i + 1), senderWidth, senderHeight, 0);
                }
                else
                {
                    SetWindowPos(handle, HWND_TOP, senderWidth * (i + 1), 0, senderWidth, senderHeight, 0);
                }
            }

            for (int i = 0; i < serverProcs.Count; ++i)
            {
                IntPtr handle = serverProcs[i].MainWindowHandle;
                if (displayVertical)
                {
                    SetWindowPos(handle, HWND_TOP, screenWidth - receiverWidth, receiverHeight * i, receiverWidth, receiverHeight, 0);
                }
                else
                {
                    SetWindowPos(handle, HWND_TOP, receiverWidth * i, screenHeight / 2, receiverWidth, receiverHeight, 0);
                }
            }
        }

        static void ParseArgs(string[] args)
        {
            if (args.Length > 0)
            {
                Int32.TryParse(args[0], out numClients);
            }
            if (args.Length > 1)
            {
                Int32.TryParse(args[1], out numServers);
            }
            if (args.Length > 2)
            {
                Int32.TryParse(args[2], out numMessages);
            }
            if (args.Length > 3)
            {
                Boolean.TryParse(args[3], out displayVertical);
            }
        }
        #endregion

        #region PublicHelpers
        // Public helper functions and accessors

        public static String TopicPath
        {
            get { return topicPath; }
            set { topicPath = value; }
        }

        public static String RequestSubName
        {
            get { return requestSubName; }
            set { requestSubName = value; }
        }

        public static String ResponseSubName
        {
            get { return responseSubName; }
            set { responseSubName = value; }
        }

        public static int NumMessages
        {
            get { return numMessages; }
            set { numMessages = value; }
        }

        public static void OutputMessageInfo(string action, BrokeredMessage message, string additionalText = "")
        {
            string id;
            if (message.SessionId == null)
            {
                id = message.ReplyToSessionId;
            }
            else
            {
                id = message.SessionId;
            }

            Console.ForegroundColor = colors[int.Parse(id) % colors.Length];
            
            Console.WriteLine("{0}{1} - Client {2}. {3}", action, message.MessageId, id, additionalText);
            Console.ResetColor();
        }
        #endregion
    }
}
