﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace secureshare.Models;

public partial class secureshareContext : DbContext
{
    public secureshareContext(DbContextOptions<secureshareContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Action> Actions { get; set; }

    public virtual DbSet<Admin> Admins { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Folder> Folders { get; set; }

    public virtual DbSet<Partition> Partitions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserFolderPermission> UserFolderPermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Action>(entity =>
        {
            entity.HasKey(e => e.ActionID).HasName("PK__Actions__FFE3F4B9847A3509");

            entity.HasOne(d => d.User).WithMany(p => p.Actions).HasConstraintName("FK__Actions__UserID__412EB0B6");
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.AdminID).HasName("PK__Admins__719FE4E817A38D8C");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentID).HasName("PK__Departme__B2079BCD70B2F54C");
        });

        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(e => e.FolderID).HasName("PK__Folders__ACD7109F9587366B");

            entity.HasOne(d => d.Partition).WithMany(p => p.Folders).HasConstraintName("FK_Folders_Partitions");
        });

        modelBuilder.Entity<Partition>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_PartitionsNew");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserID).HasName("PK__Users__1788CCAC48C8A588");
        });

        modelBuilder.Entity<UserFolderPermission>(entity =>
        {
            entity.HasOne(d => d.Folder).WithMany(p => p.UserFolderPermissions).HasConstraintName("FK__UserFolde__Folde__151B244E");

            entity.HasOne(d => d.User).WithMany(p => p.UserFolderPermissions).HasConstraintName("FK__UserFolde__UserI__160F4887");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}