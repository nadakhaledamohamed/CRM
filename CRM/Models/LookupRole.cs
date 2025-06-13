using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

[Table("Lookup_Roles")]
public partial class LookupRole
{
    [StringLength(10)]
    public string RoleName { get; set; } = null!;

    [Key]
    [Column("RoleID")]
    public int RoleId { get; set; }

    [InverseProperty("Role")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
