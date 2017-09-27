---
title: downloads
layout: page
---


{% for release in  site.github.releases %} 
{% if release.draft != true and release.prerelease != true %}
- [{{ release.name }}]({{ release.assets[0].browser_download_url }})
   Downloads: {{ release.assets[0].download_count | intcomma }} | Size: {{ release.assets[0].size | filesize }} | Date: {% if release.assets[0].created_at  %}{{ release.assets[0].created_at | date_to_string }} {% else %} N/A {% endif %}
   {% endif %}
{% endfor %}

> Prior versions can be found on the old codeplex site at [http://daxstudio.codeplex.com](http://daxstudio.codeplex.com)