(function () {
    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-dropzone]').forEach(function (zone) {
            var input = zone.querySelector('.leave-dropzone-input');
            var fileList = zone.querySelector('.leave-dropzone-files');

            if (!input || !fileList) {
                return;
            }

            var clearDragState = function () {
                zone.classList.remove('drag-over');
            };

            var updateList = function (files) {
                if (!files || files.length === 0) {
                    fileList.textContent = 'No files selected yet.';
                    return;
                }

                var items = [];
                for (var i = 0; i < files.length; i += 1) {
                    var file = files[i];
                    items.push(file.name);
                }

                fileList.textContent = items.join(', ');
            };

            var assignFiles = function (files) {
                if (!files || files.length === 0) {
                    input.value = '';
                    updateList(files);
                    return;
                }

                if (window.DataTransfer) {
                    var dt = new DataTransfer();
                    for (var i = 0; i < files.length; i += 1) {
                        dt.items.add(files[i]);
                    }
                    input.files = dt.files;
                    updateList(input.files);
                } else {
                    input.files = files;
                    updateList(input.files);
                }
            };

            zone.addEventListener('click', function () {
                input.click();
            });

            zone.addEventListener('dragenter', function (event) {
                event.preventDefault();
                zone.classList.add('drag-over');
            });

            zone.addEventListener('dragover', function (event) {
                event.preventDefault();
                zone.classList.add('drag-over');
            });

            zone.addEventListener('dragleave', function (event) {
                if (!zone.contains(event.relatedTarget)) {
                    clearDragState();
                }
            });

            zone.addEventListener('drop', function (event) {
                event.preventDefault();
                clearDragState();
                if (event.dataTransfer && event.dataTransfer.files) {
                    assignFiles(event.dataTransfer.files);
                }
            });

            input.addEventListener('change', function () {
                assignFiles(input.files);
            });

            updateList([]);
        });
    });
})();
