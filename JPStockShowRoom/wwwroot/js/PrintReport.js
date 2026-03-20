async function printStockToPDF(article, edesArt, unit, registrationStatus) {
    if (article === undefined) article = ($('#txtStockArticle').val() || '').trim() || null;
    if (edesArt === undefined) edesArt = $('#ddlStockEDesArt').val() || null;
    if (unit === undefined) unit = null;
    if (registrationStatus === undefined) {
        const raw = $('#ddlStockUnit').val();
        registrationStatus = raw ? parseInt(raw) : null;
    }
    const orderNoFrom = ($('#txtStockOrderNoFrom').val() || '').trim() || null;
    const orderNoTo = ($('#txtStockOrderNoTo').val() || '').trim() || null;

    const model = { Article: article || null, EDesArt: edesArt || null, Unit: unit || null, RegistrationStatus: registrationStatus, OrderNoFrom: orderNoFrom, OrderNoTo: orderNoTo };
    const pdfWindow = window.open('', '_blank');

    $.ajax({
        url: urlStockReport,
        type: 'POST',
        data: JSON.stringify(model),
        contentType: 'application/json; charset=utf-8',
        xhrFields: { responseType: 'blob' },
        success: function (data) {
            const blob = new Blob([data], { type: 'application/pdf' });
            const blobUrl = URL.createObjectURL(blob);
            if (pdfWindow) {
                pdfWindow.location = blobUrl;
            }
        },
        error: async function (xhr) {
            if (pdfWindow) pdfWindow.close();
            let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
            try {
                const text = await new Response(xhr.response).text();
                const json = JSON.parse(text);
                msg = json.message || text;
            } catch {
                msg = xhr.statusText || msg;
            }
            await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
        }
    });
}

async function printStockNoIMGToPDF(article, edesArt, unit, registrationStatus) {
    if (article === undefined) article = ($('#txtStockArticle').val() || '').trim() || null;
    if (edesArt === undefined) edesArt = $('#ddlStockEDesArt').val() || null;
    if (unit === undefined) unit = null;
    if (registrationStatus === undefined) {
        const raw = $('#ddlStockUnit').val();
        registrationStatus = raw ? parseInt(raw) : null;
    }
    const orderNoFrom = ($('#txtStockOrderNoFrom').val() || '').trim() || null;
    const orderNoTo = ($('#txtStockOrderNoTo').val() || '').trim() || null;

    const model = { Article: article || null, EDesArt: edesArt || null, Unit: unit || null, RegistrationStatus: registrationStatus, OrderNoFrom: orderNoFrom, OrderNoTo: orderNoTo };
    const pdfWindow = window.open('', '_blank');

    $.ajax({
        url: urlStockNoIMGReport,
        type: 'POST',
        data: JSON.stringify(model),
        contentType: 'application/json; charset=utf-8',
        xhrFields: { responseType: 'blob' },
        success: function (data) {
            const blob = new Blob([data], { type: 'application/pdf' });
            const blobUrl = URL.createObjectURL(blob);
            if (pdfWindow) pdfWindow.location = blobUrl;
        },
        error: async function (xhr) {
            if (pdfWindow) pdfWindow.close();
            let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
            try {
                const text = await new Response(xhr.response).text();
                const json = JSON.parse(text);
                msg = json.message || text;
            } catch {
                msg = xhr.statusText || msg;
            }
            await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
        }
    });
}

function switchBorrowTab(tab) {
    $('#tab-borrow-headers-link').toggleClass('active', tab === 'headers');
    $('#tab-borrow-pending-link').toggleClass('active', tab === 'pending');
    $('#tab-borrow-headers').toggle(tab === 'headers');
    $('#tab-borrow-pending').toggle(tab === 'pending');
}

function updateBorrowPendingToolbar() {
    const n = $('.chk-borrow-pending:checked').length;
    $('#toolbar-create-borrow').toggle(n > 0);
    $('#lbl-borrow-pending-selected').text(n > 0 ? `เลือก ${n} รายการ` : '');
}

function searchBorrow() {
    loadBorrowHeaders();
    loadPendingBorrows();
}

