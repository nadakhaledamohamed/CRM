using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Azure.Core;
using Request = CRM.Models.Request;

namespace CRM.Controllers
{
    public class RequestController : Controller
    {
        private readonly CallCenterContext _context;

        public RequestController(CallCenterContext context)
        {
            _context = context;
        }

        // GET: Requests
        public async Task<IActionResult> Index(string statusFilter)
        {
            var requests = _context.Requests
                .Include(r => r.Person)
                .Include(r => r.StatusHistories)
                    .ThenInclude(sh => sh.Status)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
            {
                requests = requests.Where(r =>
                    r.StatusHistories.OrderByDescending(sh => sh.UpdatedAt)
                        .FirstOrDefault()!.Status.StatusName == statusFilter);
            }

            ViewBag.StatusTypes = await _context.LookUpStatusTypes.ToListAsync();
            return View(await requests.ToListAsync());
        }

        // GET: Requests/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var request = await _context.Requests
                .Include(r => r.Person)
                .Include(r => r.StatusHistories)
                    .ThenInclude(sh => sh.Status)
                .Include(r => r.StatusHistories)
                    .ThenInclude(sh => sh.UpdatedByCodeNavigation)
                .FirstOrDefaultAsync(m => m.RequestId == id);

            if (request == null)
            {
                return NotFound();
            }

            return View(request);
        }

        // GET: Requests/Create
        public IActionResult Create()
        {
            ViewData["PersonId"] = new SelectList(_context.People, "PersonId", "FullName");
            return View();
        }

        // POST: Requests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RequestId,PersonId,RequestDetails")] Models.Request request)
        {
            if (ModelState.IsValid)
            {
                request.CreatedByCode = 1; // Replace with current user ID
                request.CreatedAt = DateTime.Now;

                _context.Add(request);
                await _context.SaveChangesAsync();

                // Add initial status
                var initialStatus = new StatusHistory
                {
                    RequestId = request.RequestId,
                    StatusId = 1, // Set to your initial status ID
                    UpdatedByCode = 1, // Replace with current user ID
                    UpdatedAt = DateTime.Now,
                    Comment = "Request created"
                };
                _context.Add(initialStatus);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            ViewData["PersonId"] = new SelectList(_context.People, "PersonId", "FullName", request.PersonId);
            return View(request);
        }

        // GET: Requests/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var request = await _context.Requests.FindAsync(id);
            if (request == null)
            {
                return NotFound();
            }
            ViewData["PersonId"] = new SelectList(_context.People, "PersonId", "FullName", request.PersonId);
            return View(request);
        }

        // POST: Requests/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RequestId,PersonId,RequestDetails")] Models.Request request)
        {
            if (id != request.RequestId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    request.UpdatedByCode = 1; // Replace with current user ID
                    request.UpdatedAt = DateTime.Now;
                    _context.Update(request);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RequestExists(request.RequestId))
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
            ViewData["PersonId"] = new SelectList(_context.People, "PersonId", "FullName", request.PersonId);
            return View(request);
        }

        // GET: Requests/ChangeStatus/5
        public async Task<IActionResult> ChangeStatus(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var request = await _context.Requests
                .Include(r => r.StatusHistories)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null)
            {
                return NotFound();
            }

            ViewBag.StatusTypes = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName");
            ViewBag.CurrentStatus = request.StatusHistories
                .OrderByDescending(sh => sh.UpdatedAt)
                .FirstOrDefault()?.Status?.StatusName;

            return View(new StatusChangeViewModel
            {
                RequestId = request.RequestId
            });
        }

        // POST: Requests/ChangeStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, StatusChangeViewModel model)
        {
            if (id != model.RequestId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var statusHistory = new StatusHistory
                {
                    RequestId = model.RequestId,
                    StatusId = model.StatusId,
                    UpdatedByCode = 1, // Replace with current user ID
                    UpdatedAt = DateTime.Now,
                    Comment = model.Notes
                };

                _context.Add(statusHistory);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Details), new { id = model.RequestId });
            }

            ViewBag.StatusTypes = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName", model.StatusId);
            return View(model);
        }

        // GET: Requests/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var request = await _context.Requests
                .Include(r => r.Person)
                .FirstOrDefaultAsync(m => m.RequestId == id);

            if (request == null)
            {
                return NotFound();
            }

            return View(request);
        }

        // POST: Requests/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var request = await _context.Requests.FindAsync(id);
            _context.Requests.Remove(request);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RequestExists(int id)
        {
            return _context.Requests.Any(e => e.RequestId == id);
        }

        [HttpGet]
        public IActionResult CreateFromPerson(int personId)
        {
            var model = new Request
            {
                PersonId = personId,
                LastFollowUpDate = null,  // User will enter this date
                Description = "",
                Comments = "",
                StatusId = 0,
                CreatedByCode=1,
                //CreatedAt=DateTime.Now
            };

            ViewBag.StatusId = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName");

            return PartialView("_CreateFromPerson", model);
        }

        [HttpPost]
        public IActionResult CreateFromPerson(Request model)
        {
            ModelState.Remove("Person");
            ModelState.Remove("CreatedByCodeNavigation");

            if (!ModelState.IsValid)
            {
                foreach (var modelState in ModelState)
                {
                    foreach (var error in modelState.Value.Errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"Key: {modelState.Key}, Error: {error.ErrorMessage}");
                    }
                }
                    ViewBag.StatusId = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName", model.StatusId);
                return PartialView("_CreateFromPerson", model);
            }

            try
            {
                var request = new Request
                {
                    PersonId = model.PersonId,
                    StatusId = model.StatusId,
                    Description = model.Description,
                    Comments = model.Comments,
                    FollowUpCount=model.FollowUpCount,
                    LastFollowUpDate = model.LastFollowUpDate,
                    UpdatedAt=model.UpdatedAt,
                    UpdatedByCode = model.UpdatedByCode,  // Replace with your method to get current user ID
                    CreatedByCode = 1,  // Replace with your method to get current user ID
                    CreatedAt = DateTime.Now
                };

                _context.Requests.Add(request);
                _context.SaveChanges();

                return Json(new { success = true, personId = model.PersonId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while saving the request: " + ex.Message);
                ViewBag.StatusId = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName", model.StatusId);
                return PartialView("_CreateFromPerson", model);
            }
        }


        private int GetCurrentUserId()
        {
            // Example: get logged-in user ID from claims or session
            // return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            return 0; // fallback or test
        }

        private async Task LoadSelectLists()
        {
            ViewBag.StatusId = new SelectList(await _context.LookUpStatusTypes.ToListAsync(), "StatusId", "StatusName");
        }

     
        public class StatusChangeViewModel
        {
            public int RequestId { get; set; }
            public int StatusId { get; set; }
            public string Notes { get; set; } = string.Empty;
        }



    }
}
