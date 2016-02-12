# Atomic Transactions with Service Bus

This sample illustrates how to use Azure Service Bus atomic transaction support by implementing a 
travel booking scenario using the [Saga pattern](http://kellabyte.com/2012/05/30/clarifying-the-saga-pattern/)
first formulated by [Hector Garcia Molina and Kenneth Salem [PDF]](http://www.cs.cornell.edu/andru/cs711/2002fa/reading/sagas.pdf) 
in 1987 as a form of a long-lived transaction.     

Mind that the sample is of substantial complexity and primarily aimed at developers building frameworks leaning 
on Azure Service Bus for creating robust foundations for business applications in the cloud and therefore the sample 
code is very intentionally not "frameworked-over" with a smooth abstraction for hosting the simulated business logic,
since the focus is on showing the interactions with the platform. 

You can most certainly use the presented capabilities directly in a business application if you wish.

In this document we will discuss the transactional capabilities of Service Bus first, then briefly discuss Sagas (you 
are encouraged to review the blog article and the paper linked above for more depth) and how we project the concept
onto Service Bus, and then we'll take a look at the code.  

##Prerequisites
If you haven't already done so, please read the release notes document that explains how to sign up for a Azure account and how to configure your environment.

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

Scenario 1: Send/Complete in a Transaction and then Complet
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