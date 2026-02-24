$(document).ready(function () {

    $(document).on('keydown', '#txtStockFindLotNo, #txtStockFindArticle', function (e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            findStock();
        }
    });

    $(document).on('keydown', '#txtTrayFilterLotNo', function (e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            searchReceivedForTray();
        }
    });

    $(document).on('click', '#btnCreateTray', async function () {
        const trayNo = $('#txtNewTrayNo').val().trim();
        const description = $('#txtNewTrayDesc').val();

        if (!trayNo) {
            await swalWarning('กรุณากรอกเลขถาด');
            return;
        }

        $('#loadingIndicator').show();

        $.ajax({
            url: urlCreateTray,
            type: 'POST',
            data: { trayNo: trayNo, description: description || null },
            success: function () {
                $('#loadingIndicator').hide();
                $('#modal-create-tray').modal('hide');
                $('#txtNewTrayNo').val('');
                $('#txtNewTrayDesc').val('');
                loadTrayList();

                if ($('#modal-select-tray').hasClass('show')) {
                    loadReverseTrayList();
                }

                swalToastSuccess('สร้างถาดเรียบร้อย');
            },
            error: async function (xhr) {
                $('#loadingIndicator').hide();
                let msg = xhr.responseJSON?.message || 'เกิดข้อผิดพลาด';
                await swalWarning(msg);
            }
        });
    });


    $(document).on('click', '#btnAddToTray', async function () {
        const trayId = $('#hddTrayId').val();
        const items = {};

        $('#tbl-add-tray-body tr').each(function () {
            const chk = $(this).find('.chk-tray-item');
            if (chk.is(':checked')) {
                const receivedId = $(this).data('received-id');
                const qtyInput = $(this).find('.input-tray-qty');
                const qty = parseFloat(qtyInput.val()) || 0;

                if (receivedId && qty > 0) {
                    items[receivedId] = qty;
                }
            }
        });

        if (Object.keys(items).length === 0) {
            await swalWarning('กรุณาเลือกสินค้าและระบุจำนวนให้ถูกต้อง');
            return;
        }

        $('#loadingIndicator').show();

        const formData = new FormData();
        formData.append('trayId', trayId);
        formData.append('itemsJson', JSON.stringify(items));

        $.ajax({
            url: urlAddToTray,
            type: 'POST',
            processData: false,
            contentType: false,
            data: formData,
            success: function () {
                $('#loadingIndicator').hide();
                $('#modal-add-to-tray').modal('hide');
                loadTrayList();
                findStock();
                swalToastSuccess(`ลงถาดแล้ว ${Object.keys(items).length} รายการ`);
            },
            error: async function (xhr) {
                $('#loadingIndicator').hide();
                let msg = xhr.responseJSON?.message || 'เกิดข้อผิดพลาด';
                await swalWarning(msg);
            }
        });
    });


    $(document).on('change', '#chkTraySelectAll', function () {
        const isChecked = $(this).is(':checked');
        $('#tbl-add-tray-body .chk-tray-item:enabled')
            .prop('checked', isChecked)
            .trigger('change');
    });


    $(document).on('change', '#tbl-add-tray-body .chk-tray-item', function () {
        const row = $(this).closest('tr');
        const input = row.find('.input-tray-qty');
        const maxQty = parseFloat(input.attr('max'));

        if ($(this).is(':checked')) {
            input.prop('disabled', false).val(maxQty);
        } else {
            input.prop('disabled', true).val('');
        }

        const allEnabled = $('#tbl-add-tray-body .chk-tray-item:enabled').length;
        const allChecked = $('#tbl-add-tray-body .chk-tray-item:enabled:checked').length;
        $('#chkTraySelectAll')
            .prop('checked', allEnabled > 0 && allChecked === allEnabled)
            .prop('indeterminate', allChecked > 0 && allChecked < allEnabled);
    });

    $(document).on("click", "#btnAddBreakDes", async function () {
        let txtAddBreakDes = $('#txtAddBreakDes').val();
        if (txtAddBreakDes == '') {
            $('#txtAddBreakDes').show();
        } else {
            await swalConfirm(
                `ต้องการเพิ่มอาการ "${txtAddBreakDes}" ใช่หรือไม่`, "ยืนยันการเพิ่มอาการใหม่", async () => {
                    const formData = new FormData();
                    formData.append("breakDescription", txtAddBreakDes);

                    $.ajax({
                        url: urlAddNewBreakDescription,
                        type: 'POST',
                        processData: false,
                        contentType: false,
                        data: formData,
                        success: async (res) => {
                            $('#loadingIndicator').hide();
                            $('#txtAddBreakDes').val('').hide();

                            $('#ddlBreakDes').empty();
                            $('#ddlBreakDes').append(new Option('-- เลือกอาการ --', ''));
                            res.forEach(item => {
                                $('#ddlBreakDes').append(new Option(item.name, item.breakDescriptionId));
                            });
                            $('#ddlBreakDes').val(res[res.length - 1].breakDescriptionId);

                            swalToastSuccess("เพิ่มอาการเรียบร้อย");
                        },
                        error: async (xhr) => {
                            $('#loadingIndicator').hide();
                            let msg = xhr.responseJSON?.message || xhr.responseText || 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
                            await swalWarning(`เกิดข้อผิดพลาด (${xhr.status} ${msg})`);
                        }
                    });
                }
            );
        }
    });

    $(document).on('click', '#btnAddBreak', async function () {
        const breakQty = $('#txtBreakQty').val();
        const breakDes = $('#ddlBreakDes').val();
        const stockId = $('#hddStockId').val();

        if (breakQty == '' || breakQty == 0) {
            await swalWarning('กรุณากรอกจำนวน งานชำรุด');
            return;
        }

        if (breakDes == '' || breakDes == 0) {
            await swalWarning('กรุณาเลือกอาการ');
            return;
        }

        await swalConfirm(
            `ต้องการเพิ่ม รายการชำรุด จำนวน ${breakQty} ชิ้น ใช่หรือไม่`, "ยืนยันการเพิ่ม รายการชำรุด", async () => {
                const formData = new FormData();
                formData.append("receivedId", stockId);
                formData.append("breakQty", breakQty);
                formData.append("breakDes", breakDes);
                $.ajax({
                    url: urlAddBreak,
                    type: 'POST',
                    processData: false,
                    contentType: false,
                    data: formData,
                    beforeSend: () => $('#loadingIndicator').show(),
                    success: async () => {
                        $('#loadingIndicator').hide();
                        CloseModal();
                        findStock();
                        await swalSuccess("เพิ่ม รายการชำรุด เรียบร้อย");
                    },
                    error: async (xhr) => {
                        $('#loadingIndicator').hide();
                        let msg = xhr.responseJSON?.message || xhr.responseText || 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
                        await swalWarning(`เกิดข้อผิดพลาด (${xhr.status} ${msg})`);
                    }
                });
            }
        );
    });

    $(document).on('change', '#chkSelectAllBreak', function () {
        const isChecked = $(this).is(':checked');
        $('#tbl-body-break .chk-row:enabled').prop('checked', isChecked);
    });
});

