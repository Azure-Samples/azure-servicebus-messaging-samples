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

namespace Microsoft.Samples.SessionMessages
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
        // Credentials to access Service Bus
        static string serviceBusNamespace;
        static string serviceBusKeyName;
        static string serviceBusKey;

        // Object for service bus management operations
        static NamespaceManager namespaceManager;

        static List<Process> receiverProcs = new List<Process>();
        static List<Process> senderProcs = new List<Process>();
        const int MAX_SESSIONS = 7;
        static int numSessions = 3;
        static int numberOfClients = 3;
        static bool displayVertical = true;
        static int sessionCount = 0;
        static bool exceptionOccurred = false;

        static string orderQueueName = "OrderQueue"; //Session Queue
        static string orderClientConfigName = "orderSendClient";

        // Used to identify sender messages with different colors
        static ConsoleColor[] colors = new ConsoleColor[] { 
            ConsoleColor.Red, 
            ConsoleColor.Green, 
            ConsoleColor.Yellow, 
            ConsoleColor.Cyan,
            ConsoleColor.Magenta,
            ConsoleColor.Blue,             
            ConsoleColor.White};

        // List of prodcts available for purchase
        static List<string> products = new List<string>() {
            "Product1",
            "Product2",
            "Product3",
            "Product4",
            "Product5",
            "Product6",
            "Product7",
            "Product8",
            "Product9",
            "Product10"};

        // constants for imported Win32 functions
        private static IntPtr HWND_TOP = new IntPtr(0);
        //private const int SW_MINIMIZE = 6;
        #endregion

        #region Imports for sample display purpose only
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);
        #endregion

        static void Main(string[] args)
        {
            try
            {
                #region Setup
                Console.Title = "Sample Manager";
                GetUserCredentials();
                numSessions = Math.Min(numSessions, MAX_SESSIONS);
                #endregion

                #region Create Queue
                Console.WriteLine("Creating Queues...");
                CreateNamespaceManager();
                QueueDescription orderQueue = CreateQueue(true);
                Console.WriteLine("Created {0}, Queue.RequiresSession = {1}", orderQueue.Path, orderQueue.RequiresSession);
                #endregion

                #region Launch Senders and Receivers
                Console.WriteLine("\nLaunching senders and receivers...");
                StartSenders();
                StartReceivers();
                Thread.Sleep(TimeSpan.FromSeconds(5.0d)); // waiting for all senders and receivers to start

                // If exception has occured notify user
                if (SampleManager.ExceptionOccurred)
                {
                    Console.WriteLine("An exception has occured. Please ensure that the app.config files have been updated as directed in Readme.htm.");
                }
                #endregion

                #region Cleanup
                Console.WriteLine("\nPress [Enter] to exit.");
                Console.ReadLine();

                // Stop Senders
                StopSenders();

                // Stop Services
                StopReceivers();

                // Delete Queues
                namespaceManager.DeleteQueue(orderQueue.Path);
                #endregion;
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception has occured.");
                Console.WriteLine(e.ToString());
                Console.ReadLine();
            }
        }

        #region HelperFunctions
        // Create the NamespaceManager for management operations (queue)
        static void CreateNamespaceManager()
        {
            // Create SAS token provider.
            TokenProvider credentials = TokenProvider.CreateSharedAccessSignatureTokenProvider(serviceBusKeyName, serviceBusKey);

            // Create the management Uri
            Uri managementUri = ServiceBusEnvironment.CreateServiceUri("sb", serviceBusNamespace, string.Empty);
            namespaceManager = new NamespaceManager(managementUri, credentials);
        }

        // Create the entity (queue)
        static QueueDescription CreateQueue(bool session)
        {
            QueueDescription queueDescription = new QueueDescription(orderQueueName) { RequiresSession = session };           

            // Try deleting the queue before creation. Ignore exception if queue does not exist.
            try
            {
                namespaceManager.DeleteQueue(orderQueueName);
            }
            catch (MessagingEntityNotFoundException)
            {
            }

            return namespaceManager.CreateQueue(queueDescription);
        }

        static void StartSenders()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "OrderClient.exe";
            for (int i = 0; i < numberOfClients; ++i)
            {
                startInfo.Arguments = (SampleManager.sessionCount % 7).ToString();
                SampleManager.sessionCount++;
                Process process = Process.Start(startInfo);
                if (!process.HasExited)
                {
                    senderProcs.Add(process);
                }
                else
                {
                    SampleManager.ExceptionOccurred = true;
                }
            }

            Thread.Sleep(500);
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
            // Start sessionfull service
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "OrderService.exe";
            Process process = Process.Start(startInfo);
            if (!process.HasExited)
            {
                receiverProcs.Add(process);
            }
            else
            {
                SampleManager.ExceptionOccurred = true;
            }

            ArrangeWindows();
        }

        static void StopReceivers()
        {
            foreach (Process proc in receiverProcs)
            {
                proc.CloseMainWindow();
            }
        }

        /// <summary>
        /// This function is only used for visual asthetics and does not provide any additional value.
        /// </summary>
        static void ArrangeWindows()
        {
            int screenWidth = Screen.PrimaryScreen.WorkingArea.Width;
            int screenHeight = Screen.PrimaryScreen.WorkingArea.Height;

            int maxHeight = screenHeight / 3;
            int maxWidth = screenWidth / 2;


            int senderWidth = screenWidth / (numberOfClients + 1);
            int senderHeight = maxHeight;
            int managerWidth = senderWidth;
            int managerHeight = senderHeight;
            int receiverWidth = screenWidth / (numSessions);
            int receiverHeight = screenHeight / 2;
            if (displayVertical)
            {
                senderWidth = screenWidth / 3;
                senderHeight = Math.Min(maxHeight, screenHeight / (numberOfClients + 1));
                managerWidth = maxWidth;
                managerHeight = senderHeight;
                receiverWidth = screenWidth / 3;
                receiverHeight = Math.Min(maxHeight, screenHeight / (numSessions));
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
        #endregion

        #region PublicHelpers
        // Public helper functions and accessors
        public static string OrderQueueName
        {
            get { return orderQueueName; }
            set { orderQueueName = value; }
        }

        public static bool ExceptionOccurred
        {
            get { return exceptionOccurred; }
            set { exceptionOccurred = value; }
        }

        public static string OrderSendClientConfigName
        {
            get
            {
                return orderClientConfigName;
            }
        }

        public static List<string> Products
        {
            get { return products; }
        }

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
        
        public static void OutputMessageInfo(string action, string message, string sessionId)
        {
            lock (typeof(SampleManager))
            {
                Console.ForegroundColor = colors[int.Parse(sessionId)];
                Console.WriteLine("{0}: {1} - CustomerId {2}.", action, message, sessionId);
                Console.ResetColor();
            }
        }
        #endregion
    }
}
