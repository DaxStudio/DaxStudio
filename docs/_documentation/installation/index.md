---
title: Installation
feature: "false"
permalink: /:collection/installation/index.html
index: true
installation: "false"
---


Below is a matrix of the supported features for the different types of installs you can choose with DAX Studio.

The "All Users" vs "Current User" is the first option you see when you run the installer. The [portable](portable) version is a separate download.

Where ever possible we recommend using the default **All Users** install option as it gives the richest user experience. It is also the safer option as the executables are installed under the Program Files folder by default where it is much harder for them to be affected by viruses or malware.

|  | All Users | Current User | Portable |
|---|---|---|---|
|Requires Admin rights to install| **√ Yes** | No | No | 
|Requires Admin rights to run | No | No | No | 
|Available for all users on the current machine| **√ Yes** | No | No | 
|Excel Add-in available| **√ Yes** | **√ Yes** | No | 
|Power BI External Tools integration| **√ Yes** | No | No | 
|Can be run from a USB drive or shared folder _(1)_| No | No | **√ Yes** | 

 _(1)_ Note - the current user will require full read/write access to this folder, you should **never** copy the portable version to a folder that requires admin rights (like *Program Files*) as this will prevent the program from running (as it assumes it has read/write access to local folder)

> **Note:** To swap between the **All Users** and **Current User** options you have to do a full uninstall, then re-install with the other option.

The following is a list of the installation documentation topics for DAX Studio.

{% assign docs = site.documentation | where: "installation", "true" | sort: "title" %}

<ul >
{% for doc in docs %}
<li >
	<a href="{{ doc.url }}">
	{{ doc.title }}
	</a>
</li>
{% endfor %}
</ul>