function clearBorrowFilter() {
    $('#txtBorrowArticle').val('');
    $('#txtBorrowNo').val('');
    $('#ddlBorrowEDesArt').val(null).trigger('change');
    $('#ddlBorrowStatusFilter').val('pending');
    searchBorrow();
}

function loadPendingBorrows() {
    const article = ($('#txtBorrowArticle').val() || '').trim() || undefined;
    const edesArt = $('#ddlBorrowEDesArt').val() || undefined;
    const statusFilter = $('#ddlBorrowStatusFilter').val() || 'pending';
    const isReturned = statusFilter === 'all' ? undefined : (statusFilter === 'returned');
    const tbody = $('#tbl-borrow-pending-body');
    tbody.html('<tr><td colspan="9" class="text-center text-muted p-3">กำลังโหลด...</td></tr>');
    $('#chk-borrow-pending-all').prop('checked', false);
    $('#toolbar-create-borrow').hide();
    $.ajax({
        url: urlGetPendingBorrowDetails,
        method: 'GET',
        data: { article, edesArt, isReturned },
        success: function (rows) {
            const count = rows ? rows.length : 0;
            $('#badge-borrow-pending-count').toggle(count > 0).text(count);
            if (count === 0) {
                tbody.html('<tr><td colspan="9" class="text-center text-muted p-3">ไม่มีรายการ</td></tr>');
                return;
            }
            tbody.html(rows.map(function (r) {
                const statusBadge = r.isReturned
                    ? '<span class="badge badge-success">คืนแล้ว</span>'
                    : '<span class="badge badge-warning">ยังไม่คืน</span>';
                const checkCell = !r.isReturned
                    ? `<input type="checkbox" class="chk-borrow-pending custom-checkbox" value="${r.borrowDetailId}" onchange="updateBorrowPendingToolbar()">`
                    : '';
                const deleteBtn = !r.isReturned && !r.borrowNo
                    ? `<button class="btn btn-sm btn-outline-danger" onclick="cancelPendingBorrow(${r.borrowDetailId})" title="ยกเลิกการยืม"><i class="fas fa-trash"></i></button>`
                    : '';
                return `<tr>
                    <td class="text-center"><div class="image-zoom-container"><img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(r.imgPath || '')}" width="50" height="50" alt=""></div></td>
                    <td class="text-center">${html(r.article || '')}</td>
                    <td class="text-center">${html(r.eDesFn || '')}</td>
                    <td class="text-center">${html(r.listGem || '')}</td>
                    <td class="text-end">${num(r.borrowQty)}</td>
                    <td class="text-center">${html(r.borrowedDate)}</td>
                    <td class="text-center">${statusBadge}</td>
                    <td class="text-center">${checkCell}</td>
                    <td class="text-center">${deleteBtn}</td>
                </tr>`;
            }).join(''));
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="9" class="text-danger text-center p-3">เกิดข้อผิดพลาด (${xhr.status})</td></tr>`);
        }
    });
}

function toggleAllBorrowPending(chk) {
    $('.chk-borrow-pending').prop('checked', chk.checked);
    updateBorrowPendingToolbar();
}

async function cancelPendingBorrow(id) {
    await swalConfirm('ยกเลิกการยืมรายการนี้?', 'ยืนยันการยกเลิก', async function () {
        $.ajax({
            url: urlCancelPendingBorrow,
            method: 'POST',
            contentType: 'application/json; charset=utf-8',
            data: JSON.stringify(id),
            success: function () {
                swalSuccess('ยกเลิกการยืมสำเร็จ');
                loadPendingBorrows();
            },
            error: async function (xhr) {
                let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
                try { const json = JSON.parse(xhr.responseText); msg = json.message || msg; } catch { msg = xhr.statusText || msg; }
                await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
            }
        });
    });
}

async function createBorrowDocument() {
    const ids = $('.chk-borrow-pending:checked').map(function () { return parseInt(this.value); }).get();
    if (ids.length === 0) {
        await swalWarning('กรุณาเลือกรายการอย่างน้อย 1 รายการ');
        return;
    }
    await swalConfirm(`สร้างใบยืมจาก ${ids.length} รายการ?`, 'ยืนยัน', async function () {
        $.ajax({
            url: urlCreateBorrowDocument,
            method: 'POST',
            contentType: 'application/json; charset=utf-8',
            data: JSON.stringify(ids),
            success: function (result) {
                swalSuccess(`สร้างใบยืม ${result.borrowNo} สำเร็จ`);
                loadBorrowHeaders();
                loadPendingBorrows();
                switchBorrowTab('headers');
            },
            error: async function (xhr) {
                let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
                try { const json = JSON.parse(xhr.responseText); msg = json.message || msg; } catch { msg = xhr.statusText || msg; }
                await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
            }
        });
    });
}

function loadBorrowHeaders() {
    const article = ($('#txtBorrowArticle').val() || '').trim() || undefined;
    const borrowNo = ($('#txtBorrowNo').val() || '').trim() || undefined;
    const edesArt = $('#ddlBorrowEDesArt').val() || undefined;
    const statusFilter = $('#ddlBorrowStatusFilter').val() || 'pending';
    const isReturned = statusFilter === 'all' ? undefined : (statusFilter === 'returned');
    const tbody = $('#tbl-borrow-headers-body');
    tbody.html('<tr><td colspan="6" class="text-center text-muted p-3">กำลังโหลด...</td></tr>');
    $.ajax({
        url: urlGetBorrowHeaders,
        method: 'GET',
        data: { article, borrowNo, edesArt, isReturned },
        success: function (rows) {
            if (!rows || rows.length === 0) {
                tbody.html('<tr><td colspan="6" class="text-center text-muted p-3">ไม่พบข้อมูล</td></tr>');
                return;
            }
            tbody.html(rows.map(function (r, i) {
                const allReturned = r.returnedCount >= r.itemCount && r.itemCount > 0;
                const statusBadge = allReturned
                    ? '<span class="badge badge-success">คืนครบ</span>'
                    : `<span class="badge badge-warning">คืน ${r.returnedCount}/${r.itemCount}</span>`;
                return `<tr>
                    <td class="text-center">${i + 1}</td>
                    <td><strong>${html(r.borrowNo)}</strong></td>
                    <td>${html(r.createDate)}</td>
                    <td class="text-center">${r.itemCount}</td>
                    <td class="text-center">${statusBadge}</td>
                    <td class="text-center">
                        <button class="btn btn-sm btn-outline-primary mr-1" onclick="showBorrowDetails('${html(r.borrowNo)}')">
                            <i class="fas fa-eye"></i> ดูรายการ
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="printBorrowByNo('${html(r.borrowNo)}')">
                            <i class="fas fa-print"></i>
                        </button>
                    </td>
                </tr>`;
            }).join(''));
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="6" class="text-danger text-center p-3">เกิดข้อผิดพลาด (${xhr.status})</td></tr>`);
        }
    });
}

