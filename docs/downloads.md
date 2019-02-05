---
title: Downloads
layout: page
---

{% for release in  site.github.releases %} 
{% if release.draft != true and release.prerelease != true %}
{% assign download_count = release.assets[0].download_count  %}
{% assign download_size = release.assets[0].size %}
- [{{ release.name }}]({{ release.assets[0].browser_download_url }})
   Size: {% include filesize.html number=download_size %} \| Date: {% if release.assets[0].created_at  %}{{ release.assets[0].created_at | date_to_string }} {% else %} N/A {% endif %}
   {% endif %}
{% endfor %}
