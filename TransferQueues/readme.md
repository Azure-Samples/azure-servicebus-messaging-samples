#Introduction
This sample demonstrates how to use the Windows Azure Service Bus messaging features within a transaction scope in order to ensure batches of messaging operations are committed atomically. See the Service Bus documentation for more information about the Service Bus before exploring the samples.

This sample demonstrates: sending and completing messages within a transaction scope; committing and aborting transactions.

##Prerequisites
If you haven't already done so, please read the release notes document that explains how to sign up for a Windows Azure account and how to configure your environment.

##Sample Flow
The sample flows in the following manner:

Create a new queue on the Service Bus;
Send and complete messages within a transaction scope:
Send a plain text message to the newly created queue;
Peek lock the message from the queue;
Within a transaction scope, send a response message;
Within a transaction scope, complete the initial message;
Complete the transaction scope;
Receive the response message.
Send and complete messages within a transaction scope that rolls back:
Send a plain text message to the queue;
Peek lock the message from the queue;
Within a transaction scope, send a response message;
Within a transaction scope, complete the initial message;
Abandon the transaction scope;
Receive from the queue - since the transaction was not completed, the response message is not in the queue and the initial message is returned to the queue when its peek lock times out.
Clean up resources associated with the sample.

Running the Sample
To run the sample:

Build and run the sample in Visual Studio.
When prompted, enter your Service Bus connection string.
 

Expected Output

Please provide a connection string to Service Bus (? for help): <Your connection string>

Creating Queues...

Scenario 1: Send/Complete in a Transaction and then Complete
Sending Message 'Message 1'
Peek-Lock the Message... Message 1
Inside Transaction 5378d67d-19f1-4d27-affa-3e93841be2aa:1
Sending Reply in a Transaction
Completing message in a Transaction
Marking the Transaction Scope as Completed
Receive the reply... Reply To - Message 1

Press [Enter] to move to the next scenario.


Scenario 2: Send/Complete in a Transaction and do not Complete
Sending Message 'Message 2'
Peek-Lock the Message... Message 2
Inside Transaction 5378d67d-19f1-4d27-affa-3e93841be2aa:2
Sending Reply in a Transaction
Completing message in a Transaction
Exiting the transaction scope without committing...
Receive the request again (this can take a while, because we're waiting for the
PeekLock to timeout)... Message 2

Press [Enter] to exit.