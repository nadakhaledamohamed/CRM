using CRM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Controllers
{
    [Route("[controller]")]
    public class StatusHistoriesController : Controller
    {
        private readonly CallCenterContext _context;

        public StatusHistoriesController(CallCenterContext context)
        {
            _context = context;
        }

        // GET: StatusHistories  
        public async Task<IActionResult> Index()
        {
            var statusHistories = await _context.StatusHistories
                .Include(s => s.Request)
                .Include(s => s.Status)
                .Include(s => s.UpdatedByCodeNavigation)
                .ToListAsync();
            return View(statusHistories);
        }

        // GET: StatusHistories/Details/5  
        public async Task<IActionResult> Details(int id)
        {
            var statusHistory = await _context.StatusHistories
                .Include(s => s.Request)
                .Include(s => s.Status)
                .Include(s => s.UpdatedByCodeNavigation)
                .FirstOrDefaultAsync(s => s.HistoryId == id);

            if (statusHistory == null)
            {
                return NotFound();
            }

            return View(statusHistory);
        }

        // GET: StatusHistories/Create  
        public IActionResult Create()
        {
            return View();
        }

        // POST: StatusHistories/Create  
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StatusHistory statusHistory)
        {
            if (ModelState.IsValid)
            {
                _context.StatusHistories.Add(statusHistory);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(statusHistory);
        }

        // GET: StatusHistories/Edit/5  
        public async Task<IActionResult> Edit(int id)
        {
            var statusHistory = await _context.StatusHistories.FindAsync(id);
            if (statusHistory == null)
            {
                return NotFound();
            }
            return View(statusHistory);
        }

        // POST: StatusHistories/Edit/5  
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StatusHistory statusHistory)
        {
            if (id != statusHistory.HistoryId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(statusHistory);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StatusHistoryExists(statusHistory.HistoryId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(statusHistory);
        }

        // GET: StatusHistories/Delete/5  
        public async Task<IActionResult> Delete(int id)
        {
            var statusHistory = await _context.StatusHistories
                .FirstOrDefaultAsync(s => s.HistoryId == id);
            if (statusHistory == null)
            {
                return NotFound();
            }

            return View(statusHistory);
        }

        // POST: StatusHistories/Delete/5  
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var statusHistory = await _context.StatusHistories.FindAsync(id);
            if (statusHistory != null)
            {
                _context.StatusHistories.Remove(statusHistory);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool StatusHistoryExists(int id)
        {
            return _context.StatusHistories.Any(e => e.HistoryId == id);
        }
    }
}
