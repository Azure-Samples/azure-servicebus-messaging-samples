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
    using Microsoft.ServiceBus.Description;
    using Microsoft.ServiceBus.Messaging;

    public class SampleManager
    {
        #region Fields
        // Credentials to access Service Bus
        static string ServiceBusNamespace;
        static string ServiceBusIssuerName;
        static string ServiceBusIssuerKey;

        // Object for service bus management operations
        static NamespaceManager namespaceClient;

        static List<Process> receiverProcs = new List<Process>();
        static List<Process> senderProcs = new List<Process>();
        const int MAX_SESSIONS = 7;
        static int numSessions = 2;
        static int numberOfClients = 2;
        static bool displayVertical = true;
        static int sessionCount = 0;
        static bool exceptionOccurred = false;

        static string pingQueueName = "PingQueue"; //non-session Queue
        static string pingClientConfigName = "pingClient";

        // Used to identify sender messages with different colors
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

                #region Create Queues
                Console.WriteLine("Creating Queues...");
                CreateNamespaceManager();
                QueueDescription pingQueue = CreateQueue(false);
                Console.WriteLine("Created {0}, Queue.RequiresSession = {1}", pingQueue.Path, pingQueue.RequiresSession);
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
                namespaceClient.DeleteQueue(pingQueue.Path);
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
            // Create SharedSecretCredential object for access control service
            TokenProvider credentials = TokenProvider.CreateSharedSecretTokenProvider(ServiceBusIssuerName, ServiceBusIssuerKey);

            // Create the management Uri
            Uri managementUri = ServiceBusEnvironment.CreateServiceUri("sb", ServiceBusNamespace, string.Empty);
            namespaceClient = new NamespaceManager(managementUri, credentials);
        }

        // Create the entity (queue)
        static QueueDescription CreateQueue(bool session)
        {
            QueueDescription queueDescription = new QueueDescription(PingQueueName) { RequiresSession = session };

            // Try deleting the queue before creation. Ignore exception if queue does not exist.
            try
            {
                namespaceClient.DeleteQueue(queueDescription.Path);
            }
            catch (MessagingEntityNotFoundException)
            {
            }

            return namespaceClient.CreateQueue(queueDescription);
        }

        private static void StartSenders()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "PingClient.exe";

            for (int i = 0; i < numberOfClients; ++i)
            {
                startInfo.Arguments = (SampleManager.sessionCount % MAX_SESSIONS).ToString();
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
            // Start sessionless service
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "PingService.exe";
            Process process = Process.Start(startInfo);
            if (!process.HasExited)
            {
                receiverProcs.Add(process);
            }
            else
            {
                SampleManager.ExceptionOccurred = true;
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
        public static string PingQueueName
        {
            get { return pingQueueName; }
            set { pingQueueName = value; }
        }

        public static bool ExceptionOccurred
        {
            get { return exceptionOccurred; }
            set { exceptionOccurred = value; }
        }

        public static string PingClientConfigName
        {
            get
            {
                return pingClientConfigName;
            }
        }

        public static void GetUserCredentials()
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

        public static void OutputMessageInfo(string action, PingData pingData)
        {
            lock (typeof(SampleManager))
            {
                Console.ForegroundColor = colors[int.Parse(pingData.SenderId)];
                Console.WriteLine("{0}: Message [{1}].", action, pingData.Message);
                Console.ResetColor();
            }
        }
        #endregion
    }
}
