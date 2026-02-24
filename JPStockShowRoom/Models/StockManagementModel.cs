namespace JPStockShowRoom.Models
{
    public class StockItemModel
    {
        public int ReceivedId { get; set; }
        public string ReceiveNo { get; set; } = string.Empty;
        public string LotNo { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Article { get; set; } = string.Empty;
        public string? TempArticle { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public string CustCode { get; set; } = string.Empty;
        public string ListNo { get; set; } = string.Empty;
        public string ListGem { get; set; } = string.Empty;
        public decimal TtQty { get; set; }
        public decimal AvailableQty { get; set; }
        public double TtWg { get; set; }
        public string EDesFn { get; set; } = string.Empty;
        public bool IsInTray { get; set; }
        public string TrayNo { get; set; } = string.Empty;
        public int TrayId { get; set; }
        public bool IsWithdrawn { get; set; }
        public bool IsRepairing { get; set; }
        public int BorrowCount { get; set; }
        public string CreateDate { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ImgPath { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string EDesArt { get; set; } = string.Empty;
    }

    public class TrayModel
    {
        public int TrayId { get; set; }
        public string TrayNo { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalQty { get; set; }
        public double TotalWg { get; set; }
        public int BorrowCount { get; set; }
        public string CreatedDate { get; set; } = string.Empty;
        public string ArticleSummary { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class TrayItemModel
    {
        public int TrayItemId { get; set; }
        public int TrayId { get; set; }
        public string TrayNo { get; set; } = string.Empty;
        public int ReceivedId { get; set; }
        public string LotNo { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Article { get; set; } = string.Empty;
        public string? TempArticle { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public string? EDesFn { get; set; }
        public string? ListGem { get; set; }
        public string CustCode { get; set; } = string.Empty;
        public string ListNo { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public double Wg { get; set; }
        public bool IsBorrowed { get; set; }
        public decimal BorrowedQty { get; set; }
        public string? ImgPath { get; set; }
        public string CreatedDate { get; set; } = string.Empty;
    }

    public class TrayBorrowModel
    {
        public int TrayBorrowId { get; set; }
        public int TrayItemId { get; set; }
        public string TrayNo { get; set; } = string.Empty;
        public string LotNo { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Article { get; set; } = string.Empty;
        public string? EDesFn { get; set; }
        public string? ListGem { get; set; }
        public string? ImgPath { get; set; }
        public decimal BorrowQty { get; set; }
        public double BorrowWg { get; set; }
        public int BorrowedBy { get; set; }
        public string BorrowedDate { get; set; } = string.Empty;
        public string? ReturnedDate { get; set; }
        public bool IsReturned { get; set; }
    }

    public class WithdrawalModel
    {
        public int WithdrawalId { get; set; }
        public int ReceivedId { get; set; }
        public string LotNo { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Article { get; set; } = string.Empty;
        public string? TempArticle { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public string CustCode { get; set; } = string.Empty;
        public string ListNo { get; set; } = string.Empty;
        public string? EDesFn { get; set; }
        public string? ListGem { get; set; }
        public string? ImgPath { get; set; }
        public string EDesArt { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public double Wg { get; set; }
        public string? Remark { get; set; }
        public int WithdrawnBy { get; set; }
        public string WithdrawnDate { get; set; } = string.Empty;
    }

    public class WithdrawalReportFilterModel
    {
        public string? Article { get; set; }
        public string? EDesArt { get; set; }
        public string? Unit { get; set; }
    }

    public class StockReportFilterModel
    {
        public string? Article { get; set; }
        public string? EDesArt { get; set; }
        public string? Unit { get; set; }
    }

    public class SendToPackModel
    {
        public string CustCode { get; set; } = string.Empty;
        public string OrderNo { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public string Special { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string Approver { get; set; } = string.Empty;
        public List<SendToPackLots> Lots { get; set; } = [];
    }

    public class SendToPackLots
    {
        public string ListNo { get; set; } = string.Empty;
        public decimal TtQty { get; set; }
        public decimal TtQtyToPack { get; set; }
        public string Tunit { get; set; } = string.Empty;
        public string TdesArt { get; set; } = string.Empty;
        public string TdesFn { get; set; } = string.Empty;
        public string? Approver { get; set; }
        public string? ImagePath { get; set; }
        public List<object> Size { get; set; } = [];
    }
}
