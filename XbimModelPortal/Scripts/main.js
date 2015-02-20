$(document).ready(function () {
    //var queryString = function () {
    //    // This function is anonymous, is executed immediately and 
    //    // the return value is assigned to QueryString!
    //    var query_string = {};
    //    var query = window.location.search.substring(1);
    //    var vars = query.split("&");
    //    for (var i = 0; i < vars.length; i++) {
    //        var pair = vars[i].split("=");
    //        // If first entry with this name
    //        if (typeof query_string[pair[0]] === "undefined") {
    //            query_string[pair[0]] = pair[1];
    //            // If second entry with this name
    //        } else if (typeof query_string[pair[0]] === "string") {
    //            var arr = [query_string[pair[0]], pair[1]];
    //            query_string[pair[0]] = arr;
    //            // If third or later entry with this name
    //        } else {
    //            query_string[pair[0]].push(pair[1]);
    //        }
    //    }
    //    return query_string;
    //}();

    function initControls() {

        $('#semantic-descriptive-info').accordion({
            heightStyle: 'fill'
        });

        $('#semantic-tabs').tabs({
            activate: function (event, ui) {
                reinitControls();
            },
            create: function (event, ui) {
                $('#semantic-model').accordion({
                    heightStyle: 'fill'
                });
                $('#requirements').accordion({
                    heightStyle: 'fill'
                });
            }
        });

        $('#btnLocate').button().click(function () {
            var id = $(this).data('id');
            if (typeof (id) != 'undefined' && viewer) {
                viewer.zoomTo(parseInt(id));
            }
        });

        $('.xbim-button').button();

        //init overlayed file input buttons
        $('#ifcButton').on('click', function () { 
            $('#ifcFileInput').click();
            return false;
        });
        $('#ifcFileInput').on('change', function () {
            $('#ifcName').html($(this).val().split(/(\\|\/)/g).pop());
        });
        $('#rqButton').on('click', function () {
            $('#rqFileInput').click();
            return false;
        });
        $('#rqFileInput').on('change', function () {
            $('#rqName').html($(this).val().split(/(\\|\/)/g).pop());
        });

    }

    var afterDialog = null;
    function showError(header, message, idAfter) {
        afterDialog = idAfter;
        $('.xbim-dialog').hide();
        $('#error-dialog-header').html(header);
        $('#error-dialog-content').html(message);
        $('#error-dialog').show();
    }
    $("#errOkButton").on("click", function () {
        $(".xbim-dialog").hide();
        $(afterDialog).show();
    });

    function whenReady(model, callback) {
        var xmlhttp = new XMLHttpRequest();
        xmlhttp.open("GET", "/Services/IsModelReady?model=" + model, true);
        xmlhttp.onreadystatechange = function () {
            if (xmlhttp.readyState === 4 && xmlhttp.status === 200) {
                var data = JSON.parse(xmlhttp.responseText);
                if (data.State === 'READY')
                    callback(model);
                else 
                    setTimeout(function () { whenReady(model, callback) }, 1000);
            }
        };
        xmlhttp.send();
    }

    //load button - validate input files (extensions at least), upload files, wait for results, load browsers and the viewer
    $('#uploadButton').on('click', function () {
        $('#dialog-container').hide();
        $('#overlay-shadow').hide();



        var ifcFile = $('#ifcFileInput')[0].files[0];
        var dpowFile = $('#rqFileInput')[0].files[0];

        if (ifcFile.name.indexOf(".wexbim") !== -1 || ifcFile.name.indexOf(".wexBIM") !== -1) {
            viewer.load(ifcFile);
            viewer.on("loaded", function () {
                $("#overlay").hide(200);
            });
            return;
        }

        if (typeof (ifcFile) === "undefined" || typeof (dpowFile) === "undefined") {
            alert('Both files have to be defined');
        }

        var formData = new FormData();
        formData.append("ifcFile", ifcFile);
        $.ajax({
            url: "Services/UploadIFC",  //Server script to process data
            type: "POST",
            //xhr: function () {  // Custom XMLHttpRequest
            //    var myXhr = $.ajaxSettings.xhr();
            //    if (myXhr.upload) { // Check if upload property exists
            //        myXhr.upload.addEventListener('progress', progressHandlingFunction, false); // For handling the progress of the upload
            //    }
            //    return myXhr;
            //},
            ////Ajax events
            //beforeSend: beforeSendHandler,
            success: function (data, status, xhr) {
                var response = data;
                if (typeof (data) == "string")
                    response = JSON.parse(data);

                //wait for wexbim and COBieLite files and load them when ready
                if (response && response.State === "UPLOADED") {
                    var wexbim = response.WexBIMName;
                    var cobie = response.COBieName;

                    whenReady(wexbim, function () {
                        viewer.load("/Services/GetData?model=" + wexbim);
                        viewer.on("loaded", function () {
                            $("#overlay").hide(200);
                        });
                    });

                    whenReady(cobie, function () {
                        browser.load("/Services/GetData?model=" + cobie);
                        browser.on("loaded", function () {
                            $("#overlay").hide(200);
                        });
                    });

                }
                else
                    showError("Error", response.Message);

            },
            error: function (xhr, status, msg) {
                showError("Error during sending IFC file", msg, '#files-upload-dialog');
            },
            // Form data
            data: formData,
            //Options to tell jQuery not to process data or worry about content-type.
            cache: false,
            contentType: false,
            processData: false
        });

        formData = new FormData();
        formData.append("dpowFile", dpowFile);
        $.ajax({
            url: "Services/UploadDPoW",  //Server script to process data
            type: "POST",
            //xhr: function () {  // Custom XMLHttpRequest
            //    var myXhr = $.ajaxSettings.xhr();
            //    if (myXhr.upload) { // Check if upload property exists
            //        myXhr.upload.addEventListener('progress', progressHandlingFunction, false); // For handling the progress of the upload
            //    }
            //    return myXhr;
            //},
            ////Ajax events
            //beforeSend: beforeSendHandler,
            success: function (data, status, xhr) {
                var response = data;
                if (typeof (data) == "string")
                    response = JSON.parse(data);

                //wait for wexbim and COBieLite files and load them when ready
                if (response && response.State === "UPLOADED") {
                    var cobie = response.COBieName;

                    whenReady(cobie, function () {
                        rBrowser.load("/Services/GetData?model=" + cobie);
                    });

                }
                else
                    showError("Error", response.Message);

            },
            error: function (xhr, status, msg) {
                showError("Error during sending IFC file", msg, '#files-upload-dialog');
            },
            // Form data
            data: formData,
            //Options to tell jQuery not to process data or worry about content-type.
            cache: false,
            contentType: false,
            processData: false
        });
    });

    function reinitControls() {
        $('#semantic-model').accordion('refresh');
        $('#semantic-descriptive-info').accordion('refresh');
        $('#requirements').accordion('refresh');
    }
    initControls();
    $(window).resize(function () {
        reinitControls();
    });

    var keepTarget = false;
    var rBrowser = new xBrowser();
    var browser = new xBrowser();
    browser.on('loaded', function (args) {
        var facility = args.model.facility;
        //render parts
        browser.renderSpatialStructure('structure', true);
        browser.renderAssetTypes('assetTypes', true);
        browser.renderSystems('systems');
        browser.renderZones('zones');
        browser.renderContacts('contacts');
        browser.renderDocuments(facility[0], 'facility-documents');

        //open and selectfacility node
        $("#structure > ul > li").click();
    });
    rBrowser.on('loaded', function (args) {
        var facility = args.model.facility;
        //render parts
        rBrowser.renderSpatialStructure('r-structure', true);
        rBrowser.renderAssetTypes('r-assetTypes', true);
        rBrowser.renderSystems('r-systems');
        rBrowser.renderZones('r-zones');
        rBrowser.renderContacts('r-contacts');
        rBrowser.renderDocuments(facility[0], 'r-facility-documents');

    });


    function initBrowser(browser) {
        browser.on('entityClick', function (args) {
            var span = $(args.element).children("span.xbim-entity");
            if (document._lastSelection)
                document._lastSelection.removeClass('ui-selected');
            span.addClass('ui-selected');
            document._lastSelection = span;
        });
        browser.on('entityActive', function (args) {
            var isRightPanelClick = false;
            if (args.element) 
                if ($(args.element).parents('#semantic-descriptive-info').length != 0)
                    isRightPanelClick = true;

            //set ID for location button
            $('#btnLocate').data('id', args.entity.id);

            browser.renderPropertiesAttributes(args.entity, 'attrprop');
            browser.renderAssignments(args.entity, 'assignments');
            browser.renderDocuments(args.entity, 'documents');
            browser.renderIssues(args.entity, 'issues');

            if (isRightPanelClick)
                $('#attrprop-header').click();

        });
    }

    initBrowser(browser);
    initBrowser(rBrowser);

    browser.on('entityDblclick', function (args) {
        var entity = args.entity;
        var allowedTypes = ['space', 'assettype', 'asset'];
        if (allowedTypes.indexOf(entity.type) === -1) return;

        var id = parseInt(entity.id);
        if (id && viewer) {
            viewer.resetStates();
            viewer.renderingMode = "x-ray";
            if (entity.type === "assettype") {
                var ids = [];
                for (var i = 0; i < entity.children.length; i++) {
                    id = parseInt(entity.children[i].id);
                    ids.push(id);
                }
                viewer.setState(xState.HIGHLIGHTED, ids);
            }
            else {
                viewer.setState(xState.HIGHLIGHTED, [id]);
            }
            viewer.zoomTo(id);
            keepTarget = true;
        }
    });
    rBrowser.on('entityDblclick', function (args) {
        var entity = args.entity;
        var allowedTypes = ["assettype"];
        if (allowedTypes.indexOf(entity.type) === -1) return;

        //find all elements of this type in the second browser and highlight them
       

    });


    //viewer set up
    var check = xViewer.check();
    var viewer = null;
    if (check.noErrors) {
        //alert('WebGL support is OK');
        viewer = new xViewer('viewer-canvas');
        viewer.background = [249, 249, 249, 255];
        viewer.on('mouseDown', function (args) {
            if (!keepTarget) viewer.setCameraTarget(args.id);
        });
        viewer.on('pick', function (args) {
            browser.activateEntity(args.id);
            viewer.renderingMode = 'normal';
            viewer.resetStates();
            keepTarget = false;
        });
        viewer.on('dblclick', function (args) {
            viewer.resetStates();
            viewer.renderingMode = 'x-ray';
            var id = args.id;
            viewer.setState(xState.HIGHLIGHTED, [id]);
            //viewer.zoomTo(id);
            keepTarget = true;
        });
        //viewer.load('Data/Duplex_MEP_20110907_SRL.wexbim');
        viewer.start();
    }
    else {
        alert('WebGL support is unsufficient');
        var msg = document.getElementById('msg');
        msg.innerHTML = '';
        for (var i in check.errors) {
            if (check.errors.hasOwnProperty(i)) {
                var error = check.errors[i];
                msg.innerHTML += "<div style='color: red;'>" + error + "</div>";
            }
        }
    }

    // ---------------------------------- FOR DEVELOPMENT ONLY ------------------------------------ //
    if (true) { //set this to false for production
        //Hide upload overlay
        $("#overlay").hide(200);

        //Load default data    
        browser.load("Data/LakesideRestaurant.json");
        viewer.load("Data/LakesideRestaurant.wexbim");
    }
    // ----------------------------- END OF DEVELOPMENT SECTION ----------------------------------- //

});