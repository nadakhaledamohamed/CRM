using System.Security.Claims;
using CRM.Extensions;
using CRM.FuncModels;
using CRM.Helpers;
using CRM.Models;
using CRM.Pagination;
using CRM.Services;
using CRM.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRM.Controllers
{
    public class FollowUpController : BaseController
    {
        private readonly CallCenterContext _context;
        private readonly FollowUpHelper _helper;
       
        public IPaginationService _PaginationService { get; }
        public IFollowUpService _service { get; }

        private readonly ISortingService _sortingService;
        public FollowUpController(CallCenterContext context, IFollowUpService service
            , FollowUpHelper helper, IPaginationService paginationService,
        ISortingService sortingService) : base(context)
        {
            _context = context;
            _service = service;
            _helper = helper;
            _PaginationService = paginationService;
            _sortingService = sortingService;
        }



        // =================== GET: Fields for follow up log modal ===================



        // Updated FollowUpDetails method in your controller
        // =================== GET: Fields for follow up log modal ===================

        [HttpGet]
        public async Task<IActionResult> FollowUpDetails(int Id)
        {
            if (Id == 0) return NotFound();

            var request = await _context.Requests
                .Include(r => r.Person)
                .Include(r => r.Status)
                .ThenInclude(s => s.FollowUpSettings) // Include status-specific settings
                .Include(r => r.Reason)
                .FirstOrDefaultAsync(r => r.RequestId == Id);

            if (request == null)
            {
                return NotFound("Request not found.");
            }
            // Add this to your Details action
            var canFollowUp = await _helper.CanFollowUpAsync(
                request.StatusId.Value,
                request.FollowUpCount.Value,
                request.Status?.StatusName
            );

            ViewBag.CanFollowUp = canFollowUp;
            // Get status-specific settings instead of global settings
            var statusSettings = request.Status?.FollowUpSettings;
            ViewBag.MaxFollowUps = statusSettings?.MaxFollowUps ?? 0;
            ViewBag.FollowUpInterval = statusSettings?.FollowUpIntervalDays ?? 0;
            ViewBag.AutoCloseDays = statusSettings?.AutoCloseDays ?? 0;
            ViewBag.RequiresFollowUp = request.Status?.RequireFollowUp ?? false;
            // In your Details action or wherever this view is rendered
            var followUpSettings = await _helper.GetFollowUpSettingsForStatusAsync(request.StatusId.Value);
            ViewBag.FollowUpSettings = followUpSettings;

            // Also get the follow-up status text and CSS class
            var followUpStatusText = await _helper.GetFollowUpStatusTextAsync(
                request.StatusId.Value,
                request.LastFollowUpDate,
                request.CreatedAt,
                request.FollowUpCount.Value
            );
            var followUpCssClass = await _helper.GetFollowUpCssClassAsync(
                request.StatusId.Value,
                request.LastFollowUpDate,
                request.CreatedAt,
                request.FollowUpCount.Value
            );

            ViewBag.FollowUpStatusText = followUpStatusText;
            ViewBag.FollowUpCssClass = followUpCssClass;
            if (request.Status?.RequireFollowUp == true && statusSettings == null)
            {
                ViewBag.WarningMessage = $"Status '{request.Status.StatusName}' requires follow-up but no settings are configured.";
            }

            var person = request.Person;

            // Get user's full name and metadata
            var createdBy = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.CreatedByCode);
            var updatedBy = request.UpdatedByCode.HasValue
                ? await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UpdatedByCode.Value)
                : null;

            // Get major interests
            var majorInterests = await _context.MajorPersons
                .Where(mp => mp.PersonID == person.PersonId)
                .OrderBy(mp => mp.Academic_Setting_ID)
                .Join(_context.LookupMajors,
                      mp => mp.MajorID,
                      m => m.MajorId,
                      (mp, m) => m.MajorInterest)
                .ToListAsync();

            // Get follow-up history
            var followUpHistory = await _context.FollowUp_Log
                .Where(f => f.RequestId == Id)
                .Include(f => f.Status)
                .Include(f => f.FollowUpType)
                .Include(f => f.UpdatedByCodeNavigation)
                .OrderByDescending(f => f.UpdatedAt)
                .Select(f => new FollowUpHistoryViewModel
                {
                    FollowUpId = f.FollowUp_ID,
                    StatusName = f.Status.StatusName,
                    FollowUpTypeName = f.FollowUpType.FollowUpName,
                    ChangeReason = f.ChangeReason,
                    Comment = f.Comment,
                    IsCurrentStatus = f.IsCurrentStatus,
                    UpdatedAt = f.UpdatedAt,
                    UpdatedByName = f.UpdatedByCodeNavigation != null ? f.UpdatedByCodeNavigation.FullName : "Unknown"
                })
                .ToListAsync();

            // Use status-specific helper methods if status requires follow-up
            bool isOverdue = false;
            DateTime? nextFollowUpDate = null;
            string statusText = "Follow-up not required";
            string cssClass = "text-muted";

            if (request.Status?.RequireFollowUp == true && request.Status.StatusId != null)
            {
                // Use the new async methods for status-specific calculations
                var followUpSummary = await _helper.GetFollowUpSummaryAsync(
                    request.RequestId,
                    request.Status.StatusId,
                    request.LastFollowUpDate,
                    request.CreatedAt,
                    request.FollowUpCount ?? 0);

                var summary = (dynamic)followUpSummary;
                isOverdue = summary.IsOverdue;
                nextFollowUpDate = summary.NextFollowUpDate;
                statusText = summary.StatusText;
                cssClass = summary.CssClass;

                // Additional ViewBag data for the view
                ViewBag.MaxFollowUpsReached = summary.MaxFollowUpsReached;
                ViewBag.DaysUntilAutoClose = summary.DaysUntilAutoClose;
            }
            else
            {
                // Fallback to legacy helper methods for backward compatibility
                isOverdue = _helper.IsOverdue(request.LastFollowUpDate, request.CreatedAt);
                nextFollowUpDate = _helper.GetNextFollowUpDate(request.LastFollowUpDate, request.CreatedAt, request.FollowUpCount ?? 0);
                statusText = _helper.GetFollowUpStatusText(request.LastFollowUpDate, request.CreatedAt, request.FollowUpCount ?? 0);
                cssClass = _helper.GetFollowUpCssClass(isOverdue, nextFollowUpDate, request.FollowUpCount ?? 0);
            }

            // Build the view model
            var model = new FollowUpDetailsViewModel
            {
                RequestID = request.RequestId,
                PersonID = person.PersonId,
                FullName = $"{person.FirstName}",
                Email = person.Email,
                Phone = person.Phone,
                NationalId = person.NationalId,
                CertificateName = person.Certificate?.CertificateName,
                HighSchoolName = person.HighSchool?.HighSchoolName,
                HowDidYouKnowUs = person.HowDidYouKnowUs?.HowDidYouKnowUs,
                UserType = person.UserType,
                MajorInterests = majorInterests,
                RequestStatus = request.Status?.StatusName,
                StatusId = request.Status?.StatusId,
                ReasonDescription = request.Reason?.Reason_Description,
                Comments = request.Comments,
                CreatedAt = request.CreatedAt,
                UpdatedAt = request.UpdatedAt,
                CreatedByName = createdBy?.FullName ?? "Unknown",
                UpdatedByName = updatedBy?.FullName ?? "N/A",
                FollowUpCount = request.FollowUpCount ?? 0,
                LastFollowUpDate = request.LastFollowUpDate,
                IsOverdue = isOverdue,
                NextFollowUpDate = nextFollowUpDate,
                FollowUpHistory = followUpHistory
            };

            return View("FollowUpDetails", model);
        }


        [HttpGet]
        public async Task<IActionResult> CreateFollowUpForm(int requestId)
        {
            var request = await _context.Requests
                .Include(r => r.Status)
                .Include(r => r.Person) // Include Person to get current National ID
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request == null)
                return NotFound();

            // Check if this is the first follow-up
            var followUpCount = request.FollowUpCount ?? 0;
            var isFirstFollowUp = followUpCount == 0;

            ViewData["StatusList"] = new SelectList(await _context.LookUpStatusTypes.ToListAsync(), "StatusId", "StatusName", request.StatusId ?? 0);
            ViewData["FollowUpType_ID"] = new SelectList(await _context.Lookup_FollowUpType.ToListAsync(), "FollowUpType_ID", "FollowUpName");

            var model = new FollowUpLogViewModel
            {
                RequestID = request.RequestId,
                StatusID = request.StatusId ?? 0,
                UpdatedAt = request.UpdatedAt ?? DateTime.Now,
                NationalId = request.Person?.NationalId ?? "", // Pre-populate with existing National ID
                IsFirstFollowUp = isFirstFollowUp // Flag to indicate if National ID is required
            };

            return PartialView("_CreateFollowUp", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFollowUpForm(FollowUpLogViewModel model)
        {
            // Get the associated request with person info
            var request = await _context.Requests
                .Include(r => r.Person)
                .FirstOrDefaultAsync(r => r.RequestId == model.RequestID);

            if (request == null)
            {
                return NotFound();
            }

            // Check if this is the first follow-up
            var followUpCount = request.FollowUpCount ?? 0;
            var isFirstFollowUp = followUpCount == 0;

            // Custom validation for National ID on first follow-up
            //if (isFirstFollowUp && string.IsNullOrWhiteSpace(model.NationalId))
            //{
            //    ModelState.AddModelError("NationalId", "National ID is required for the first follow-up.");
            //}

            if (!ModelState.IsValid)
            {
                model.IsFirstFollowUp = isFirstFollowUp; // Set flag for view
                ViewData["StatusList"] = new SelectList(await _context.LookUpStatusTypes.ToListAsync(), "StatusId", "StatusName", model.StatusID);
                ViewData["FollowUpType_ID"] = new SelectList(await _context.Lookup_FollowUpType.ToListAsync(), "FollowUpType_ID", "FollowUpName", model.FollowUpType_ID);
                return PartialView("_CreateFollowUp", model);
            }

            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Update Person's National ID if provided and different
            if (!string.IsNullOrWhiteSpace(model.NationalId) &&
                request.Person?.NationalId != model.NationalId.Trim())
            {
                if (request.Person != null)
                {
                    request.Person.NationalId = model.NationalId.Trim();
                    request.Person.UpdatedAt = DateTime.Now;
                    request.Person.UpdatedByCode = currentUserId;
                }
            }

            // Mark all previous follow-ups as not current
            var previousFollowUps = await _context.FollowUp_Log
                .Where(f => f.RequestId == model.RequestID && f.IsCurrentStatus)
                .ToListAsync();

            foreach (var prevFollowUp in previousFollowUps)
            {
                prevFollowUp.IsCurrentStatus = false;
            }

            // Update request status and follow-up tracking
            request.StatusId = model.StatusID;
            request.LastFollowUpDate = DateTime.Now;
            request.FollowUpCount = (request.FollowUpCount ?? 0) + 1;
            request.UpdatedAt = DateTime.Now;
            request.UpdatedByCode = currentUserId;

            // Create new follow-up log entry
            var followUp = new FollowUp_Log
            {
                RequestId = model.RequestID,
                StatusId = model.StatusID,
                ChangeReason = model.ChangeReason,
                IsCurrentStatus = true,
                Comment = model.Comment,
                UpdatedAt = DateTime.Now,
                UpdatedByCode = currentUserId,
                FollowUpType_ID = model.FollowUpType_ID

            };

            _context.FollowUp_Log.Add(followUp);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Follow-up added successfully!" });
        }



        [HttpGet]
        public async Task<IActionResult> FollowUpRequired(
            string? statusFilter = null,
            string? priorityFilter = null,
            string? searchTerm = null,
            string? followUpTypeFilter = null,  // ADD THIS PARAMETER
            DateTime? overdueFrom = null,
            DateTime? overdueTo = null,
            int page = 1,
            int pageSize = 10,
            string sortBy = "Priority",
            string sortOrder = "desc")
        {
            // Clean up filters
            statusFilter = string.IsNullOrWhiteSpace(statusFilter) ? null : statusFilter;
            priorityFilter = string.IsNullOrWhiteSpace(priorityFilter) ? null : priorityFilter;
            searchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;
            followUpTypeFilter = string.IsNullOrWhiteSpace(followUpTypeFilter) ? null : followUpTypeFilter;

            try
            {
                // Get requests that require follow-up using direct query
                var requestQuery = _context.Requests
                    .Include(r => r.Person)
                    .Include(r => r.Status)
                        .ThenInclude(s => s.FollowUpSettings)
                    .Include(r => r.Reason)
                    .Where(r => r.Status.RequireFollowUp == true || r.Status.FollowUp_SettingID.HasValue)
                    .AsQueryable();

                var allRequests = await requestQuery.ToListAsync();

                // Filter to show only requests that actually need follow-up
                var filteredNotificationsList = new List<FollowUpNotificationViewModel>();

                foreach (var request in allRequests)
                {
                    var settings = request.Status?.FollowUpSettings;
                    var requiresFollowUp = request.Status?.RequireFollowUp ?? false;
                    var followUpCount = request.FollowUpCount ?? 0;

                    if (!requiresFollowUp || settings == null) continue;

                    // Smart filtering logic
                    bool shouldInclude = false;

                    if (settings.FollowUpIntervalDays <= 0) // Manual/Problem Solving
                    {
                        // Always show if not maxed out
                        shouldInclude = followUpCount < settings.MaxFollowUps;
                    }
                    else // Scheduled follow-up
                    {
                        // Only show when actually due
                        var lastDate = request.LastFollowUpDate ?? request.CreatedAt;
                        var daysSince = (DateTime.Now - lastDate).Days;
                        shouldInclude = daysSince >= settings.FollowUpIntervalDays && followUpCount < settings.MaxFollowUps;
                    }

                    if (shouldInclude)
                    {
                        // Apply follow-up type filter if specified
                        if (!string.IsNullOrEmpty(followUpTypeFilter))
                        {
                            var matchesFilter = followUpTypeFilter switch
                            {
                                "scheduled" => settings.FollowUpIntervalDays > 0,
                                "problem_solving" => settings.FollowUpIntervalDays <= 0,
                                "overdue" => settings.FollowUpIntervalDays > 0 &&
                                           (DateTime.Now - (request.LastFollowUpDate ?? request.CreatedAt)).Days >= settings.FollowUpIntervalDays,
                                _ => true
                            };

                            if (!matchesFilter)
                                shouldInclude = false;
                        }
                    }

                    if (shouldInclude)
                    {
                        var notification = new FollowUpNotificationViewModel
                        {
                            RequestID = request.RequestId,
                            FullName = request.Person?.FirstName ?? "N/A",
                            Email = request.Person?.Email,
                            Phone = request.Person?.Phone,
                            StatusId = request.StatusId ?? 0,
                            StatusName = request.Status?.StatusName ?? "N/A",
                            FollowUpCount = followUpCount,
                            LastFollowUpDate = request.LastFollowUpDate,
                            CreatedAt = request.CreatedAt,
                            //Priority = GetRequestFollowUpPriority(request),
                            //UrgencyCssClass = GetRequestUrgencyCssClass(request)
                        };

                        filteredNotificationsList.Add(notification);
                    }
                }

                // Convert to queryable for further filtering
                var notificationsQuery = filteredNotificationsList.AsQueryable();

                // Apply additional filters
                notificationsQuery = ApplyNotificationFilters(notificationsQuery, statusFilter, priorityFilter, searchTerm, overdueFrom, overdueTo);

                // Apply sorting
                notificationsQuery = ApplyNotificationSorting(notificationsQuery, sortBy, sortOrder);

                // Convert back to list for pagination
                var filteredList = notificationsQuery.ToList();

                // Apply pagination
                var totalCount = filteredList.Count;
                var paginatedItems = filteredList
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Create pagination info
                var paginationInfo = new PaginationInfo
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    StartItem = totalCount == 0 ? 0 : ((page - 1) * pageSize) + 1,
                    EndItem = Math.Min(page * pageSize, totalCount)
                };

                // Create follow-up data for each item
                var followUpData = new Dictionary<int, object>();
                foreach (var item in paginatedItems)
                {
                    try
                    {
                        var request = allRequests.First(r => r.RequestId == item.RequestID);
                        var followUpInfo = CreateRequestFollowUpData(request);
                        followUpData[item.RequestID] = followUpInfo;
                    }
                    catch (Exception)
                    {
                        followUpData[item.RequestID] = new
                        {
                            IsOverdue = false,
                            StatusText = "Error loading status",
                            CssClass = "neutral",
                            NextFollowUpDate = (DateTime?)null,
                            IsManualFollowUp = false,
                            FollowUpType = "Unknown"
                        };
                    }
                }

                // Get statuses that require follow-up for filter dropdown
                ViewBag.AllStatuses = await _context.LookUpStatusTypes
                    .Where(s => s.RequireFollowUp == true || s.FollowUp_SettingID.HasValue)
                    .OrderBy(s => s.StatusName)
                    .Select(s => s.StatusName)
                    .ToListAsync();

                ViewBag.AllPriorities = new List<string> { "Urgent", "High", "Medium", "Low" };

                // Set ViewBag data
                ViewBag.Pagination = paginationInfo;
                ViewBag.FollowUpData = followUpData;
                ViewBag.CurrentUserName = User.GetFullName();
                ViewBag.TotalUserRequests = totalCount;
                ViewBag.CurrentFilters = new
                {
                    StatusFilter = statusFilter,
                    PriorityFilter = priorityFilter,
                    SearchTerm = searchTerm,
                    FollowUpTypeFilter = followUpTypeFilter,  // ADD THIS LINE
                    OverdueFrom = overdueFrom,
                    OverdueTo = overdueTo,
                    SortBy = sortBy,
                    SortOrder = sortOrder
                };

                // Handle no results message
                if (!paginatedItems.Any() && User.Identity?.IsAuthenticated == true)
                {
                    var hasActiveFilters = HasActiveFilters(statusFilter, priorityFilter, searchTerm, followUpTypeFilter, overdueFrom, overdueTo);
                    var totalWithoutFilters = filteredNotificationsList.Count;

                    if (hasActiveFilters && totalWithoutFilters > 0)
                    {
                        ViewBag.NoDataMessage = "No follow-up requests found matching your criteria.";
                        ViewBag.ShowFilterMessage = true;
                    }
                    else
                    {
                        ViewBag.NoDataMessage = "No requests need follow-up at this time.";
                        ViewBag.ShowFilterMessage = false;
                    }
                }
                else
                {
                    ViewBag.ShowFilterMessage = false;
                }

                return View("FollowUpRequired", paginatedItems);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error loading follow-up requests: {ex.Message}");
                ViewBag.ErrorMessage = "Unable to load follow-up requests. Please try again later.";
                return View("FollowUpRequired", new List<FollowUpNotificationViewModel>());
            }
        }

        // Helper method for priority calculation
        private int GetRequestFollowUpPriority(Request request)
        {
            var settings = request.Status?.FollowUpSettings;
            if (settings == null) return 1;

            var followUpCount = request.FollowUpCount ?? 0;
            var intervalDays = settings.FollowUpIntervalDays;

            if (intervalDays > 0) // Scheduled follow-up
            {
                var lastDate = request.LastFollowUpDate ?? request.CreatedAt;
                var daysSince = (DateTime.Now - lastDate).Days;
                var daysOverdue = daysSince - intervalDays;

                if (daysOverdue > 5) return 4; // Urgent
                if (daysOverdue > 2) return 3; // High  
                if (daysOverdue >= 0) return 2; // Medium
                return 1; // Low
            }
            else // Manual/Problem Solving
            {
                var lastDate = request.LastFollowUpDate ?? request.CreatedAt;
                var daysSince = (DateTime.Now - lastDate).Days;

                if (daysSince > 7) return 3; // High
                if (daysSince > 3) return 2; // Medium
                return 1; // Low
            }
        }

        private string GetRequestUrgencyCssClass(Request request)
        {
            var priority = GetRequestFollowUpPriority(request);
            return priority switch
            {
                4 => "urgent",
                3 => "high",
                2 => "medium",
                1 => "low",
                _ => "neutral"
            };
        }

        // Helper method for applying notification filters
        private IQueryable<FollowUpNotificationViewModel> ApplyNotificationFilters(
            IQueryable<FollowUpNotificationViewModel> query,
            string? statusFilter,
            string? priorityFilter,
            string? searchTerm,
            DateTime? overdueFrom,
            DateTime? overdueTo)
        {
            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(x => x.StatusName == statusFilter);

            if (!string.IsNullOrEmpty(priorityFilter))
            {
                query = priorityFilter.ToLower() switch
                {
                    "urgent" => query.Where(x => x.Priority >= 4),
                    "high" => query.Where(x => x.Priority == 3),
                    "medium" => query.Where(x => x.Priority == 2),
                    "low" => query.Where(x => x.Priority == 1),
                    _ => query
                };
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(x =>
                    x.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (x.Email != null && x.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Phone != null && x.Phone.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
            }

            if (overdueFrom.HasValue)
                query = query.Where(x => x.LastFollowUpDate >= overdueFrom || x.CreatedAt >= overdueFrom);

            if (overdueTo.HasValue)
                query = query.Where(x => x.LastFollowUpDate <= overdueTo || x.CreatedAt <= overdueTo);

            return query;
        }

        // Helper method for applying notification sorting
        private IQueryable<FollowUpNotificationViewModel> ApplyNotificationSorting(
            IQueryable<FollowUpNotificationViewModel> query,
            string sortBy,
            string sortOrder)
        {
            return sortBy.ToLower() switch
            {
                "priority" => sortOrder == "asc"
                    ? query.OrderBy(x => x.Priority)
                    : query.OrderByDescending(x => x.Priority),
                "name" => sortOrder == "asc"
                    ? query.OrderBy(x => x.FullName)
                    : query.OrderByDescending(x => x.FullName),
                "lastfollowup" => sortOrder == "asc"
                    ? query.OrderBy(x => x.LastFollowUpDate ?? DateTime.MinValue)
                    : query.OrderByDescending(x => x.LastFollowUpDate ?? DateTime.MinValue),
                "followups" => sortOrder == "asc"
                    ? query.OrderBy(x => x.FollowUpCount)
                    : query.OrderByDescending(x => x.FollowUpCount),
                _ => query.OrderByDescending(x => x.Priority).ThenByDescending(x => x.CreatedAt)
            };
        }

        private bool HasActiveFilters(string? statusFilter, string? priorityFilter, string? followUpTypeFilter,string? searchTerm, DateTime? overdueFrom, DateTime? overdueTo)
        {
            return !string.IsNullOrEmpty(statusFilter) ||
                   !string.IsNullOrEmpty(priorityFilter) ||
                   !string.IsNullOrEmpty(searchTerm) ||  !string.IsNullOrEmpty(followUpTypeFilter) ||
                   overdueFrom.HasValue ||
                   overdueTo.HasValue;
        }

        // Reuse the same CreateRequestFollowUpData method from GetAllRequests
        private object CreateRequestFollowUpData(Request request)
        {
            var settings = request.Status?.FollowUpSettings;
            var requiresFollowUp = request.Status?.RequireFollowUp ?? false;
            var followUpCount = request.FollowUpCount ?? 0;

            if (settings == null || !requiresFollowUp)
            {
                return new
                {
                    RequiresFollowUp = false,
                    IsOverdue = false,
                    MaxFollowUpsReached = false,
                    StatusText = "No follow-up required",
                    Settings = (object?)null,
                    IsManualFollowUp = false,
                    FollowUpType = "None",
                    CanFollowUp = false
                };
            }

            var maxReached = followUpCount >= settings.MaxFollowUps;
            var isManualFollowUp = settings.FollowUpIntervalDays <= 0;
            var isOverdue = false;
            var statusText = "Available";

            if (!isManualFollowUp && settings.FollowUpIntervalDays > 0)
            {
                var lastDate = request.LastFollowUpDate ?? request.CreatedAt;
                var daysSince = (DateTime.Now - lastDate).Days;
                isOverdue = daysSince >= settings.FollowUpIntervalDays;

                if (maxReached)
                    statusText = "Max reached";
                else if (isOverdue)
                    statusText = $"Overdue ({daysSince - settings.FollowUpIntervalDays + 1} days)";
                else
                    statusText = $"Due in {settings.FollowUpIntervalDays - daysSince} days";
            }
            else if (isManualFollowUp)
            {
                statusText = maxReached ? "Max reached" : "Problem Solving Available";
            }

            return new
            {
                RequiresFollowUp = requiresFollowUp,
                IsOverdue = isOverdue,
                MaxFollowUpsReached = maxReached,
                StatusText = statusText,
                Settings = settings,
                IsManualFollowUp = isManualFollowUp,
                FollowUpType = isManualFollowUp ? "Problem Solving" : "Scheduled",
                CanFollowUp = requiresFollowUp && !maxReached
            };
        }
        //    [HttpGet]
        //    public async Task<IActionResult> FollowUpRequired(
        //string? statusFilter = null,
        //string? priorityFilter = null,
        //string? searchTerm = null,
        //DateTime? overdueFrom = null,
        //DateTime? overdueTo = null,
        //int page = 1,
        //int pageSize = 10,
        //string sortBy = "Priority",
        //string sortOrder = "desc")
        //    {
        //        // Clean up empty string parameters to null
        //        statusFilter = string.IsNullOrWhiteSpace(statusFilter) ? null : statusFilter;
        //        priorityFilter = string.IsNullOrWhiteSpace(priorityFilter) ? null : priorityFilter;
        //        searchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;

        //        var settings = await _context.FollowUpSetting.FirstOrDefaultAsync();
        //        if (settings == null)
        //        {
        //            return Content("Follow-up settings are not configured in the database.");
        //        }

        //        try
        //        {
        //            // Get all follow-up notifications
        //            var allNotifications = await _service.GetFollowUpNotificationsAsync();

        //            // Convert to queryable for filtering
        //            var query = allNotifications.AsQueryable();

        //            // Apply filters
        //            query = ApplyFollowUpFilters(query, statusFilter, priorityFilter, searchTerm, overdueFrom, overdueTo);

        //            // Apply sorting
        //            query = ApplyFollowUpSorting(query, sortBy, sortOrder);

        //            // Convert back to list for pagination service
        //            var filteredList = query.ToList();

        //            // Create pagination request
        //            var paginationRequest = new PaginationModels
        //            {
        //                Page = page,
        //                PageSize = pageSize,
        //                SortBy = sortBy,
        //                SortOrder = sortOrder
        //            };

        //            // Apply pagination manually since we're working with in-memory data
        //            var totalCount = filteredList.Count;
        //            var paginatedItems = filteredList
        //                .Skip((page - 1) * pageSize)
        //                .Take(pageSize)
        //                .ToList();

        //            // Create pagination info
        //            var paginationInfo = _PaginationService.CalculatePaginationInfo(totalCount, page, pageSize);

        //            // Prepare follow-up data for each item
        //            var followUpData = new Dictionary<int, object>();
        //            foreach (var item in paginatedItems)
        //            {
        //                var isOverdue = _helper.IsOverdue(item.LastFollowUpDate, item.CreatedAt ?? DateTime.Now);
        //                var statusText = _helper.GetFollowUpStatusText(item.LastFollowUpDate, item.CreatedAt ?? DateTime.Now, item.FollowUpCount);
        //                var nextFollowUpDate = _helper.GetNextFollowUpDate(item.LastFollowUpDate, item.CreatedAt ?? DateTime.Now, item.FollowUpCount);
        //                var cssClass = _helper.GetFollowUpCssClass(isOverdue, nextFollowUpDate, item.FollowUpCount);

        //                followUpData[item.RequestID] = new
        //                {
        //                    IsOverdue = isOverdue,
        //                    StatusText = statusText,
        //                    CssClass = cssClass,
        //                    NextFollowUpDate = nextFollowUpDate
        //                };
        //            }

        //            // Prepare unique filter options - Get all statuses from database
        //            ViewBag.AllStatuses = await _context.LookUpStatusTypes
        //                .OrderBy(s => s.StatusName)
        //                .Select(s => s.StatusName)
        //                .ToListAsync();

        //            ViewBag.AllPriorities = new List<string> { "Urgent", "High", "Medium", "Low" };

        //            // Pass data to view
        //            ViewBag.Pagination = paginationInfo;
        //            ViewBag.FollowUpData = followUpData;
        //            ViewBag.IntervalDays = settings.FollowUpIntervalDays;
        //            ViewBag.CurrentUserName = User.GetFullName();
        //            ViewBag.TotalUserRequests = totalCount;
        //            ViewBag.CurrentFilters = new
        //            {
        //                StatusFilter = statusFilter,
        //                PriorityFilter = priorityFilter,
        //                SearchTerm = searchTerm,
        //                OverdueFrom = overdueFrom,
        //                OverdueTo = overdueTo,
        //                SortBy = sortBy,
        //                SortOrder = sortOrder
        //            };

        //            // Check if no results and user is authenticated
        //            if (!paginatedItems.Any() && User.Identity.IsAuthenticated)
        //            {
        //                var hasActiveFilters = HasActiveFilters(statusFilter, priorityFilter, searchTerm, overdueFrom, overdueTo);
        //                var totalWithoutFilters = allNotifications.Count;

        //                if (hasActiveFilters && totalWithoutFilters > 0)
        //                {
        //                    // There is data, but filters are excluding everything
        //                    ViewBag.NoDataMessage = "No follow-up requests found matching your criteria.";
        //                    ViewBag.ShowFilterMessage = true;
        //                }
        //                else
        //                {
        //                    // Genuinely no follow-up data
        //                    ViewBag.NoDataMessage = "No requests need follow-up at this time.";
        //                    ViewBag.ShowFilterMessage = false;
        //                }
        //            }
        //            else
        //            {
        //                ViewBag.ShowFilterMessage = false;
        //            }

        //            return View("FollowUpRequired", paginatedItems);
        //        }
        //        catch (Exception ex)
        //        {
        //            // Log the error and show a user-friendly message
        //            Console.WriteLine($"❌ Error loading follow-up requests: {ex.Message}");
        //            ViewBag.ErrorMessage = "Unable to load follow-up requests. Please try again later.";
        //            return View("FollowUpRequired", new List<FollowUpNotificationViewModel>());
        //        }
        //    }
        // Helper method to apply filters
        private IQueryable<FollowUpNotificationViewModel> ApplyFollowUpFilters(
            IQueryable<FollowUpNotificationViewModel> query,
            string? statusFilter,
            string? priorityFilter,
            string? searchTerm,
            DateTime? overdueFrom,
            DateTime? overdueTo)
        {
            // Status filter
            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(x => x.StatusName == statusFilter);

            // Priority filter
            if (!string.IsNullOrEmpty(priorityFilter))
            {
                query = priorityFilter.ToLower() switch
                {
                    "urgent" => query.Where(x => x.Priority >= 4),
                    "high" => query.Where(x => x.Priority == 3),
                    "medium" => query.Where(x => x.Priority == 2),
                    "low" => query.Where(x => x.Priority == 1),
                    _ => query
                };
            }

            // Search term (searches in name, email, phone)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(x =>
                    x.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (x.Email != null && x.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Phone != null && x.Phone.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
            }

            // Overdue date filters
            if (overdueFrom.HasValue)
                query = query.Where(x => x.LastFollowUpDate >= overdueFrom || x.CreatedAt >= overdueFrom);

            if (overdueTo.HasValue)
                query = query.Where(x => x.LastFollowUpDate <= overdueTo || x.CreatedAt <= overdueTo);

            return query;
        }

        // Helper method to apply sorting
        private IQueryable<FollowUpNotificationViewModel> ApplyFollowUpSorting(
            IQueryable<FollowUpNotificationViewModel> query,
            string sortBy,
            string sortOrder)
        {
            var isAscending = sortOrder?.ToLower() == "asc";

            return sortBy?.ToLower() switch
            {
                "name" => isAscending
                    ? query.OrderBy(x => x.FullName)
                    : query.OrderByDescending(x => x.FullName),
                "status" => isAscending
                    ? query.OrderBy(x => x.StatusName)
                    : query.OrderByDescending(x => x.StatusName),
                "email" => isAscending
                    ? query.OrderBy(x => x.Email)
                    : query.OrderByDescending(x => x.Email),
                "followups" => isAscending
                    ? query.OrderBy(x => x.FollowUpCount)
                    : query.OrderByDescending(x => x.FollowUpCount),
                "lastfollowup" => isAscending
                    ? query.OrderBy(x => x.LastFollowUpDate ?? DateTime.MinValue)
                    : query.OrderByDescending(x => x.LastFollowUpDate ?? DateTime.MinValue),
                "createdat" => isAscending
                    ? query.OrderBy(x => x.CreatedAt)
                    : query.OrderByDescending(x => x.CreatedAt),
                _ => isAscending
                    ? query.OrderBy(x => x.Priority)
                    : query.OrderByDescending(x => x.Priority)
            };
        }

        // Helper method to check if any filters are active
        //private bool HasActiveFilters(string? statusFilter, string? priorityFilter, string? searchTerm,
        //    DateTime? overdueFrom, DateTime? overdueTo)
        //{
        //    return !string.IsNullOrEmpty(statusFilter) ||
        //           !string.IsNullOrEmpty(priorityFilter) ||
        //           !string.IsNullOrEmpty(searchTerm) ||
        //           overdueFrom.HasValue ||
        //           overdueTo.HasValue;
        //}

        // =================== GET: Fields for edit last follow up log modal ===================

        [HttpGet]
        public async Task<IActionResult> EditLastFollowUpForm(int requestId)
        {
            // Get the most recent follow-up for this request
            var lastFollowUp = await _context.FollowUp_Log
                .Where(f => f.RequestId == requestId)
                .Include(f => f.Status)
                .Include(f => f.FollowUpType)
                .OrderByDescending(f => f.UpdatedAt)
                .FirstOrDefaultAsync();

            if (lastFollowUp == null)
                return NotFound("No follow-up entries found for this request.");

            // Populate dropdown lists
            ViewData["StatusList"] = new SelectList(await _context.LookUpStatusTypes.ToListAsync(), "StatusId", "StatusName", lastFollowUp.StatusId);
            ViewData["FollowUpType_ID"] = new SelectList(await _context.Lookup_FollowUpType.ToListAsync(), "FollowUpType_ID", "FollowUpName", lastFollowUp.FollowUpType_ID);

            var model = new FollowUpLogViewModel
            {
                FollowUpLog_ID = lastFollowUp.FollowUp_ID,
                RequestID = lastFollowUp.RequestId,
                StatusID = lastFollowUp.StatusId,
                FollowUpType_ID = lastFollowUp.FollowUpType_ID ?? 0,
                ChangeReason = lastFollowUp.ChangeReason,
                Comment = lastFollowUp.Comment,
                UpdatedAt = lastFollowUp.UpdatedAt ?? DateTime.Now

            };

            return PartialView("_EditLastFollowUp", model);
        }

        // =================== POST: Update last follow up log entry ===================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLastFollowUpForm(FollowUpLogViewModel model)
        {
            ModelState.Remove(nameof(model.NationalId));

            if (!ModelState.IsValid)
            {
                ViewData["StatusList"] = new SelectList(await _context.LookUpStatusTypes.ToListAsync(), "StatusId", "StatusName", model.StatusID);
                ViewData["FollowUpType_ID"] = new SelectList(await _context.Lookup_FollowUpType.ToListAsync(), "FollowUpType_ID", "FollowUpName", model.FollowUpType_ID);
                return PartialView("_EditLastFollowUp", model);
            }

            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Verify this is actually the last follow-up entry for the request
            var lastFollowUp = await _context.FollowUp_Log
                .Where(f => f.RequestId == model.RequestID)
                .OrderByDescending(f => f.UpdatedAt)
                .FirstOrDefaultAsync();

            if (lastFollowUp == null || lastFollowUp.FollowUp_ID != model.FollowUpLog_ID)
            {
                return Json(new { success = false, message = "Only the most recent follow-up can be edited." });
            }

            // Update the follow-up entry
            lastFollowUp.StatusId = model.StatusID;
            lastFollowUp.FollowUpType_ID = model.FollowUpType_ID;
            lastFollowUp.ChangeReason = model.ChangeReason;
            lastFollowUp.Comment = model.Comment;
            lastFollowUp.UpdatedAt = DateTime.Now;
            lastFollowUp.UpdatedByCode = currentUserId;

            // Update the associated request since this is the most recent follow-up
            var request = await _context.Requests.FindAsync(model.RequestID);
            if (request != null)
            {
                request.StatusId = model.StatusID;
                request.UpdatedAt = DateTime.Now;
                request.UpdatedByCode = currentUserId;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Last follow-up updated successfully!" });
        }




    }
}