using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace JPStockShowRoom.Data.SPDbContext.Entities;

public partial class TrayItem
{
    [Key]
    public int TrayItemId { get; set; }

    public int TrayId { get; set; }

    public int ReceivedId { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string LotNo { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string Barcode { get; set; } = null!;

    [Column(TypeName = "numeric(18, 1)")]
    public decimal Qty { get; set; }

    public double Wg { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public bool IsActive { get; set; }
}
