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

                    var groupedModel = model.Where(w => w.IsActive)
                        .GroupBy(x => new { x.Article, x.TempArticle, x.OrderNo, x.EDesFn, x.ListGem })
                        .Select(g => new StockItemModel
                        {
                            Article = g.Key.Article,
                            TempArticle = g.Key.TempArticle,
                            OrderNo = g.Key.OrderNo,
                            EDesFn = g.Key.EDesFn,
                            ListGem = g.Key.ListGem,
                            TtQty = g.Sum(x => x.TtQty),
                            AvailableQty = g.Sum(x => x.AvailableQty),
                            IsRepairing = g.Any(x => x.IsRepairing),
                            IsInTray = g.Any(x => x.IsInTray),
                            TrayNo = string.Join(", ", g.Where(x => x.IsInTray && !string.IsNullOrEmpty(x.TrayNo))
                                .Select(x => x.TrayNo).Distinct().OrderBy(t => t)),
                            CreateDate = g.OrderByDescending(x => x.CreateDate).First().CreateDate,
                            ImgPath = g.FirstOrDefault(x => !string.IsNullOrEmpty(x.ImgPath))?.ImgPath ?? string.Empty,
                        })
                        .ToList();

                    page.Content()
                        .PaddingVertical(2)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                for (int c = 0; c < colCount; c++)
                                    cols.RelativeColumn(1);
                            });

                            foreach (var item in groupedModel)
                            {
                                table.Cell().Padding(2).ShowEntire().Element(e => e.CreateStockItemCard(item));
                            }

                            int remainder = groupedModel.Count % colCount;
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

        public byte[] GenerateStockNoIMGReport(List<StockItemModel> model)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Portrait());
                    page.Margin(0.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Tahoma"));

                    page.Content().Column(col =>
                    {
                        col.Item().Element(content => content.StockNoIMGReportContent(model));
                    });

                    page.Footer().AlignCenter().Text(x =>
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

        public byte[] GenerateBorrowReport(List<BorrowModel> model)
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
                            content.BorrowReportContent(model);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateWithdrawalReport(List<WithdrawalModel> model)
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
                            content.WithdrawaleportContent(model);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

    }
}