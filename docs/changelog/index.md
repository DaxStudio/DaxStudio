---
title: Changelog
layout: page
js-footer: /js/changelog-footer.js
---

{% for release in  site.github.releases %} 
  {% if release.draft != true and release.prerelease != true %}
<h1 style="color:royalblue"> {{ release.name }} </h1>
*Published: {{ release.published_at | date_to_string }}*

{{ release.body }}

---

  {% endif %}
{% endfor %}
