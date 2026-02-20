// เก็บสถานะว่า tab ไหนโหลดข้อมูลแล้ว
let tabDataLoaded = {
    billing: true, // ลงบิล โหลดจาก server-side แล้ว
    export: false,
    packing: false
};

$(document).ready(function () {
    $(document).on('keydown', '#txtFindReceivedNo, #txtFindLotNo', function (e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            findReceive();
        }
    });

    // โหลดข้อมูลเมื่อเปลี่ยน tab
    $(document).on('shown.bs.tab', '#receiveTabs button[data-bs-toggle="tab"]', function (e) {
        const targetId = $(e.target).attr('id');
        switch (targetId) {
            case 'tab-receive-billing': loadBillingData(); break;
            case 'tab-receive-export': loadExportData(); break;
            case 'tab-receive-packing': loadPackingData(); break;
        }
    });

    $(document).on('click', '#btnUpdateLot', async function () {
        $('#loadingIndicator').show();

        const tbody = $('#tbl-received-body');
        const hddReceiveNo = $('#hddReceiveNo').val();
        const orderNos = [];
        const receiveIds = [];

        tbody.find('tr').each(function () {
            const chk = $(this).find('.chk-row');
            if (chk.is(':checked')) {
                const orderNo = $(this).data('order-no');
                if (orderNo && !orderNos.includes(orderNo)) {
                    orderNos.push(orderNo);
                }
                const receiveId = $(this).data('receive-id');
                if (receiveId) {
                    receiveIds.push(receiveId);
                }
            }
        });

        if (receiveIds.length === 0 || orderNos.length === 0) {
            $('#loadingIndicator').hide();
            await swalWarning('กรุณาเลือกข้อมูลที่จะนำเข้า');
            return;
        }

        const formData = new FormData();
        formData.append("receiveNo", hddReceiveNo);
        orderNos.forEach(no => formData.append("orderNos", no));
        receiveIds.forEach(no => formData.append("receiveIds", no));

        $.ajax({
            url: urlUpdateLotItems,
            type: 'PATCH',
            processData: false,
            contentType: false,
            data: formData,
            success: async function () {
                $('#loadingIndicator').hide();
                $('#modal-update').modal('hide');
                refreshReceiveRow(hddReceiveNo);
                swalToastSuccess(`นำเข้าแล้ว ${receiveIds.length} รายการ`);
            },
            error: async function (xhr) {
                $('#loadingIndicator').hide();
                let msg = xhr.responseJSON?.message || xhr.responseText || 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
                await swalWarning(`เกิดข้อผิดพลาด (${xhr.status} ${msg})`);
            }
        });
    });

    $(document).on('click', '#btnCancelUpdateLot', async function () {
        $('#loadingIndicator').show();

        const tbody = $('#tbl-cancel-received-body');
        const hddReceiveNo = $('#hddCancelReceiveNo').val();
        const orderNos = [];
        const receiveIds = [];

        tbody.find('tr').each(function () {
            const chk = $(this).find('.chk-row');
            if (chk.is(':checked')) {
                const orderNo = $(this).data('order-no');
                if (orderNo && !orderNos.includes(orderNo)) {
                    orderNos.push(orderNo);
                }
                const receiveId = $(this).data('receive-id');
                if (receiveId) {
                    receiveIds.push(receiveId);
                }
            }
        });

        if (receiveIds.length === 0 || orderNos.length === 0) {
            $('#loadingIndicator').hide();
            await swalWarning('กรุณาเลือกข้อมูลที่จะยกเลิก');
            return;
        }

        const formData = new FormData();
        formData.append("receiveNo", hddReceiveNo);
        orderNos.forEach(no => formData.append("orderNos", no));
        receiveIds.forEach(no => formData.append("receiveIds", no));

        $.ajax({
            url: urlCancelUpdateLotItems,
            type: 'PATCH',
            processData: false,
            contentType: false,
            data: formData,
            success: async function (lot) {
                $('#loadingIndicator').hide();
                $('#modal-cancel-update').modal('hide');
                refreshReceiveRow(hddReceiveNo);
                swalToastSuccess(`ยกเลิกการนำเข้าแล้ว ${receiveIds.length} รายการ`);
            },
            error: async function (xhr) {
                $('#loadingIndicator').hide();
                let msg = xhr.responseJSON?.message || xhr.responseText || 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ';
                await swalWarning(`เกิดข้อผิดพลาด (${xhr.status} ${msg})`);
            }
        });
    });

    $(document).on('change', '#chkSelectAll', function () {
        const isChecked = $(this).is(':checked');
        $('#tbl-received-body .chk-row:enabled')
            .prop('checked', isChecked)
            .trigger('change');
    });

    $(document).on('change', '#tbl-received-body .chk-row', function () {
        const allEnabled = $('#tbl-received-body .chk-row:enabled').length;
        const allChecked = $('#tbl-received-body .chk-row:enabled:checked').length;

        $('#chkSelectAll')
            .prop('checked', allEnabled > 0 && allChecked === allEnabled)
            .prop('indeterminate', allChecked > 0 && allChecked < allEnabled);
    });

    $(document).on('change', '#chkCancelSelectAll', function () {
        const isChecked = $(this).is(':checked');
        $('#tbl-cancel-received-body .chk-row:enabled')
            .prop('checked', isChecked)
            .trigger('change');
    });

});

