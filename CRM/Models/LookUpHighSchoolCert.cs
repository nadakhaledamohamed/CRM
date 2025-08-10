using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

[Table("LookUp_HighSchool_Cert")]
public partial class LookUpHighSchoolCert
{
    [Key]
    [Column("Certificate_ID")]
    public int CertificateId { get; set; }

    [Column("certificate_Name")]
    [StringLength(500)]
    public string CertificateName { get; set; } = null!;

    [InverseProperty("Certificate")]
    public virtual ICollection<Person> People { get; set; } = new List<Person>();
}
