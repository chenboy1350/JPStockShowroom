using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace JPStockShowRoom.Data.SWDbContext.Entities;

public partial class TrayBorrow
{
    [Key]
    [Column("TrayBorrowID")]
    public int TrayBorrowId { get; set; }

    [Column("TrayItemID")]
    public int TrayItemId { get; set; }

    [Column(TypeName = "numeric(18, 1)")]
    public decimal BorrowQty { get; set; }

    public double BorrowWg { get; set; }

    public int BorrowedBy { get; set; }

    public bool IsReturned { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? BorrowDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ReturnDate { get; set; }

    public int? UpdatedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdateDate { get; set; }

    [ForeignKey("TrayItemId")]
    [InverseProperty("TrayBorrow")]
    public virtual TrayItem TrayItem { get; set; } = null!;
}