let _selectedArticle = null;
let _allArticles = [];

function renderArticlePanel(articles) {
    const container = $('#article-list');

    if (!articles || articles.length === 0) {
        container.html('<div class="text-center text-muted small p-2">ไม่พบ Article</div>');
        return;
    }


    const allBtn = `<button class="btn btn-sm w-100 text-start article-btn ${_selectedArticle === null ? 'btn-primary' : 'btn-outline-secondary'}" 
                        onclick="selectArticle(null)">
                        <i class="fas fa-th-list me-1"></i> ทั้งหมด
                    </button>`;

    const btns = articles.map(a => {
        const isActive = _selectedArticle === a;
        return `<button class="btn btn-sm w-100 text-start article-btn ${isActive ? 'btn-primary' : 'btn-outline-secondary'}" 
                    onclick="selectArticle('${html(a)}')" title="${html(a)}">
                    <i class="fas fa-tag me-1"></i> ${html(a)}
                </button>`;
    }).join('');

    container.html(allBtn + btns);
}

function filterArticlePanel() {
    const keyword = $('#txtArticleSearch').val().trim().toLowerCase();
    const filtered = keyword
        ? _allArticles.filter(a => a.toLowerCase().includes(keyword))
        : _allArticles;
    renderArticlePanel(filtered);
}

function selectArticle(article) {
    _selectedArticle = article;
    renderArticlePanel(
        $('#txtArticleSearch').val().trim()
            ? _allArticles.filter(a => a.toLowerCase().includes($('#txtArticleSearch').val().trim().toLowerCase()))
            : _allArticles
    );
    findStock();
    loadTrayList();
}

function clearArticleFilter() {
    $('#txtArticleSearch').val('');
    $('#ddlEDesArt').val('');
    $('#ddlUnit').val('');
    _selectedArticle = null;
    renderArticlePanel(_allArticles);
    findStock();
}

var _articlePanelVisible = true;
var _trayPanelVisible = true;

function updatePanelLayout() {
    const colArticle = $('#col-article');
    const colStock = $('#col-stock');
    const colTray = $('#col-tray');
    const btnShowArticle = $('#btnShowArticle');
    const btnShowTray = $('#btnShowTray');

    // Article Panel
    if (_articlePanelVisible) {
        colArticle.css({ 'max-width': '', 'padding': '', 'flex': '', 'opacity': '1' });
        btnShowArticle.addClass('d-none');
    } else {
        colArticle.css({ 'max-width': '0', 'padding': '0', 'flex': '0 0 0', 'opacity': '0' });
        btnShowArticle.removeClass('d-none');
    }

    // Tray Panel
    if (_trayPanelVisible) {
        colTray.css({ 'max-width': '', 'padding': '', 'flex': '', 'opacity': '1' });
        btnShowTray.addClass('d-none');
    } else {
        colTray.css({ 'max-width': '0', 'padding': '0', 'flex': '0 0 0', 'opacity': '0' });
        btnShowTray.removeClass('d-none');
    }

    // Stock Panel Width Calculation
    colStock.removeClass('col-lg-6 col-lg-8 col-lg-10 col-lg-12');

    let stockColSize = 12;
    if (_articlePanelVisible) stockColSize -= 2;
    if (_trayPanelVisible) stockColSize -= 4;

    colStock.addClass(`col-lg-${stockColSize}`);
}

function toggleArticlePanel() {
    _articlePanelVisible = !_articlePanelVisible;
    updatePanelLayout();
}

function toggleTrayPanel() {
    _trayPanelVisible = !_trayPanelVisible;
    updatePanelLayout();
}

let _allStockRows = [];
let _activeStockTab = 'general';

function switchStockTab(tab) {
    _activeStockTab = tab;

    $('#tab-stock-general').toggleClass('active', tab === 'general');
    $('#tab-stock-pending').toggleClass('active', tab === 'pending');
    renderStockTable();
}

function onFilterDDLChange() {
    _selectedArticle = null;
    findStock();
}

