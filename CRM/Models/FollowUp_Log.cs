using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

[Table("FollowUp_Log")]
public partial class FollowUp_Log
{
    [Key]
    [Column("FollowUp_ID")]
    public int FollowUp_ID { get; set; }

    [Column("RequestID")]
    public int RequestId { get; set; }

    [Column("StatusID")]
    public int StatusId { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string? ChangeReason { get; set; }

    [Column("IsCurrent_Status")]
    public bool IsCurrentStatus { get; set; }

    public string? Comment { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [Column("UpdatedBy_Code")]
    public int? UpdatedByCode { get; set; }

    [ForeignKey("RequestId")]
    [InverseProperty("FollowUp_Logs")]
    public virtual Request Request { get; set; } = null!;

    [ForeignKey("StatusId")]
    [InverseProperty("FollowUp_Logs")]
    public virtual LookUpStatusType Status { get; set; } = null!;

    [ForeignKey("UpdatedByCode")]
    [InverseProperty("FollowUp_Logs")]
    public virtual User? UpdatedByCodeNavigation { get; set; }

    [Column("FollowUpType_ID")]
    public int? FollowUpType_ID { get; set; }

    [ForeignKey("FollowUpType_ID")]
    [InverseProperty("FollowUpLog")]
    public virtual Lookup_FollowUpType  FollowUpType { get; set; } = null!;
}
