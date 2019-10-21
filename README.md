# sensenet ECM Task Management

[![Join the chat at https://gitter.im/SenseNet/sn-taskmanagement](https://badges.gitter.im/SenseNet/sn-taskmanagement.svg)](https://gitter.im/SenseNet/sn-taskmanagement?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![NuGet](https://img.shields.io/nuget/v/SenseNet.TaskManagement.Core.svg)](https://www.nuget.org/packages/SenseNet.TaskManagement.Core)

Task Management is a .Net component for managing **long-running background tasks** in any application. It is a robust and **scalable** solution that is **extendable** with **3rd party task executors** designed for solving atomic background tasks (e.g. extracting a compressed file or generating preview images for a document).

- Takes off the load from web servers (because they are for serving client requests, not for performing resource-heavy tasks).
- Prevents the web process from crushing in case of 3rd party plugins (e.g. an out of memory issue with a text extractor tool should not kill the web process).
- Fully scalable, as you can deploy any number of agent machines to be able to perform more tasks at the same time.
- It can provide rich progress information about running tasks.

This project was developed as a supporting component for [sensenet ECM](https://github.com/SenseNet/sensenet), but can be used in conjunction with any application!

## Overview
Task Management consists of the following subcomponents:

1. **Task Management web application**: this is the central hub for registering tasks and performing callbacks when the execution has finished. When a task arrives, the web app notifies the *agents* (via *SignalR*) and lets one of them (the winner) take the new task.
2. **Agent**: this is the process that will start the appropriate task executor plugin for a certain task.
3. **Executor**: this is the command-line tool that actually performs the task - e.g. checks a document for viruses. This is the main extensibility point: you can create a custom executor, deploy it into the appropriate folder, and you can start registering your tasks right away.
3. **Service**: a Windows service that keeps the configured number of agents (3 by default) alive. You can deploy any number of *agent machines* with this service, agent and executors installed.

![Task Management architecture](http://wiki.sensenet.com/images/2/2b/Taskmanagement-communication.png "Task Management architecture")

Task Management *does not contain any built-in task executors*. It is only the framework that provides the environment for your custom task executors.

For details and extensibility points, please visit the following articles.

- [Task Management overview](http://wiki.sensenet.com/Task_Management)
- [Task Management for Developers](http://wiki.sensenet.com/Task_Management_-_for_Developers)
- [Deployment](http://wiki.sensenet.com/Task_Management_deployment) 