function findStock() {
    const article = _selectedArticle || '';
    const edesArt = $('#ddlEDesArt').val() || '';
    const unit = $('#ddlUnit').val() || '';
    const tbody = $('#tbl-stock-body');

    tbody.html('<tr><td colspan="9" class="text-center text-muted">กำลังค้นหา...</td></tr>');

    $.ajax({
        url: urlGetStockList,
        method: 'GET',
        data: { article: article, edesArt: edesArt, unit: unit },
        success: function (rows) {
            _allStockRows = rows || [];

            const generalCount = _allStockRows.filter(r => r.article).length;
            const pendingCount = _allStockRows.filter(r => !r.article && r.tempArticle).length;
            $('#badge-stock-general').text(generalCount);
            $('#badge-stock-pending').text(pendingCount);

            if (!_selectedArticle) {
                const filteredArticles = [...new Set(
                    _allStockRows
                        .map(r => r.article)
                        .filter(a => a)
                )].sort();
                _allArticles = filteredArticles;
                renderArticlePanel(filteredArticles);
            }

            renderStockTable();
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="9" class="text-danger text-center">เกิดข้อผิดพลาด (${xhr.status} ${xhr.statusText})</td></tr>`);
        }
    });
}

function renderStockTable() {
    const tbody = $('#tbl-stock-body');
    let rows;

    if (_activeStockTab === 'pending') {
        rows = _allStockRows.filter(r => !r.article && r.tempArticle);
    } else {
        rows = _allStockRows.filter(r => r.article);
    }

    if (!rows || rows.length === 0) {
        tbody.html('<tr><td colspan="9" class="text-center text-muted">ไม่พบข้อมูล</td></tr>');
        return;
    }

    const body = rows.map(function (r, i) {
        let statusBadge, actionBtn;

        if (_activeStockTab === 'pending') {
            statusBadge = `<span class="badge badge-warning">รอลงทะเบียน</span>`;
            actionBtn = '';
        } else if (r.isWithdrawn) {
            statusBadge = `<span class="badge badge-secondary">เบิกออกแล้ว</span>`;
            actionBtn = '';
        } else {
            if (r.isInTray) {
                const inTrayQty = r.ttQty - r.availableQty;
                let badges = `<span class="badge badge-success">ในคลัง</span><span class="badge badge-info">ถาด ${html(r.trayNo)} (${num(inTrayQty)})</span>`;
                if (r.borrowCount > 0) {
                    badges += `<span class="badge badge-warning"><i class="fas fa-hand-holding"></i> ยืมอยู่</span>`;
                }
                if (r.isRepairing) {
                    badges += `<span class="badge badge-danger"><i class="fas fa-hammer"></i> ส่งซ่อม</span>`;
                }
                statusBadge = `<div class="d-flex flex-column gap-1 align-items-center">${badges}</div>`;
            } else {
                let badges = `<span class="badge badge-success">ในคลัง</span>`;
                if (r.isRepairing) {
                    badges += `<span class="badge badge-danger"><i class="fas fa-hammer"></i> ส่งซ่อม</span>`;
                }
                statusBadge = r.isRepairing
                    ? `<div class="d-flex flex-column gap-1 align-items-center">${badges}</div>`
                    : `<span class="badge badge-success">ในคลัง</span>`;
            }

            const breakBtn = `<button class="btn btn-warning btn-sm w-100" onclick="showModalBreak(${r.receivedId}, '${html(r.article || r.tempArticle || '')}')" title="ส่งซ่อม">
                        <i class="fas fa-hammer"></i> ส่งซ่อม
                    </button>`;

            if (r.availableQty > 0) {
                actionBtn = `<div class="d-flex flex-column gap-1 align-items-center">
                    <button class="btn btn-info btn-sm w-100" onclick="showAddToTrayReverse(${r.receivedId}, '${html(r.article)}', ${r.availableQty})" title="ลงถาด">
                        <i class="fas fa-inbox"></i> ลงถาด
                    </button>
                    <button class="btn btn-danger btn-sm w-100" onclick="withdrawStock(${r.receivedId}, ${r.availableQty}, ${r.ttWg})" title="เบิกออก">
                        <i class="fas fa-file-export"></i> เบิก
                    </button>
                    ${breakBtn}
                </div>`;
            } else {
                actionBtn = `<div class="d-flex flex-column gap-1 align-items-center">
                    <span class="text-muted small">จองหมดแล้ว</span>
                    ${breakBtn}
                </div>`;
            }
        }

        return `
                    <tr data-received-id="${r.receivedId}">
                        <td>
                            <div class="image-zoom-container">
                                <img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(r.fileName)}" width="80" height="80" alt="Product Image">
                            </div>
                        </td>
                        <td class="text-center"><strong>${html(r.article || r.tempArticle || '')}</strong></td>
                        <td class="text-center">${html(r.orderNo)}</td>
                        <td><small>${html(r.eDesFn)}</small></td>
                        <td class="text-center">${r.listGem != null ? r.listGem : ''}</td>
                        <td class="text-center">${r.createDate}</td>
                        <td class="text-end">
                            <div>${num(r.ttQty)}</div>
                            ${r.ttQty !== r.availableQty ? `<small class="text-success" title="พร้อมเบิก">(${num(r.availableQty)})</small>` : ''}
                        </td>
                        <td class="text-center">${statusBadge}</td>
                        <td class="text-center">${actionBtn}</td>
                    </tr>`;
    }).join('');

    tbody.html(body);
}

function clearStockFilter() {
    clearArticleFilter();
}

function loadTrayList() {
    let article = $('#txtTraySearchArticle').val();
    if (!article && _selectedArticle) {
        article = _selectedArticle;
    }

    const tbody = $('#tbl-tray-body');
    tbody.html('<tr><td colspan="4" class="text-center text-muted">กำลังโหลด...</td></tr>');

    $.ajax({
        url: urlGetTrayList,
        method: 'GET',
        data: { article: article },
        success: function (trays) {
            if (!trays || trays.length === 0) {
                tbody.html('<tr><td colspan="2" class="text-center text-muted">ไม่พบถาด</td></tr>');
                return;
            }

            const rows = trays.map(function (t) {
                let articles = '';
                if (t.articleSummary) {
                    const articleList = t.articleSummary.split(',').map(s => s.trim());
                    if (articleList.length > 2) {
                        const display = articleList.slice(0, 2).join(', ') + '...';
                        articles = `<div class="text-muted small" title="${html(t.articleSummary)}" style="cursor: pointer;">
                                        <i class="fas fa-tags"></i> ${html(display)}
                                    </div>`;
                    } else {
                        articles = `<div class="text-muted small"><i class="fas fa-tags"></i> ${html(t.articleSummary)}</div>`;
                    }
                }

                return `
                    <tr data-tray-id="${t.trayId}">
                        <td>
                            <strong>${html(t.trayNo)}</strong>
                            ${t.description ? `<br><small class="text-muted">${html(t.description)}</small>` : ''}
                            ${articles}
                        </td>
                        <td class="text-center">
                            <div class="d-flex gap-1 justify-content-center">
                                <button class="btn btn-info btn-sm" onclick="showTrayDetail(${t.trayId}, '${html(t.trayNo)}')" title="ดูรายการ">
                                    <i class="fas fa-eye"></i>
                                </button>
                                <button class="btn btn-success btn-sm" onclick="showAddToTrayModal(${t.trayId}, '${html(t.trayNo)}')" title="เพิ่มสินค้า">
                                    <i class="fas fa-plus"></i>
                                </button>
                                <button class="btn btn-warning btn-sm" onclick="showBorrowList(${t.trayId})" title="รายการยืม">
                                    <i class="fas fa-hand-holding"></i>
                                </button>
                                ${t.borrowCount === 0 ? `<button class="btn btn-danger btn-sm" onclick="deleteTray(${t.trayId}, '${html(t.trayNo)}')" title="ลบถาด"><i class="fas fa-trash"></i></button>` : ''}
                            </div>
                        </td>
                    </tr>`;
            }).join('');

            tbody.html(rows);
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="4" class="text-danger text-center">เกิดข้อผิดพลาด</td></tr>`);
        }
    });
}



