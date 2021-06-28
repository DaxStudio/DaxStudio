---
title: Changelog
layout: minimal
---

{% assign release = site.github.latest_release %}
{% raw %}
<h1 style="color:royalblue"> {{ release.name }}</h1>
*Published {{ release.published_at | date_to_string }}*

{{ release.body }}

{% endraw %}