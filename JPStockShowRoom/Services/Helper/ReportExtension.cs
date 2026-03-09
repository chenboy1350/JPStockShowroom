using JPStockShowRoom.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace JPStockShowRoom.Services.Helper
{
    public static class ReportExtension
    {
        public static void CreateStockItemCard(this IContainer container, StockItemModel item)
        {
            container.Border(1)
                            .BorderColor(Colors.Black)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                });

                                table.Cell().Element(CellStyle).AlignCenter().Text($"{item.OrderNo}").FontSize(8).SemiBold();
                                table.Cell().Element(CellStyle).AlignCenter().Text($"{item.TempArticle}").FontSize(8).SemiBold();
                                table.Cell().Element(CellStyle).AlignCenter().AlignMiddle().Height(60).Width(70).Element(container => container.RenderItemImage(null, item.ImgPath));

                                table.Cell().Element(CellStyle).AlignCenter().Text($"{item.EDesFn.Trim()} / {item.ListGem.Trim()}").FontSize(8).SemiBold();

                                table.Cell().Element(CellStyle).AlignCenter().Text($"QTY : {item.TtQty}").FontSize(8).SemiBold();
                                table.Cell().Element(CellStyle).AlignLeft().Text($"TRAY : {item.TrayNo}").FontSize(6).SemiBold();

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.Padding(3);
                                }
                            });
        }

        public static void StockNoIMGReportContent(this IContainer container, List<StockItemModel> model)
        {
            container.Column(col =>
            {
                col.Item().PaddingBottom(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(30);
                        columns.RelativeColumn(30);
                    });
                    table.Cell().Element(c => c.Padding(3)).AlignLeft().Column(inner =>
                    {
                        inner.Item().Text("รายงานสต็อค").FontSize(11).SemiBold();
                    });
                    table.Cell().Element(c => c.Padding(3)).AlignRight()
                         .Text($"วันที่ออกเอกสาร : {DateTime.Now.ToString("dd MMMM yyyy HH:mm", new CultureInfo("th-TH"))}")
                         .FontSize(7);
                });

                col.Item().PaddingVertical(2).Element(c =>
                {
                    c.Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);  // #
                            columns.RelativeColumn(3);  // Article
                            columns.RelativeColumn(2);  // OrderNo
                            columns.RelativeColumn(3);  // EDesFn
                            columns.RelativeColumn(2);  // ListGem
                            columns.RelativeColumn(2);  // วันที่
                            columns.RelativeColumn(2);  // จำนวน
                            columns.RelativeColumn(2);  // สถานะ
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("#").FontSize(8);
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("Article").FontSize(8);
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("OrderNo").FontSize(8);
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("FN").FontSize(8);
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("GEM").FontSize(8);
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("วันที่").FontSize(8);
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("จำนวน").FontSize(8);
                            header.Cell().Element(HeaderStyle).AlignCenter().Text("สถานะ").FontSize(8);

                            static IContainer HeaderStyle(IContainer c) =>
                                c.Border(0.5f).BorderColor(Colors.Black).Background(Colors.Grey.Lighten3).Padding(3);
                        });

                        var grouped = model.Where(w => w.IsActive)
                            .GroupBy(x => new { x.Article, x.TempArticle, x.OrderNo, x.EDesFn, x.ListGem })
                            .Select(g => new
                            {
                                g.Key.Article,
                                g.Key.TempArticle,
                                g.Key.OrderNo,
                                g.Key.EDesFn,
                                g.Key.ListGem,
                                TtQty = g.Sum(x => x.TtQty),
                                AvailableQty = g.Sum(x => x.AvailableQty),
                                IsRepairing = g.Any(x => x.IsRepairing),
                                IsInTray = g.Any(x => x.IsInTray),
                                TrayNo = string.Join(", ", g.Where(x => x.IsInTray && !string.IsNullOrEmpty(x.TrayNo))
                                    .Select(x => x.TrayNo).Distinct().OrderBy(t => t)),
                                CreateDate = g.OrderByDescending(x => x.CreateDate).First().CreateDate,
                            })
                            .ToList();

                        for (int i = 0; i < grouped.Count; i++)
                        {
                            var item = grouped[i];

                            string statusText;
                            if (string.IsNullOrEmpty(item.Article))
                                statusText = "รอลงทะเบียน";
                            else if (item.IsRepairing)
                                statusText = "ส่งซ่อม";
                            else if (item.IsInTray)
                                statusText = $"ถาด: {item.TrayNo}";
                            else
                                statusText = "ในคลัง";

                            var articleText = string.IsNullOrEmpty(item.Article)
                                ? (item.TempArticle ?? string.Empty)
                                : item.Article;
                            var subArticle = !string.IsNullOrEmpty(item.Article) && !string.IsNullOrEmpty(item.TempArticle)
                                ? $"({item.TempArticle})"
                                : string.Empty;

                            table.Cell().Element(CellStyle).AlignCenter().Text($"{i + 1}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignLeft().Column(inner =>
                            {
                                inner.Item().Text(articleText).FontSize(8).SemiBold();
                                if (!string.IsNullOrEmpty(subArticle))
                                    inner.Item().Text(subArticle).FontSize(7).FontColor(Colors.Grey.Darken1);
                            });
                            table.Cell().Element(CellStyle).AlignCenter().Text(item.OrderNo ?? string.Empty).FontSize(8);
                            table.Cell().Element(CellStyle).AlignLeft().Text(item.EDesFn ?? string.Empty).FontSize(7);
                            table.Cell().Element(CellStyle).AlignCenter().Text(item.ListGem ?? string.Empty).FontSize(7);
                            table.Cell().Element(CellStyle).AlignCenter().Text(item.CreateDate ?? string.Empty).FontSize(7);
                            table.Cell().Element(CellStyle).AlignCenter().Text(item.TtQty.ToString()).FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text(statusText).FontSize(7);
                        }

                        static IContainer CellStyle(IContainer c) =>
                            c.Border(0.5f).BorderColor(Colors.Black).Padding(3);
                    });
                });
            });
        }

        public static void BreakReportHeader(this IContainer container, string? breakNo = null)
        {
            container.PaddingBottom(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(30);
                    columns.RelativeColumn(30);
                });

                table.Cell().Element(CellStyle).AlignLeft().Column(col =>
                {
                    col.Item().Text("รายการซ่อม").FontSize(11).SemiBold();
                    if (!string.IsNullOrWhiteSpace(breakNo))
                        col.Item().Text($"เลขที่ : {breakNo}").FontSize(9).SemiBold();
                });
                table.Cell().Element(CellStyle).AlignRight()
                     .Text($"วันที่ออกเอกสาร : {DateTime.Now.ToString("dd MMMM yyyy", new CultureInfo("th-TH"))}")
                     .FontSize(7);

                static IContainer CellStyle(IContainer c) => c.Padding(3);
            });
        }

        public static void BreakReportContent(this IContainer container, List<LostAndRepairModel> model)
        {
            var breakNo = model.Count > 0 && model.All(x => x.BreakNo != null && x.BreakNo == model[0].BreakNo)
                ? model[0].BreakNo
                : null;

            container.Column(col =>
            {
                col.Item().PaddingTop(5).Element(c => c.BreakReportHeader(breakNo));

                col.Item().PaddingVertical(2).ShowOnce().Element(c =>
                {
                    c.Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Cell().Element(CellStyle).AlignCenter().Text("#").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("รูป").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("OrderNo").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("รหัสสินค้า").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("FN").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("GEM").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("อาการ").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("จำนวน").FontSize(8);

                        for (int i = 0; i < model.Count; i++)
                        {
                            var item = model[i];
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{i + 1}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().AlignMiddle().Height(60).Width(70).Element(container => container.RenderItemImage(null, item.ImgPath));
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.OrderNo}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.Article}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.EdesFn}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.ListGem}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.BreakDescription}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.BreakQty}").FontSize(8);
                        }

                        table.Cell().ColumnSpan(8).Padding(2).Column(row =>
                        {
                            row.Item()
                                .PaddingTop(30)
                                .Element(container =>
                                {
                                    container.Row(row =>
                                    {
                                        row.RelativeItem().AlignLeft().Column(col =>
                                        {
                                            col.Item().Text("หมายเหตุ .............................................................................................").AlignCenter();
                                        });
                                    });
                                });
                        });

                        table.Cell().ColumnSpan(8).Padding(2).Column(row =>
                        {
                            row.Item()
                                .PaddingTop(20)
                                .Element(container =>
                                {
                                    container.Row(row =>
                                    {
                                        row.RelativeItem().AlignLeft().Column(col =>
                                        {
                                            col.Item().Text("ลงชื่อ ...............................").AlignCenter();
                                        });
                                    });
                                });
                        });
                    });
                });
            });

            static IContainer CellStyle(IContainer container)
            {
                return container
                    .Border(0.5f)
                    .BorderColor(Colors.Black)
                    .Padding(3);
            }
        }

        public static void BorrowReportHeader(this IContainer container, string? borrowNo = null)
        {
            container.PaddingBottom(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(30);
                    columns.RelativeColumn(30);
                });

                table.Cell().Element(CellStyle).AlignLeft().Column(col =>
                {
                    col.Item().Text("รายการยืม").FontSize(11).SemiBold();
                    if (!string.IsNullOrWhiteSpace(borrowNo))
                        col.Item().Text($"เลขที่ : {borrowNo}").FontSize(9).SemiBold();
                });
                table.Cell().Element(CellStyle).AlignRight()
                     .Text($"วันที่ออกเอกสาร : {DateTime.Now.ToString("dd MMMM yyyy", new CultureInfo("th-TH"))}")
                     .FontSize(7);

                static IContainer CellStyle(IContainer c) => c.Padding(3);
            });
        }

        public static void BorrowReportContent(this IContainer container, List<BorrowModel> model)
        {
            var borrowNo = model.Count > 0 && model.All(x => x.BorrowNo != null && x.BorrowNo == model[0].BorrowNo)
                ? model[0].BorrowNo
                : null;

            container.Column(col =>
            {
                col.Item().PaddingTop(5).Element(c => c.BorrowReportHeader(borrowNo));

                col.Item().PaddingVertical(2).ShowOnce().Element(c =>
                {
                    c.Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Cell().Element(CellStyle).AlignCenter().Text("#").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("รูป").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("Article").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("FN").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("GEM").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("จำนวน").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("สถานะ").FontSize(8);

                        for (int i = 0; i < model.Count; i++)
                        {
                            var item = model[i];
                            var statusText = item.IsReturned
                                ? $"คืนแล้ว{(string.IsNullOrWhiteSpace(item.ReturnedDate) ? "" : $"\n{item.ReturnedDate}")}"
                                : "ยังไม่คืน";
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{i + 1}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().AlignMiddle().Height(60).Width(70).Element(container => container.RenderItemImage(null, item.ImgPath));
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.Article}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.EDesFn}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.ListGem}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.BorrowQty}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text(statusText).FontSize(8);
                        }

                        table.Cell().ColumnSpan(7).Padding(2).Column(row =>
                        {
                            row.Item()
                                .PaddingTop(30)
                                .Element(container =>
                                {
                                    container.Row(row =>
                                    {
                                        row.RelativeItem().AlignLeft().Column(col =>
                                        {
                                            col.Item().Text("หมายเหตุ .............................................................................................").AlignCenter();
                                        });
                                    });
                                });
                        });

                        table.Cell().ColumnSpan(7).Padding(2).Column(row =>
                        {
                            row.Item()
                                .PaddingTop(20)
                                .Element(container =>
                                {
                                    container.Row(row =>
                                    {
                                        row.RelativeItem().AlignLeft().Column(col =>
                                        {
                                            col.Item().Text("ลงชื่อ ...............................").AlignCenter();
                                        });
                                    });
                                });
                        });
                    });
                });
            });

            static IContainer CellStyle(IContainer container)
            {
                return container
                    .Border(0.5f)
                    .BorderColor(Colors.Black)
                    .Padding(3);
            }
        }

        public static void WithdrawalReportHeader(this IContainer container, string? withdrawalNo = null)
        {
            container.PaddingBottom(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(30);
                    columns.RelativeColumn(30);
                });

                table.Cell().Element(CellStyle).AlignLeft().Column(col =>
                {
                    col.Item().Text("รายการเบิก").FontSize(11).SemiBold();
                    if (!string.IsNullOrWhiteSpace(withdrawalNo))
                        col.Item().Text($"เลขที่ : {withdrawalNo}").FontSize(9).SemiBold();
                });
                table.Cell().Element(CellStyle).AlignRight()
                     .Text($"วันที่ออกเอกสาร : {DateTime.Now.ToString("dd MMMM yyyy", new CultureInfo("th-TH"))}")
                     .FontSize(7);

                static IContainer CellStyle(IContainer c) => c.Padding(3);
            });
        }

        public static void WithdrawaleportContent(this IContainer container, List<WithdrawalModel> model)
        {
            var withdrawalNo = model.Count > 0 && model.All(x => x.WithdrawalNo != null && x.WithdrawalNo == model[0].WithdrawalNo)
                ? model[0].WithdrawalNo
                : null;

            container.Column(col =>
            {
                col.Item().PaddingTop(5).Element(c => c.WithdrawalReportHeader(withdrawalNo));

                col.Item().PaddingVertical(2).ShowOnce().Element(c =>
                {
                    c.Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Cell().Element(CellStyle).AlignCenter().Text("#").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("รูป").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("OrderNo").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("รหัสสินค้า").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("FN").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("GEM").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("จำนวน").FontSize(8);

                        for (int i = 0; i < model.Count; i++)
                        {
                            var item = model[i];
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{i + 1}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().AlignMiddle().Height(60).Width(70).Element(container => container.RenderItemImage(null, item.ImgPath));
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.OrderNo}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.Article}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.EDesFn}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.ListGem}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.Qty}").FontSize(8);
                        }

                        table.Cell().ColumnSpan(7).Padding(2).Column(row =>
                        {
                            row.Item()
                                .PaddingTop(30)
                                .Element(container =>
                                {
                                    container.Row(row =>
                                    {
                                        row.RelativeItem().AlignLeft().Column(col =>
                                        {
                                            col.Item().Text("หมายเหตุ .............................................................................................").AlignCenter();
                                        });
                                    });
                                });
                        });

                        table.Cell().ColumnSpan(7).Padding(2).Column(row =>
                        {
                            row.Item()
                                .PaddingTop(20)
                                .Element(container =>
                                {
                                    container.Row(row =>
                                    {
                                        row.RelativeItem().AlignLeft().Column(col =>
                                        {
                                            col.Item().Text("ลงชื่อ ...............................").AlignCenter();
                                        });
                                    });
                                });
                        });
                    });
                });
            });

            static IContainer CellStyle(IContainer container)
            {
                return container
                    .Border(0.5f)
                    .BorderColor(Colors.Black)
                    .Padding(3);
            }
        }

        private static void RenderItemImage(this IContainer container, byte[]? ImageBytes = null, string? ImagePath = null)
        {
            if (ImageBytes != null && ImageBytes.Length > 0)
            {
                container.Image(ImageBytes)
                    .FitArea()
                    .WithCompressionQuality(ImageCompressionQuality.High);
            }
            else if (!string.IsNullOrEmpty(ImagePath))
            {
                if (File.Exists(ImagePath))
                {
                    container.Image(ImagePath)
                        .FitArea()
                        .WithCompressionQuality(ImageCompressionQuality.High);
                }
                else
                {
                    container.RenderPlaceholder($"File not found:\n{Path.GetFileName(ImagePath)}");
                }
            }
            else
            {
                container.RenderPlaceholder("No Image Available");
            }
        }

        public static void RenderHeaderImage(this IContainer container, byte[]? ImageBytes = null, string? ImagePath = null)
        {
            if (ImageBytes != null && ImageBytes.Length > 0)
            {
                container.Image(ImageBytes)
                    .FitArea()
                    .WithCompressionQuality(ImageCompressionQuality.High);
            }
            else if (!string.IsNullOrEmpty(ImagePath))
            {
                if (File.Exists(ImagePath))
                {
                    container.Image(ImagePath)
                        .FitArea()
                        .WithCompressionQuality(ImageCompressionQuality.High);
                }
                else
                {
                    container.RenderPlaceholder($"File not found:\n{Path.GetFileName(ImagePath)}");
                }
            }
            else
            {
                container.RenderPlaceholder("No Image Available");
            }
        }

        private static void RenderPlaceholder(this IContainer container, string message = "No Image")
        {
            container.Background(Colors.Grey.Lighten3)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten1)
                .AlignCenter()
                .AlignMiddle()
                .Column(col =>
                {
                    col.Item().Text("💎")
                        .FontSize(24)
                        .AlignCenter()
                        .FontColor(Colors.Blue.Medium);

                    col.Item().PaddingTop(2).Text(message)
                        .FontSize(6)
                        .AlignCenter()
                        .FontColor(Colors.Grey.Darken1);
                });
        }
    }
}
