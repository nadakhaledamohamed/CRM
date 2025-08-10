using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CRM.FuncModels;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Models;

[Table("LookUp_StatusTypes")]
[Index("StatusName", Name = "UQ__StatusTy__05E7698A1B6E7D5A", IsUnique = true)]
public partial class LookUpStatusType
{
    [Key]
    [Column("StatusID")]
  
    public int StatusId { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string StatusName { get; set; } = null!;
    public int? FollowUp_SettingID { get; set; }
    public bool RequireFollowUp { get; set; }
    public ICollection<Request>? Requests { get; set; }

    [ForeignKey("FollowUp_SettingID")]
    [InverseProperty("LookUpStatusTypes")]
    [ValidateNever]
    public virtual FollowUpSettings? FollowUpSettings { get; set; } = null!;

    public virtual ICollection<FollowUp_Log> FollowUp_Logs { get; set; } = new List<FollowUp_Log>();
}