function showCreateTrayModal() {
    $('#txtNewTrayNo').val('');
    $('#txtNewTrayDesc').val('');
    $('#modal-create-tray').modal('show');
}

async function deleteTray(trayId, trayNo) {
    const result = await Swal.fire({
        title: `ลบถาด ${trayNo}?`,
        text: "สินค้าในถาดนี้จะถูกคืนสถานะเป็น 'พร้อมเบิก' ทั้งหมด",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'ยืนยันลบ',
        confirmButtonColor: '#d33',
        cancelButtonText: 'ยกเลิก'
    });

    if (!result.isConfirmed) return;

    $('#loadingIndicator').show();

    $.ajax({
        url: urlDeleteTray || '/Home/DeleteTray', // Fallback or defined global
        type: 'DELETE',
        data: { trayId: trayId },
        success: function () {
            $('#loadingIndicator').hide();
            loadTrayList();
            findStock();
            swalToastSuccess('ลบถาดเรียบร้อย');
        },
        error: async function (xhr) {
            $('#loadingIndicator').hide();
            let msg = xhr.responseJSON?.message || 'เกิดข้อผิดพลาด';
            await swalWarning(msg);
        }
    });
}



function showAddToTrayModal(trayId, trayNo) {
    $('#hddTrayId').val(trayId);
    $('#txtTitleAddToTray').html(`<i class="fas fa-inbox"></i> เลือกสินค้าลงถาด: <strong>${html(trayNo)}</strong>`);
    $('#txtAddToTrayArticle').val('');

    const modal = $('#modal-add-to-tray');
    const tbody = modal.find('#tbl-add-tray-body');
    tbody.html('<tr><td colspan="8" class="text-center text-muted">กำลังโหลด...</td></tr>');

    modal.modal('show');


    loadReceivedForTray(trayId, '', '', _selectedArticle || '');
}

