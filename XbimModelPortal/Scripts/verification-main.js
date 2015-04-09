$(document).ready(function () {
    //global init variables
    var reportAvailable = false;
    var cobieFile = null;
    var dpowFile = null;

    function initWizard() {
        //initial values
        reportAvailable = false;
        cobieFile = null;
        dpowFile = null;

        //initial GUI
        $("#progress-report-loader").hide(0);
        $("input").button();
        $("button").button();
        
    }

    function initReportTab() {
        reportAvailable = false;
        $("#btn-download-report").button().button("disable");
        $("#btn-download-report img").show();
    }

    function whenReady(model, callback) {
        var xmlhttp = new XMLHttpRequest();
        xmlhttp.open("GET", "Verification/IsModelReady?model=" + model, true);
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

    function initSemanticBrowser(url) {
        var browser = new xBrowser("en", "uk");
        browser.on("loaded", function (args) {
            var facility = args.model.facility;
            //render parts
            browser.renderSpatialStructure("v-structure", true);
            browser.renderAssetTypes("v-assetTypes", true);
            browser.renderDocuments(facility[0], "v-facility-documents");

            //add passed mark to all objects where it's name starts with '[T]' and failed to these starting with '[F]'
            $("#semantic-browser-navigation .xbim-entity").each(function () {
                var span = $(this);
                var txt = span.text().trim();
                if (txt.indexOf("[T]") === 0) {
                    span.prev().remove();
                    span.before("<img src='/Content/img/check16.png' style='float:left;'>");
                    span.text(txt.substring(3));
                } else if (txt.indexOf("[F]") === 0) {
                    span.prev().remove();
                    span.before("<img src='/Content/img/err16.png' style='float:left;'>");
                    span.text(txt.substring(3));
                }
            });
        });
        browser.on("entityActive", function (args) {
            browser.renderPropertiesAttributes(args.entity, "attrprop");
            browser.renderAssignments(args.entity, "assignments");

            //show verification results
            $("#attrprop td:first-child").each(function () {
                var tdName = $(this);
                var txt = tdName.text().trim();
                if (txt.indexOf("[F]") === 0)
                    tdName.html("<img src=\"/Content/img/err16.png\" style=\"vertical-align: middle;  margin-right: 10px;\" />" + txt.substring(3));
                else if (txt.indexOf("[T]") === 0) {
                    tdName.html("<img src=\"/Content/img/check16.png\" style=\"vertical-align: middle;  margin-right: 10px;\" />" + txt.substring(3));
                }
            });
        });
        browser.load(url);
    }

    initWizard();

    $("#wizard").steps({
        headerTag: "h3",
        bodyTag: "section",
        transitionEffect: "fade",
        autoFocus: true,
        enablePagination: false,
        onStepChanging: function (event, currentIndex, newIndex) {
            if(newIndex === 3)
                //return reportAvailable;
                return true;
            var ext;
            var exts;
            if (currentIndex === 0) {
                if (!cobieFile) {
                    $("#form-err-1").text("Select COBie file.");
                    return false;
                }
                ext = cobieFile.name.split('.').pop().toLowerCase();
                exts = $("#input-cobie-file").attr("accept");
                if (exts.indexOf(ext) === -1) {
                    $("#form-err-1").text("Invalid file extension");
                    return false;
                } else {
                    $("#form-err-1").text("");
                    return true;
                }
            }
            if (currentIndex === 1) {
                if (!dpowFile) {
                    $("#form-err-2").text("Select DPoW file.");
                    return false;
                }
                ext = dpowFile.name.split('.').pop().toLowerCase();
                exts = $("#input-dpow-file").attr("accept");
                if (exts.indexOf(ext) === -1) {
                    $("#form-err-2").text("Invalid file extension");
                    return false;
                } else {
                    $("#form-err-2").text("");
                    return true;
                }
            }
            return true;
        }
    });
   
    //transmit click from proxy buttons to hidden input elements
    $("#btn-cobie-file").click(function() {
        $("#input-cobie-file").click();
    });
    $("#btn-dpow-file").click(function () {
        $("#input-dpow-file").click();
    });

    $("#input-cobie-file").change(function() {
        initReportTab();
        var file = this.files[0];
        if (file) {
            cobieFile = file;
            if ($("#wizard").steps("next"))
                $(".cobie-file-name").text(file.name);
        }
    });

    $("#input-dpow-file").change(function () {
        initReportTab();
        var file = this.files[0];
        if (file) {
            dpowFile = file;
            if ($("#wizard").steps("next"))
                $(".dpow-file-name").text(file.name);
        }
    });

    
    var doReport = true;
    $("#btn-run-validation").click(function() {
        //send data for validation and present the result
        $("#progress-report").show(1000);
        $("#progress-report-loader").show(1000);
        var data = new FormData();
        data.append("cobie", cobieFile);
        data.append("dpow", dpowFile);
        $.ajax({
            url: "Verification/VerifyCobieFile", //Server script to process data
            type: "POST",
            xhr: function() { // Custom XMLHttpRequest
                var myXhr = $.ajaxSettings.xhr();
                if (myXhr.upload) { // Check if upload property exists
                    myXhr.upload.addEventListener("progress", function(evt) {
                        var percentComplete = Math.round(evt.loaded / evt.total * 100);
                        $("#progress-report").text("Uploaded " + percentComplete + "%");
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
                    $("#progress-report").text("Your model has been uploaded and is being processed on the server now...");
                    var report = response.report;
                    var state = response.state;
                    var xlsReport = response.xlsReport;

                    whenReady(report, function() {
                        reportAvailable = true;
                        //roll to the last step
                        var current = $("#wizard").steps("getCurrentIndex");
                        while (current !== 3) {
                            if (!$("#wizard").steps("next"))
                                break;
                            current = $("#wizard").steps("getCurrentIndex");
                        }
                        //show structured JSON report
                        $("#semantic-browser-navigation, #semantic-browser-details").accordion({
                            heightStyle: "fill"
                        });
                        initSemanticBrowser("Verification/GetData?model=" + report);
                    });

                    whenReady(state, function() {
                        var reportState = function() {
                            $.get("Verification/GetData?model=" + state, null, function (stateData, reportStatus) {
                                $("#progress-report").html(stateData);
                            });
                            if (doReport)
                                setTimeout(reportState, 500);
                            else {
                                $("#progress-report").html("");
                                $("#progress-report-loader").hide();
                            }
                        }
                        reportState();
                    });

                    whenReady(xlsReport, function () {
                        doReport = false;
                        $("#btn-download-report").button("enable");
                        $("#btn-download-report img").hide();
                        $("#btn-download-report").off("click");
                        $("#btn-download-report").click(function () {
                            //download the file
                            var dotIndex = cobieFile.name.lastIndexOf(".");
                            var name = cobieFile.name.substring(0, dotIndex);
                            window.location = 'Verification/GetData?model=' + xlsReport + "&name=" + encodeURI(name);
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

