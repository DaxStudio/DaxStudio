---
title: Downloads
layout: page
js-footer: "/js/index-footer.js"
---

# Current Release

> The download links for the current release are also available on the [homepage](/)

{% assign download = site.github.latest_release %}

{% assign exe_assets = download.assets | where_exp: "item", "item.browser_download_url contains '.exe'" %}
{% assign installer = exe_assets[0] %}
{% assign zip_assets = download.assets | where_exp: "item", "item.browser_download_url contains '.zip'" %}
{% assign portable = zip_assets[0] %}

<!-- Installer Download link -->
<div class="main-explain-area jumbotron center">
  <div class="centered">
    <div style="padding-bottom:10px">Download the latest release of DAX Studio here:</div>
  
    <a href="{{ installer.browser_download_url }}">
      <button class="btn btn-lg btn-success"> 
        <h3><i class="fa fa-download"></i>&nbsp; {{download.name}}</h3>
        <div>(installer)</div>
      </button>
    </a>
    {% assign download_count = installer.download_count %}
    {% assign download_size = installer.size %}
    <div class="download-info">
      <i class="fa fa-download" title="downloads"></i> <span>Size: {% include filesize.html number=download_size %} | </span>
      <i class="fa fa-calendar" title="release date"></i><span>{% if installer.created_at != nil %}{{ installer.created_at | date_to_string }} {% else %}N/A{% endif %} </span>
      <span id="download_cnt"></span> 
    </div>
  </div>
</div>

<!-- Portable Version download link -->

  {% if portable != nil  %}  
  <div class="centered">

    <a href="{{ portable.browser_download_url }}">
      
        <div><i class="fa fa-download"></i>&nbsp; {{download.name}} (portable)</div>
      
    </a>
    {% assign download_count_1 = portable.download_count %}
    {% assign download_size_1 = portable.size %}
    <div class="download-info">
      <i class="fa fa-download" title="downloads"></i> <span>Size: {% include filesize.html number=download_size_1 %} | </span>
      <i class="fa fa-calendar" title="release date"></i>&nbsp;<span>{% if portable.created_at != nil %}{{ portable.created_at | date_to_string }} {% else %}N/A{% endif %} </span>
      <span id="download_cnt_zip"></span> 
    </div>
  </div>

{% endif %}



# Prior Releases

> We always recommend running the latest release if possible, but if you need a prior release for some reason the following is a list of all the previous releases of DAX Studio. 

{% assign idx = 0 %}

{% for release in  site.github.releases %} 
  {% if release.draft != true and release.prerelease != true and idx > 0 %}
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
  Size: {% include filesize.html number=download_size %} \| Date: {% if asset.created_at  %}{{ asset.created_at | date_to_string }} {% else %} N/A {% endif %} \| Downloads: {% include intcomma.html number=download_count %}
     {% endfor %}      


{% comment %}

{% assign asset = release.assets[0] %}
{% assign download_type = "installer" %}
- [{{ release.name }} ({{ download_type}})]({{ asset.browser_download_url }})
  Size: {% include filesize.html number=download_size %} \| Date: {% if asset.created_at  %}{{ asset.created_at | date_to_string }} {% else %} N/A {% endif %}
{% endcomment %}

  {% endif %}
  {% assign idx = 1 %}

{% endfor %}
