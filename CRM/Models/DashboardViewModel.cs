using System.Diagnostics;

namespace CRM.Models
{
    public class DashboardViewModel
    {
        public int TotalPersons { get; set; }
        public int TotalRequests { get; set; }
        public int ActiveRequests { get; set; }
        public List<Request>? RecentRequests { get; set; }

        // Add stage counts
        public int NewRequests { get; set; }
        public int InProgressRequests { get; set; }
        public int PendingRequests { get; set; }
        public int CompletedRequests { get; set; }
     
        public List<Activity> RecentActivities { get; set; }
    }
}