let _borrowDetailRows = [];

function showBorrowDetails(borrowNo) {
    $('#lbl-borrow-no').text(borrowNo);
    _borrowDetailRows = [];
    const tbody = $('#tbl-borrow-detail-body');
    tbody.html('<tr><td colspan="7" class="text-center text-muted p-3">กำลังโหลด...</td></tr>');
    $('#modal-borrow-detail').modal('show');
    $.ajax({
        url: urlGetBorrowDetailsByNo,
        method: 'GET',
        data: { borrowNo: borrowNo },
        success: function (rows) {
            _borrowDetailRows = rows || [];
            renderBorrowDetails();
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="7" class="text-danger text-center p-3">เกิดข้อผิดพลาด (${xhr.status})</td></tr>`);
        }
    });
}

function renderBorrowDetails() {
    const filter = $('input[name="borrow-detail-filter"]:checked').val() || 'all';
    let rows = _borrowDetailRows;
    if (filter === 'pending') rows = rows.filter(function (r) { return !r.isReturned; });
    else if (filter === 'returned') rows = rows.filter(function (r) { return r.isReturned; });

    const tbody = $('#tbl-borrow-detail-body');
    if (rows.length === 0) {
        tbody.html('<tr><td colspan="7" class="text-center text-muted p-3">ไม่พบรายการ</td></tr>');
        return;
    }
    tbody.html(rows.map(function (r) {
        const statusBadge = r.isReturned
            ? `<span class="badge badge-success">คืนแล้ว</span>${r.returnedDate ? '<br><small class="text-muted">' + html(r.returnedDate) + '</small>' : ''}`
            : '<span class="badge badge-warning">ยังไม่คืน</span>';
        return `<tr>
                    <td class="text-center"><div class="image-zoom-container"><img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(r.imgPath || '')}" width="50" height="50" alt=""></div></td>
                    <td>${html(r.article || '')}</td>
                    <td>${html(r.eDesFn || '')}</td>
                    <td>${html(r.listGem || '')}</td>
                    <td class="text-center">${num(r.borrowQty)}</td>
                    <td class="text-center">${html(r.borrowedDate)}</td>
                    <td class="text-center">${statusBadge}</td>
                </tr>`;
    }).join(''));
}

async function printBorrowByNo(borrowNo) {
    const model = { BorrowNo: borrowNo };
    const pdfWindow = window.open('', '_blank');

    $.ajax({
        url: urlBorrowReport,
        type: 'POST',
        data: JSON.stringify(model),
        contentType: 'application/json; charset=utf-8',
        xhrFields: { responseType: 'blob' },
        success: function (data) {
            const blob = new Blob([data], { type: 'application/pdf' });
            const blobUrl = URL.createObjectURL(blob);
            if (pdfWindow) {
                pdfWindow.location = blobUrl;
            }
        },
        error: async function (xhr) {
            if (pdfWindow) pdfWindow.close();
            let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
            try {
                const text = await new Response(xhr.response).text();
                const json = JSON.parse(text);
                msg = json.message || text;
            } catch {
                msg = xhr.statusText || msg;
            }
            await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
        }
    });
}

function switchBreakTab(tab) {
    $('#tab-break-headers-link').toggleClass('active', tab === 'headers');
    $('#tab-break-pending-link').toggleClass('active', tab === 'pending');
    $('#tab-break-headers').toggle(tab === 'headers');
    $('#tab-break-pending').toggle(tab === 'pending');
}

function updateBreakPendingToolbar() {
    const n = $('.chk-break-pending:checked').length;
    $('#toolbar-create-break').toggle(n > 0);
    $('#lbl-break-pending-selected').text(n > 0 ? `เลือก ${n} รายการ` : '');
}

function searchBreak() {
    loadBreakHeaders();
    loadPendingBreaks();
}

function clearBreakFilter() {
    $('#txtBreakArticle').val('');
    $('#txtBreakNo').val('');
    $('#ddlBreakEDesArt').val(null).trigger('change');
    searchBreak();
}

function loadBreakHeaders() {
    const article = ($('#txtBreakArticle').val() || '').trim() || undefined;
    const breakNo = ($('#txtBreakNo').val() || '').trim() || undefined;
    const edesArt = $('#ddlBreakEDesArt').val() || undefined;
    const tbody = $('#tbl-break-headers-body');
    tbody.html('<tr><td colspan="5" class="text-center text-muted p-3">กำลังโหลด...</td></tr>');
    $.ajax({
        url: urlGetBreakHeaders,
        method: 'GET',
        data: { article, breakNo, edesArt },
        success: function (rows) {
            if (!rows || rows.length === 0) {
                tbody.html('<tr><td colspan="5" class="text-center text-muted p-3">ไม่พบข้อมูล</td></tr>');
                return;
            }
            tbody.html(rows.map(function (r, i) {
                return `<tr>
                    <td class="text-center">${i + 1}</td>
                    <td><strong>${html(r.breakNo)}</strong></td>
                    <td>${html(r.createDate)}</td>
                    <td class="text-center">${r.itemCount}</td>
                    <td class="text-center">
                        <button class="btn btn-sm btn-outline-primary mr-1" onclick="showBreakDetails('${html(r.breakNo)}')">
                            <i class="fas fa-eye"></i> ดูรายการ
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="printBreakByNo('${html(r.breakNo)}')">
                            <i class="fas fa-print"></i>
                        </button>
                    </td>
                </tr>`;
            }).join(''));
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="5" class="text-danger text-center p-3">เกิดข้อผิดพลาด (${xhr.status})</td></tr>`);
        }
    });
}

