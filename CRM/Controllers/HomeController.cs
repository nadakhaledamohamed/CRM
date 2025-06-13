using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Models;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Controllers
{
    public class HomeController : Controller
    {
        private readonly CallCenterContext _context;

        public HomeController(CallCenterContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var totalPersons = await _context.People.CountAsync();
            var totalRequests = await _context.Requests.CountAsync();

            var activeRequests = await _context.Requests
                .Include(r => r.StatusHistories)
                .Where(r => r.StatusHistories != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault() != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status!.StatusName == "Active")
                .CountAsync();

            var recentRequests = await _context.Requests
                .Include(r => r.Person)
                .Include(r => r.StatusHistories)
                    .ThenInclude(sh => sh.Status)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .ToListAsync();

            var newRequests = await _context.Requests
                .Include(r => r.StatusHistories)
                .Where(r => r.StatusHistories != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault() != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status!.StatusName == "New")
                .CountAsync();

            var inProgressRequests = await _context.Requests
                .Include(r => r.StatusHistories)
                .Where(r => r.StatusHistories != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault() != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status!.StatusName == "In Progress")
                .CountAsync();

            var pendingRequests = await _context.Requests
                .Include(r => r.StatusHistories)
                .Where(r => r.StatusHistories != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault() != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status!.StatusName == "Pending")
                .CountAsync();

            var completedRequests = await _context.Requests
                .Include(r => r.StatusHistories)
                .Where(r => r.StatusHistories != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault() != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status != null &&
                    r.StatusHistories
                        .OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status!.StatusName == "Completed")
                .CountAsync();

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
            };
         
            return View(dashboard);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }

   
}