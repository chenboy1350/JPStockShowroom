using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JPStockShowRoom.Data.SPDbContext.Entities;

public partial class TrayBorrow
{
    [Key]
    public int TrayBorrowId { get; set; }

    public int TrayItemId { get; set; }

    public int TrayId { get; set; }

    [Column(TypeName = "numeric(18, 1)")]
    public decimal BorrowQty { get; set; }

    public double BorrowWg { get; set; }

    public int BorrowedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime BorrowedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ReturnedDate { get; set; }

    public bool IsReturned { get; set; }

    public bool IsActive { get; set; }
}