function loadReceivedForTray(trayId, article) {
    const tbody = $('#tbl-add-tray-body');
    tbody.html('<tr><td colspan="8" class="text-center text-muted">กำลังค้นหา...</td></tr>');

    $.ajax({
        url: urlGetReceivedForTray,
        method: 'GET',
        data: { trayId: trayId, article: article },
        success: function (items) {
            tbody.empty();

            if (!items || items.length === 0) {
                tbody.append('<tr><td colspan="8" class="text-center text-muted">ไม่พบสินค้าที่สามารถลงถาดได้</td></tr>');
                return;
            }

            const rows = items.map(function (x) {
                return `
                    <tr data-received-id="${x.receivedId}">
                        <td>
                            <div class="image-zoom-container">
                                <img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(x.fileName || '')}" width="80" height="80" alt="Product Image">
                            </div>
                        </td>
                        <td class="text-center">${html(x.article || '')}<br /><small>${html(x.tempArticle || '')}</small></td>
                        <td>${html(x.orderNo)}</td>
                        <td><small>${html(x.eDesFn || '')}</small></td>
                        <td class="text-center">${html(x.listGem || '')}</td>
                        <td class="text-end text-success font-weight-bold">${num(x.availableQty)}</td>
                        <td class="text-center">
                            <div class="d-flex align-items-center justify-content-center">
                                <div class="chk-wrapper me-2">
                                    <input type="checkbox" id="chk_item_${x.receivedId}" class="chk-tray-item custom-checkbox">
                                    <label for="chk_item_${x.receivedId}"></label>
                                </div>
                                <input type="number" class="form-control input-tray-qty form-control-sm" 
                                       style="width: 100px;"
                                       value="" min="0.01" max="${x.availableQty}" step="any" disabled>
                            </div>
                        </td>
                    </tr>`;
            }).join('');

            tbody.append(rows);


            $('#chkTraySelectAll').prop('checked', false).prop('indeterminate', false);
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="8" class="text-danger text-center">เกิดข้อผิดพลาด (${xhr.status})</td></tr>`);
        }
    });
}



function showTrayDetail(trayId, trayNo) {
    $('#hddDetailTrayId').val(trayId);
    $('#txtTitleTrayDetail').html(`<i class="fas fa-box-open"></i> รายการในถาด: <strong>${html(trayNo)}</strong>`);

    const modal = $('#modal-tray-detail');
    const tbody = modal.find('#tbl-tray-detail-body');
    tbody.html('<tr><td colspan="10" class="text-center text-muted">กำลังโหลด...</td></tr>');

    modal.modal('show');

    $.ajax({
        url: urlGetTrayItems,
        method: 'GET',
        data: { trayId: trayId },
        success: function (items) {
            tbody.empty();

            if (!items || items.length === 0) {
                tbody.append('<tr><td colspan="11" class="text-center text-muted">ไม่มีสินค้าในถาดนี้</td></tr>');
                $('#txtTrayDetailSummary').text('');
                return;
            }

            let totalQty = 0;
            let totalWg = 0;

            const rows = items.map(function (x, i) {
                totalQty += Number(x.qty) || 0;
                totalWg += Number(x.wg) || 0;

                const availableQty = x.qty - x.borrowedQty;
                const statusBadge = x.isBorrowed
                    ? `<span class="badge badge-warning">ถูกยืมหมด</span>`
                    : (x.borrowedQty > 0 ? `<span class="badge badge-info">ยืมบางส่วน (${num(x.borrowedQty)})</span>` : `<span class="badge badge-success">อยู่ในถาด</span>`);


                const actionBtn = availableQty <= 0
                    ? ''
                    : `<button class="btn btn-warning btn-sm" onclick="borrowItem(${x.trayItemId}, ${availableQty})" title="ยืม">
                           <i class="fas fa-hand-holding"></i> ยืม
                       </button>
                       <button class="btn btn-danger btn-sm" onclick="removeFromTray(${x.trayItemId}, ${trayId}, '${html(trayNo)}')" title="นำออก">
                           <i class="fas fa-times"></i>
                       </button>`;

                return `
                    <tr data-tray-item-id="${x.trayItemId}">
                        <td>
                            <div class="image-zoom-container">
                                <img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(x.imgPath || '')}" width="80" height="80" alt="Product Image">
                            </div>
                        </td>
                        <td class="text-center">${html(x.article || '')}<br /><small>${html(x.tempArticle || '')}</small></td>
                        <td>${html(x.orderNo)}</td>
                        <td><small>${html(x.eDesFn || '')}</small></td>
                        <td class="text-center">${html(x.listGem || '')}</td>
                        <td class="text-center">${html(x.createdDate || '')}</td>
                        <td class="text-end">${num(x.qty)}</td>
                        <td class="text-center">${statusBadge}</td>
                        <td class="text-center">${actionBtn}</td>
                    </tr>`;
            }).join('');

            tbody.append(rows);
            $('#txtTrayDetailSummary').text(`รวม ${items.length} รายการ | จำนวน: ${num(totalQty)} | น้ำหนัก: ${num(totalWg)} g`);
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="11" class="text-danger text-center">เกิดข้อผิดพลาด</td></tr>`);
        }
    });
}



async function borrowItem(trayItemId, maxQty) {
    const { value: borrowQty } = await Swal.fire({
        title: 'ยืมสินค้าจากถาด',
        text: `ระบุจำนวนที่ต้องการยืม (สูงสุด ${num(maxQty)})`,
        input: 'number',
        inputAttributes: {
            min: 0.01,
            max: maxQty,
            step: 'any'
        },
        inputValue: maxQty,
        showCancelButton: true,
        confirmButtonText: 'ยืม',
        cancelButtonText: 'ยกเลิก',
        preConfirm: (value) => {
            if (!value || value <= 0) {
                Swal.showValidationMessage('กรุณาระบุจำนวนที่ถูกต้อง');
                return false;
            }
            if (parseFloat(value) > parseFloat(maxQty)) {
                Swal.showValidationMessage('จำนวนที่ยืมเกินกว่าที่มีในถาด');
                return false;
            }
            return value;
        }
    });

    if (!borrowQty) return;

    $('#loadingIndicator').show();

    $.ajax({
        url: urlBorrowFromTray,
        type: 'POST',
        data: { trayItemId: trayItemId, borrowQty: borrowQty },
        success: function () {
            $('#loadingIndicator').hide();
            const trayId = $('#hddDetailTrayId').val();
            const trayNo = $('#txtTitleTrayDetail').text().split(':')[1]?.trim() || '';
            showTrayDetail(trayId, trayNo);
            loadTrayList();
            swalToastSuccess('ยืมสินค้าเรียบร้อย');
        },
        error: async function (xhr) {
            $('#loadingIndicator').hide();
            let msg = xhr.responseJSON?.message || 'เกิดข้อผิดพลาด';
            await swalWarning(msg);
        }
    });
}