function showModalUpdateLot(receiveNo) {
    const txtFindLotNo = $('#txtFindLotNo').val().trim();

    const modal = $('#modal-update');
    const tbody = modal.find('#tbl-received-body');

    tbody.empty().append('<tr><td colspan="10" class="text-center text-muted">กำลังโหลด...</td></tr>');

    modal.find('#txtTitleUpdate').html(
        "<i class='fas fa-folder-plus'></i> รายการนำเข้าใบรับ: " + html(receiveNo)
    );

    modal.modal('show');

    $.ajax({
        url: urlImportReceiveNo,
        method: 'GET',
        data: {
            receiveNo: receiveNo,
            lotNo: txtFindLotNo,
        },
        dataType: 'json',
    })
        .done(function (items) {
            tbody.empty();

            $('#hddReceiveNo').val(receiveNo);

            if (!items || items.length === 0) {
                tbody.append('<tr><td colspan="10" class="text-center text-muted">ไม่พบข้อมูล</td></tr>');
                return;
            }

            const rows = items.map(function (x, i) {
                const safeId = ('chk_' + String(x.receiveNo ?? ('row' + i))).replace(/[^A-Za-z0-9_-]/g, '_');

                const isReceived = x.isReceived === true;
                const checkedAttr = isReceived ? '' : 'checked';
                const disabledAttr = isReceived ? 'disabled' : '';
                const rowClass = isReceived ? 'text-muted' : '';

                const lotNoDisplay = isReceived ? `<del>${html(x.lotNo)}</del>` : `<strong>${html(x.lotNo)}</strong>`;
                const orderNoDisplay = isReceived ? `<del>${html(x.orderNo)}</del>` : html(x.orderNo);
                const cusCodeDisplay = isReceived ? `<del>${html(x.custCode)}</del>` : html(x.custCode);
                const edesFnDisplay = isReceived ? `<del>${html(x.edesFn)}</del>` : html(x.edesFn);
                const articleDisplay = isReceived ? `<del>${html(x.article)}</del>` : html(x.article);
                const qtyDisplay = isReceived ? `<del>${num(x.ttQty)}</del>` : num(x.ttQty);
                const wgDisplay = isReceived ? `<del>${num(x.ttWg)}</del>` : num(x.ttWg);

                return `
                <tr class="${rowClass}" 
                        data-receive-id="${html(x.receivedID)}" 
                        data-order-no="${html(x.orderNo)}" 
                        data-ttqty="${numRaw(x.ttQty)}" 
                        data-ttwg="${numRaw(x.ttWg)}">
                    <td class="text-center">${i + 1}</td>
                    <td class="text-center">${cusCodeDisplay}</td>
                    <td>${orderNoDisplay}</td>
                    <td>${lotNoDisplay}</td>
                    <td class="text-center">${html(x.listNo)}</td>
                    <td>${edesFnDisplay}</td>
                    <td>${articleDisplay}</td>
                    <td class="text-end">${qtyDisplay}</td>
                    <td class="text-end">${wgDisplay}</td>
                    <td class="text-center">
                        <div class="chk-wrapper">
                            <input type="checkbox" id="${safeId}_${i}" class="chk-row custom-checkbox" ${checkedAttr} ${disabledAttr}>
                            <label for="${safeId}_${i}" class="d-none"></label>
                        </div>
                    </td>
                </tr>`;
            }).join('');

            tbody.append(rows);

            tbody.append(`
            <tr class="table-secondary fw-bold" id="totalRow">
                <td colspan="7" class="text-end">รวม</td>
                <td class="text-end" id="sumTtQty">0</td>
                <td class="text-end" id="sumTtWg">0</td>
                <td></td>
            </tr>
        `);


            calcTotal();

            const allEnabled = tbody.find('.chk-row:enabled').length;
            const allChecked = tbody.find('.chk-row:enabled:checked').length;

            $('#chkSelectAll')
                .prop('checked', allEnabled > 0 && allChecked === allEnabled)
                .prop('indeterminate', allChecked > 0 && allChecked < allEnabled);

            tbody.on('change', '.chk-row', function () {
                calcTotal();

                const allEnabled = tbody.find('.chk-row:enabled').length;
                const allChecked = tbody.find('.chk-row:enabled:checked').length;

                $('#chkSelectAll')
                    .prop('checked', allEnabled > 0 && allChecked === allEnabled)
                    .prop('indeterminate', allChecked > 0 && allChecked < allEnabled);
            });
        })
        .fail(function (xhr) {
            tbody.empty().append(
                `<tr><td colspan="10" class="text-danger text-center">
            เกิดข้อผิดพลาดในการโหลดข้อมูล (${xhr.status} ${xhr.statusText})
        </td></tr>`
            );
        });

    function calcTotal() {
        let sumQty = 0;
        let sumWg = 0;
        tbody.find('tr').each(function () {
            const tr = $(this);
            const chk = tr.find('.chk-row');
            if (chk.length && chk.is(':checked')) {
                sumQty += Number(tr.data('ttqty')) || 0;
                sumWg += Number(tr.data('ttwg')) || 0;
            }
        });
        $('#sumTtQty').text(num(sumQty));
        $('#sumTtWg').text(num(sumWg));
    }
}

