async function printStockToPDF(article, edesArt, unit) {
    if (article === undefined) article = ($('#txtStockArticle').val() || '').trim() || null;
    if (edesArt === undefined) edesArt = $('#ddlStockEDesArt').val() || null;
    if (unit === undefined) unit = $('#ddlStockUnit').val() || null;

    const model = { Article: article || null, EDesArt: edesArt || null, Unit: unit || null };
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

async function printWithdrawalToPDF() {
    const article = ($('#txtWithdrawalArticle').val() || '').trim() || null;
    const edesArt = $('#ddlWithdrawalEDesArt').val() || null;
    const unit = $('#ddlWithdrawalUnit').val() || null;

    const model = { Article: article, EDesArt: edesArt, Unit: unit };
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
