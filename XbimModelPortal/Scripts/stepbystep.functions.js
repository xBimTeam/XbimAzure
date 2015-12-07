$(document).ready(function() {
    $(".input-group .file-button").click(function() {
        $(this).parent().find("input[type='file']").click();
    });
    $(".input-group input[type='file']").change(function() {
        var file = this.files[0];
        if (file) {
            $(this).parent().find("input[type='text']").val(file.name);
        }
    });
});

function whenReady(controller, model, callback) {
// ReSharper disable once InconsistentNaming
    var xmlhttp = new XMLHttpRequest();
    xmlhttp.open("GET", "/" + controller + "/IsModelReady?model=" + model, true);
    xmlhttp.onreadystatechange = function() {
        if (xmlhttp.readyState === 4 && xmlhttp.status === 200) {
            var data = JSON.parse(xmlhttp.responseText);
            if (data.State === "READY")
                callback(model);
            else
                setTimeout(function () { whenReady(controller, model, callback) }, 500);
        }
    };
    xmlhttp.send();
}

function reportProgress(message) {
    $("#progress-report").html(message);
}

function getExtension(name) {
    name = (name instanceof File) ? name.name : name;
    var dotIndex = name.lastIndexOf(".");
    if (dotIndex < 0) return null;
    return name.substring(dotIndex).toLowerCase();
}

// Custom XMLHttpRequest
function getXHR() {
    var myXhr = $.ajaxSettings.xhr();
    if (myXhr.upload) { // Check if upload property exists
        myXhr.upload.addEventListener("progress", function(evt) {
            var percentComplete = Math.round(evt.loaded / evt.total * 100);
            reportProgress("Uploaded " + percentComplete + "%");
        }, false); // For handling the progress of the upload
    }
    return myXhr;
}

function getFileNameWithoutExtension(name) {
    name = (name instanceof File) ? name.name : name;
    var dotIndex = name.lastIndexOf(".");
    return name.substring(0, dotIndex);
}

function hasRightExtension(input) {
    if (!input)
        throw "Input not defined";
    if (typeof (input) === "string")
        input = $(input)[0];

    if (!input.files || !input.files[0]) return false;
    var name = input.files[0].name;
    var exts = $(input).attr("accept").split(",");
    var ext = getExtension(name);
    if (exts.indexOf(ext) < 0) {
        return false;
    }
    return true;
}

function getFile(inputId) {
    var file = $(inputId)[0].files[0];
    if (!file) {
        reportProgress("You have to select a file first.");
        return null;
    }

    if (!hasRightExtension(inputId)) {
        reportProgress("You have to specify a valid input file");
        return null;
    }

    return file;
}