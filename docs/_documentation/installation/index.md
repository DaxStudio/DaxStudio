---
title: Installation
feature: "false"
permalink: /:collection/installation/index.html
index: true
installation: "false"
---

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