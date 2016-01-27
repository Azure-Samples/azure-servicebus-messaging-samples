#Azure Service BusMessaging samples

This repository contains the official set of samples for the Azure Service Bus Messaging service and
Service Bus for Windows Server. 

Fork and play!


##Requirements and Setup

These samples run against the cloud service and require that you have an active Azure subscription available 
for use. If you do not have a subscription, [sign up for a free trial](https://azure.microsoft.com/pricing/free-trial/), 
which will give you **ample** credit to experiment with Service Bus Messaging. 
  
Tghe samples assume that you are running on a supported 
Windows version and have a .NET Framework 4.5+ build environment available. [Visual Studio 2015](https://www.visualstudio.com/) is recommended to 
explore the samples; the free community edition will work just fine.    

To run the samples, you must perform a few setup steps, including creating and configuring a Service Bus Namespace. 
For the required [setup.ps1](setup.ps1) and [cleanup.ps1](cleanup.ps1) scripts, **you must have Azure Powershell installed** 
([if you don't here's how](https://azure.microsoft.com/en-us/documentation/articles/powershell-install-configure/)) and 
run these scripts from the Azure Powershell environment.

### Setup      
The [setup.ps1](setup.ps1) script will either use the account and subscription you have previously configured for your Azure Powershell environment
or prompt you to log in and, if you have multiple subscriptions associated wiuth your account, select a subscription. 

The script will then create a new Azure Service Bus Namespace for running the samples and configure it with shared access signature (SAS) rules
granting send, listen, and management access to the new namespace. The configuration settings are stored in the file "azure-msg-config.properties", 
which is placed into the user profile directory on your machine. All samples use the same [entry-point boilerplate code](common/Main.cs) that 
retrieves the settings from this file and then launches the sample code. The upside of this approach is that you will never have live credentials 
left in configuration files or in code that you might accidentally check in when you fork this repository and experiment with it.   

### Cleanup

The [cleanup.ps1](cleanup.ps1) script removes the created Service Bus Namespace and deletes the "azure-relay-config.properties" file from 
your user profile directory.
 
## Common Considerations

Most samples use shared [entry-point boilerplate code](common/Main.cs) that loads the configuration and then launches the sample's 
**Program.Run()** instance methods. 

Except for the samples that explicitly demonstrate security capabilities, all samples are invoked with an externally issued SAS token 
rather than a connection string or a raw SAS key. The security model design of Service Bus generally prefers clients to handle tokens 
rather than keys, because tokens can be constrained to a particular scope and can be issued to expire at a certain time. 
More about SAS and tokens can be found [here](https://azure.microsoft.com/documentation/articles/service-bus-shared-access-signature-authentication/).               

##Samples

* **Test** - The [Test](Test) sample shows ...