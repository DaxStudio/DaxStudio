// this function updates the changelog with links to 
// github issues when it finds text like #123
$(document).ready(function() {
    var regex = /(?:#)(\d{1,4})(?=\s+)/gi;

    $('li').html(function(i, oldHTML) {
        return oldHTML.replace(regex, "<a href=\"https://github.com/daxstudio/daxstudio/issues/$1\">#$1<\/a>");
    });
  }
);