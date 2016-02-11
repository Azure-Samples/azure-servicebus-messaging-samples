namespace MessagingSamples
{
    using System;
    using System.Collections;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus.Messaging;

    class SagaTaskManager : IEnumerable
    {
        readonly MessagingFactory messagingFactory;
        CancellationToken cancellationToken;
        Collection<Task> tasks = new Collection<Task>();

        public SagaTaskManager(MessagingFactory messagingFactory, CancellationToken cancellationToken)
        {
            this.messagingFactory = messagingFactory;
            this.cancellationToken = cancellationToken;
        }

        public void Add(string taskQueueName, Func<BrokeredMessage, MessageSender, MessageSender, Task> doWork, string nextStepQueue, string compensatorQueue)
        {
            var tcs = new TaskCompletionSource<bool>();
            var rcv = this.messagingFactory.CreateMessageReceiver(taskQueueName);
            var nextStepSender = this.messagingFactory.CreateMessageSender(nextStepQueue, taskQueueName);
            var compensatorSender = this.messagingFactory.CreateMessageSender(compensatorQueue, taskQueueName);

            this.cancellationToken.Register(() => { rcv.Close(); tcs.SetResult(true); });
            rcv.OnMessageAsync((m)=>doWork(m, nextStepSender, compensatorSender), new OnMessageOptions { AutoComplete = false });
            this.tasks.Add(tcs.Task);
        }

        public Task Task => Task.WhenAll(this.tasks);
        public IEnumerator GetEnumerator()
        {
            return this.tasks.GetEnumerator();
        }
    }
}