function loadPendingBreaks() {
    const article = ($('#txtBreakArticle').val() || '').trim() || undefined;
    const edesArt = $('#ddlBreakEDesArt').val() || undefined;
    const tbody = $('#tbl-break-pending-body');
    tbody.html('<tr><td colspan="8" class="text-center text-muted p-3">กำลังโหลด...</td></tr>');
    $('#chk-break-pending-all').prop('checked', false);
    $('#toolbar-create-break').hide();
    $.ajax({
        url: urlGetPendingBreakDetails,
        method: 'GET',
        data: { article, edesArt },
        success: function (rows) {
            const count = rows ? rows.length : 0;
            $('#badge-break-pending-count').toggle(count > 0).text(count);
            if (count === 0) {
                tbody.html('<tr><td colspan="9" class="text-center text-muted p-3">ไม่มีรายการรอสร้างใบส่งซ่อม</td></tr>');
                return;
            }
            tbody.html(rows.map(function (r) {
                return `<tr>
                    <td class="text-center"><div class="image-zoom-container"><img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(r.imgPath || '')}" width="50" height="50" alt=""></div></td>
                    <td class="text-center">${html(r.article || '')}</td>
                    <td class="text-center">${html(r.orderNo || '')}</td>
                    <td class="text-center">${html(r.edesFn || '')}</td>
                    <td class="text-center">${html(r.listGem || '')}</td>
                    <td class="text-center">${html(r.breakDescription || '')}</td>
                    <td class="text-end">${num(r.breakQty)}</td>
                    <td class="text-center">${html(r.createDateTH || '')}</td>
                    <td class="text-center"><input type="checkbox" class="chk-break-pending custom-checkbox" value="${r.breakID}" onchange="updateBreakPendingToolbar()"></td>
                </tr>`;
            }).join(''));
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="9" class="text-danger text-center p-3">เกิดข้อผิดพลาด (${xhr.status})</td></tr>`);
        }
    });
}

function toggleAllBreakPending(chk) {
    $('.chk-break-pending').prop('checked', chk.checked);
    updateBreakPendingToolbar();
}

async function createBreakDocument() {
    const ids = $('.chk-break-pending:checked').map(function () { return parseInt(this.value); }).get();
    if (ids.length === 0) {
        await swalWarning('กรุณาเลือกรายการอย่างน้อย 1 รายการ');
        return;
    }
    await swalConfirm(`สร้างใบส่งซ่อมจาก ${ids.length} รายการ?`, 'ยืนยัน', async function () {
        $.ajax({
            url: urlCreateBreakDocument,
            method: 'POST',
            contentType: 'application/json; charset=utf-8',
            data: JSON.stringify(ids),
            success: function (result) {
                swalSuccess(`สร้างใบส่งซ่อม ${result.breakNo} สำเร็จ`);
                loadBreakHeaders();
                loadPendingBreaks();
                switchBreakTab('headers');
            },
            error: async function (xhr) {
                let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
                try { const json = JSON.parse(xhr.responseText); msg = json.message || msg; } catch { msg = xhr.statusText || msg; }
                await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
            }
        });
    });
}

function showBreakDetails(breakNo) {
    $('#lbl-break-no').text(breakNo);
    const tbody = $('#tbl-break-detail-body');
    tbody.html('<tr><td colspan="8" class="text-center text-muted p-3">กำลังโหลด...</td></tr>');
    $('#modal-break-detail').modal('show');
    $.ajax({
        url: urlGetBreakDetailsByNo,
        method: 'GET',
        data: { breakNo: breakNo },
        success: function (rows) {
            if (!rows || rows.length === 0) {
                tbody.html('<tr><td colspan="8" class="text-center text-muted p-3">ไม่พบรายการ</td></tr>');
                return;
            }
            tbody.html(rows.map(function (r) {
                return `<tr>
                    <td class="text-center"><div class="image-zoom-container"><img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(r.imgPath || '')}" width="50" height="50" alt=""></div></td>
                    <td>${html(r.article || '')}</td>
                    <td>${html(r.orderNo || '')}</td>
                    <td>${html(r.edesFn || '')}</td>
                    <td>${html(r.listGem || '')}</td>
                    <td>${html(r.breakDescription || '')}</td>
                    <td class="text-center">${num(r.breakQty)}</td>
                    <td class="text-center">${html(r.createDateTH || '')}</td>
                </tr>`;
            }).join(''));
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="8" class="text-danger text-center p-3">เกิดข้อผิดพลาด (${xhr.status})</td></tr>`);
        }
    });
}

