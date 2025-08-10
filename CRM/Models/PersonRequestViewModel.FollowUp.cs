using System.ComponentModel.DataAnnotations.Schema;
using CRM.FuncModels;
using CRM.Helpers;
namespace CRM.Models
{
    public partial class PersonRequestViewModel
    {
        // Add these properties to store the follow-up settings
        [NotMapped]
        public int MaxFollowUps { get; set; }

        [NotMapped]
        public int FollowUpIntervalDays { get; set; }

        [NotMapped]
        public bool IsFollowUpOverdue
        {
            get
            {
                // If max follow-ups reached, not overdue
                if (FollowUpCount >= MaxFollowUps) return false;

                // If status doesn't require follow-up, not overdue
                if (!RequiresFollowUp) return false;

                var referenceDate = LastFollowUpDate ?? Request_CreatedAt;
                return (DateTime.Now.Date - referenceDate.Date).Days >= FollowUpIntervalDays;
            }
        }

        [NotMapped]
        public int DaysSinceLastFollowUp
        {
            get
            {
                var referenceDate = LastFollowUpDate ?? Request_CreatedAt;
                return (DateTime.Now.Date - referenceDate.Date).Days;
            }
        }

        [NotMapped]
        public DateTime? NextFollowUpDate
        {
            get
            {
                if (FollowUpCount >= MaxFollowUps) return null;
                if (!RequiresFollowUp) return null;

                var lastDate = LastFollowUpDate ?? Request_CreatedAt;
                return lastDate.AddDays(FollowUpIntervalDays);
            }
        }

        [NotMapped]
        public string FollowUpStatusText
        {
            get
            {
                if (FollowUpCount >= MaxFollowUps)
                    return "Max follow-ups reached";

                if (!RequiresFollowUp)
                    return "No follow-up needed";

                if (NextFollowUpDate.HasValue)
                {
                    var daysUntil = (NextFollowUpDate.Value.Date - DateTime.Now.Date).Days;
                    return daysUntil switch
                    {
                        < 0 => $"Overdue ({-daysUntil} days)",
                        0 => "Due today",
                        1 => "Due tomorrow",
                        > 1 => $"Due in {daysUntil} days"
                    };
                }
                return "No follow-up needed";
            }
        }

        [NotMapped]
        public string FollowUpStatusCssClass
        {
            get
            {
                if (FollowUpCount >= MaxFollowUps) return "text-muted";
                if (!RequiresFollowUp) return "text-muted";
                if (IsFollowUpOverdue) return "text-danger fw-bold";
                if (NextFollowUpDate?.Date == DateTime.Now.Date) return "text-warning fw-bold";
                return "text-success";
            }
        }

        [NotMapped]
        public bool IsNearAutoClose
        {
            get
            {
                if (FollowUpCount < MaxFollowUps || !LastFollowUpDate.HasValue) return false;
                var autoCloseDate = LastFollowUpDate.Value.AddDays(9);
                return autoCloseDate <= DateTime.Now.Date.AddDays(2);
            }
        }

        [NotMapped]
        public DateTime? AutoCloseDate =>
            (FollowUpCount >= MaxFollowUps && LastFollowUpDate.HasValue)
                ? LastFollowUpDate.Value.AddDays(9)
                : null;

        [NotMapped]
        public bool CanFollowUp =>
            FollowUpCount < MaxFollowUps &&
            !string.IsNullOrEmpty(StatusName) &&
            !StatusName.ToLower().Contains("closed") &&
            !StatusName.ToLower().Contains("completed");
    }
}