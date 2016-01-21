namespace MessagingSamples
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    class Program : IBasicQueueSendReceiveSample
    {

        public async Task Run(string namespaceAddress, string queueName, string sendToken, string receiveToken)
        {
            // ***************************************************************************************
            // This sample demonstrates how to use MessagePeek feature to look into the content of 
            // Service bus entities (Queues , Subscriptions).
            // ***************************************************************************************

            var senderMessagingFactory = await MessagingFactory.CreateAsync(namespaceAddress, TokenProvider.CreateSharedAccessSignatureTokenProvider(sendToken));
            var sender = await senderMessagingFactory.CreateMessageSenderAsync(queueName);

            var receiverMessagingFactory = await MessagingFactory.CreateAsync(namespaceAddress, TokenProvider.CreateSharedAccessSignatureTokenProvider(receiveToken));
            var receiver = await receiverMessagingFactory.CreateMessageReceiverAsync(queueName, ReceiveMode.PeekLock);


            await sender.SendAsync(new BrokeredMessage("Test1") { TimeToLive = TimeSpan.FromMinutes(1)});
            await sender.SendAsync(new BrokeredMessage("Test2") { TimeToLive = TimeSpan.FromMinutes(1) });
            await sender.SendAsync(new BrokeredMessage("Test3") { TimeToLive = TimeSpan.FromMinutes(1) });

            while (true)
            {
                BrokeredMessage msg = await receiver.PeekAsync();
                if (msg != null)
                {
                    Console.WriteLine("{0} {1} - {2} - {3}", msg.EnqueuedTimeUtc.ToLocalTime().ToShortDateString(), 
                                                             msg.EnqueuedTimeUtc.ToLocalTime().ToLongTimeString(), 
                                                             msg.SequenceNumber, msg.Label);
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

            senderMessagingFactory.Close();
            receiverMessagingFactory.Close();
            Console.WriteLine("Press [Enter] to quit...");
            Console.ReadLine();
        }

    }
}
