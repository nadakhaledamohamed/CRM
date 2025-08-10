using CRM.Models;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Models;

[Table("Lookup_Organization")]
public class Lookup_Organization
{
    [Key]

    [Column ("Organization_id")]
    public int Organization_id { get; set; }

    public string Organization_name { get; set; } = null!;

    public string? Address { get; set; }

    public string? Logo_Path { get; set; }

    public string? Website { get; set; }

    public virtual ICollection<AcademicSetting> Academic_Settings { get; set; } = new List<AcademicSetting>();

    public virtual ICollection<Person> People { get; set; } = new List<Person>();

    public virtual ICollection<User_org> User_orgs { get; set; } = new List<User_org>();
}
