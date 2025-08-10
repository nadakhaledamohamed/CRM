using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

[Table("LookUp_HighSchools")]
public partial class LookUpHighSchool
{
    [Key]
    [Column("HighSchool_ID")]
    public int HighSchoolId { get; set; }

    [StringLength(500)]     
    public string HighSchoolName { get; set; } = null!;

    [InverseProperty("HighSchool")]
    public virtual ICollection<Person> People { get; set; } = new List<Person>();
}
