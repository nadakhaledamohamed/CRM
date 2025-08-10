using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Models;
using System.Linq;
using System.Threading.Tasks;
using CRM.Services;

namespace CRM.Controllers
{
    public class HomeController : Controller
    {
        private readonly CallCenterContext _context;
        //private readonly FollowUpAutomationService _followUpService;



        public HomeController(CallCenterContext context )
        {
            _context = context;
            // _followUpService = followUpService;, FollowUpAutomationService followUpService
        }

        public FollowUpAutomationService FollowUpService { get; }

        public async Task<IActionResult> Index()
        {
            var totalPersons = await _context.People.CountAsync();
            var totalRequests = await _context.Requests.CountAsync();



     
            var activeRequests = await _context.Requests
                .Include(r => r.FollowUp_Logs)
                .Where(r => r.FollowUp_Logs != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault() != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status!.StatusName == "Active")
                .CountAsync();

            var recentRequests = await _context.Requests
                .Include(r => r.Person)
                .Include(r => r.FollowUp_Logs)
                    .ThenInclude(sh => sh.Status)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .ToListAsync();

            var newRequests = await _context.Requests
                .Include(r => r.FollowUp_Logs)
                .Where(r => r.FollowUp_Logs != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault() != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status!.StatusName == "New")
                .CountAsync();

            var inProgressRequests = await _context.Requests
                .Include(r => r.FollowUp_Logs)
                .Where(r => r.FollowUp_Logs != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault() != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status!.StatusName == "In Progress")
                .CountAsync();

            var pendingRequests = await _context.Requests
                .Include(r => r.FollowUp_Logs)
                .Where(r => r.FollowUp_Logs != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault() != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status!.StatusName == "Pending")
                .CountAsync();

            var completedRequests = await _context.Requests
                .Include(r => r.FollowUp_Logs)
                .Where(r => r.FollowUp_Logs != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault() != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status != null &&
                    r.FollowUp_Logs
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status!.StatusName == "Completed")
                .CountAsync();
            //follow up service 
            //var followUpAlerts = await _followUpService.GetFollowUpNotificationsAsync();
            var dashboard = new DashboardViewModel
            {
                TotalPersons = totalPersons,
                TotalRequests = totalRequests,
                ActiveRequests = activeRequests,
                RecentRequests = recentRequests,
                NewRequests = newRequests,
                InProgressRequests = inProgressRequests,
                PendingRequests = pendingRequests,
                CompletedRequests = completedRequests
                //,FollowUpAlerts = followUpAlerts
            };
         
            return View(dashboard);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }

   
}