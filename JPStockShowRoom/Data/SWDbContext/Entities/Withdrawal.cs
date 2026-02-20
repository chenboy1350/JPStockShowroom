using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace JPStockShowRoom.Data.SWDbContext.Entities;

public partial class Withdrawal
{
    [Key]
    [Column("WithdrawalID")]
    public int WithdrawalId { get; set; }

    [Column("StockID")]
    public int StockId { get; set; }

    [Column(TypeName = "numeric(18, 1)")]
    public decimal Qty { get; set; }

    public double Wg { get; set; }

    [StringLength(500)]
    [Unicode(false)]
    public string? Remark { get; set; }

    public int WithdrawnBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreateDate { get; set; }

    public int? UpdatedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdateDate { get; set; }
}
