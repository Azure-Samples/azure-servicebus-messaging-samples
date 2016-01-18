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
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Microsoft.ServiceBus.HttpClient
{
    public class Program
    {
        public const string ApiVersion = "&api-version=2012-03"; // This api version works for current Azure Service and Service Bsu Server 1.0 and later.
        public const string QueueName = "HttpClientSampleQueue";

        static void Main(string[] args)
        {

            // BE AWARE THAT HARDCODING YOUR NAMESPACE KEY IS A SECURITY RISK IF YOU SHARE THIS CODE.

            // Service bus authentication methods are described in http://msdn.microsoft.com/en-us/library/windowsazure/dn155925.aspx.

            /* ACS TOKEN FOR SERVICE */
            // Get a WRAP token from ACS for the specified Azure Service Bus namespace. Get the namespace key from the Azure portal: Go to 
            // the list of Service Bus namespaces, mark the namespace and then press the Connection Information button at the bottom of the page.
            const string Namespace = "YOUR_NAMESPACE";
            const string NamespaceOwner = "owner";
            const string NamespaceKey = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";
            string token = GetAcsToken(Namespace, NamespaceOwner, NamespaceKey);
            string baseAddressHttp = "https://" + Namespace + ".servicebus.windows.net";

            /* SHARED SECRET TOKEN FOR SERVICE
            // Create a shared secret token for the specified Azure Service Bus namespace. The token provider calls ACS.
            // Get the namespace key from the Azure portal: Go to the list of Service Bus namespaces, mark the namespace
            // and then press the Connection Information button at the bottom of the page.
            const string Namespace = "YOUR_NAMESPACE";
            const string NamespaceOwner = "owner";
            const string NamespaceKey = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";
            string baseAddressHttp = "https://" + Namespace + ".servicebus.windows.net";
            string token = GetSharedSecretToken(baseAddressHttp, NamespaceOwner, NamespaceKey);
            */

            /* SAS TOKEN FOR SERVICE
            // Create SAS token for service. Get the primary or secondary key from the Azure portal: Go to the namespace page and click
            // the Configure tab at the top of the page. Select the appropriate key in the Policy Name field. You can either use the primary
            // or the secondary key. You can use the SharedAccessSignatureTokenProvider or construct the token yourself.
            // SAS tokens are described in http://msdn.microsoft.com/en-us/library/windowsazure/dn170477.aspx.
            const string Namespace = "YOUR_NAMESPACE";
            const string KeyName = "RootManageSharedAccessKey";
            const string Key = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";
            string baseAddressHttp = "https://" + Namespace + ".servicebus.windows.net";
            string token = GetSasToken1(baseAddressHttp, KeyName, Key); // First method to generate SAS token.
            //string token = GetSasToken2(baseAddressHttp, KeyName, Key); // Second method to generate SAS token.
            */

            /* SAS TOKEN FOR SERVER
            // Create SAS token for server 1.1. (SAS is not available for Server 1.0.) Run the cmdlet Get-SBAuthorizationRule on the server to get the namespace
            // key and key name. If you are using Windows Server Service Bus and this client runs on a different machine than Service Bus, import the
            // server certificate to the client machine as described in http://msdn.microsoft.com/en-us/library/windowsazure/jj192993(v=azure.10).aspx.
            // This sample implements two method to generate a SAS token: GetSasToken1() and GetSasToken2(). You can use either one.
            // You can use the SharedAccessSignatureTokenProvider or construct the token yourself.
            // SAS tokens are described in http://msdn.microsoft.com/en-us/library/windowsazure/dn170477.aspx.
            const string ServerName = "YOUR_SERVER_FQDN";
            const string Namespace = "ServiceBusDefaultNamespace";
            const string KeyName = "RootManageSharedAccessKey";
            const string NamespaceKey = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX=";
            string baseAddressHttp = "https://" + ServerName + ":9355/" + Namespace;
            string token = GetSasToken1(baseAddressHttp, KeyName, NamespaceKey); // First method to generate SAS token.
            //string token = GetSasToken2("http://" + ServerName + "/" + Namespace + "/", KeyName, NamespaceKey); // Second method to generate SAS token.
            */

            /* OAUTH TOKEN FOR SERVER
            // Request OAuth token from server 1.0 or 1.1. Supply username and password. If you are using Windows Server Service Bus and this client runs on a different machine
            // than Service Bus, import the server certificate to the client machine as described in http://msdn.microsoft.com/en-us/library/windowsazure/jj192993(v=azure.10).aspx.
            const string ServerName = "YOUR_SERVER_FQDN";
            const string Namespace = "ServiceBusDefaultNamespace";
            string Username = Environment.UserName + "@" + Environment.GetEnvironmentVariable("USERDNSDOMAIN");
            const string Password = "XXXXXXXXXXXXXXXX";
            string baseAddressHttp = "https://" + ServerName + ":9355/" + Namespace;
            string token = GetOAuthToken(baseAddressHttp, baseAddressHttp, Username, Password);
            */

            string queueAddress = baseAddressHttp + "/" + QueueName;

            // Create queue of size 2GB. Specify a default TTL of 2 minutes. Time durations
            // are formatted according to ISO 8610 (see http://en.wikipedia.org/wiki/ISO_8601#Durations).
            // IMPORTANT: QueueDescription properties must be specified in alphabetically order.
            Console.WriteLine("Creating queue ...");
            byte[] queueDescription = Encoding.UTF8.GetBytes("<entry xmlns='http://www.w3.org/2005/Atom'><content type='application/xml'>"
                + "<QueueDescription xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.microsoft.com/netservices/2010/10/servicebus/connect\">"
                + "<DefaultMessageTimeToLive>PT2M</DefaultMessageTimeToLive>"
                + "<MaxSizeInMegabytes>2048</MaxSizeInMegabytes>"
                + "</QueueDescription></content></entry>");
            HttpHelper.CreateEntity(queueAddress, token, queueDescription);

            // Send message. Add broker properties by specifying JSON string.
            Console.WriteLine("Sending message ...");
            WebClient webClient = new WebClient();
            webClient.Headers[HttpRequestHeader.Authorization] = token;
            webClient.Headers[HttpRequestHeader.ContentType] = "application/atom+xml;type=entry;charset=utf-8";
            webClient.Headers.Add("BrokerProperties", "{ \"TimeToLive\":30, \"Label\":\"M1\"}");
            webClient.Headers.Add("Priority", "High");
            webClient.Headers.Add("Customer", "12345");
            webClient.UploadData(queueAddress + "/messages" + "?timeout=60&" + ApiVersion, "POST", Encoding.UTF8.GetBytes("This is the first message."));

             // Receive and delete message from queue.
            Console.WriteLine("Receiving message ...");
            ServiceBusHttpMessage receiveMessage1 = HttpHelper.ReceiveAndDeleteMessage(queueAddress, token, ApiVersion);
            ProcessMessage(receiveMessage1);

            Console.WriteLine("Press ENTER to exit...");
            Console.ReadLine();

            // Delete topic.
            Console.WriteLine("Deleting queue ...");
            HttpHelper.DeleteEntity(queueAddress, token);
        }

        static void ProcessMessage(ServiceBusHttpMessage message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Body           : " + Encoding.UTF8.GetString(message.body));
            Console.WriteLine("Message ID     : " + message.brokerProperties.MessageId);
            Console.WriteLine("Label          : " + message.brokerProperties.Label);
            Console.WriteLine("SeqNum         : " + message.brokerProperties.SequenceNumber);
            Console.WriteLine("TTL            : " + message.brokerProperties.TimeToLive + " seconds");
            Console.WriteLine("Locked until   : " + ((message.brokerProperties.LockedUntilUtcDateTime ==null) ? "unlocked" : (message.brokerProperties.LockedUntilUtcDateTime + " UTC")));
            foreach (string key in message.customProperties.AllKeys)
            {
                Console.WriteLine("Custom property: " + key + " = " + message.customProperties[key]);
            }
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        // This method calls the Azure Access Control Service (ACS) to obtain a WRAP token
        // that grants the bearer to perform on operation on the specified namespace.
        static string GetAcsToken(string serviceNamespace, string issuerName, string issuerSecret)
        {
            var acsEndpoint = "https://" + serviceNamespace + "-sb.accesscontrol.windows.net/WRAPv0.9/";

            // Note that the realm used when requesting a token uses the HTTP scheme,
            // even though calls to the service are always issued over HTTPS.
            var realm = "http://" + serviceNamespace + ".servicebus.windows.net/";

            NameValueCollection values = new NameValueCollection();
            values.Add("wrap_name", issuerName);
            values.Add("wrap_password", issuerSecret);
            values.Add("wrap_scope", realm);

            WebClient webClient = new WebClient();
            byte[] response = webClient.UploadValues(acsEndpoint, values);

            string responseString = Encoding.UTF8.GetString(response);

            var responseProperties = responseString.Split('&');
            var tokenProperty = responseProperties[0].Split('=');
            var token = Uri.UnescapeDataString(tokenProperty[1]);

            Console.WriteLine("Token: " + token);
            return "WRAP access_token=\"" + token + "\"";
        }

        // This method uses the Service Bus SharedSecretTokenProvider to generate a WRAP token
        // that grants the bearer to perform on operation on the specified namespace.
        static string GetSharedSecretToken(string serviceNamespace, string issuerName, string issuerSecret)
        {
            TokenProvider tokenProvider = TokenProvider.CreateSharedSecretTokenProvider(issuerName, issuerSecret);
            string token = string.Empty;

            try
            {
                token = tokenProvider.EndGetWebToken(tokenProvider.BeginGetWebToken(serviceNamespace, "Manage", false, TimeSpan.FromMinutes(20), null, null));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception on token acquisition: {0}", ex);
                throw;
            }

            Console.WriteLine("Token: " + token);
            return token;
        }

        // This method generates a SAS token using the Service Bus SharedAccessSignatureTokenProvider.
        // SAS tokens are described in http://msdn.microsoft.com/en-us/library/windowsazure/dn170477.aspx.
        static string GetSasToken1(string uri, string keyName, string key)
        {
            TokenProvider tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(keyName, key);
            string token = string.Empty;

            try
            {
                token = tokenProvider.EndGetWebToken(tokenProvider.BeginGetWebToken(uri, "Manage", false, TimeSpan.FromMinutes(20), null, null));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception on token acquisition: {0}", ex);
                throw;
            }

            Console.WriteLine("Token: " + token);
            return token;
        }

        // This method creates a SAS token. This is an alternative to using GetSasToken1() method.
        // SAS tokens are described in http://msdn.microsoft.com/en-us/library/windowsazure/dn170477.aspx.
        static string GetSasToken2(string uri, string keyName, string key)
        {
            var expiry = GetExpiry(1200); // Set token lifetime to 20 minutes.
            string stringToSign = System.Web.HttpUtility.UrlEncode(uri) + "\n" + expiry;
            HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));

            string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            string token = String.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}",
                HttpUtility.UrlEncode(uri), HttpUtility.UrlEncode(signature), expiry, keyName);
            Console.WriteLine("Token: " + token);
            return token;
        }

        static string GetOAuthToken(string stsAddress, string scope, string userName, string userPassword)
        {
            const string OAuthTokenServicePath = "/$STS/OAuth/";
            const string ClientPasswordFormat = "grant_type=authorization_code&client_id={0}&client_secret={1}&scope={2}";

            string requestUri = stsAddress + OAuthTokenServicePath;
            string requestContent = string.Format(CultureInfo.InvariantCulture, ClientPasswordFormat, HttpUtility.UrlEncode(userName), HttpUtility.UrlEncode(userPassword), HttpUtility.UrlEncode(scope));
            byte[] body = Encoding.UTF8.GetBytes(requestContent);

            HttpWebRequest request = WebRequest.Create(requestUri) as HttpWebRequest;
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 1;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = body.Length;
            request.Timeout = Convert.ToInt32(60*1000, CultureInfo.InvariantCulture);

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(body, 0, body.Length);
            }

            string rawAccessToken = null;
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                using (Stream stream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        rawAccessToken = reader.ReadToEnd();
                    }
                }
            }

            string token = string.Format(CultureInfo.InvariantCulture, "WRAP access_token=\"{0}\"", rawAccessToken);
            Console.WriteLine("Token: " + token);
            return token;
        }

        static uint GetExpiry(uint tokenLifetimeInSeconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            TimeSpan diff = DateTime.Now.ToUniversalTime() - origin;
            return Convert.ToUInt32(diff.TotalSeconds) + tokenLifetimeInSeconds;
        }
    }
}
