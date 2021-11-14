## Build Status

| Branch | Status |
|---|---|
| master | ![Master Build Status](https://ci.appveyor.com/api/projects/status/sl6fd3oelckihp3p?svg=true) |
| develop | ![Develop Build Status](https://ci.appveyor.com/api/projects/status/sl6fd3oelckihp3p/branch/develop?svg=true) ![](https://img.shields.io/appveyor/tests/darrengosbell/daxstudio/develop.svg?style=flat-square) |

## Building Dax Studio

All of the dependencies for DAX Studio are available as nuget packages, 
so doing a nuget restore should be enough to build this solution in Visual Studio 2017

When preparing to make changes in order to submit a pull request you should create a feature
branch off the `develop` branch. The develop branch contains the current development build of the code
including any new features. The master branch only contains the code for the last stable release. 
(we merge from develop to master when doing a public release)

For details about [debugging](debugging)

## Editing the documentation locally

DAX Studio uses github-pages for daxstudio.org. If you have Windows 10 you can install a development
environment for the documentation site using WSL (Windows Subsystem for Linux)

Once you've added the WSL feature and installed an Ubuntu distro you need to open a BASH shell
and run the following commands:

```
sudo apt update
sudo apt upgrade
sudo apt install make gcc
sudo apt install build-essential
sudo apt install ruby ruby-all-dev
sudo apt install zlib1g-dev
sudo apt install g++
sudo gem install jekyll
sudo gem install bundler
sudo gem install github-pages 
bundle install
bundle update github-pages
```

Once you have the above dependencies installed, 
the following commands will run the DAX Studio site locally:

```
cd /mnt/c/<folder with DaxStudio git repo>/docs
bundler exec jekyll serve --force_polling
```
