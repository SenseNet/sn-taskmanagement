# Task Management Agent
The agent process is started by the service (the configured number of instances). This process connects to the central task management web application (via SignalR) and waits for new tasks to arrive.

When a new task comes in, all *free* agents (the ones that did not start a task recently) will try to get it and one of them will be able to lock it for itself.

The agent then starts the appropriate task executor (e.g. a preview generator command line tool) with the received task details. The agent is responsible for tracking task execution (e.g. ensuring that the tool does not time out) and for communicating with the central web app.

For details and extensibility points please visit the following articles.

- [Task Management overview](http://wiki.sensenet.com/Task_Management)
- [Task Management for Developers](http://wiki.sensenet.com/Task_Management_-_for_Developers)
- [Deployment](http://wiki.sensenet.com/Task_Management_deployment) 