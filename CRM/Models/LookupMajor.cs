using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

[Table("Lookup_Major")]
public partial class LookupMajor
{
    [Column("Major_Interest")]
    [StringLength(150)]
    public string MajorInterest { get; set; } = null!;

    [Key]
    [Column("MajorID")]
    public int MajorId { get; set; }

    //[InverseProperty("Major")]
    //public virtual ICollection<Person> People { get; set; } = new List<Person>();
    public virtual ICollection<MajorPerson>? MajorPersons { get; set; }
}
