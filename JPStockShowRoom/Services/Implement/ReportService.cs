using JPStockShowRoom.Models;
using JPStockShowRoom.Services.Helper;
using JPStockShowRoom.Services.Interface;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data.SqlTypes;
using static JPStockShowRoom.Services.Helper.Enum;

namespace JPStockShowRoom.Services.Implement
{
    public class ReportService(IWebHostEnvironment webHostEnvironment) : IReportService
    {
        private readonly IWebHostEnvironment _env = webHostEnvironment;

        public byte[] GenerateStockReport(List<StockItemModel> model)
        {
            var imgPath = Path.Combine(_env.WebRootPath, "img", "logo.png");

            QuestPDF.Settings.License = LicenseType.Community;

            const int colCount = 5;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Portrait());
                    page.Margin(0.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Tahoma"));

                    page.Header()
                        .PaddingBottom(5)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);
                            });
                            table.Cell().Element(CellStyle).AlignCenter().Text($"Showroom").FontSize(11).SemiBold();
                            table.Cell().Element(CellStyle).AlignRight().Text($"วันที่ออกเอกสาร : {DateTime.Now:dd-MM-yyyy HH:mm}").FontSize(7);

                            static IContainer CellStyle(IContainer container) => container.Padding(3);
                        });

                    page.Content()
                        .PaddingVertical(2)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                for (int c = 0; c < colCount; c++)
                                    cols.RelativeColumn(1);
                            });

                            foreach (var item in model)
                            {
                                table.Cell().Padding(2).ShowEntire().Element(e => e.CreateStockItemCard(item));
                            }

                            int remainder = model.Count % colCount;
                            if (remainder > 0)
                            {
                                for (int i = 0; i < colCount - remainder; i++)
                                    table.Cell();
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("หน้า ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateBreakReport(List<LostAndRepairModel> model)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Portrait());
                    page.Margin(0.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Tahoma"));

                    page.Content().Column(col =>
                    {
                        col.Item().Element(content =>
                        {
                            content.BreakReportContent(model);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

    }
}