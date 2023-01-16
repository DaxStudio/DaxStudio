## Build Status

| Branch | Status |
|---|---|
| master | ![Master Build Status](https://ci.appveyor.com/api/projects/status/sl6fd3oelckihp3p?svg=true) |
| develop | ![Develop Build Status](https://ci.appveyor.com/api/projects/status/sl6fd3oelckihp3p/branch/develop?svg=true) ![](https://img.shields.io/appveyor/tests/darrengosbell/daxstudio/develop.svg?style=flat-square) |

## Building Dax Studio

All of the dependencies for DAX Studio are available as nuget packages, 
so doing a nuget restore should be enough to build this solution in Visual Studio 2022

When preparing to make changes in order to submit a pull request you should create a feature
branch off the `develop` branch. The develop branch contains the current development build of the code
including any new features. The master branch only contains the code for the last stable release. 
(we merge from develop to master when doing a public release)

See the following for details about [debugging](debugging) DAX Studio