async function removeFromTray(trayItemId, trayId, trayNo) {
    const result = await Swal.fire({
        title: 'ยืนยันนำออกจากถาด',
        text: 'ต้องการนำสินค้ารายการนี้ออกจากถาด?',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'นำออก',
        cancelButtonText: 'ยกเลิก'
    });

    if (!result.isConfirmed) return;

    $('#loadingIndicator').show();

    const formData = new FormData();
    formData.append('trayItemIds', trayItemId);

    $.ajax({
        url: urlRemoveFromTray,
        type: 'PATCH',
        processData: false,
        contentType: false,
        data: formData,
        success: function () {
            $('#loadingIndicator').hide();
            showTrayDetail(trayId, trayNo);
            loadTrayList();
            findStock();
            swalToastSuccess('นำสินค้าออกจากถาดเรียบร้อย');
        },
        error: async function (xhr) {
            $('#loadingIndicator').hide();
            let msg = xhr.responseJSON?.message || 'เกิดข้อผิดพลาด';
            await swalWarning(msg);
        }
    });
}

function showBorrowList(trayId) {
    const modal = $('#modal-borrow-list');
    const tbody = modal.find('#tbl-borrow-body');
    tbody.html('<tr><td colspan="8" class="text-center text-muted">กำลังโหลด...</td></tr>');

    modal.modal('show');

    $.ajax({
        url: urlGetBorrowList,
        method: 'GET',
        data: { trayId: trayId || null },
        success: function (borrows) {
            tbody.empty();

            if (!borrows || borrows.length === 0) {
                tbody.append('<tr><td colspan="9" class="text-center text-muted">ไม่มีรายการยืม</td></tr>');
                return;
            }

            const rows = borrows.map(function (b, i) {
                return `
                    <tr data-borrow-id="${b.trayBorrowId}">
                        <td>
                            <div class="image-zoom-container">
                                <img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(b.imgPath || '')}" width="80" height="80" alt="Product Image">
                            </div>
                        </td>
                        <td class="text-center">
                            ${html(b.article || '')}<br /><small>${html(b.tempArticle || '')}</small>
                        </td>
                        <td><small>${html(b.eDesFn || '')}</small></td>
                        <td class="text-center">${html(b.listGem || '')}</td>
                        <td class="text-center"><strong>${html(b.trayNo)}</strong></td>
                        <td class="text-end">${num(b.borrowQty)}</td>
                        <td class="text-muted small">${html(b.borrowedDate)}</td>
                        <td class="text-center">
                            <button class="btn btn-primary btn-sm" onclick="returnToTray(${b.trayBorrowId})">
                                <i class="fas fa-undo"></i> คืน
                            </button>
                        </td>
                    </tr>`;
            }).join('');

            tbody.append(rows);
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="8" class="text-danger text-center">เกิดข้อผิดพลาด</td></tr>`);
        }
    });
}

async function returnToTray(trayBorrowId) {
    const result = await Swal.fire({
        title: 'ยืนยันคืนสินค้า',
        text: 'ต้องการคืนสินค้ากลับถาด?',
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: 'คืน',
        cancelButtonText: 'ยกเลิก'
    });

    if (!result.isConfirmed) return;

    $('#loadingIndicator').show();

    $.ajax({
        url: urlReturnToTray,
        type: 'PATCH',
        data: { trayBorrowId: trayBorrowId },
        success: function () {
            $('#loadingIndicator').hide();

            const trayId = $('#hddDetailTrayId').val();
            showBorrowList(trayId || null);
            loadTrayList();
            swalToastSuccess('คืนสินค้ากลับถาดเรียบร้อย');
        },
        error: async function (xhr) {
            $('#loadingIndicator').hide();
            let msg = xhr.responseJSON?.message || 'เกิดข้อผิดพลาด';
            await swalWarning(msg);
        }
    });
}



async function withdrawStock(receivedId, maxQty, maxWg) {
    const { value: formValues } = await Swal.fire({
        title: 'เบิกสินค้าออก',
        html: `
            <div class="form-group text-left">
                <label>จำนวนที่ต้องการเบิก (สูงสุด ${num(maxQty)})</label>
                <input id="swal-input-qty" type="number" class="form-control" value="${maxQty}" min="0.01" max="${maxQty}" step="any">
            </div>
            <div class="form-group text-left mt-3">
                <label>หมายเหตุ</label>
                <input id="swal-input-remark" type="text" class="form-control" placeholder="เช่น เบิกให้ลูกค้า, เบิกเพื่อใช้งาน">
            </div>
        `,
        focusConfirm: false,
        showCancelButton: true,
        confirmButtonText: 'เบิกออก',
        cancelButtonText: 'ยกเลิก',
        confirmButtonColor: '#dc3545',
        preConfirm: () => {
            const qty = document.getElementById('swal-input-qty').value;
            const remark = document.getElementById('swal-input-remark').value;

            if (!qty || qty <= 0) {
                Swal.showValidationMessage('กรุณาระบุจำนวนที่ถูกต้อง');
                return false;
            }
            if (parseFloat(qty) > parseFloat(maxQty)) {
                Swal.showValidationMessage('จำนวนที่เบิกเกินกว่าที่มีในสต็อก');
                return false;
            }

            return { qty: qty, remark: remark };
        }
    });

    if (!formValues) return;

    $('#loadingIndicator').show();

    const formData = new FormData();
    formData.append('receivedId', receivedId);
    formData.append('withdrawQty', formValues.qty);
    if (formValues.remark) formData.append('remark', formValues.remark);

    $.ajax({
        url: urlWithdrawFromStock,
        type: 'POST',
        processData: false,
        contentType: false,
        data: formData,
        success: function () {
            $('#loadingIndicator').hide();
            findStock();
            swalToastSuccess('เบิกสินค้าออกเรียบร้อย');
        },
        error: async function (xhr) {
            $('#loadingIndicator').hide();
            let msg = xhr.responseJSON?.message || 'เกิดข้อผิดพลาด';
            await swalWarning(msg);
        }
    });
}

function showWithdrawalHistory() {
    const modal = $('#modal-withdrawal-history');
    const tbody = modal.find('#tbl-withdrawal-body');
    tbody.html('<tr><td colspan="8" class="text-center text-muted">กำลังโหลด...</td></tr>');

    modal.modal('show');

    $.ajax({
        url: urlGetWithdrawalList,
        method: 'GET',
        success: function (items) {
            tbody.empty();

            if (!items || items.length === 0) {
                tbody.append('<tr><td colspan="8" class="text-center text-muted">ยังไม่มีรายการเบิกออก</td></tr>');
                $('#txtWithdrawalSummary').text('');
                return;
            }

            let totalQty = 0;
            let totalWg = 0;

            const rows = items.map(function (x) {
                totalQty += Number(x.qty) || 0;
                totalWg += Number(x.wg) || 0;

                return `
                    <tr data-withdrawal-id="${x.withdrawalId}">
                        <td>
                            <div class="image-zoom-container">
                                <img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(x.imgPath || "")}" width="80" height="80" alt="Product Image">
                            </div>
                        </td>
                        <td class="text-center">
                            ${html(x.article || x.tempArticle || "")}<br /><small>${html(x.tempArticle || "")}</small>
                        </td>
                        <td class="text-center"><strong>${html(x.orderNo)}</strong></td>
                        <td><small>${html(x.eDesFn || "")}</small></td>
                        <td class="text-center">${html(x.listGem || "")}</td>
                        <td class="text-end">${num(x.qty)}</td>
                        <td>${x.remark ? html(x.remark) : '<span class="text-muted">-</span>'}</td>
                        <td class="text-center text-muted small">${html(x.withdrawnDate)}</td>
                    </tr>`;
            }).join('');

            tbody.append(rows);
            $('#txtWithdrawalSummary').text(`รวม ${items.length} รายการ | จำนวน: ${num(totalQty)} | น้ำหนัก: ${num(totalWg)} g`);
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="8" class="text-danger text-center">เกิดข้อผิดพลาด</td></tr>`);
        }
    });
}

