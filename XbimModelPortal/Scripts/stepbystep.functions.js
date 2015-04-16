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
	xmlhttp.open("GET", controller + "/IsModelReady?model=" + model, true);
	xmlhttp.onreadystatechange = function () {
		if (xmlhttp.readyState === 4 && xmlhttp.status === 200) {
			var data = JSON.parse(xmlhttp.responseText);
			if (data.State === "READY")
				callback(model);
			else
				setTimeout(function () { whenReady(model, callback) }, 500);
		}
	};
	xmlhttp.send();
}

function reportProgress(message) {
    $("#progress-report").text(message);
}