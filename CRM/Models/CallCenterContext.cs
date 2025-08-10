using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using CRM.Models;
using CRM.FuncModels;


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

    public virtual DbSet<FollowUp_Log> FollowUp_Log { get; set; }

    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<MajorPerson> MajorPersons { get; set; }

    public virtual DbSet<Lookup_FollowUpType> Lookup_FollowUpType { get; set; }

    public virtual DbSet<Lookup_Nationality> Lookup_Nationality { get; set; }
    public virtual DbSet<Lookup_City> Lookup_City { get; set; }

    public virtual DbSet<Lookup_ReasonDescription> Lookup_ReasonDescription { get; set; }

    public virtual DbSet<LookUp_Grade> LookUp_Grade { get; set; }
    public virtual DbSet<FollowUpSettings> FollowUpSetting { get; set; }
    public virtual DbSet<User_org> User_orgs { get; set; }
    public virtual DbSet<Lookup_Organization> Lookup_Organizations { get; set; }
    // Only needed if you want fallback configuration when DI isn't used



    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //     ////#warning //To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //     => optionsBuilder.UseSqlServer("Server=sql-svr_01;Database=CallCenterCRM;User Id=APP;Password=APP@1234;MultipleActiveResultSets=true;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MajorPerson>(entity =>
        {
            entity.ToTable("MajorPerson");
            entity.HasKey(e => e.MajorPerson_ID);


            entity.Property(e => e.Priority)
                  .IsRequired();

            entity.HasOne(e => e.Person)
                .WithMany(p => p.MajorPersons)
                .HasForeignKey(e => e.PersonID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MajorPerson_Person");

            entity.HasOne(e => e.LookupMajor)
                .WithMany(m => m.MajorPersons)
                .HasForeignKey(e => e.MajorID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MajorPerson_Major");

            entity.HasOne(e => e.AcademicSetting)
                .WithMany(a => a.MajorPersons)
                .HasForeignKey(e => e.Academic_Setting_ID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MajorPerson_AcademicSetting");

        });
        //modelBuilder.Entity<Lookup_Organization>(entity =>
        //{
        //    entity.HasKey(e => e.Organization_id);
        //    entity.ToTable("Lookup_Organization");

        //    entity.Property(e => e.Address).HasMaxLength(250);
        //    entity.Property(e => e.Logo_Path).HasMaxLength(250);
        //    entity.Property(e => e.Organization_name).HasMaxLength(250);
        //    entity.Property(e => e.Website).HasMaxLength(250);
        //});
        modelBuilder.Entity<AcademicSetting>(entity =>
        {
            entity.Property(e => e.AcademicSettingId).IsRequired();
            entity.Property(e => e.SemsterName).HasComment("1 for fall ,2 for spring ");
            entity.HasOne(e => e.Org)
                  .WithMany(o => o.Academic_Settings)
                  .HasForeignKey(e => e.Org_id)
                  .HasConstraintName("FK_Academic_Setting_Lookup_Organization");

        });

        modelBuilder.Entity<LookUpHighSchoolCert>(entity =>
        {
            entity.HasKey(e => e.CertificateId).HasName("PK_LookUp_HighSchool_Cert_1");
        });

        modelBuilder.Entity<LookUpStatusType>(entity =>
        {
            entity.HasKey(e => e.StatusId).HasName("PK__StatusTy__C8EE2043CC4C0C0E");
            //  RequireFollowUp column
            entity.Property(e => e.RequireFollowUp)
                  .HasDefaultValue(false); // Default to false

            //  FollowUp_SettingID as nullable foreign key
            entity.Property(e => e.FollowUp_SettingID)
                  .IsRequired(false); // Nullable foreign key

            // Configure relationship with FollowUpSettings
            entity.HasOne(d => d.FollowUpSettings)
                  .WithMany(p => p.LookUpStatusTypes)
                  .HasForeignKey(d => d.FollowUp_SettingID)
                  .OnDelete(DeleteBehavior.SetNull) // If FollowUpSettings is deleted, set FK to NULL
                  .HasConstraintName("FK_LookUpStatusType_FollowUpSettings");
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

            entity.Property(e => e.UserType).HasComment("1 for Lead & 2 for Guardian ");

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

            //entity.HasOne(d => d.Major).WithMany(p => p.People).HasConstraintName("FK_Person_Lookup_Major");

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

        modelBuilder.Entity<FollowUp_Log>(entity =>
        {
            entity.HasKey(e => e.FollowUp_ID).HasName("PK__StatusHi__4D7B4ADDB00E26D6");

            entity.HasOne(d => d.Request).WithMany(p => p.FollowUp_Logs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StatusHistory_Requests");

            entity.HasOne(d => d.Status).WithMany(p => p.FollowUp_Logs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StatusHistory_LookUp_StatusTypes");


            entity.HasOne(d => d.FollowUpType)

          .WithMany(p => p.FollowUpLog)
          .HasForeignKey(d => d.FollowUpType_ID)
          .OnDelete(DeleteBehavior.ClientSetNull)
          .HasConstraintName("FK_StatusHistory_FollowUpType");

            entity.HasOne(d => d.UpdatedByCodeNavigation).WithMany(p => p.FollowUp_Logs).HasConstraintName("FK_StatusHistory_Users");
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


        modelBuilder.Entity<LookUp_Grade>(entity =>
        {
            entity.HasKey(e => e.GradeID);
            entity.Property(e => e.GradeName)
                  .HasMaxLength(100);
        });

        modelBuilder.Entity<Lookup_City>(entity =>
        {
            entity.HasKey(e => e.CityID);
            entity.Property(e => e.CityName)
                  .HasMaxLength(100);
        });

        modelBuilder.Entity<Lookup_FollowUpType>(entity =>
        {
            entity.ToTable("Lookup_FollowUpType");
            entity.HasKey(e => e.FollowUpType_ID);
            entity.Property(e => e.FollowUpName)
                  .HasMaxLength(150);
        });

        modelBuilder.Entity<Lookup_ReasonDescription>(entity =>
        {
            entity.HasKey(e => e.ReasonID);
            entity.Property(e => e.Reason_Description)
                  .HasMaxLength(250);
        });

        modelBuilder.Entity<Lookup_Nationality>(entity =>
        {
            entity.HasKey(e => e.NationalityID);
            entity.Property(e => e.NationalityName)
                  .HasMaxLength(150);
        });

        modelBuilder.Entity<FollowUpSettings>(entity =>
        {
            entity.HasKey(e => e.FollowUp_SettingID);
            entity.Property(e => e.FollowUpIntervalDays).HasColumnName("FollowUpIntervalDays");
            entity.Property(e => e.MaxFollowUps).HasColumnName("MaxFollowUps");
            entity.Property(e => e.AutoCloseDays).HasColumnName("AutoCloseDays");
        });

        modelBuilder.Entity<User_org>(entity =>
        {
            entity.HasKey(e => e.tabelid);
            entity.ToTable("User_org");

            entity.HasOne(d => d.Org).WithMany(p => p.User_orgs)
                .HasForeignKey(d => d.Org_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_org_Lookup_Organization");

            entity.HasOne(d => d.Role).WithMany(p => p.User_orgs)
                .HasForeignKey(d => d.Roleid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_org_Lookup_Roles");

            entity.HasOne(d => d.user).WithMany(p => p.User_orgs)
                .HasForeignKey(d => d.userid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_org_Users");
        });

        modelBuilder.Entity<Lookup_Organization>(entity =>
        {
            entity.HasKey(e => e.Organization_id);
            entity.ToTable("Lookup_Organization");

            entity.Property(e => e.Address).HasMaxLength(250);
            entity.Property(e => e.Logo_Path).HasMaxLength(250);
            entity.Property(e => e.Organization_name).HasMaxLength(250);
            entity.Property(e => e.Website).HasMaxLength(250);
        });
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    //public DbSet<CRM.Models.PersonRequestViewModel> PersonRequestViewModel { get; set; } = default!;
    //    public object FollowUpNotifications { get; internal set; }
}
