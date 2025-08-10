namespace CRM.ViewModel
{
    public class FollowUpDetailsViewModel
    {
        // Person Info
        public int PersonID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? NationalId { get; set; }
        public string? CertificateName { get; set; }
        public string? HighSchoolName { get; set; }
        public string? HowDidYouKnowUs { get; set; }
        public int? UserType { get; set; }
        public string? UserTypeName => UserType == 1 ? "Lead" : "Guardian";
        public List<string>? MajorInterests { get; set; }

        // Request Info
        public int RequestID { get; set; }
        public string? ReasonDescription { get; set; }
        public string? Comments { get; set; }
        public string? StatusName { get; set; }
        public int? StatusId { get; set; }
        public bool? IsCurrentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedByName { get; set; }
       public string? RequestStatus { get; set; }

        // Follow-Up Info
        
        public int FollowUpCount { get; set; }
        public DateTime? LastFollowUpDate { get; set; }
        public DateTime? NextFollowUpDate { get; set; }
        public bool IsOverdue { get; set; }
        public string FollowUpStatusText { get; set; } = string.Empty;
        public string FollowUpStatusCssClass { get; set; } = "text-muted";

        public List<FollowUpHistoryViewModel> FollowUpHistory { get; set; } = new List<FollowUpHistoryViewModel>();
        public List<FollowUpLogViewModel> FollowUpLogs { get; set; } = new List<FollowUpLogViewModel>();

    }

    public class FollowUpHistoryViewModel
    {
        public int FollowUpId { get; set; }
        public string? StatusName { get; set; }
        public string? FollowUpTypeName { get; set; }
        public string? ChangeReason { get; set; }
        public string? Comment { get; set; }
        public bool IsCurrentStatus { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedByName { get; set; }
    }
}
