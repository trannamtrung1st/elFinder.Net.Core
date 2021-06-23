# elFinder.Net.Core
<img src="https://raw.githubusercontent.com/trannamtrung1st/elFinder.Net.Core/main/Assets/logo.png" alt="Logo" width="100px" />

## Getting Started
1. Install the NuGet package: https://www.nuget.org/packages/elFinder.Net.Core/
2. Look at the [basic demo project](https://github.com/trannamtrung1st/elFinder.Net.Core/tree/main/elFinder.Net.Core/Demos/elFinder.Net.Demo31) for an example of how to integrate it into your web project. (the example uses ASP.NET Core 3.1 and some additional packages listed below).

## Advanced
The [advanced demo project](https://github.com/trannamtrung1st/elFinder.Net.Core/tree/main/elFinder.Net.Core/Demos/elFinder.Net.AdvancedDemo) has some additional use cases enabled, including:
- Integrate Authentication/Authorization (Cookies, OAuth2 JWT).
- Multi-tenant support.
- Integrate [Quota management plugin](https://github.com/trannamtrung1st/elFinder.Net.Core/tree/main/elFinder.Net.Core/Plugins/elFinder.Net.Plugins.FileSystemQuotaManagement).
- For an example of how to write a plugin, see [Logging plugin example](https://github.com/trannamtrung1st/elFinder.Net.Core/tree/main/elFinder.Net.Core/Plugins/elFinder.Net.Plugins.LoggingExample). 
This plugin intercepts all method calls of `IConnector` and `IDriver` instances then logs the method's information (arguments, method name, return value, .etc) to the console output.

## Customization
Since file management is a complex topic and the requirements are diverse, here are some ways to customize the library:
1. Override the default implementation
2. Use interceptors (as those plugins here which use [Castle DynamicProxy](http://www.castleproject.org/projects/dynamicproxy/)) and built-in .NET Core DI container

Some important classes and their descriptions:
- `IConnector/Connector`: the backend connector which handles elFinder commands sent from clients.
- `IDriver/FileSystemDriver`: the driver which provides a storage mechanism (in this case, the OS file system). 
There are other drivers for different storage, e.g, [elFinder AzureStorage](https://github.com/fsmirne/elFinder.NetCore.AzureStorage).
- `IFile/FileSystemFile; IDirectory/FileSystemDirectory`: the file system's abstractions/implementations.
- Others: please download the repository, then run the demo projects. They should walk through all of the important classes.

## About this repository  
There are 3 main projects:
- **elFinder.Net.Core**: the core backend connector for elFinder.
- **elFinder.Net.AspNetCore**: enable ASP.NET Core 2.2 projects to easily integrate the connector package.
- **elFinder.Net.Drivers.FileSystem**: the default Local File System driver.

Plugins:
- **Plugins/elFinder.Net.Plugins.FileSystemQuotaManagement**: enable quota management and restriction features.

## Credits
**elFinder.Net.Core** is based on the project [elFinder.NetCore](https://github.com/gordon-matt/elFinder.NetCore) of Matt Gordon. Many thanks for the excellent works.
For those who may get confused about which package to use, try and find the one that best suits your project.
I create this with some modification that suits my use cases and the repository is **currently active**. Some of the main differences are:
- Enable better security, ACL (for both Frontend and Backend) 
- Support .NET Standard 2.0 (remove ASP.NET Core dependency).
- Customizable through models and interfaces
- More features and extensibility: plugins, interceptors, etc.
- Support more commands.
- Follow the specification from https://github.com/Studio-42/elFinder/wiki more strictly.
