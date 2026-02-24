using System;
using System.Collections.Generic;
using JPStockShowRoom.Data.SWDbContext.Entities;
using Microsoft.EntityFrameworkCore;

namespace JPStockShowRoom.Data.SWDbContext;

public partial class SWDbContext : DbContext
{
    public SWDbContext(DbContextOptions<SWDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Break> Break { get; set; }

    public virtual DbSet<MappingPermission> MappingPermission { get; set; }

    public virtual DbSet<Permission> Permission { get; set; }

    public virtual DbSet<Stock> Stock { get; set; }

    public virtual DbSet<Tray> Tray { get; set; }

    public virtual DbSet<TrayBorrow> TrayBorrow { get; set; }

    public virtual DbSet<TrayItem> TrayItem { get; set; }

    public virtual DbSet<Withdrawal> Withdrawal { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Thai_100_CI_AI");

        modelBuilder.Entity<MappingPermission>(entity =>
        {
            entity.HasKey(e => e.MappingPermissionId).HasName("PK_MenuPermission");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("PK_Menu");
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasKey(e => e.StockId).HasName("PK_Receive");
        });

        modelBuilder.Entity<Tray>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<TrayBorrow>(entity =>
        {
            entity.HasOne(d => d.TrayItem).WithMany(p => p.TrayBorrow)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TrayBorrow_TrayItem");
        });

        modelBuilder.Entity<TrayItem>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Tray).WithMany(p => p.TrayItem)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TrayItem_Tray");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