function showModalCancelLot(receiveNo) {
    const txtFindLotNo = $('#txtFindLotNo').val().trim();

    const modal = $('#modal-cancel-update');
    const tbody = modal.find('#tbl-cancel-received-body');

    tbody.empty().append('<tr><td colspan="10" class="text-center text-muted">กำลังโหลด...</td></tr>');

    modal.find('#txtTitleCancelUpdate').html(
        "<i class='fas fa-folder-plus'></i> รายการนำเข้าใบรับ: " + html(receiveNo)
    );

    modal.modal('show');

    $.ajax({
        url: urlCancelImportReceiveNo,
        method: 'GET',
        data: {
            receiveNo: receiveNo,
            lotNo: txtFindLotNo,
        },
        dataType: 'json',
    })
        .done(function (items) {
            tbody.empty();

            $('#hddCancelReceiveNo').val(receiveNo);

            if (!items || items.length === 0) {
                tbody.append('<tr><td colspan="10" class="text-center text-muted">ไม่พบข้อมูล</td></tr>');
                return;
            }

            const rows = items.map(function (x, i) {
                const safeId = ('chk_' + String(x.receiveNo ?? ('row' + i))).replace(/[^A-Za-z0-9_-]/g, '_');

                const isReceived = x.isReceived === false;
                const checkedAttr = isReceived ? '' : 'checked';
                const disabledAttr = isReceived ? 'disabled' : '';
                const rowClass = isReceived ? 'text-muted' : '';

                const lotNoDisplay = isReceived ? `<del>${html(x.lotNo)}</del>` : `<strong>${html(x.lotNo)}</strong>`;
                const orderNoDisplay = isReceived ? `<del>${html(x.orderNo)}</del>` : html(x.orderNo);
                const cusCodeDisplay = isReceived ? `<del>${html(x.custCode)}</del>` : html(x.custCode);
                const edesFnDisplay = isReceived ? `<del>${html(x.edesFn)}</del>` : html(x.edesFn);
                const articleDisplay = isReceived ? `<del>${html(x.article)}</del>` : html(x.article);
                const qtyDisplay = isReceived ? `<del>${num(x.ttQty)}</del>` : num(x.ttQty);
                const wgDisplay = isReceived ? `<del>${num(x.ttWg)}</del>` : num(x.ttWg);

                return `
            <tr class="${rowClass}" 
                    data-receive-id="${html(x.receivedID)}" 
                    data-order-no="${html(x.orderNo)}" 
                    data-ttqty="${numRaw(x.ttQty)}" 
                    data-ttwg="${numRaw(x.ttWg)}">
                <td class="text-center">${i + 1}</td>
                <td class="text-center">${cusCodeDisplay}</td>
                <td>${orderNoDisplay}</td>
                <td>${lotNoDisplay}</td>
                <td class="text-center">${html(x.listNo)}</td>
                <td>${edesFnDisplay}</td>
                <td>${articleDisplay}</td>
                <td class="text-end">${qtyDisplay}</td>
                <td class="text-end">${wgDisplay}</td>
                <td class="text-center">
                    <div class="chk-wrapper">
                        <input type="checkbox" id="${safeId}_${i}" class="chk-row custom-checkbox" ${checkedAttr} ${disabledAttr}>
                        <label for="${safeId}_${i}" class="d-none"></label>
                    </div>
                </td>
            </tr>`;
            }).join('');

            tbody.append(rows);

            tbody.append(`
            <tr class="table-secondary fw-bold" id="totalRow">
                <td colspan="7" class="text-end">รวม</td>
                <td class="text-end" id="sumTtQty">0</td>
                <td class="text-end" id="sumTtWg">0</td>
                <td></td>
            </tr>
        `);


            calcTotal();

            const allEnabled = tbody.find('.chk-row:enabled').length;
            const allChecked = tbody.find('.chk-row:enabled:checked').length;

            $('#chkCancelSelectAll')
                .prop('checked', allEnabled > 0 && allChecked === allEnabled)
                .prop('indeterminate', allChecked > 0 && allChecked < allEnabled);

            tbody.on('change', '.chk-row', function () {
                calcTotal();

                const allEnabled = tbody.find('.chk-row:enabled').length;
                const allChecked = tbody.find('.chk-row:enabled:checked').length;

                $('#chkCancelSelectAll')
                    .prop('checked', allEnabled > 0 && allChecked === allEnabled)
                    .prop('indeterminate', allChecked > 0 && allChecked < allEnabled);
            });
        })
        .fail(function (xhr) {
            tbody.empty().append(
                `<tr><td colspan="10" class="text-danger text-center">
                เกิดข้อผิดพลาดในการโหลดข้อมูล (${xhr.status} ${xhr.statusText})
            </td></tr>`
            );
        });

    function calcTotal() {
        let sumQty = 0;
        let sumWg = 0;
        tbody.find('tr').each(function () {
            const tr = $(this);
            const chk = tr.find('.chk-row');
            if (chk.length && chk.is(':checked')) {
                sumQty += Number(tr.data('ttqty')) || 0;
                sumWg += Number(tr.data('ttwg')) || 0;
            }
        });
        $('#sumTtQty').text(num(sumQty));
        $('#sumTtWg').text(num(sumWg));
    }
}

