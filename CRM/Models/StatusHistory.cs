using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

[Table("StatusHistory")]
public partial class StatusHistory
{
    [Key]
    [Column("HistoryID")]
    public int HistoryId { get; set; }

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
    [InverseProperty("StatusHistories")]
    public virtual Request Request { get; set; } = null!;

    [ForeignKey("StatusId")]
    [InverseProperty("StatusHistories")]
    public virtual LookUpStatusType Status { get; set; } = null!;

    [ForeignKey("UpdatedByCode")]
    [InverseProperty("StatusHistories")]
    public virtual User? UpdatedByCodeNavigation { get; set; }
}
