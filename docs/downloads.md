---
title: Downloads
layout: page
---

{% for release in  site.github.releases %} 
  {% if release.draft != true and release.prerelease != true %}
### {{ release.name }}
    {% assign sorted = release.assets | sort: 'browser_download_url' | reverse %}
    {% for asset in sorted %}
      {% assign download_count = asset.download_count  %}
      {% assign download_size = asset.size %}
      {% assign dl_ext = asset.browser_download_url | slice: -4, 4%}
      {% assign download_type = "installer" %}
      {% if dl_ext == ".zip" %}
        {% assign download_type = "portable" %}
      {% endif %}
- [{{ release.name }} ({{ download_type}})]({{ asset.browser_download_url }}) <br/>
  Size: {% include filesize.html number=download_size %} \| Date: {% if asset.created_at  %}{{ asset.created_at | date_to_string }} {% else %} N/A {% endif %} 
    {% endfor %}

{% comment %}

{% assign asset = release.assets[0] %}
{% assign download_type = "installer" %}
- [{{ release.name }} ({{ download_type}})]({{ asset.browser_download_url }})
  Size: {% include filesize.html number=download_size %} \| Date: {% if asset.created_at  %}{{ asset.created_at | date_to_string }} {% else %} N/A {% endif %}
{% endcomment %}

  {% endif %}
{% endfor %}
