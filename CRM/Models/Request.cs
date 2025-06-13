using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

public partial class Request
{
    [Key]
    [Column("RequestID")]
    public int RequestId { get; set; }

    [StringLength(255)]
    public string? Comments { get; set; }

    [Column("CreatedBy_Code")]
    public int CreatedByCode { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [Column("UpdatedBy_Code")]
    public int? UpdatedByCode { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LastFollowUpDate { get; set; }

    public int FollowUpCount { get; set; }

    [Column("PersonID")]
    public int PersonId { get; set; }

    public string Description { get; set; } = null!;


    public int StatusId { get; set; }

    // Navigation Property
    [ValidateNever]
    public LookUpStatusType Status { get; set; }

    [ForeignKey("CreatedByCode")]
    [InverseProperty("RequestCreatedByCodeNavigations")]
    public virtual User CreatedByCodeNavigation { get; set; } = null!;

    [ForeignKey("PersonId")]
    [InverseProperty("Requests")]
    public virtual Person Person { get; set; } = null!;

    [InverseProperty("Request")]
    public virtual ICollection<StatusHistory> StatusHistories { get; set; } = new List<StatusHistory>();

    [ForeignKey("UpdatedByCode")]
    [InverseProperty("RequestUpdatedByCodeNavigations")]
    public virtual User? UpdatedByCodeNavigation { get; set; }
}
