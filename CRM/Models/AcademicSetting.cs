using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

[Table("Academic_Setting")]
public partial class AcademicSetting
{
    [Key]
    [Column("Academic_Setting_ID")]
    public int AcademicSettingId { get; set; }

    public int Year { get; set; }

    /// <summary>
    /// 1 for fall ,2 for spring 
    /// </summary>
    [StringLength(50)]
    public string SemsterName { get; set; } = null!;

    [Column("NumberOf_Interests")]
    public int NumberOfInterests { get; set; }

    public bool IsActive { get; set; }
}
