//---------------------------------------------------------------------------------
// Copyright (c) 2013, Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//---------------------------------------------------------------------------------

using System;
using System.Threading;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.ServiceBus.Samples.MessagePump
{
    public class MessagePump : IDisposable
    {
        const bool EnableFaultInjection = false;  // SET TO true FOR TESTING PURPOSE ONLY.
        const int NumberOfFactories = 5;
        const int PrefetchCount = 500;
        const int NumberOfMessagesToMeasure = 10000; // Measure pump time for specified number of messages.

        MessagingFactory[] sourceMessagingFactory;
        MessagingFactory[] destinationMessagingFactory;
        MessageReceiver[] sourceQueueMessageReceiver;
        MessageSender[] destinationQueueMessageSender;

        TimerWaitTime receiverWaitTime;
        TimerWaitTime senderWaitTime;
        Int32 disposeStarted;

        // PERF TESTING.
        PerfMeasure perfMeasure;

        // FOR TESTING PURPOSE ONLY.
        FaultInjector faultInjector;

        enum OperationResult { Success, WaitAndRetry, PermanentFailure };

        public MessagePump(Uri sourceNamespaceUri, TokenProvider sourceTokenProvider, string sourceQueueOrSubscriptionName, Uri destinationNamespaceUri, TokenProvider destinationTokenProvider, string destinationQueueOrTopicName)
        {
            // Create messaging factories for the source queue.
            sourceMessagingFactory = new MessagingFactory[NumberOfFactories];
            for (int i = 0; i < NumberOfFactories; i++)
            {
                MessagingFactorySettings sourceMessagingFactorySettings = new MessagingFactorySettings();
                sourceMessagingFactorySettings.TokenProvider = sourceTokenProvider;
                this.sourceMessagingFactory[i] = MessagingFactory.Create(sourceNamespaceUri, sourceMessagingFactorySettings);
            }

            // Create messaging factories for the destination queue.
            destinationMessagingFactory = new MessagingFactory[NumberOfFactories];
            for (int i = 0; i < NumberOfFactories; i++)
            {
                MessagingFactorySettings destinationMessagingFactorySettings = new MessagingFactorySettings();
                destinationMessagingFactorySettings.TokenProvider = destinationTokenProvider;
                destinationMessagingFactorySettings.NetMessagingTransportSettings.BatchFlushInterval = TimeSpan.FromSeconds(0.05);
                this.destinationMessagingFactory[i] = MessagingFactory.Create(destinationNamespaceUri, destinationMessagingFactorySettings);
            }

            // Create MessageReceivers that receive from the source queue.
            sourceQueueMessageReceiver = new MessageReceiver[NumberOfFactories];
            for (int i = 0; i < NumberOfFactories; i++)
            {
                this.sourceQueueMessageReceiver[i] = this.sourceMessagingFactory[i].CreateMessageReceiver(sourceQueueOrSubscriptionName);
                this.sourceQueueMessageReceiver[i].PrefetchCount = PrefetchCount;
            }

            // Create a MessageSender that sends to the destination queue.
            destinationQueueMessageSender = new MessageSender[NumberOfFactories];
            for (int i = 0; i < NumberOfFactories; i++)
            {
                this.destinationQueueMessageSender[i] = this.destinationMessagingFactory[i].CreateMessageSender(destinationQueueOrTopicName);
            }

            // For source and destination, create one object each to maintain wait times in case of errors.
            receiverWaitTime = new TimerWaitTime();
            senderWaitTime = new TimerWaitTime();

            // FOR TESTING PURPOSE ONLY.
            this.faultInjector = new FaultInjector(EnableFaultInjection);

            // PERF TESTING.
            perfMeasure = new PerfMeasure(NumberOfMessagesToMeasure);
            this.perfMeasure.StartCount();

            // Start receiving messages from the source queue.
            Console.WriteLine("Start message pump.");
            for (int i = 0; i < NumberOfFactories; i++)
            {
                AsyncArguments arg = new AsyncArguments(this.sourceQueueMessageReceiver[i], this.destinationQueueMessageSender[i]);
                ThreadPool.QueueUserWorkItem(new WaitCallback(this.ProcessBeginReceive), arg);
            }
        }

        public void Dispose()
        {
            disposeStarted = 1;
            for (int i = 0; i < NumberOfFactories; i++)
            {
                this.sourceQueueMessageReceiver[i].Close();
                this.destinationQueueMessageSender[i].Close();
            }
            GC.SuppressFinalize(this);
        }

        void ProcessBeginReceive(Object obj)
        {
            AsyncArguments arg = (AsyncArguments)obj;
            IAsyncResult result = null;

            // Call BeginReceive() on the source queue. If the callback is executed on the same thread, call BeginReceive() again.
            do
            {
                try
                {
                    AsyncArguments newArg = new AsyncArguments(arg);

                    // FOR TESTING PURPOSE ONLY: Inject Service Bus error. 
                    faultInjector.InjectFaultBeforeReceivingMessage();

                    result = newArg.Receiver.BeginReceive(TimeSpan.FromSeconds(30), this.ProcessEndReceive, newArg);
                }
                catch (Exception ex)
                {
                    // Break loop if we are disposing this message pump object. 
                    if (ex.GetType() == typeof(OperationCanceledException))
                    {
                        if (disposeStarted > 0)
                        {
                            return;
                        }
                    }
                    long waitTime = receiverWaitTime.Get();
                    Console.WriteLine("MessagePump: BeginReceive returns error. Wait {0}ms: {1} {2}", waitTime, ex.GetType(), ex.Message);
                    arg.Timer = new Timer(this.ProcessTimerReceive, arg, waitTime, Timeout.Infinite);
                    return;
                }
            }
            while (result != null && result.CompletedSynchronously);
        }

        void ProcessEndReceive(IAsyncResult result)
        {
            AsyncArguments arg = (AsyncArguments)result.AsyncState;

            // Complete the Receive operation.
            BrokeredMessage message = null;
            try
            {
                message = arg.Receiver.EndReceive(result);

                // FOR TESTING PURPOSE ONLY: Inject Service Bus error. 
                faultInjector.InjectFaultAfterReceivingMessage();
            }
            catch (Exception ex)
            {
                // Don't initiate reception of next message if we are disposing this message pump object. 
                if (ex.GetType() == typeof(OperationCanceledException))
                {
                    if (disposeStarted > 0)
                    {
                        return;
                    }
                }

                // Start timer.
                long waitTime = receiverWaitTime.Get();
                Console.WriteLine("MessagePump: EndReceive returns error. Wait {0}ms: {1} {2}", waitTime, ex.GetType(), ex.Message);
                arg.Timer = new Timer(this.ProcessTimerReceive, arg, waitTime, Timeout.Infinite);
                return;
            }

            // The receive operation completed successfully. Reset the receiver wait time.
            receiverWaitTime.Reset();

            if (message != null)
            {
                // Send message to destination queue.
                //Console.WriteLine("MessagePump: Received message {0} from source queue. {1}", message.Label, message.LockToken);
                arg.Message = this.CloneBrokeredMessage(message);
                arg.LockToken = message.LockToken;
                ProcessBeginSend(arg);
            }

            // If this method was executed on a different thread than BeginReceive(),
            // call ProcessBeginReceive() to receive the next message from the source queue.
            if (! result.CompletedSynchronously)
            {
                this.ProcessBeginReceive(arg);
            }
        }

        void ProcessBeginSend(AsyncArguments arg)
        {
            //Console.WriteLine("MessagePump: Send message {0} to destination queue.", message.Label);

            // Save a copy of the message. We need this copy in case we need to send this message
            // again. We also need the LockToken that Service Bus has assigned to this message.
            BrokeredMessage message = arg.Message;
            arg.Message = this.CloneBrokeredMessage(message);
            
            try
            {
                // FOR TESTING PURPOSE ONLY: Inject Service Bus error. 
                faultInjector.InjectFaultBeforeSendingMessage();

                arg.Sender.BeginSend(message, this.ProcessEndSend, arg);
            }
            catch (Exception ex)
            {
                // Permanent failure. Deadletter message.
                Console.WriteLine("MessagePump: BeginSend() returns error when sending message {0} to destination queue: {1} {2}", arg.Message.Label, ex.GetType(), ex.Message);
                arg.Receiver.BeginDeadLetter(arg.LockToken, ProcessEndDeadLetter, arg);
            }
        }

        void ProcessEndSend(IAsyncResult result)
        {
            AsyncArguments arg = (AsyncArguments)result.AsyncState;

            switch (this.ExecuteEndSend(result))
            {
                case OperationResult.Success: // Message was successfully sent to Service Bus. Complete message.
                    //Console.WriteLine("MessagePump: Send operation completed for message {0}.", arg.Message.Label);
                    senderWaitTime.Reset(); // The Send operation completed successfully. Reset the sender wait time.
                    this.ProcessBeginComplete(arg);
                    break;
                case OperationResult.WaitAndRetry: // Destination queue is temporarily unavailable. Wait.
                    long waitTime = senderWaitTime.Get();
                    Console.WriteLine("MessagePump: Destination queue is temporarily unavailable when sending message {0}. Wait {1}ms.", arg.Message.Label, waitTime);
                    arg.Timer = new Timer(this.ProcessTimerSend, arg, waitTime, Timeout.Infinite);
                    break;
                case OperationResult.PermanentFailure: // Permanent error. Deadletter message.
                    Console.WriteLine("MessagePump: EndSend() returns permanent error when sending message {0} to Service Bus.", arg.Message.Label);
                    this.ProcessBeginDeadLetter(arg);
                    break;
            }
        }
        
        // Remove message from source queue.
        void ProcessBeginComplete(AsyncArguments arg)
        {
            //Console.WriteLine("MessagePump: Complete message {0}", arg.Message.Label);
            try
            {
                // FOR TESTING PURPOSE ONLY: Inject Service Bus error. 
                faultInjector.InjectFaultBeforeCompletingMessage();

                arg.Receiver.BeginComplete(arg.LockToken, this.ProcessEndComplete, arg);
            }
            catch (Exception ex)
            {
                long waitTime = receiverWaitTime.Get(); 
                Console.WriteLine("MessagePump: BeginComplete() returns error when completing message {0}. Wait {1}ms. {2} {3}", arg.Message.Label, waitTime, ex.GetType(), ex.Message);
                arg.Timer = new Timer(this.ProcessTimerComplete, arg, waitTime, Timeout.Infinite);
            }
        }

        void ProcessEndComplete(IAsyncResult result)
        {
            AsyncArguments arg = (AsyncArguments)result.AsyncState;
            switch (this.ExecuteEndComplete(result))
            {
                case OperationResult.Success: // Message was successfully completed.
                    //Console.WriteLine("MessagePump: Complete operation completed for message {0}.", arg.Message.Label);
                    // PERF TESTING.
                    this.perfMeasure.IncrementCount();
                    receiverWaitTime.Reset(); // The Complete operation completed successfully. Reset the receiver wait time.
                    break;
                case OperationResult.WaitAndRetry: // Source queue is temporarily unavailable. Wait.
                    long waitTime = receiverWaitTime.Get();
                    Console.WriteLine("MessagePump: Source queue is temporarily unavailable when completing message {0}. Wait {1}ms.", arg.Message.Label, waitTime);
                    arg.Timer = new Timer(this.ProcessTimerComplete, arg, waitTime, Timeout.Infinite);
                    break;
                case OperationResult.PermanentFailure: // Permanent error.
                    Console.WriteLine("MessagePump: EndComplete() returns permanent error when completing message {0} to Service Bus.", arg.Message.Label);
                    break;
            }
        }

        // Deadletter message in source queue.
        void ProcessBeginDeadLetter(AsyncArguments arg)
        {
            //Console.WriteLine("MessagePump: Deadletter message {0}", arg.Message.Label);
            try
            {
                // FOR TESTING PURPOSE ONLY: Inject Service Bus error. 
                faultInjector.InjectFaultBeforeDeadLetteringMessage();

                arg.Receiver.BeginDeadLetter(arg.LockToken, this.ProcessEndDeadLetter, arg);
            }
            catch (Exception ex)
            {
                long waitTime = senderWaitTime.Get();
                Console.WriteLine("MessagePump: BeginDeadLetter() returns error when deadlettering message {0}. Wait {1}ms. {2} {3}", arg.Message.Label, waitTime, ex.GetType(), ex.Message);
                arg.Timer = new Timer(this.ProcessTimerDeadLetter, arg, waitTime, Timeout.Infinite);
            }
        }

        void ProcessEndDeadLetter(IAsyncResult result)
        {
            AsyncArguments arg = (AsyncArguments)result.AsyncState;
            switch (this.ExecuteEndDeadLetter(result))
            {
                case OperationResult.Success: // Message was successfully deadlettered.
                    //Console.WriteLine("MessagePump: Deadletter operation completed for message {0}.", arg.Message.Label);
                    receiverWaitTime.Reset(); // The Deadletter operation completed successfully. Reset the receiver wait time.
                    break;
                case OperationResult.WaitAndRetry: // Source queue is temporarily unavailable. Wait.
                    long waitTime = receiverWaitTime.Get();
                    Console.WriteLine("MessagePump: Source queue is temporarily unavailable when deadlettering message {0}. Wait {1}ms.", arg.Message.Label, waitTime);
                    arg.Timer = new Timer(this.ProcessTimerDeadLetter, arg, waitTime, Timeout.Infinite);
                    break;
                case OperationResult.PermanentFailure: // Permanent error.
                    Console.WriteLine("MessagePump: EndDeadLetter() returns permanent error when completing message {0} to Service Bus.", arg.Message.Label);
                    break;
            }
        }

        OperationResult ExecuteEndSend(IAsyncResult result)
        {
            AsyncArguments arg = (AsyncArguments)result.AsyncState; 
            
            try
            {
                arg.Sender.EndSend(result);

                // FOR TESTING PURPOSE ONLY: Inject Service Bus error.
                faultInjector.InjectFaultAfterSendingMessage();

                return OperationResult.Success;
            }
            catch (MessagingException ex)
            {
                if (ex.IsTransient)
                {
                    Console.WriteLine("MessagePump: Transient exception when sending message {0}: {1} {2}", ((AsyncArguments)result.AsyncState).Message.Label, ex.GetType(), ex.Message);
                    return OperationResult.WaitAndRetry;
                }
                else
                {
                    Console.WriteLine("MessagePump: Permanent exception when sending message {0}: {1} {2}", ((AsyncArguments)result.AsyncState).Message.Label, ex.GetType(), ex.Message);
                    return OperationResult.PermanentFailure;
                }
            }

            catch (Exception ex)
            {
                Type exceptionType = ex.GetType();
                if (exceptionType == typeof(TimeoutException))
                {
                    Console.WriteLine("MessagePump: Exception: {0}", exceptionType);
                    return OperationResult.WaitAndRetry;
                }
                else
                {
                    Console.WriteLine("MessagePump: Exception: {0}", exceptionType);
                    return OperationResult.PermanentFailure;
                }
            }
        }

        OperationResult ExecuteEndComplete(IAsyncResult result)
        {
            AsyncArguments arg = (AsyncArguments)result.AsyncState;

            try
            {
                arg.Receiver.EndComplete(result);

                // FOR TESTING PURPOSE ONLY: Inject Service Bus error.
                faultInjector.InjectFaultAfterCompletingMessage();

                return OperationResult.Success;
            }
            catch (MessagingException ex)
            {
                if (ex.IsTransient)
                {
                    Console.WriteLine("MessagePump: Transient exception when completing message {0}: {1} {2}", ((AsyncArguments)result.AsyncState).Message.Label, ex.GetType(), ex.Message);
                    return OperationResult.WaitAndRetry;
                }
                else
                {
                    Console.WriteLine("MessagePump: Permanent exception when completing message {0}: {1} {2}", ((AsyncArguments)result.AsyncState).Message.Label, ex.GetType(), ex.Message);
                    return OperationResult.PermanentFailure;
                }
            }

            catch (Exception ex)
            {
                Type exceptionType = ex.GetType();
                if (exceptionType == typeof(TimeoutException))
                {
                    Console.WriteLine("MessagePump: Exception: {0}", exceptionType);
                    return OperationResult.WaitAndRetry;
                }
                else
                {
                    Console.WriteLine("MessagePump: Exception: {0}", exceptionType);
                    return OperationResult.PermanentFailure;
                }
            }
        }

        OperationResult ExecuteEndDeadLetter(IAsyncResult result)
        {
            AsyncArguments arg = (AsyncArguments)result.AsyncState;

            try
            {
                arg.Receiver.EndDeadLetter(result);

                // FOR TESTING PURPOSE ONLY: Inject Service Bus error.
                faultInjector.InjectFaultAfterDeadLetteringMessage();

                return OperationResult.Success;
            }
            catch (MessagingException ex)
            {
                if (ex.IsTransient)
                {
                    Console.WriteLine("MessagePump: Transient exception when deadlettering message {0}: {1} {2}", ((AsyncArguments)result.AsyncState).Message.Label, ex.GetType(), ex.Message);
                    return OperationResult.WaitAndRetry;
                }
                else
                {
                    Console.WriteLine("MessagePump: Permanent exception when deadlettering message {0}: {1} {2}", ((AsyncArguments)result.AsyncState).Message.Label, ex.GetType(), ex.Message);
                    return OperationResult.PermanentFailure;
                }
            }

            catch (Exception ex)
            {
                Type exceptionType = ex.GetType();
                if (exceptionType == typeof(TimeoutException))
                {
                    Console.WriteLine("MessagePump: Exception: {0}", exceptionType);
                    return OperationResult.WaitAndRetry;
                }
                else
                {
                    Console.WriteLine("MessagePump: Exception: {0}", exceptionType);
                    return OperationResult.PermanentFailure;
                }
            }
        }

        // Timer expired. Resume to receive messages from source queue.
        void ProcessTimerReceive(Object stateInfo)
        {
            AsyncArguments arg = (AsyncArguments)stateInfo;
            Console.WriteLine("MessagePump: Resume receiving message.");
            arg.Timer.Dispose();
            this.ProcessBeginReceive(arg);
        }

        // Timer expired. Resume to send cloned message to destination queue.
        void ProcessTimerSend(Object stateInfo)
        {
            AsyncArguments arg = (AsyncArguments)stateInfo;
            Console.WriteLine("MessagePump: Resume sending message.");
            arg.Timer.Dispose();
            this.ProcessBeginSend(arg);
        }

        // Timer expired. Resume to complete message.
        void ProcessTimerComplete(Object stateInfo)
        {
            AsyncArguments arg = (AsyncArguments)stateInfo;
            Console.WriteLine("MessagePump: Resume completing message.");
            arg.Timer.Dispose();
            this.ProcessBeginComplete(arg);
        }

        // Timer expired. Resume to deadletter message.
        void ProcessTimerDeadLetter(Object stateInfo)
        {
            AsyncArguments arg = (AsyncArguments)stateInfo;
            Console.WriteLine("MessagePump: Resume deadlettering message.");
            arg.Timer.Dispose();
            this.ProcessBeginDeadLetter(arg);
        }

        // Clone BrokeredMessage including system properties.
        BrokeredMessage CloneBrokeredMessage(BrokeredMessage source)
        {
            BrokeredMessage destination = source.Clone();
            destination.ContentType = source.ContentType;
            destination.CorrelationId = source.CorrelationId;
            destination.Label = source.Label;
            destination.MessageId = source.MessageId;
            destination.ReplyTo = source.ReplyTo;
            destination.ReplyToSessionId = source.ReplyToSessionId;
            destination.ScheduledEnqueueTimeUtc = source.ScheduledEnqueueTimeUtc;
            destination.SessionId = source.SessionId;
            destination.TimeToLive = source.TimeToLive;
            destination.To = source.To;

            return destination;
        }
    }
}