async function printBreakByNo(breakNo) {
    const model = { BreakNo: breakNo };
    const pdfWindow = window.open('', '_blank');
    $.ajax({
        url: urlBreakReport,
        type: 'POST',
        data: JSON.stringify(model),
        contentType: 'application/json; charset=utf-8',
        xhrFields: { responseType: 'blob' },
        success: function (data) {
            const blob = new Blob([data], { type: 'application/pdf' });
            const blobUrl = URL.createObjectURL(blob);
            if (pdfWindow) pdfWindow.location = blobUrl;
        },
        error: async function (xhr) {
            if (pdfWindow) pdfWindow.close();
            let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
            try {
                const text = await new Response(xhr.response).text();
                const json = JSON.parse(text);
                msg = json.message || text;
            } catch { msg = xhr.statusText || msg; }
            await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
        }
    });
}

function switchWithdrawalTab(tab) {
    $('#tab-withdrawal-headers-link').toggleClass('active', tab === 'headers');
    $('#tab-withdrawal-pending-link').toggleClass('active', tab === 'pending');
    $('#tab-withdrawal-headers').toggle(tab === 'headers');
    $('#tab-withdrawal-pending').toggle(tab === 'pending');
}

function updatePendingToolbar() {
    const n = $('.chk-pending:checked').length;
    $('#toolbar-create-withdrawal').toggle(n > 0);
    $('#lbl-pending-selected').text(n > 0 ? `เลือก ${n} รายการ` : '');
}

