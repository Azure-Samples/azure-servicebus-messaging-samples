//---------------------------------------------------------------------------------
// Microsoft (R)  Windows Azure AppFabric SDK
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples.SessionMessages
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows.Forms;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
    

    public class SampleManager
    {
        #region Fields

        static string ServiceBusConnectionString;      

        static NamespaceManager namespaceManager;

        static List<Process> receiverProcs = new List<Process>();
        static List<Process> senderProcs = new List<Process>();
        static int numSessions = 4;
        static int numSenders = 1;
        static int numReceivers = 4;        
        static int numMessages = 100;

        static bool displayVertical = true;

        static string baseQueueName = "OrderQueue";
        static string sessionlessQueueName = baseQueueName + "_NoSession";
        static string sessionQueueName = baseQueueName + "_Session";

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

            // Create queues:
            Console.WriteLine("\nCreating Queues...");
            QueueDescription sessionlessQueue = CreateQueue(false);
            Console.WriteLine("Created {0}, Queue.RequiresSession = false", sessionlessQueue.Path);
            QueueDescription sessionQueue = CreateQueue(true);
            Console.WriteLine("Created {0}, Queue.RequiresSession = true", sessionQueue.Path);

            // Start senders and receivers:
            Console.WriteLine("\nLaunching senders and receivers...");
            StartSenders();
            StartReceivers();

            Console.WriteLine("\nPress [Enter] to exit.");
            Console.ReadLine();

            // Cleanup:
            namespaceManager.DeleteQueue(sessionlessQueue.Path);
            namespaceManager.DeleteQueue(sessionQueue.Path);
            StopSenders();
            StopReceivers();        
        }

        #region HelperFunctions
        static void GetUserCredentials()
        {
            Console.Write("Please provide a connection string to Service Bus (/? for help):\n ");
            ServiceBusConnectionString = Console.ReadLine();

            if ((String.Compare(ServiceBusConnectionString, "/?") == 0) || (ServiceBusConnectionString.Length == 0))
            {
                Console.Write("To connect to the Service Bus cloud service, go to the Windows Azure portal and select 'View Connection String'.\n");
                Console.Write("To connect to the Service Bus for Windows Server, use the get-sbClientConfiguration PowerShell cmdlet.\n\n");
                Console.Write("A Service Bus connection string has the following format: \nEndpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<keyName>;SharedAccessKey=<key>");

                ServiceBusConnectionString = Console.ReadLine();
                Environment.Exit(0);
            }
        }

        // Create the management entities (queue)
        static void CreateNamespaceClient()
        {
            namespaceManager =  NamespaceManager.CreateFromConnectionString(ServiceBusConnectionString);
        }

        static QueueDescription CreateQueue(bool session)
        {
            string queueName = (session ? sessionQueueName : sessionlessQueueName);

            QueueDescription queueDescription = new QueueDescription(queueName) 
            { 
                RequiresSession = session 
            };
            
            // Delete the queue if already exists before creation. 
            if (namespaceManager.QueueExists(queueName))
            {
                namespaceManager.DeleteQueue(queueName);
            }

            return namespaceManager.CreateQueue(queueDescription);
        }

        private static void StartSenders()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "SessionMessagesSampleSender.exe";
            startInfo.Arguments = CreateArgs();
            for (int i = 0; i < numSenders; ++i)
            {
                Process process = Process.Start(startInfo);
                senderProcs.Add(process);
            }
            Thread.Sleep(500);
            ArrangeWindows();
        }

        private static void StopSenders()
        {
            foreach (Process proc in senderProcs)
            {
                proc.CloseMainWindow();
            }
        }

        static void StartReceivers()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "SessionMessagesSampleReceiver.exe";
            startInfo.Arguments = CreateArgs();
            for (int i = 0; i < numReceivers; ++i)
            {
                Process process = Process.Start(startInfo);
                receiverProcs.Add(process);
            }
            Thread.Sleep(500);
            ArrangeWindows();
        }       

        static void StopReceivers()
        {
            foreach (Process proc in receiverProcs)
            {
                proc.CloseMainWindow();
            }
        }

        static string CreateArgs()
        {
            string args = ServiceBusConnectionString;
            return args;
        }

        static void ArrangeWindows()
        {  
            int screenWidth = Screen.PrimaryScreen.WorkingArea.Width;
            int screenHeight = Screen.PrimaryScreen.WorkingArea.Height;
           
            int maxHeight = screenHeight / 3;
            int maxWidth = screenWidth / 2;

            
            int senderWidth = screenWidth / (numSenders + 1);            
            int senderHeight = maxHeight;
            int managerWidth = senderWidth;
            int managerHeight = senderHeight;
            int receiverWidth = screenWidth / (numReceivers);
            int receiverHeight = screenHeight / 2;
            if (displayVertical)
            {
                senderWidth = screenWidth / 3;
                senderHeight = Math.Min(maxHeight, screenHeight / (numSenders + 1));
                managerWidth = maxWidth;
                managerHeight = senderHeight;
                receiverWidth = screenWidth / 3;
                receiverHeight = Math.Min(maxHeight, screenHeight / (numReceivers));
            }

            Console.Title = "Manager";
            IntPtr mainHandle = Process.GetCurrentProcess().MainWindowHandle;
            SetWindowPos(mainHandle, HWND_TOP, 0, 0, managerWidth, managerHeight, 0);
            
            for (int i = 0; i < senderProcs.Count; ++i)
            {
                IntPtr handle = senderProcs[i].MainWindowHandle;
                if (displayVertical)
                {
                    SetWindowPos(handle, HWND_TOP, 0, senderHeight * (i + 1), senderWidth, senderHeight, 0);
                }
                else
                {
                    SetWindowPos(handle, HWND_TOP, senderWidth * (i + 1), 0, senderWidth, senderHeight, 0);
                }
            }

            for (int i = 0; i < receiverProcs.Count; ++i)
            {
                IntPtr handle = receiverProcs[i].MainWindowHandle;
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
                 Int32.TryParse(args[0], out numSessions);
            }
            if (args.Length > 1)
            {
                Int32.TryParse(args[1], out numSenders); 
            }
            if (args.Length > 2)
            {
                Int32.TryParse(args[1], out numReceivers);
            }
            if (args.Length > 3)
            {
                Int32.TryParse(args[2], out numMessages);
            }
            if (args.Length > 4)
            {
                Boolean.TryParse(args[3], out displayVertical);
            }
        }
        #endregion

        #region PublicHelpers
        // Public helper functions and accessors

        public static String SessionQueueName
        {
            get { return sessionQueueName; }
            set { sessionQueueName = value; }
        }

        public static String SessionlessQueueName
        {
            get { return sessionlessQueueName; }
            set { sessionlessQueueName = value; }
        }

        public static int NumSessions
        {
            get { return numSessions; }
            set { numSessions = value; }
        }

        public static int NumMessages
        {
            get { return numMessages; }
            set { numMessages = value; }
        }

        public static void OutputMessageInfo(string action, BrokeredMessage message, string additionalText = "")
        {
            Console.ForegroundColor = colors[int.Parse(message.SessionId) % colors.Length];
            Console.WriteLine("{0}{1} - Group {2}. {3}", action, message.MessageId, message.SessionId, additionalText);
            Console.ResetColor();
        }
        #endregion
    }
}
