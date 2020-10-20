---
title: Updates
layout: minimal
---

{% assign release =  site.github.latest_release %} 

# {{ release.name }}
{{ release.published_at | date_to_string }}

{{ release.body }}

