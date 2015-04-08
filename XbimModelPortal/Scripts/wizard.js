$(document).ready(function() {
    var reportAvailable = false;
    $("#progress-report-loader").hide(0);

    $("#wizard").steps({
        headerTag: "h3",
        bodyTag: "section",
        transitionEffect: "fade",
        autoFocus: true,
        enableFinishButton: false,
        onStepChanging: function(event, currentIndex, newIndex) {
            switch (newIndex) {
            case 2:
                return reportAvailable;
            default:
                break;
            }


            switch (currentIndex) {
            case 0:
            case 1:
                var file = $("#input-cobie-file")[0].files[0];
                if (typeof (file) === "undefined") {
                    $(".validation-report").text("Select COBie file.");
                    return false;
                }
                var ext = file.name.split('.').pop().toLowerCase();
                var exts = $("#input-cobie-file").attr("accept");
                if (exts.indexOf(ext) === -1) {
                    $(".validation-report").text("Invalid file extension");
                    return false;
                } else {
                    $(".validation-report").text("");
                    return true;
                }
            case 2:
            default:
                break;
            }


            return true;
        }
    });
    $("input").button();
    $("button").button();

    //initial state is disabled until the fixed file is available
    $("#btn-download-fix").button("disable");

    $("#btn-cobie-file").click(function() {
        $("#input-cobie-file").click();
    });

    $("#input-cobie-file").change(function() {
        reportAvailable = false;
        $("#btn-download-fix").button("disable");
        $("#btn-download-fix img").show();

        var file = this.files[0];
        if (file) {
            if ($("#wizard").steps("next"))
                $(".cobie-file-name").text(file.name);
        }
    });

    function whenReady(model, callback) {
        var xmlhttp = new XMLHttpRequest();
        xmlhttp.open("GET", "IsModelReady?model=" + model, true);
        xmlhttp.onreadystatechange = function() {
            if (xmlhttp.readyState === 4 && xmlhttp.status === 200) {
                var data = JSON.parse(xmlhttp.responseText);
                if (data.State === "READY")
                    callback(model);
                else
                    setTimeout(function() { whenReady(model, callback) }, 500);
            }
        };
        xmlhttp.send();
    }

    var fileName = "file.xlsx";
    var progress = $("#progress-report");
    var doReport = true;
    $("#btn-run-validation").click(function() {
        //send data for validation and present the result
        $("#progress-report-loader").show(1000);
        var file = $("#input-cobie-file")[0].files[0];
        fileName = file.name;
        var data = new FormData();
        data.append("file", file);
        $.ajax({
            url: "ValidateCobieFile", //Server script to process data
            type: "POST",
            xhr: function() { // Custom XMLHttpRequest
                var myXhr = $.ajaxSettings.xhr();
                if (myXhr.upload) { // Check if upload property exists
                    myXhr.upload.addEventListener("progress", function(evt) {
                        var percentComplete = Math.round(evt.loaded / evt.total * 100);
                        progress.text("Uploaded " + percentComplete + "%");
                    }, false); // For handling the progress of the upload
                }
                return myXhr;
            },
            ////Ajax events
            //beforeSend: beforeSendHandler,
            success: function(data, status, xhr) {
                var response = data;
                if (typeof (data) == "string")
                    response = JSON.parse(data);

                //wait for wexbim and COBieLite files and load them when ready
                if (response && response.uploaded) {
                    progress.text("Your model has been uploaded and is being processed on the server now...");
                    var report = response.report;
                    var state = response.state;
                    var fixedCobie = response.fixedCobie;

                    whenReady(report, function() {
                        //get report data
                        $.get("GetData?model=" + report, null, function(reportData, reportStatus) {
                            reportAvailable = true;
                            //roll to the last step
                            var current = $("#wizard").steps("getCurrentIndex");
                            while (current !== 2) {
                                if (!$("#wizard").steps("next"))
                                    break;
                                current = $("#wizard").steps("getCurrentIndex");
                            }
                            $("#validation-report").val(reportData);
                        });
                    });

                    whenReady(state, function() {
                        var reportState = function() {
                            $.get("GetData?model=" + state, null, function(stateData, reportStatus) {
                                progress.text(stateData);
                            });
                            if (doReport)
                                setTimeout(reportState, 1000);
                            else {
                                progress.text("");
                                $("#progress-report-loader").hide();
                            }
                        }
                        reportState();
                    });

                    whenReady(fixedCobie, function() {
                        doReport = false;
                        $("#btn-download-fix").button("enable");
                        $("#btn-download-fix img").hide();
                        $("#btn-download-fix").click(function() {
                            //download the file
                            var dotIndex = fileName.lastIndexOf(".");
                            var name = fileName.substring(0, dotIndex);
                            window.location = 'GetData?model=' + fixedCobie + "&name=" + encodeURI(name);
                        });

                    });
                }
            },
            error: function(xhr, status, msg) {
            },
            // Form data
            data: data,
            //Options to tell jQuery not to process data or worry about content-type.
            cache: false,
            contentType: false,
            processData: false
        });


    });
});