# elFinder.Net.Core
<img src="https://raw.githubusercontent.com/trannamtrung1st/elFinder.Net.Core/main/Assets/logo.png" alt="Logo" width="100px" />

## Instruction
1. Install the NuGet package: https://www.nuget.org/packages/elFinder.Net.Core/
2. Look at the [demo project](https://github.com/trannamtrung1st/elFinder.Net.Core/tree/main/elFinder.Net.Core/elFinder.Net.Demo31) for an example of how to integrate it into your own web project. (the example uses ASP.NET Core 3.1 and some additional packages listed below).

## About this repository  
There are 3 main projects:
- **elFinder.Net.Core**: the core backend connector for elFinder.
- **elFinder.Net.AspNetCore**: enable ASP.NET Core 2.2 projects to easily integrate the connector package.
- **elFinder.Net.Drivers.FileSystem**: the default Local File System driver.

## Credits
**elFinder.Net.Core** is based on the project [elFinder.NetCore](https://github.com/gordon-matt/elFinder.NetCore) of Matt Gordon. Many thanks for the excellent works.
For those who may get confused about which package to use, try and find the one that best suits your project.
I create this with some modification that suits my use cases and the repository is **currently active**. Some of the main differences are:
- Enable better Security, ACL (both for Frontend and Backend) 
- Support .NET Standard 2.0 (remove ASP.NET Core dependency).
- Enable customization and logging by using model classes instead of directly returning the IActionResult.
- Support more commands.
- Follow the specification from https://github.com/Studio-42/elFinder/wiki more strictly.
- For more examples, please see the [demo project](https://github.com/trannamtrung1st/elFinder.Net.Core/tree/main/elFinder.Net.Core/elFinder.Net.Demo31).
