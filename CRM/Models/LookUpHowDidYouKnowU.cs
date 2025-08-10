using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

[Table("LookUp_HowDidYouKnowUs")]
public partial class LookUpHowDidYouKnowU
{
    [Key]
    [Column("HowDidYouKnowUs_ID")]
    public int HowDidYouKnowUsId { get; set; }

    [StringLength(250)]
    public string HowDidYouKnowUs { get; set; } = null!;

    [InverseProperty("HowDidYouKnowUs")]
    public virtual ICollection<Person> People { get; set; } = new List<Person>();
}