// ==================== Reverse Add to Tray ====================

let _reverseStockId = null;
let _reverseSelectedTrayId = null;

function showAddToTrayReverse(stockId, article, maxQty) {
    _reverseStockId = stockId;
    _reverseSelectedTrayId = null;

    $('#hddReverseStockId').val(stockId);
    $('#txtReverseSearchTray').val(article || '');
    $('#txtReverseQty').val(maxQty).attr('max', maxQty);
    $('#lblReverseAvailableQty').text(num(maxQty));

    loadReverseTrayList();
    $('#modal-select-tray').modal('show');
}

function loadReverseTrayList() {
    const search = $('#txtReverseSearchTray').val();
    const tbody = $('#tbl-reverse-tray-list');
    tbody.html('<tr><td colspan="5" class="text-center text-muted">กำลังค้นหา...</td></tr>');

    $.ajax({
        url: urlGetTrayList,
        method: 'GET',
        data: { article: search },
        success: function (trays) {
            tbody.empty();
            if (!trays || trays.length === 0) {
                tbody.append('<tr><td colspan="5" class="text-center text-muted">ไม่พบถาด</td></tr>');
                return;
            }

            const rows = trays.map(t => {
                const isSelected = _reverseSelectedTrayId === t.trayId;
                const bgClass = isSelected ? 'bg-info' : '';
                return `
                    <tr style="cursor: pointer;" class="${bgClass}" onclick="selectReverseTray(${t.trayId}, this)">
                        <td class="text-center">
                            <input type="radio" name="radReverseTray" value="${t.trayId}" ${isSelected ? 'checked' : ''}>
                        </td>
                        <td><strong>${html(t.trayNo)}</strong></td>
                        <td>${html(t.description || '')}</td>
                        <td class="text-center">${t.itemCount}</td>
                        <td><small>${html(t.articleSummary || '')}</small></td>
                    </tr>
                `;
            }).join('');
            tbody.append(rows);
        },
        error: function () {
            tbody.html('<tr><td colspan="5" class="text-center text-danger">เกิดข้อผิดพลาด</td></tr>');
        }
    });
}

function selectReverseTray(trayId, tr) {
    _reverseSelectedTrayId = trayId;
    $('#tbl-reverse-tray-list tr').removeClass('bg-info');
    $(tr).addClass('bg-info');
    $(tr).find('input[type="radio"]').prop('checked', true);
}

