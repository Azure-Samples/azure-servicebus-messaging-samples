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
    // This class contains all parameters that are required to receive, send and complete a single message.
    // For each message that is pumped, the message pump creates one instance of this class.

    class AsyncArguments
    {
        public MessageReceiver Receiver { get; private set; }
        public MessageSender Sender { get; private set; }
        public BrokeredMessage Message { get; set; }
        public Guid LockToken { get; set; }
        public Timer Timer { get; set; }

        public AsyncArguments(MessageReceiver messageReceiver, MessageSender messageSender, BrokeredMessage message, Guid lockToken)
        {
            this.Receiver = messageReceiver;
            this.Sender = messageSender;
            this.Message = message;
            this.LockToken = lockToken;
            this.Timer = null;
        }

        public AsyncArguments(MessageReceiver messageReceiver, MessageSender messageSender)
        {
            this.Receiver = messageReceiver;
            this.Sender = messageSender;
            this.Message = null;
            this.LockToken = Guid.Empty;
            this.Timer = null;
        }

        public AsyncArguments(AsyncArguments arg)
        {
            this.Receiver = arg.Receiver;
            this.Sender = arg.Sender;
            this.Message = null;
            this.LockToken = Guid.Empty;
            this.Timer = null;
        }
    }
}
