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
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Microsoft.ServiceBus.HttpClient
{
    class HttpHelper
    {
        public static void CreateEntity(string address, string token, byte[] entityDescription)
        {
            WebClient webClient = CreateWebClient(token);
            try
            {
                webClient.UploadData(address + "?timeout=60", "PUT", entityDescription);
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (response != null)
                    {
                        Console.WriteLine("HTTP Status Code: " + (int)response.StatusCode);
                        if ((int)response.StatusCode == 409)
                        {
                            Console.WriteLine("Entity " + address + " already exists.");
                        }
                    }
                }
            }
        }

        public static void DeleteEntity(string address, string token)
        {
            WebClient webClient = CreateWebClient(token);
            webClient.UploadData(address + "?timeout=60", "DELETE", new byte[0]);
        }

        public static void SendMessage(string address, string token, ServiceBusHttpMessage message, string apiVersion)
        {
            WebClient webClient = CreateWebClient(token);

            // Serialize BrokerProperties.
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(BrokerProperties));
            MemoryStream ms = new MemoryStream();
            serializer.WriteObject(ms, message.brokerProperties);
            webClient.Headers.Add("BrokerProperties", Encoding.UTF8.GetString(ms.ToArray()));
            ms.Close();

            // Add custom properties.
            if (message.customProperties != null)
            {
                webClient.Headers.Add(message.customProperties);
            }
            webClient.UploadData(address + "/messages" + "?timeout=60" + apiVersion, "POST", message.body);
        }

        public static ServiceBusHttpMessage ReceiveMessage(string address, string token, string apiVersion)
        {
            return Receive("POST", address, token, apiVersion);
        }

        public static ServiceBusHttpMessage ReceiveAndDeleteMessage(string address, string token, string apiVersion)
        {
            return Receive("DELETE", address, token, apiVersion);
        }

        private static ServiceBusHttpMessage Receive(string HttpVerb, string address, string token, string apiVersion)
        {
            WebClient webClient = CreateWebClient(token);
            ServiceBusHttpMessage message = new ServiceBusHttpMessage();
            message.body = webClient.UploadData(address + "/messages/head?timeout=60" + apiVersion, HttpVerb, new byte[0]);
            WebHeaderCollection responseHeaders = webClient.ResponseHeaders;
            
            // Deserialize BrokerProperties.
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(BrokerProperties));
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(responseHeaders["BrokerProperties"])))
            {
                message.brokerProperties = (BrokerProperties)serializer.ReadObject(ms);
            }

            // Get custom propoerties.
            foreach( string key in responseHeaders.AllKeys)
            {
                if (!key.Equals("Transfer-Encoding") && !key.Equals("BrokerProperties") && !key.Equals("Content-Type") && !key.Equals("Location") && !key.Equals("Date") && !key.Equals("Server"))
                {
                    message.customProperties.Add(key, responseHeaders[key]);
                }
            }

            return message;
        }

        public static byte[] DeleteMessage(string address, string token, Guid messageId, Guid LockId)
        {
            WebClient webClient = CreateWebClient(token);
            return webClient.UploadData(address + "/messages/" + messageId.ToString() + "/" + LockId.ToString(), "DELETE", new byte[0]);
        }

        private static WebClient CreateWebClient(string token)
        {
            WebClient webClient = new WebClient();
            webClient.Headers[HttpRequestHeader.Authorization] = token;
            webClient.Headers[HttpRequestHeader.ContentType] = "application/atom+xml;type=entry;charset=utf-8";
            return webClient;
        }
    }
}
