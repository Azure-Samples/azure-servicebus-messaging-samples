//---------------------------------------------------------------------------------
// Copyright (c) 2012, Microsoft Corporation
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

namespace Microsoft.Samples.BrokeredMessagingGeoReplication
{
    using System;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    public class ManagementObjects
    {
        // Connection string of your Azure Service Bus or Windows Server Service Bus namespace. For Azure Service Bus
        // namespaces, get your namespace from the Azure portal. For  Windows Server Service Bus namespace, run the
        // cmdlet Get-SBClientConfiguration on the server.
        //
        // If yu are using Windows Server Service Bus and this client runs on a different machine than Service Bus,
        // import the server certificate to the client machine as described in
        // http://msdn.microsoft.com/en-us/library/windowsazure/jj192993(v=azure.10).aspx.
        //
        // BE AWARE THAT HARDCODING YOUR CONNECTION STRING IS A SECURITY RISK IF YOU SHARE THIS CODE.
        public const string primaryConnectionString = "Endpoint=sb://YOUR-PRIMARY-NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";
        public const string secondaryConnectionString = "Endpoint=sb://YOUR_SECONDARY-NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";

        string replica;
        ServiceBusConnectionStringBuilder connBuilder;
        NamespaceManager namespaceManager;
        
        public ManagementObjects(string r)
        {
            this.replica = r;

            if (this.replica.Equals("primary"))
            {
                this.connBuilder = new ServiceBusConnectionStringBuilder(primaryConnectionString);
            }
            else
            {
                this.connBuilder = new ServiceBusConnectionStringBuilder(secondaryConnectionString);
            }

            this.namespaceManager = NamespaceManager.CreateFromConnectionString(this.connBuilder.ToString());
        }

        public QueueDescription CreateQueue(string queueName)
        {
            QueueDescription queueDesc;
            if (this.namespaceManager.QueueExists(queueName))
            {
                queueDesc = this.namespaceManager.GetQueue(queueName);
            }
            else
            {
                queueDesc = this.namespaceManager.CreateQueue(queueName);
            }
            return queueDesc;
        }

        public void DeleteQueue(string queueName)
        {
            if (this.namespaceManager.QueueExists(queueName))
            {
                this.namespaceManager.DeleteQueue(queueName);
            }
        }

        public MessagingFactory GetMessagingFactory()
        {
            return MessagingFactory.CreateFromConnectionString(this.connBuilder.ToString());
        }
    }
}