// ค้นหาตาม tab ที่ active
function findReceive() {
    const activeTab = $('#receiveTabs .nav-link.active').attr('id');
    switch (activeTab) {
        case 'tab-receive-billing': loadBillingData(); break;
        case 'tab-receive-export': loadExportData(); break;
        case 'tab-receive-packing': loadPackingData(); break;
    }
}

// โหลดข้อมูล ลงบิล
function loadBillingData() {
    const receiveNo = $('#txtFindReceivedNo').val().trim();
    const lotNo = $('#txtFindLotNo').val().trim();
    const tbody = $('#tbl-billing-body');

    tbody.html('<tr><td colspan="5" class="text-center text-muted">กำลังค้นหา...</td></tr>');

    $.ajax({
        url: urlGetReceiveList,
        method: 'GET',
        data: { receiveNo: receiveNo, lotNo: lotNo },
        success: function (rows) {
            renderReceiveTable(tbody, rows);
            tabDataLoaded.billing = true;
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="5" class="text-danger text-center">เกิดข้อผิดพลาด (${xhr.status} ${xhr.statusText})</td></tr>`);
        }
    });
}

// โหลดข้อมูล ส่งออก
function loadExportData() {
    const receiveNo = $('#txtFindReceivedNo').val().trim();
    const lotNo = $('#txtFindLotNo').val().trim();
    const tbody = $('#tbl-export-body');

    tbody.html('<tr><td colspan="5" class="text-center text-muted">กำลังค้นหา...</td></tr>');

    $.ajax({
        url: urlGetReceiveList,
        method: 'GET',
        data: { receiveNo: receiveNo, lotNo: lotNo },
        success: function (rows) {
            renderReceiveTable(tbody, rows);
            tabDataLoaded.export = true;
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="5" class="text-danger text-center">เกิดข้อผิดพลาด (${xhr.status} ${xhr.statusText})</td></tr>`);
        }
    });
}

