using System.ComponentModel.DataAnnotations;

namespace CRM.ViewModel
{
    public class FollowUpLogViewModel
    {
        public int RequestID { get; set; }
       public int FollowUpLog_ID { get; set; }

        //[Required(ErrorMessage="Please Enter National ID ")]
        public string? NationalId { get; set; }
        public bool IsFirstFollowUp { get; set; }

        [Required]
        [Display(Name = "Status")]
        public int StatusID { get; set; }

        public string? RequestStatus { get; set; }


        [Display(Name = "Change Reason")]
        public string? ChangeReason { get; set; }

        [Display(Name = "Is Current Status")]
        public bool IsCurrent_Status { get; set; }

        [Display(Name = "Comment")]
        public string? Comment { get; set; }

       
        [Display(Name = "Follow-Up Type")]
        public int? FollowUpType_ID { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string FollowUpStatusText { get; set; } = "";
        public string FollowUpCssClass { get; set; } = "";
        public DateTime? AutoCloseDate { get; set; }
    }
}