function searchWithdrawal() {
    loadWithdrawalHeaders();
    loadPendingWithdrawals();
}

function clearWithdrawalFilter() {
    $('#txtWithdrawalArticle').val('');
    $('#txtWithdrawalNo').val('');
    $('#ddlWithdrawalEDesArt').val(null).trigger('change');
    searchWithdrawal();
}

function loadPendingWithdrawals() {
    const article = ($('#txtWithdrawalArticle').val() || '').trim() || undefined;
    const edesArt = $('#ddlWithdrawalEDesArt').val() || undefined;
    const tbody = $('#tbl-withdrawal-pending-body');
    tbody.html('<tr><td colspan="10" class="text-center text-muted p-3">กำลังโหลด...</td></tr>');
    $('#chk-pending-all').prop('checked', false);
    $('#toolbar-create-withdrawal').hide();
    $.ajax({
        url: urlGetPendingWithdrawalDetails,
        method: 'GET',
        data: { article, edesArt },
        success: function (rows) {
            const count = rows ? rows.length : 0;
            $('#badge-pending-count').toggle(count > 0).text(count);
            if (count === 0) {
                tbody.html('<tr><td colspan="10" class="text-center text-muted p-3">ไม่มีรายการรอสร้างใบเบิก</td></tr>');
                return;
            }
            tbody.html(rows.map(function (r) {
                const canDelete = !r.withdrawalNo;
                const deleteBtn = canDelete
                    ? `<button class="btn btn-sm btn-outline-danger" onclick="cancelPendingWithdrawal(${r.withdrawalId})" title="ลบและคืนสต็อค"><i class="fas fa-trash"></i></button>`
                    : '';
                return `<tr>
                    <td class="text-center"><div class="image-zoom-container"><img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(r.imgPath || '')}" width="50" height="50" alt=""></div></td>
                    <td class="text-center">${html(r.article || r.tempArticle || '')}</td>
                    <td class="text-center">${html(r.orderNo)}</td>
                    <td class="text-center">${html(r.eDesFn || '')}</td>
                    <td class="text-center">${html(r.listGem || '')}</td>
                    <td class="text-end">${num(r.qty)}</td>
                    <td>${html(r.remark || '')}</td>
                    <td class="text-center">${html(r.withdrawnDate)}</td>
                    <td class="text-center"><input type="checkbox" class="chk-pending custom-checkbox" value="${r.withdrawalId}" onchange="updatePendingToolbar()"></td>
                    <td class="text-center">${deleteBtn}</td>
                </tr>`;
            }).join(''));
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="10" class="text-danger text-center p-3">เกิดข้อผิดพลาด (${xhr.status})</td></tr>`);
        }
    });
}

function toggleAllPending(chk) {
    $('.chk-pending').prop('checked', chk.checked);
    updatePendingToolbar();
}

async function cancelPendingWithdrawal(id) {
    await swalConfirm('ลบรายการเบิกนี้และคืนจำนวนกลับเข้าสต็อค?', 'ยืนยันการลบ', async function () {
        $.ajax({
            url: urlCancelPendingWithdrawal,
            method: 'POST',
            contentType: 'application/json; charset=utf-8',
            data: JSON.stringify(id),
            success: function () {
                swalSuccess('ลบรายการและคืนสต็อคสำเร็จ');
                loadPendingWithdrawals();
            },
            error: async function (xhr) {
                let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
                try { const json = JSON.parse(xhr.responseText); msg = json.message || msg; } catch { msg = xhr.statusText || msg; }
                await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
            }
        });
    });
}

async function createWithdrawalDocument() {
    const ids = $('.chk-pending:checked').map(function () { return parseInt(this.value); }).get();
    if (ids.length === 0) {
        await swalWarning('กรุณาเลือกรายการอย่างน้อย 1 รายการ');
        return;
    }
    await swalConfirm(`สร้างใบเบิกจาก ${ids.length} รายการ?`, 'ยืนยัน', async function () {
        $.ajax({
            url: urlCreateWithdrawalDocument,
            method: 'POST',
            contentType: 'application/json; charset=utf-8',
            data: JSON.stringify(ids),
            success: function (result) {
                swalSuccess(`สร้างใบเบิก ${result.withdrawalNo} สำเร็จ`);
                loadWithdrawalHeaders();
                loadPendingWithdrawals();
                switchWithdrawalTab('headers');
            },
            error: async function (xhr) {
                let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
                try { const json = JSON.parse(xhr.responseText); msg = json.message || msg; } catch { msg = xhr.statusText || msg; }
                await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
            }
        });
    });
}

function loadWithdrawalHeaders() {
    const article = ($('#txtWithdrawalArticle').val() || '').trim() || undefined;
    const withdrawalNo = ($('#txtWithdrawalNo').val() || '').trim() || undefined;
    const edesArt = $('#ddlWithdrawalEDesArt').val() || undefined;
    const tbody = $('#tbl-withdrawal-headers-body');
    tbody.html('<tr><td colspan="5" class="text-center text-muted p-3">กำลังโหลด...</td></tr>');
    $.ajax({
        url: urlGetWithdrawalHeaders,
        method: 'GET',
        data: { article, withdrawalNo, edesArt },
        success: function (rows) {
            if (!rows || rows.length === 0) {
                tbody.html('<tr><td colspan="5" class="text-center text-muted p-3">ไม่พบข้อมูล</td></tr>');
                return;
            }
            tbody.html(rows.map(function (r, i) {
                return `<tr>
                    <td class="text-center">${i + 1}</td>
                    <td><strong>${html(r.withdrawalNo)}</strong></td>
                    <td>${html(r.createDate)}</td>
                    <td class="text-center">${r.itemCount}</td>
                    <td class="text-center">
                        <button class="btn btn-sm btn-info mr-1" onclick="showWithdrawalDetails('${html(r.withdrawalNo)}')">
                            <i class="fas fa-eye"></i> ดูรายการ
                        </button>
                        <button class="btn btn-sm btn-secondary" onclick="printWithdrawalByNo('${html(r.withdrawalNo)}')">
                            <i class="fas fa-print"></i>
                        </button>
                    </td>
                </tr>`;
            }).join(''));
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="5" class="text-danger text-center p-3">เกิดข้อผิดพลาด (${xhr.status})</td></tr>`);
        }
    });
}

