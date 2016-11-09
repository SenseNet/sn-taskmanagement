# Task Management Service

This is a *Windows service* that keeps task agents up and running. The only configuration it needs is the number of agents you want it to keep alive. When you stop this service, it will stop all the running agent processes started by itself.

Task Management is **scalable** because you can deploy any number of **agent machines** with this service installed and registered.

For details and extensibility points please visit the following articles.

- [Task Management overview](http://wiki.sensenet.com/Task_Management)
- [Task Management for Developers](http://wiki.sensenet.com/Task_Management_-_for_Developers)
- [Deployment](http://wiki.sensenet.com/Task_Management_deployment) 