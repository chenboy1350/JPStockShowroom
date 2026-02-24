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
                                    return container
                                        .Border(0.5f)
                                        .BorderColor(Colors.Black)
                                        .Padding(3);
                                }
                            });
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

        public static void BreakReportHeader(this IContainer container)
        {
            container.PaddingBottom(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(30);
                    columns.RelativeColumn(30);
                });

                table.Cell().Element(CellStyle).AlignLeft().Text("รายการส่งซ่อม").FontSize(11).SemiBold();
                table.Cell().Element(CellStyle).AlignRight()
                     .Text($"วันที่ออกเอกสาร : {DateTime.Now.ToString("dd MMMM yyyy", new CultureInfo("th-TH"))}")
                     .FontSize(7);

                static IContainer CellStyle(IContainer c) => c.Padding(3);
            });
        }

        public static void BreakReportContent(this IContainer container, List<LostAndRepairModel> model)
        {
            container.Column(col =>
            {
                // ส่วนหัว
                col.Item().PaddingTop(5).Element(c => c.BreakReportHeader());

                // ตาราง - ใช้ ShowOnce() เพื่อไม่ให้แบ่งข้าม page
                col.Item().PaddingVertical(2).ShowOnce().Element(c =>
                {
                    c.Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });

                        // Header row
                        table.Cell().Element(CellStyle).AlignCenter().Text("#").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("เลขที่ใบรับ").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("LotNo").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("รหัสลูกค้า\nOrderNo").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("ลำดับ").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("รหัสสินค้า").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("จำนวนที่\nส่งซ่อม").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("อาการ").FontSize(8);
                        table.Cell().Element(CellStyle).AlignCenter().Text("วันที่แจ้ง").FontSize(8);

                        for (int i = 0; i < model.Count; i++)
                        {
                            var item = model[i];
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{i + 1}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.ReceiveNo}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.LotNo}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.CustCode}/{item.OrderNo}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.ListNo}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.Article}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.BreakQty}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.BreakDescription}").FontSize(8);
                            table.Cell().Element(CellStyle).AlignCenter().Text($"{item.CreateDateTH}").FontSize(8);
                        }

                        // Footer signature
                        table.Cell().ColumnSpan(9).Padding(2).Column(row =>
                        {
                            row.Item()
                                .PaddingTop(60) // ลด padding เพื่อประหยัดพื้นที่
                                .Element(container =>
                                {
                                    container.Row(row =>
                                    {
                                        row.RelativeItem().AlignCenter().Column(col =>
                                        {
                                            col.Item().Text("(...............................)").AlignCenter();
                                            col.Item().Text("ผู้แจ้ง").FontSize(8).AlignCenter();
                                        });
                                        row.RelativeItem().AlignCenter().Column(col =>
                                        {
                                            col.Item().Text("(...............................)").AlignCenter();
                                            col.Item().Text("ผู้อนุมัติ").FontSize(8).AlignCenter();
                                        });
                                        row.RelativeItem().AlignCenter().Column(col =>
                                        {
                                            col.Item().Text("(...............................)").AlignCenter();
                                            col.Item().Text("ผู้รับ").FontSize(8).AlignCenter();
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

    }
}
