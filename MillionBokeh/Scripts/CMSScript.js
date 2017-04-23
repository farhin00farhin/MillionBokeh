function InitialiseCMS(id, editurl, imageUploadUrl, imageLoadUrl) {
    // 20170327 SA Initialise TinyMCE
    tinymce.init({
        selector: '.tinymce'
    });

    // 20170327 SA Get the anitofrgery token which we will need to make the POST calls when editing things.
    var form = $('#__AjaxAntiForgeryForm');
    var token = $('input[name="__RequestVerificationToken"]', form).val();

    // 20170327 SA Set up some tooltips for all the editable elements.
    $(".cmsdisplay").attr("title", "Click to Edit");
    $(".cmsdisplay").attr("data-placement", "left");
    $('.cmsdisplay').tooltip();
    $(".cmsdisplay").css("cursor", "pointer");

    
    $(".cmsdisplay").click(function () {
        //20170327 SA first check if editing isn't disabled (for links to work)
        if (!$(this).is(".stopedit")) {
            // 20170327 SA Get the HTML input elements that will be used to take user input.
            $editor = $('[data-property=' + $(this).data("property") + '].cmseditor');
            $update = $('[data-property=' + $(this).data("property") + '].cmsupdate');
            $picker = $('[data-property=' + $(this).data("property") + '].cmspicker');

            // 20170327 SA Show the input elements
            $editor.not(".tinymce").show();
            $update.show();
            $editor.parents(".cmseditor").show();
            $picker.show();
        }
    });

    $(".cmsupdate").click(function () {
        tinyMCE.triggerSave();

        // 20170327 SA Get the HTML input elements that were involved in this update and keep them in variables so we can use them later.
        $display = $('[data-property=' + $(this).data("property") + '].cmsdisplay');
        $editor = $('[data-property=' + $(this).data("property") + '].cmseditor');
        $update = $('[data-property=' + $(this).data("property") + '].cmsupdate');
        $picker = $('[data-property=' + $(this).data("property") + '].cmspicker');

        $datedisplay = $('[data-property=' + $(this).data("property") + '].cmsdisplay.datedisplay');
        $timedisplay = $('[data-property=' + $(this).data("property") + '].cmsdisplay.timedisplay');

        // 20170327 SA These are the two things we'll need to pass to the controller to update.
        var propertyName = $editor.data("property");
        var newValue = $editor.val();

        $.ajax({
            url: editurl,
            type: 'POST',
            data: {
                __RequestVerificationToken: token,
                id: id,
                propertyName: propertyName,
                propertyValue: newValue
            },
            success: function (result) {
                // 20170327 SA If we're expecting a date, then get the updated value and populate the date and time boxes
                if ($datedisplay.length > 0 || $timedisplay.length > 0) {
                    var resultDate = Date.parse(result);
                    if (resultDate) {
                        resultDate = moment(result);
                        $datedisplay.html(resultDate.format("DD/MM/YYYY"));
                        $timedisplay.html(resultDate.format("h:mm A"));
                    }
                }
                    // 20170327 SA If we're updating a link, then set the href of the element
                else if ($display.is("a")) {
                    $display.attr("href", result);
                }
                    // 20170327 SA If we're updating just text, then update the inner html of the display element with the result
                else {
                    $display.html(result);
                }

                // 20170327 SA Hide the inputs
                $editor.hide();
                $update.hide();
                $editor.parents(".cmseditor").hide();
                $picker.hide();
            }
        });
    });

    $(".cmseditor.image").change(function () {
        // 20170327 SA To upload images we need to make the form data ourselves
        var data = new FormData();
        data.append("id", id);
        var files = $(".cmseditor.image").get(0).files;
        if (files.length > 0) {
            data.append("__RequestVerificationToken", token);
            data.append("Image", files[0]);
        }
        $display = $('[data-property=' + $(this).data("property") + '].cmsdisplay');
        $editor = $('[data-property=' + $(this).data("property") + '].cmseditor');

        $.ajax({
            url: imageUploadUrl,
            type: "POST",
            processData: false,
            contentType: false,
            data: data,
            success: function (response) {
                $display.attr("src", imageLoadUrl + response + "?" + new Date().getTime());
                $editor.hide();
            },
            error: function (er) {
                alert(er);
            }
        });
    });

    $("select.cmspicker").change(function () {
        // 20170327 SA Keep the hidden field in sync, when the user changes the dorpodown
        $editor = $('[data-property=' + $(this).data("property") + '].cmseditor');
        $editor.val($(this).find(":selected").val());
    });

    // 20170327 SA Member gallery specific functionality. This is almost the same as the normal image upload, but some parts are left out.
    $(".cmseditor.galleryupload").change(function () {
        var data = new FormData();
        data.append("id", id);
        var files = $(".cmseditor.galleryupload").get(0).files;
        if (files.length > 0) {
            data.append("__RequestVerificationToken", token);
            data.append("Image", files[0]);
        }
        $editor = $('[data-property=' + $(this).data("property") + '].cmseditor');

        $.ajax({
            url: "/Members/UploadGalleryImage/",
            type: "POST",
            processData: false,
            contentType: false,
            data: data,
            success: function (response) {
                location.reload();
            },
            error: function (er) {
                alert(er);
            }

        });
    });

}

