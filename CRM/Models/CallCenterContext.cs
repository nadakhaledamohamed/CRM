using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using CRM.Models;

namespace CRM.Models;

public partial class CallCenterContext : DbContext
{
    public CallCenterContext()
    {
    }

    public CallCenterContext(DbContextOptions<CallCenterContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AcademicSetting> AcademicSettings { get; set; }

    public virtual DbSet<LookUpHighSchool> LookUpHighSchools { get; set; }

    public virtual DbSet<LookUpHighSchoolCert> LookUpHighSchoolCerts { get; set; }

    public virtual DbSet<LookUpHowDidYouKnowU> LookUpHowDidYouKnowUs { get; set; }

    public virtual DbSet<LookUpStatusType> LookUpStatusTypes { get; set; }

    public virtual DbSet<LookupMajor> LookupMajors { get; set; }

    public virtual DbSet<LookupRole> LookupRoles { get; set; }

    public virtual DbSet<Person> People { get; set; }

    public virtual DbSet<Request> Requests { get; set; }

    public virtual DbSet<StatusHistory> StatusHistories { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
//#warning //To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=ADM-ICT-SW-1;Database=CallCenterCRM;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AcademicSetting>(entity =>
        {
            entity.Property(e => e.AcademicSettingId).ValueGeneratedNever();
            entity.Property(e => e.SemsterName).HasComment("1 for fall ,2 for spring ");
        });

        modelBuilder.Entity<LookUpHighSchoolCert>(entity =>
        {
            entity.HasKey(e => e.CertificateId).HasName("PK_LookUp_HighSchool_Cert_1");
        });

        modelBuilder.Entity<LookUpStatusType>(entity =>
        {
            entity.HasKey(e => e.StatusId).HasName("PK__StatusTy__C8EE2043CC4C0C0E");
        });

        modelBuilder.Entity<LookupMajor>(entity =>
        {
            entity.Property(e => e.MajorInterest).IsFixedLength();
        });

        modelBuilder.Entity<LookupRole>(entity =>
        {
            entity.Property(e => e.RoleName).IsFixedLength();
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(e => e.PersonId).HasName("PK__Applican__39AE914807969DE7");

            entity.Property(e => e.UserType).HasComment("1 for Applicant & 2 for Gurdian ");

            entity.HasOne(d => d.Certificate).WithMany(p => p.People)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Person_LookUp_HighSchool_Cert1");

            entity.HasOne(d => d.CreatedByCodeNavigation).WithMany(p => p.PersonCreatedByCodeNavigations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Person_Users1");

            entity.HasOne(d => d.HighSchool).WithMany(p => p.People)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Person_LookUp_HighSchools1");

            entity.HasOne(d => d.HowDidYouKnowUs).WithMany(p => p.People)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Person_LookUp_HowDidYouKnowUs");

            entity.HasOne(d => d.Major).WithMany(p => p.People).HasConstraintName("FK_Person_Lookup_Major");

            entity.HasOne(d => d.UpdatedByCodeNavigation).WithMany(p => p.PersonUpdatedByCodeNavigations).HasConstraintName("FK_Person_Users");
        });

        modelBuilder.Entity<Request>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Requests__33A8519A39A60AAD");

            entity.HasOne(d => d.CreatedByCodeNavigation).WithMany(p => p.RequestCreatedByCodeNavigations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Requests_Users");

            entity.HasOne(d => d.Person).WithMany(p => p.Requests)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Requests_Person");

            entity.HasOne(d => d.UpdatedByCodeNavigation).WithMany(p => p.RequestUpdatedByCodeNavigations).HasConstraintName("FK_Requests_Users1");
            //  for LookupStatus relationship
            entity.HasOne(d => d.Status)
                .WithMany(p => p.Requests)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull) 
                .HasConstraintName("FK_Requests_LookupStatus");
        });

        modelBuilder.Entity<StatusHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__StatusHi__4D7B4ADDB00E26D6");

            entity.HasOne(d => d.Request).WithMany(p => p.StatusHistories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StatusHistory_Requests");

            entity.HasOne(d => d.Status).WithMany(p => p.StatusHistories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StatusHistory_LookUp_StatusTypes");

            entity.HasOne(d => d.UpdatedByCodeNavigation).WithMany(p => p.StatusHistories).HasConstraintName("FK_StatusHistory_Users");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCACD67BA816");

            entity.Property(e => e.CreatedBy).IsFixedLength();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedBy).IsFixedLength();

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Roles");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

public DbSet<CRM.Models.PersonRequestViewModel> PersonRequestViewModel { get; set; } = default!;
}
