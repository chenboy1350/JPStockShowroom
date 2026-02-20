using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace JPStockShowRoom.Data.SPDbContext.Entities;

public partial class Assignment
{
    [Key]
    public int AssignmentId { get; set; }

    public int NumberWorkers { get; set; }

    public bool HasPartTime { get; set; }

    public bool IsReturned { get; set; }

    public bool IsActive { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreateDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdateDate { get; set; }
}