function confirmAddToTrayReverse() {
    if (!_reverseSelectedTrayId) {
        swalWarning('กรุณาเลือกถาดปลายทาง');
        return;
    }

    const qty = parseFloat($('#txtReverseQty').val());
    if (!qty || qty <= 0) {
        swalWarning('กรุณาระบุจำนวนที่ถูกต้อง');
        return;
    }

    const max = parseFloat($('#txtReverseQty').attr('max'));
    if (qty > max) {
        swalWarning(`จำนวนเกินที่มี (Max: ${max})`);
        return;
    }

    $('#loadingIndicator').show();

    // Reuse existing AddToTray API
    // Mapping: Dictionary<int, decimal> -> JSON
    const items = {};
    items[_reverseStockId] = qty;

    const formData = new FormData();
    formData.append('trayId', _reverseSelectedTrayId);
    formData.append('itemsJson', JSON.stringify(items));

    $.ajax({
        url: urlAddToTray,
        type: 'POST',
        processData: false,
        contentType: false,
        data: formData,
        success: function () {
            $('#loadingIndicator').hide();
            $('#modal-select-tray').modal('hide');

            // Refresh main lists
            loadTrayList();
            findStock();

            swalToastSuccess('ลงถาดเรียบร้อย');
        },
        error: async function (xhr) {
            $('#loadingIndicator').hide();
            let msg = xhr.responseJSON?.message || 'เกิดข้อผิดพลาด';
            await swalWarning(msg);
        }
    });
}

async function printBreakToPDF() {
    const stockId = $('#hddStockId').val();

    const tbekbody = $('#tbl-body-break');
    const breakIDs = [];

    tbekbody.find('tr').each(function () {
        const chk = $(this).find('.chk-row');
        if (chk.is(':checked')) {
            const breakID = $(this).data('break-id');
            if (breakID) breakIDs.push(breakID);
        }
    });

    if (breakIDs.length === 0) {
        await swalWarning('กรุณาเลือก break ที่ต้องการพิมพ์');
        return;
    }

    let model = {
        ReceivedId: stockId ? parseInt(stockId) : null,
        BreakIDs: breakIDs
    };

    let pdfWindow = window.open('', '_blank');

    $.ajax({
        url: urlBreakReport,
        type: 'POST',
        data: JSON.stringify(model),
        contentType: "application/json; charset=utf-8",
        xhrFields: {
            responseType: 'blob'
        },
        success: function (data) {
            const blob = new Blob([data], { type: 'application/pdf' });
            const blobUrl = URL.createObjectURL(blob);

            if (pdfWindow) {
                pdfWindow.location = blobUrl;
            }
        },
        error: async function (xhr) {
            if (pdfWindow) {
                pdfWindow.close();
            }

            let msg = xhr.responseJSON?.message || xhr.responseText || 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
            await swalWarning(`เกิดข้อผิดพลาด (${xhr.status} ${msg})`);
        }
    });
}

async function showModalBreak(receivedId, article) {
    $('#hddStockId').val(receivedId || '');

    const modal = $('#modal-break');
    const tbody = modal.find('#tbl-body-break');
    tbody.empty().append('<tr><td colspan="12" class="text-center text-muted">กำลังโหลด...</td></tr>');

    if (receivedId) {
        $('#btnAddBreakList').removeClass('d-none');
        modal.find('#txtTitleBreak').html("<i class='fas fa-hammer'></i> รายการแจ้งซ่อม : " + html(article || String(receivedId)));
    }
    else {
        $('#btnAddBreakList').addClass('d-none');
        modal.find('#txtTitleBreak').html("<i class='fas fa-hammer'></i> รายการแจ้งซ่อมทั้งหมด");
    }

    modal.modal('show');

    let model = {
        ReceivedId: receivedId ? parseInt(receivedId) : null,
    };

    $.ajax({
        url: urlGetBreak,
        type: 'POST',
        data: JSON.stringify(model),
        contentType: "application/json; charset=utf-8",
        success: function (data) {
            tbody.empty();
            if (!data || data.length === 0) {
                tbody.append('<tr><td colspan="12" class="text-center text-muted">ไม่พบข้อมูล</td></tr>');
                return;
            }
            const rows = data.map(function (x, i) {
                return `
                <tr data-break-id="${html(x.breakID)}">
                    <td>
                        <div class="image-zoom-container">
                            <img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(x.imgPath || '')}" width="80" height="80" alt="Product Image">
                        </div>
                    </td>
                    <td>${html(x.article)}</td>
                    <td>${html(x.orderNo)}</td>
                    <td>${html(x.edesFn)}</td>
                    <td>${html(x.breakDescription)}</td>
                    <td>${html(x.listGem)}</td>
                    <td class="text-center">${html(x.createDateTH)}</td>
                    <td class="text-end">${html(x.breakQty)}</td>
                    <td class="text-center">${x.isReported ? '✔️' : '❌'}</td>
                    <td class="text-center">
                        <div class="chk-wrapper">
                            <input type="checkbox" id="${x.breakID}_as${i}" class="chk-row custom-checkbox" ${!x.isReported ? 'checked' : ''}>
                            <label for="${x.breakID}_as${i}" class="d-none"></label>
                        </div>
                    </td>
                </tr>`;
            }).join('');
            tbody.append(rows);
        },
        error: async function (xhr) {
            tbody.empty().append('<tr><td colspan="12" class="text-center text-muted">ไม่พบข้อมูล</td></tr>');
            let msg = xhr.responseJSON?.message || xhr.responseText || 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
            await swalWarning(`เกิดข้อผิดพลาด (${xhr.status} ${msg})`);
        }
    });
}

async function showModalAddBreak() {
    $('#txtBreakQty').val(0)
    $('#ddlBreakDes').val(null)
    $('#txtAddBreakDes').val('')
    $('#txtAddBreakDes').hide();

    $('#ddlBreakDes').select2({
        dropdownParent: $('#modal-add-break'),
    });

    $('#txtBreakQty').val(0)
    const modal = $('#modal-add-break');
    modal.modal('show');
}