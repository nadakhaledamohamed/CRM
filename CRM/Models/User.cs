using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace CRM.Models;

[Index("Username", Name = "UQ__Users__536C85E4737850FD", IsUnique = true)]
[Index("Email", Name = "UQ__Users__A9D10534CE0E781A", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("UserID")]
    public int UserId { get; set; }

    [StringLength(150)]
    public string Username { get; set; } = null!;

    [StringLength(150)]
    public string Email { get; set; } = null!;

    [StringLength(255)]
    public string PasswordHash { get; set; } = null!;

    [StringLength(250)]
    public string FullName { get; set; } = null!;

    public int RoleId { get; set; }

    public bool IsActive { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LastLogin { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [StringLength(250)]
    public string? UpdatedBy { get; set; }

    public int UserCode { get; set; }


    public int? CreatedBy { get; set; } 

    [InverseProperty("CreatedByCodeNavigation")]
    public virtual ICollection<Person> PersonCreatedByCodeNavigations { get; set; } = new List<Person>();

    [InverseProperty("UpdatedByCodeNavigation")]
    public virtual ICollection<Person> PersonUpdatedByCodeNavigations { get; set; } = new List<Person>();

    [InverseProperty("CreatedByCodeNavigation")]
    public virtual ICollection<Request> RequestCreatedByCodeNavigations { get; set; } = new List<Request>();

    [InverseProperty("UpdatedByCodeNavigation")]
    public virtual ICollection<Request> RequestUpdatedByCodeNavigations { get; set; } = new List<Request>();

    [ForeignKey("RoleId")]
    [InverseProperty("Users")]
    public virtual LookupRole Role { get; set; } = null!;

    [InverseProperty("UpdatedByCodeNavigation")]
    public virtual ICollection<FollowUp_Log> FollowUp_Logs { get; set; } = new List<FollowUp_Log>();
    public virtual ICollection<User_org> User_orgs { get; set; } = new List<User_org>();
}


//| Property                          | Relationship | Description                                    |
//| --------------------------------- | ------------ | ---------------------------------------------- |
//| `PersonCreatedByCodeNavigations`  | One-to-many  | This user created many `Person` records        |
//| `PersonUpdatedByCodeNavigations`  | One-to-many  | This user updated many `Person` records        |
//| `RequestCreatedByCodeNavigations` | One-to-many  | This user created many `Request` records       |
//| `RequestUpdatedByCodeNavigations` | One-to-many  | This user updated many `Request` records       |
//| `Role`                            | Many-to-one  | User belongs to a `LookupRole` via `RoleId`    |
//| `StatusHistories`                 | One-to-many  | This user updated many `StatusHistory` records |