function showWithdrawalDetails(withdrawalNo) {
    $('#lbl-withdrawal-no').text(withdrawalNo);
    const tbody = $('#tbl-withdrawal-detail-body');
    tbody.html('<tr><td colspan="8" class="text-center text-muted p-3">กำลังโหลด...</td></tr>');
    $('#modal-withdrawal-detail').modal('show');
    $.ajax({
        url: urlGetWithdrawalDetailsByNo,
        method: 'GET',
        data: { withdrawalNo: withdrawalNo },
        success: function (rows) {
            if (!rows || rows.length === 0) {
                tbody.html('<tr><td colspan="8" class="text-center text-muted p-3">ไม่พบรายการ</td></tr>');
                return;
            }
            tbody.html(rows.map(function (r) {
                return `<tr>
                    <td class="text-center"><div class="image-zoom-container"><img class="imgOrderLot" src="${urlGetImage}?filename=${encodeURIComponent(r.imgPath || '')}" width="50" height="50" alt=""></div></td>
                    <td>${html(r.article || r.tempArticle || '')}</td>
                    <td>${html(r.orderNo)}</td>
                    <td>${html(r.eDesFn || '')}</td>
                    <td>${html(r.listGem || '')}</td>
                    <td class="text-center">${num(r.qty)}</td>
                    <td>${html(r.remark || '')}</td>
                    <td class="text-center">${html(r.withdrawnDate)}</td>
                </tr>`;
            }).join(''));
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="8" class="text-danger text-center p-3">เกิดข้อผิดพลาด (${xhr.status})</td></tr>`);
        }
    });
}

async function printWithdrawalByNo(withdrawalNo) {
    const model = { WithdrawalNo: withdrawalNo };
    const pdfWindow = window.open('', '_blank');

    $.ajax({
        url: urlWithdrawalReport,
        type: 'POST',
        data: JSON.stringify(model),
        contentType: 'application/json; charset=utf-8',
        xhrFields: { responseType: 'blob' },
        success: function (data) {
            const blob = new Blob([data], { type: 'application/pdf' });
            const blobUrl = URL.createObjectURL(blob);
            if (pdfWindow) {
                pdfWindow.location = blobUrl;
            }
        },
        error: async function (xhr) {
            if (pdfWindow) pdfWindow.close();
            let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
            try {
                const text = await new Response(xhr.response).text();
                const json = JSON.parse(text);
                msg = json.message || text;
            } catch {
                msg = xhr.statusText || msg;
            }
            await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
        }
    });
}

async function printWithdrawalToPDF() {
    const article = ($('#txtWithdrawalArticle').val() || '').trim() || null;
    const edesArt = $('#ddlWithdrawalEDesArt').val() || null;
    const model = { Article: article, EDesArt: edesArt };
    const pdfWindow = window.open('', '_blank');

    $.ajax({
        url: urlWithdrawalReport,
        type: 'POST',
        data: JSON.stringify(model),
        contentType: 'application/json; charset=utf-8',
        xhrFields: { responseType: 'blob' },
        success: function (data) {
            const blob = new Blob([data], { type: 'application/pdf' });
            const blobUrl = URL.createObjectURL(blob);
            if (pdfWindow) {
                pdfWindow.location = blobUrl;
            }
        },
        error: async function (xhr) {
            if (pdfWindow) pdfWindow.close();
            let msg = 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
            try {
                const text = await new Response(xhr.response).text();
                const json = JSON.parse(text);
                msg = json.message || text;
            } catch {
                msg = xhr.statusText || msg;
            }
            await swalWarning(`เกิดข้อผิดพลาด (${xhr.status}) ${msg}`);
        }
    });
}
