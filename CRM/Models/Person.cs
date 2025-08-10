using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

[Table("Person")]
public partial class Person
{
    [Key]
    [Column("PersonID")]
    public int PersonId { get; set; }

    public int? Org_id { get; set; }
    [StringLength(50)]
    public string FirstName { get; set; } = null!;

    //[StringLength(50)]
    //public string? LastName { get; set; } 

    [StringLength(50)]
    public string? Phone { get; set; } = null!;

    [StringLength(100)]
    public string? Email { get; set; } = null!;

    [Column("NationalID")]
    [StringLength(50)]
    public string? NationalId { get; set; } 

    [Column("Certificate_ID")]
    
    [Display(Name = "Certificate Type")]
    public int? CertificateId { get; set; }

    [Column("HighSchool_ID")]
 
    [Display(Name = "High School")]
    public int? HighSchoolId { get; set; }

  [Column("HowDidYouKnowUs_ID")]
//[Required(ErrorMessage = "Please specify how you heard about us")]
[Display(Name = "How Did You Know Us")]
    public int? HowDidYouKnowUsId { get; set; }

    [StringLength(500)]
    [Column("HowDidYouKnowUs_Other")]
    public string? HowDidYouKnowUs_Other { get; set; }

    /// <summary>
    /// 1 for Applicant &amp; 2 for Gurdian 
    /// </summary>

    public string? whatsApp { get; set; }
    public int UserType { get; set; }

    [Column("CreatedBy_Code")]
    public int CreatedByCode { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }=DateTime.Now;

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [Column("UpdatedBy_Code")]
    public int? UpdatedByCode { get; set; }

    //[Required(ErrorMessage = "Major is required")]
    //[Column("Major_ID")]
    //public int? MajorId { get; set; }

    [ForeignKey("CertificateId")]
    [InverseProperty("People")]
    [ValidateNever]
    public virtual LookUpHighSchoolCert Certificate { get; set; } = null!;

    [ForeignKey("CreatedByCode")]
    [InverseProperty("PersonCreatedByCodeNavigations")]
    [ValidateNever]
    public virtual User CreatedByCodeNavigation { get; set; } = null!;

    [ForeignKey("HighSchoolId")]
    [InverseProperty("People")]
    [ValidateNever]
    public virtual LookUpHighSchool HighSchool { get; set; } = null!;

    [ForeignKey("HowDidYouKnowUsId")]
    [InverseProperty("People")]
    [ValidateNever]
    public virtual LookUpHowDidYouKnowU HowDidYouKnowUs { get; set; } = null!;

    //[ForeignKey("MajorId")]
    //[InverseProperty("People")]
    //public virtual LookupMajor? Major { get; set; }

    [InverseProperty("Person")]
    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    [ForeignKey("UpdatedByCode")]
    [InverseProperty("PersonUpdatedByCodeNavigations")]
    [ValidateNever]
    public virtual User? UpdatedByCodeNavigation { get; set; }

    public virtual ICollection<MajorPerson>? MajorPersons { get; set; }


    // Foreign key fields
    [Column("CityID")]
    public int? CityID { get; set; }

    [Column("GradeID")]
    public int? GradeID { get; set; }

    [Column("NationalityID")]
    public int? NationalityID { get; set; }

    [ForeignKey("CityID")]
    [InverseProperty("People")]
    [ValidateNever]
    public virtual Lookup_City? City { get; set; }

    [ForeignKey("GradeID")]
    [InverseProperty("People")]
    [ValidateNever]
    public virtual LookUp_Grade? Grade { get; set; }

    [ForeignKey("NationalityID")]
    [InverseProperty("People")]
    [ValidateNever]
    public virtual Lookup_Nationality? Nationality { get; set; }
    [ForeignKey("Org_id")]
    [InverseProperty("People")]
    [ValidateNever]
    public virtual Lookup_Organization? Org { get; set; }
}

