
function AutoComplete(term, postUrl) {
    var dataList = document.getElementById("suggestions");
    dataList.innerHTML = "";

    if (term.length < 3) {
        return;
    }
    var xmlhttp;
    if (window.XMLHttpRequest) {
        // code for IE7+, Firefox, Chrome, Opera, Safari
        xmlhttp = new XMLHttpRequest();
    } else {
        // code for IE6, IE5
        xmlhttp = new ActiveXObject("Microsoft.XMLHTTP");
    }

    xmlhttp.onreadystatechange = function () {
        if (this.readyState == 4 && this.status == 200) {
            var response = xmlhttp.responseText;
            var jsonOptions = JSON.parse(response);
            var dataList = document.getElementById("suggestions");
            dataList.innerHTML = "";
            for (var i = 0; i < jsonOptions.length; i++) {
                var option = document.createElement('option');
                option.value = jsonOptions[i];
                dataList.appendChild(option);
            }
        }
    };

    xmlhttp.open("POST", postUrl, true);
    xmlhttp.setRequestHeader("Content-type", "application/x-www-form-urlencoded");
    xmlhttp.send("term=" + term);
}