using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JPStockShowRoom.Data.SPDbContext.Entities;

public partial class Tray
{
    [Key]
    public int TrayId { get; set; }

    [StringLength(50)]
    public string TrayNo { get; set; } = null!;

    [StringLength(200)]
    public string? Description { get; set; }

    public int CreatedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public bool IsActive { get; set; }
}
