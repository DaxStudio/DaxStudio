---
title: Changelog
layout: page
js-footer: /js/changelog-footer.js
---

> See the full changelog for all releases [here](/changelog)

{% assign release = site.github.latest_release %}



<h1 style="color:royalblue"> {{ release.name }} </h1>
*Published: {{ release.published_at | date_to_string }}*

{{ release.body }}

