$(document).ready(function() {
    if (typeof(Storage) !== "undefined") {
        // Code for localStorage/sessionStorage.
        if (localStorage.release) {
            var release = JSON.parse(localStorage.release);
        }
    } else {
        // Sorry! No Web Storage support..
    }

    var hoursSinceDownloadRefresh = 0;
    // start with the cached download count if we have one
    if (release && release.downloadCnt ) {
        //console.log('returning download cnt from cache');
        $('#download_cnt').html('<span> | downloads: </span><span class="badge badge-info">' + release.downloadCnt.toLocaleString() + "</span>");
        var today = new Date();
        var lastRefresh = new Date(release.refreshDate);
        hoursSinceDownloadRefresh = Math.round(Math.abs(today - lastRefresh)/36e5);
        //console.log("hours since last download cnt refresh: " + hoursSinceDownloadRefresh);
    } 
    
    // we only refresh the download count if it's older than 1 hour to try
    // and prevent errors from github rate limiting the api
    if (!release || hoursSinceDownloadRefresh > 1)
    {
        $.ajax({
            url: "https://api.github.com/repos/daxstudio/daxstudio/releases/latest"
        }).then(function(data) {
        
            if (typeof(Storage) !== "undefined") {
                localData = {
                    refreshDate: new Date(),
                    downloadCnt: data.assets[0].download_count
                }
                localStorage.release = JSON.stringify(localData);
            }
            
            //console.log('downloads: ' + data.assets[0].download_count);
            $('#download_cnt').html('<span> | downloads: </span><span class="badge badge-info">' + data.assets[0].download_count.toLocaleString() + "</span>");
        });
    }
  });