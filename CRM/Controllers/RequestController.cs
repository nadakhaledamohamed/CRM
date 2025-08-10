using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Azure.Core;
using Request = CRM.Models.Request;
using System.Security.Claims;

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
                .Include(r => r.FollowUp_Logs)
                    .ThenInclude(sh => sh.Status)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
            {
                requests = requests.Where(r =>
                    r.FollowUp_Logs.OrderByDescending(sh => sh.UpdatedAt)
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
                .Include(r => r.FollowUp_Logs)
                    .ThenInclude(sh => sh.Status)
                .Include(r => r.FollowUp_Logs)
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
                request.CreatedByCode = 1; 
                request.CreatedAt = DateTime.Now;

                _context.Add(request);
                await _context.SaveChangesAsync();

                // Add initial status
                var initialStatus = new FollowUp_Log
                {
                    RequestId = request.RequestId,
                    StatusId = 1, 
                    UpdatedByCode = 1, 
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
                    request.UpdatedByCode = 1;
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
                .Include(r => r.FollowUp_Logs)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null)
            {
                return NotFound();
            }

            ViewBag.StatusTypes = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName");
            ViewBag.CurrentStatus = request.FollowUp_Logs
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
                var statusHistory = new FollowUp_Log
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
        public async Task<IActionResult> CreateFromPerson(int personId)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);
            var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

            try
            {
                System.Diagnostics.Debug.WriteLine($"GET CreateFromPerson called with personId: {personId}");

                var viewModel = new RequestViewModel
                {
                    PersonId = personId,
                    LastFollowUpDate = null,
                    ReasonID = null,
                    Comments = "",
                    StatusId = null,
                    CreatedbyCode = currentUserId,
                    CreatedByCodeName = currentUserName,
                    FollowUpCount = 0,
                    CreatedAt = DateTime.Now,
                    ReasonDescription_Other = ""
                };

                // Load dropdown data - get active reasons and add "Other" option
                var reasons = await _context.Lookup_ReasonDescription
                    .OrderBy(r => r.Reason_Description)
                    .Select(r => new { r.ReasonID, r.Reason_Description })
                    .ToListAsync();

                // Add "Other" option at the end (using negative ID to distinguish)
                var reasonsList = reasons.ToList();
                reasonsList.Add(new { ReasonID = -1, Reason_Description = "Other (Please specify)" });

                ViewBag.StatusId = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName");
                ViewBag.ReasonID = new SelectList(reasonsList, "ReasonID", "Reason_Description");
                ViewBag.CurrentUserName = currentUserName;

                System.Diagnostics.Debug.WriteLine("Returning partial view");
                return PartialView("_CreateFromPerson", viewModel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GET CreateFromPerson: {ex.Message}");

                // Fallback dropdown data
                ViewBag.StatusId = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName");
                ViewBag.ReasonID = new SelectList(new List<object>(), "ReasonID", "Reason_Description");
                ViewBag.CurrentUserName = currentUserName;

                return PartialView("_CreateFromPerson", new RequestViewModel { PersonId = personId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromPerson(RequestViewModel viewModel)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);
            var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

            try
            {
                System.Diagnostics.Debug.WriteLine($"POST CreateFromPerson called with PersonId: {viewModel.PersonId}");
                System.Diagnostics.Debug.WriteLine($"ReasonID received: {viewModel.ReasonID}");
                System.Diagnostics.Debug.WriteLine($"ReasonDescription_Other: {viewModel.ReasonDescription_Other}");

                // Remove navigation properties from validation
                ModelState.Remove("CreatedByName");
                ModelState.Remove("UpdatedByName");
                ModelState.Remove("CreatedByCodeName");
                ModelState.Remove("StatusName");
                ModelState.Remove("ReasonDescription");

                // Handle "Other" reason selection
                if (viewModel.ReasonID == -1)
                {
                    System.Diagnostics.Debug.WriteLine("Other reason selected.");

                    if (string.IsNullOrWhiteSpace(viewModel.ReasonDescription_Other))
                    {
                        ModelState.AddModelError("ReasonDescription_Other", "Please specify the reason when 'Other' is selected.");
                    }
                    else
                    {
                        var trimmed = viewModel.ReasonDescription_Other.Trim();
                        System.Diagnostics.Debug.WriteLine($"Custom Reason Input: {trimmed}");

                        // Check if reason already exists (case-insensitive)
                        var existingReason = await _context.Lookup_ReasonDescription
                            .FirstOrDefaultAsync(r => r.Reason_Description.ToLower() == trimmed.ToLower());

                        if (existingReason != null)
                        {
                            // Use existing reason
                            viewModel.ReasonID = existingReason.ReasonID;
                            System.Diagnostics.Debug.WriteLine($"Using existing reason ID: {existingReason.ReasonID}");
                        }
                        else
                        {
                            // Create new reason
                            var newReason = new Lookup_ReasonDescription
                            {
                                Reason_Description = trimmed
                            };

                            _context.Lookup_ReasonDescription.Add(newReason);
                            await _context.SaveChangesAsync();

                            viewModel.ReasonID = newReason.ReasonID;
                            System.Diagnostics.Debug.WriteLine($"New reason saved with ID: {newReason.ReasonID}");
                        }
                    }
                }

                if (!ModelState.IsValid)
                {
                    System.Diagnostics.Debug.WriteLine("ModelState is invalid:");
                    foreach (var modelState in ModelState)
                    {
                        foreach (var error in modelState.Value.Errors)
                        {
                            System.Diagnostics.Debug.WriteLine($"Key: {modelState.Key}, Error: {error.ErrorMessage}");
                        }
                    }

                    // Reload dropdown data for return view
                    await LoadDropdownData(viewModel.StatusId, viewModel.ReasonID);
                    ViewBag.CurrentUserName = currentUserName;
                    return PartialView("_CreateFromPerson", viewModel);
                }

                // Map ViewModel to Entity Model
                var request = new Request
                {
                    PersonId = viewModel.PersonId,
                    StatusId = viewModel.StatusId.Value, // Should be validated as required
                    ReasonID = viewModel.ReasonID.Value, // Should be validated as required
                    Comments = viewModel.Comments ?? "",
                    FollowUpCount = 0, // Initialize to 0
                    LastFollowUpDate = null, // No follow-up initially
                    CreatedByCode = currentUserId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = null,
                    UpdatedByCode = null
                };

                _context.Requests.Add(request);
                await _context.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"Request saved successfully with ID: {request.RequestId}");
                return Json(new { success = true, personId = viewModel.PersonId, requestId = request.RequestId });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in POST CreateFromPerson: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                ModelState.AddModelError("", "An error occurred while saving the request: " + ex.Message);

                // Reload dropdown data for error return
                await LoadDropdownData(viewModel.StatusId, viewModel.ReasonID);
                ViewBag.CurrentUserName = currentUserName;
                return PartialView("_CreateFromPerson", viewModel);
            }
        }

        // Helper method to load dropdown data
        private async Task LoadDropdownData(int? selectedStatusId = null, int? selectedReasonId = null)
        {
            try
            {
                // Load status dropdown
                ViewBag.StatusId = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName", selectedStatusId);

                var reasonsList = await _context.Lookup_ReasonDescription
                    .OrderBy(r => r.Reason_Description)
                    .Select(r => new SelectListItem
                    {
                        Value = r.ReasonID.ToString(),
                        Text = r.Reason_Description
                    })
                    .ToListAsync();

                // Add "Other" option
                reasonsList.Add(new SelectListItem
                {
                    Value = "-1",
                    Text = "Other (Please specify)"
                });

                ViewBag.ReasonID = new SelectList(reasonsList, "Value", "Text", selectedReasonId?.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading dropdown data: {ex.Message}");

                // Fallback empty dropdowns
                ViewBag.StatusId = new SelectList(new List<object>(), "StatusId", "StatusName");
                ViewBag.ReasonID = new SelectList(new List<object>(), "ReasonID", "Reason_Description");
            }
        }
        // Optional: Method to check if reason exists (for AJAX calls)
        [HttpPost]
        public async Task<IActionResult> CheckReasonExists([FromBody] CheckReasonRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ReasonText))
                {
                    return Json(new { exists = false });
                }

                var existingReason = await _context.Lookup_ReasonDescription
                    .FirstOrDefaultAsync(r => r.Reason_Description.ToLower().Contains(request.ReasonText.ToLower().Trim())
                        || request.ReasonText.ToLower().Trim().Contains(r.Reason_Description.ToLower()));

                if (existingReason != null)
                {
                    return Json(new
                    {
                        exists = true,
                        existingReason = existingReason.Reason_Description,
                        reasonId = existingReason.ReasonID
                    });
                }

                return Json(new { exists = false });
            }
            catch (Exception)
            {
                return Json(new { exists = false });
            }
        }

        // DTO for the check reason request
        public class CheckReasonRequest
        {
            public string ReasonText { get; set; } = string.Empty;
        }

        //[HttpGet]
        //public async Task<IActionResult> CreateFromPerson(int personId)
        //{
        //    var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        //    var user =  await _context.Users.FindAsync(currentUserId);
        //    var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

        //    try
        //    {
        //        // Debug log
        //        System.Diagnostics.Debug.WriteLine($"GET CreateFromPerson called with personId: {personId}");

        //        var model = new Request
        //        {
        //            PersonId = personId,
        //            LastFollowUpDate = null,
        //            //Description = "",
        //            ReasonID = null,
        //            Comments = "",
        //            StatusId = 0,
        //            CreatedByCode = currentUserId,
        //            FollowUpCount = 0
        //        };

        //        ViewBag.StatusId = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName");

        //        System.Diagnostics.Debug.WriteLine("Returning partial view");
        //        return PartialView("_CreateFromPerson", model);
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"Error in GET CreateFromPerson: {ex.Message}");
        //        return PartialView("_CreateFromPerson", new Request { PersonId = personId });
        //    }
        //}


        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> CreateFromPerson(Request model)
        //{
        //    var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        //    var user = await _context.Users.FindAsync(currentUserId);
        //    var currentUserName = user?.FullName ?? user?.Username ?? "Current User";
        //    try
        //    {
        //        System.Diagnostics.Debug.WriteLine($"POST CreateFromPerson called with PersonId: {model.PersonId}");

        //        // Remove navigation properties from validation
        //        ModelState.Remove("Person");
        //        ModelState.Remove("CreatedByCodeNavigation");
        //        ModelState.Remove("UpdatedByCodeNavigation");
        //        ModelState.Remove("Status");

        //        if (!ModelState.IsValid)
        //        {
        //            System.Diagnostics.Debug.WriteLine("ModelState is invalid:");
        //            foreach (var modelState in ModelState)
        //            {
        //                foreach (var error in modelState.Value.Errors)
        //                {
        //                    System.Diagnostics.Debug.WriteLine($"Key: {modelState.Key}, Error: {error.ErrorMessage}");
        //                }
        //            }

        //            ViewBag.StatusId = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName", model.StatusId);
        //            return PartialView("_CreateFromPerson", model);
        //        }

        //        var request = new Request
        //        {
        //            PersonId = model.PersonId,
        //            StatusId = model.StatusId,
        //            //Description = model.Description ?? "",
        //            ReasonID = model.ReasonID,
        //            Comments = model.Comments ?? "",
        //            FollowUpCount = model.FollowUpCount,
        //            LastFollowUpDate = model.LastFollowUpDate,
        //            CreatedByCode = currentUserId, // Replace with actual user ID
        //            CreatedAt = DateTime.Now,
        //            UpdatedAt = null,
        //            UpdatedByCode = currentUserId,
        //        };

        //        _context.Requests.Add(request);
        //        _context.SaveChanges();

        //        System.Diagnostics.Debug.WriteLine("Request saved successfully");
        //        return Json(new { success = true, personId = model.PersonId });
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"Error in POST CreateFromPerson: {ex.Message}");
        //        ModelState.AddModelError("", "An error occurred while saving the request: " + ex.Message);
        //        ViewBag.StatusId = new SelectList(_context.LookUpStatusTypes, "StatusId", "StatusName", model.StatusId);
        //        return PartialView("_CreateFromPerson", model);
        //    }
        //}

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