// โหลดข้อมูล แพ็คกิ้ง
function loadPackingData() {
    const receiveNo = $('#txtFindReceivedNo').val().trim();
    const lotNo = $('#txtFindLotNo').val().trim();
    const tbody = $('#tbl-packing-body');

    tbody.html('<tr><td colspan="5" class="text-center text-muted">กำลังค้นหา...</td></tr>');

    $.ajax({
        url: urlGetReceiveList,
        method: 'GET',
        data: { receiveNo: receiveNo, lotNo: lotNo },
        success: function (rows) {
            renderReceiveTable(tbody, rows);
            tabDataLoaded.packing = true;
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="5" class="text-danger text-center">เกิดข้อผิดพลาด (${xhr.status} ${xhr.statusText})</td></tr>`);
        }
    });
}

// render ตารางรายการใบส่ง
function renderReceiveTable(tbody, rows) {
    if (!rows || rows.length === 0) {
        tbody.html('<tr><td colspan="5" class="text-center text-muted">ไม่พบข้อมูล</td></tr>');
        return;
    }

    const body = rows.map((r, index) => {
        const status = r.isReceived && !r.hasRevButNotAll
            ? '<span class="badge badge-success">รับเข้าครบแล้ว</span>'
            : r.hasRevButNotAll && !r.isReceived
                ? '<span class="badge badge-warning">รับเข้ายังไม่ครบ</span>'
                : '<span class="badge badge-secondary">รอรับเข้า</span>';

        const action = !r.isReceived && !r.hasRevButNotAll
            ? `<button class="btn btn-warning btn-sm" onclick="showModalUpdateLot('${html(r.receiveNo)}')">
                   <i class="fas fa-folder"></i> ตรวจสอบ
               </button>`
            : '';

        return `
            <tr data-receive-no="${html(r.receiveNo)}">
                <td>${index + 1}</td>
                <td class="col-receiveno"><strong>${html(r.receiveNo)}</strong></td>
                <td class="col-mdate">${html(r.mdate)}</td>
                <td class="col-status">${status}</td>
                <td class="col-action">${action}</td>
            </tr>`;
    }).join('');

    tbody.html(body);
}


function refreshReceiveRow(receiveNo) {
    $.ajax({
        url: urlGetReceiveRow,
        type: 'GET',
        data: { receiveNo: receiveNo },
        success: function (row) {
            if (!row) return;

            // ค้นหา row ในทุก tab table
            const tr = $(`#tbl-billing-body, #tbl-export-body, #tbl-packing-body`)
                .find(`tr[data-receive-no="${row.receiveNo}"]`);

            if (!tr.length) return;

            tr.find('.col-mdate').text(row.mdate);

            if (row.isReceived && !row.hasRevButNotAll) {
                tr.find('.col-status').html('<span class="badge badge-success">รับเข้าครบแล้ว</span>');
                tr.find('.col-action').html('');
            } else if (row.hasRevButNotAll && !row.isReceived) {
                tr.find('.col-status').html('<span class="badge badge-warning">รับเข้ายังไม่ครบ</span>');
                tr.find('.col-action').html(`
                    <button class="btn btn-warning btn-sm" onclick="showModalUpdateLot('${row.receiveNo}')">
                        <i class="fas fa-folder"></i> ตรวจสอบ
                    </button>
                `);
            } else {
                tr.find('.col-status').html('<span class="badge badge-secondary">รอรับเข้า</span>');
                tr.find('.col-action').html(`
                    <button class="btn btn-warning btn-sm" onclick="showModalUpdateLot('${row.receiveNo}')">
                        <i class="fas fa-folder"></i> ตรวจสอบ
                    </button>
                `);
            }
        }
    });
}


function ClearFindByReceive() {
    $('#txtFindReceivedNo').val('');
    $('#txtFindLotNo').val('');

    // เคลียร์ตารางทุก tab
    $('#tbl-billing-body').html('');
    $('#tbl-export-body').html('');
    $('#tbl-packing-body').html('');

    // รีเซ็ตสถานะ
    tabDataLoaded = { billing: false, export: false, packing: false };

    // โหลดใหม่เฉพาะ tab ที่ active
    findReceive();
}