// เก็บสถานะว่า tab ไหนโหลดข้อมูลแล้ว
let tabDataLoaded = {
    billing: true, // ลงบิล โหลดจาก server-side แล้ว
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
            case 'tab-receive-packing': loadPackingData(); break;
        }
    });

    $(document).on('click', '#btnUpdateLot', async function () {
        $('#loadingIndicator').show();

        const modal = $('#modal-update');
        const isSP = modal.data('mode') === 'sp';
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

        if (receiveIds.length === 0) {
            $('#loadingIndicator').hide();
            await swalWarning('กรุณาเลือกข้อมูลที่จะนำเข้า');
            return;
        }

        if (!isSP && orderNos.length === 0) {
            $('#loadingIndicator').hide();
            await swalWarning('กรุณาเลือกข้อมูลที่จะนำเข้า');
            return;
        }

        const formData = new FormData();
        formData.append("receiveNo", hddReceiveNo);
        receiveIds.forEach(id => formData.append("receiveIds", id));

        if (!isSP) {
            orderNos.forEach(no => formData.append("orderNos", no));
        }

        const url = isSP ? urlUpdateSPLotItems : urlUpdateLotItems;

        $.ajax({
            url: url,
            type: 'PATCH',
            processData: false,
            contentType: false,
            data: formData,
            success: async function () {
                $('#loadingIndicator').hide();
                modal.modal('hide');
                if (!isSP) refreshReceiveRow(hddReceiveNo);
                else refreshSPReceiveRow(hddReceiveNo);
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

        const modal = $('#modal-cancel-update');
        const isSP = modal.data('mode') === 'sp';
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

        if (receiveIds.length === 0 || (!isSP && orderNos.length === 0)) {
            $('#loadingIndicator').hide();
            await swalWarning('กรุณาเลือกข้อมูลที่จะยกเลิก');
            return;
        }

        const formData = new FormData();
        formData.append("receiveNo", hddReceiveNo);
        receiveIds.forEach(id => formData.append("receiveIds", id));
        if (!isSP) {
            orderNos.forEach(no => formData.append("orderNos", no));
        }

        const url = isSP ? urlCancelUpdateSPLotItems : urlCancelUpdateLotItems;

        $.ajax({
            url: url,
            type: 'PATCH',
            processData: false,
            contentType: false,
            data: formData,
            success: async function () {
                $('#loadingIndicator').hide();
                modal.modal('hide');
                if (!isSP) refreshReceiveRow(hddReceiveNo);
                else refreshSPReceiveRow(hddReceiveNo);
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
    modal.data('mode', 'jp');
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
    modal.data('mode', 'jp');
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

                const notInStock = x.isInStock === false;
                const checkedAttr = notInStock ? '' : 'checked';
                const disabledAttr = notInStock ? 'disabled' : '';
                const rowClass = notInStock ? 'text-muted' : '';

                const lotNoDisplay = notInStock ? `<del>${html(x.lotNo)}</del>` : `<strong>${html(x.lotNo)}</strong>`;
                const orderNoDisplay = html(x.orderNo);
                const cusCodeDisplay = html(x.custCode);
                const edesFnDisplay = html(x.edesFn);
                const articleDisplay = html(x.article);
                const qtyDisplay = num(x.ttQty);
                const wgDisplay = num(x.ttWg);

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

function showModalCancelSPLot(receiveNo) {
    const txtFindLotNo = $('#txtFindLotNo').val().trim();

    const modal = $('#modal-cancel-update');
    modal.data('mode', 'sp');
    const tbody = modal.find('#tbl-cancel-received-body');

    tbody.empty().append('<tr><td colspan="10" class="text-center text-muted">กำลังโหลด...</td></tr>');

    modal.find('#txtTitleCancelUpdate').html(
        "<i class='fas fa-folder-plus'></i> รายการนำเข้าใบรับ: " + html(receiveNo)
    );

    modal.modal('show');

    $.ajax({
        url: urlCancelImportSPReceiveNo,
        method: 'GET',
        data: { receiveNo: receiveNo, lotNo: txtFindLotNo },
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
                const safeId = ('chk_cancel_sp_' + String(x.receiveNo ?? ('row' + i))).replace(/[^A-Za-z0-9_-]/g, '_');
                const isNotReceived = x.isReceived === false;
                const checkedAttr = isNotReceived ? '' : 'checked';
                const disabledAttr = isNotReceived ? 'disabled' : '';
                const rowClass = isNotReceived ? 'text-muted' : '';

                const lotNoDisplay = isNotReceived ? `<del>${html(x.lotNo)}</del>` : `<strong>${html(x.lotNo)}</strong>`;
                const orderNoDisplay = isNotReceived ? `<del>${html(x.orderNo)}</del>` : html(x.orderNo);
                const cusCodeDisplay = isNotReceived ? `<del>${html(x.custCode)}</del>` : html(x.custCode);
                const edesFnDisplay = isNotReceived ? `<del>${html(x.edesFn)}</del>` : html(x.edesFn);
                const articleDisplay = isNotReceived ? `<del>${html(x.article)}</del>` : html(x.article);
                const qtyDisplay = isNotReceived ? `<del>${num(x.ttQty)}</del>` : num(x.ttQty);
                const wgDisplay = isNotReceived ? `<del>${num(x.ttWg)}</del>` : num(x.ttWg);

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

            const allEnabled = tbody.find('.chk-row:enabled').length;
            const allChecked = tbody.find('.chk-row:enabled:checked').length;

            $('#chkCancelSelectAll')
                .prop('checked', allEnabled > 0 && allChecked === allEnabled)
                .prop('indeterminate', allChecked > 0 && allChecked < allEnabled);
        })
        .fail(function (xhr) {
            tbody.empty().append(
                `<tr><td colspan="10" class="text-danger text-center">
                เกิดข้อผิดพลาดในการโหลดข้อมูล (${xhr.status} ${xhr.statusText})
            </td></tr>`
            );
        });
}

// ค้นหาตาม tab ที่ active
function findReceive() {
    const activeTab = $('#receiveTabs .nav-link.active').attr('id');
    switch (activeTab) {
        case 'tab-receive-billing': loadBillingData(); break;
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

// โหลดข้อมูล Stock Packing
function loadPackingData() {
    const receiveNo = $('#txtFindReceivedNo').val().trim();
    const lotNo = $('#txtFindLotNo').val().trim();
    const tbody = $('#tbl-packing-body');

    tbody.html('<tr><td colspan="5" class="text-center text-muted">กำลังค้นหา...</td></tr>');

    $.ajax({
        url: urlGetSPReceiveList,
        method: 'GET',
        data: { receiveNo: receiveNo, lotNo: lotNo },
        success: function (rows) {
            renderReceiveTable(tbody, rows, 'showModalSPUpdateLot', 'showModalCancelSPLot');
            tabDataLoaded.packing = true;
        },
        error: function (xhr) {
            tbody.html(`<tr><td colspan="5" class="text-danger text-center">เกิดข้อผิดพลาด (${xhr.status} ${xhr.statusText})</td></tr>`);
        }
    });
}

// render ตารางรายการใบส่ง
function renderReceiveTable(tbody, rows, onCheckFn = 'showModalUpdateLot', onCancelFn = 'showModalCancelLot') {
    if (!rows || rows.length === 0) {
        tbody.html('<tr><td colspan="5" class="text-center text-muted">ไม่พบข้อมูล</td></tr>');
        return;
    }

    const body = rows.map((r, index) => {
        let status;
        if (r.isReceived && !r.hasRevButNotAll) {
            status = '<span class="badge badge-success">รับเข้าครบแล้ว</span>';
        } else if (r.hasRevButNotAll && !r.isReceived) {
            status = '<span class="badge badge-warning">รับเข้ายังไม่ครบ</span>';
        } else {
            status = '<span class="badge badge-secondary">รอรับเข้า</span>';
        }

        let action = '';
        if (!r.isReceived) {
            action += `<button class="btn btn-warning btn-sm" onclick="${onCheckFn}('${html(r.receiveNo)}')">
                           <i class="fas fa-folder"></i> ตรวจสอบ
                       </button>`;
        }
        if (r.hasRevButNotAll || r.isReceived) {
            action += `<button class="btn btn-danger btn-sm ms-1" onclick="${onCancelFn}('${html(r.receiveNo)}')">
                           <i class="fas fa-times-circle"></i> ยกเลิกรับเข้า
                       </button>`;
        }

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

function showModalSPUpdateLot(receiveNo) {
    const txtFindLotNo = $('#txtFindLotNo').val().trim();

    const modal = $('#modal-update');
    modal.data('mode', 'sp');
    const tbody = modal.find('#tbl-received-body');

    tbody.empty().append('<tr><td colspan="10" class="text-center text-muted">กำลังโหลด...</td></tr>');
    modal.find('#txtTitleUpdate').html("<i class='fas fa-folder-plus'></i> รายการนำเข้าใบรับ: " + html(receiveNo));
    modal.modal('show');

    $.ajax({
        url: urlImportSPReceiveNo,
        method: 'GET',
        data: { receiveNo: receiveNo, lotNo: txtFindLotNo },
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
                const safeId = ('chk_sp_' + String(x.receiveNo ?? ('row' + i))).replace(/[^A-Za-z0-9_-]/g, '_');
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
        })
        .fail(function (xhr) {
            tbody.empty().append(
                `<tr><td colspan="10" class="text-danger text-center">เกิดข้อผิดพลาดในการโหลดข้อมูล (${xhr.status} ${xhr.statusText})</td></tr>`
            );
        });
}


function refreshReceiveRow(receiveNo) {
    $.ajax({
        url: urlGetReceiveRow,
        type: 'GET',
        data: { receiveNo: receiveNo },
        success: function (row) {
            if (!row) return;

            const tr = $('#tbl-billing-body').find(`tr[data-receive-no="${row.receiveNo}"]`);

            if (!tr.length) return;

            tr.find('.col-mdate').text(row.mdate);

            let actionHtml = '';
            if (row.isReceived && !row.hasRevButNotAll) {
                tr.find('.col-status').html('<span class="badge badge-success">รับเข้าครบแล้ว</span>');
                actionHtml = `<button class="btn btn-danger btn-sm ms-1" onclick="showModalCancelLot('${row.receiveNo}')">
                    <i class="fas fa-times-circle"></i> ยกเลิกรับเข้า
                </button>`;
            } else if (row.hasRevButNotAll && !row.isReceived) {
                tr.find('.col-status').html('<span class="badge badge-warning">รับเข้ายังไม่ครบ</span>');
                actionHtml = `<button class="btn btn-warning btn-sm" onclick="showModalUpdateLot('${row.receiveNo}')">
                        <i class="fas fa-folder"></i> ตรวจสอบ
                    </button>
                    <button class="btn btn-danger btn-sm ms-1" onclick="showModalCancelLot('${row.receiveNo}')">
                        <i class="fas fa-times-circle"></i> ยกเลิกรับเข้า
                    </button>`;
            } else {
                tr.find('.col-status').html('<span class="badge badge-secondary">รอรับเข้า</span>');
                actionHtml = `<button class="btn btn-warning btn-sm" onclick="showModalUpdateLot('${row.receiveNo}')">
                    <i class="fas fa-folder"></i> ตรวจสอบ
                </button>`;
            }
            tr.find('.col-action').html(actionHtml);
        }
    });
}


function refreshSPReceiveRow(receiveNo) {
    $.ajax({
        url: urlGetSPReceiveRow,
        type: 'GET',
        data: { receiveNo: receiveNo },
        success: function (row) {
            if (!row) return;

            const tr = $('#tbl-packing-body').find(`tr[data-receive-no="${row.receiveNo}"]`);

            if (!tr.length) return;

            tr.find('.col-mdate').text(row.mdate);

            let actionHtml = '';
            if (row.isReceived && !row.hasRevButNotAll) {
                tr.find('.col-status').html('<span class="badge badge-success">รับเข้าครบแล้ว</span>');
                actionHtml = `<button class="btn btn-danger btn-sm ms-1" onclick="showModalCancelSPLot('${row.receiveNo}')">
                    <i class="fas fa-times-circle"></i> ยกเลิกรับเข้า
                </button>`;
            } else if (row.hasRevButNotAll && !row.isReceived) {
                tr.find('.col-status').html('<span class="badge badge-warning">รับเข้ายังไม่ครบ</span>');
                actionHtml = `<button class="btn btn-warning btn-sm" onclick="showModalSPUpdateLot('${row.receiveNo}')">
                        <i class="fas fa-folder"></i> ตรวจสอบ
                    </button>
                    <button class="btn btn-danger btn-sm ms-1" onclick="showModalCancelSPLot('${row.receiveNo}')">
                        <i class="fas fa-times-circle"></i> ยกเลิกรับเข้า
                    </button>`;
            } else {
                tr.find('.col-status').html('<span class="badge badge-secondary">รอรับเข้า</span>');
                actionHtml = `<button class="btn btn-warning btn-sm" onclick="showModalSPUpdateLot('${row.receiveNo}')">
                    <i class="fas fa-folder"></i> ตรวจสอบ
                </button>`;
            }
            tr.find('.col-action').html(actionHtml);
        }
    });
}

function ClearFindByReceive() {
    $('#txtFindReceivedNo').val('');
    $('#txtFindLotNo').val('');

    $('#tbl-billing-body').html('');
    $('#tbl-packing-body').html('');

    tabDataLoaded = { billing: false, packing: false };

    // โหลดใหม่เฉพาะ tab ที่ active
    findReceive();
}