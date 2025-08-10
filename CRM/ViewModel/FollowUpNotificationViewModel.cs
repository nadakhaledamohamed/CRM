using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.ViewModel
{
    public class FollowUpNotificationViewModel
    {
        public int RequestID { get; set; }
        public int StatusId { get; set; }
        public int PersonID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int FollowUpCount { get; set; }
        public DateTime? LastFollowUpDate { get; set; }
        public string? StatusName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool? IsCurrent_Status { get; set; }
        public bool IsDueToday =>
            LastFollowUpDate == null || LastFollowUpDate.Value.AddDays(3) <= DateTime.Now.Date;

        /// <summary>
        /// Days overdue for follow-up
        /// </summary>
        public int DaysOverdue
        {
            get
            {
                if (LastFollowUpDate == null)
                    return 0; // No follow-up yet, treat as due

                var dueDate = LastFollowUpDate.Value.AddDays(3);
                return Math.Max(0, (DateTime.Now.Date - dueDate.Date).Days);
            }
        }

        /// <summary>
        /// Priority level for sorting (higher = more urgent)
        /// </summary>
        public int Priority => DaysOverdue * 10 + (3 - FollowUpCount);

        /// <summary>
        /// CSS class for UI styling based on urgency
        /// </summary>
        public string UrgencyCssClass
        {
            get
            {
                return DaysOverdue switch
                {
                    0 => "table-warning",  // Due today
                    1 => "table-danger",   // 1 day overdue
                    >= 2 => "table-dark",  // 2+ days overdue
                    _ => ""
                };
            }
        }
    }
}