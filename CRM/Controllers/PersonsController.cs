using System.Linq.Expressions;
using System.Security.Claims;
using Azure.Core;
using CRM.Extensions;
using CRM.FuncModels;
using CRM.Pagination;
using CRM.Helpers;
using CRM.Models;
using CRM.Pagination;
using CRM.Services;
using CRM.ViewModel;
using DocumentFormat.OpenXml.Vml.Office;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QuestPDF.Fluent;

using Request = CRM.Models.Request;

namespace CRM.Controllers
{
    [Authorize]
    public class PersonsController : BaseController
    {
        private readonly CallCenterContext _context;

        public FollowUpAutomationService _FollowUpService;
        private readonly IBulkUploadService _bulkUploadService;
        private readonly ISortingService _sortingService;

        public FollowUpHelper _helper { get; }
        public IPaginationService _PaginationService { get; }

        public PersonsController(CallCenterContext context, FollowUpAutomationService followUpService
            ,FollowUpHelper helper, IBulkUploadService bulkUploadService ,IPaginationService paginationService,
        ISortingService sortingService) :base(context)
        {
            _context = context;
            _FollowUpService = followUpService;
            _helper = helper;
           _bulkUploadService = bulkUploadService;
            _PaginationService = paginationService;
            _sortingService = sortingService;
        }

        [HttpGet]
        public async Task<IActionResult> BulkUpload()
        {
            var model = new BulkUploadViewModel();

            // Get current user info
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);
            var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

            model.CreatedByCode = currentUserId;
            model.CreatedByName = currentUserName;

            // Get current academic setting
            var currentAcademicSetting = await _context.AcademicSettings
                .FirstOrDefaultAsync(a => a.IsActive == true);
            if (currentAcademicSetting != null)
            {
                model.MaxNumberOfInterests = currentAcademicSetting.NumberOfInterests;
                model.CurrentAcademicSettingId = currentAcademicSetting.AcademicSettingId;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpload(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select an Excel file to upload.";
                return RedirectToAction("BulkUpload");
            }

            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var fileExtension = Path.GetExtension(excelFile.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                TempData["ErrorMessage"] = "Please upload a valid Excel file (.xlsx or .xls).";
                return RedirectToAction("BulkUpload");
            }

            try
            {
                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var results = await _bulkUploadService.ProcessExcelFileAsync(excelFile, currentUserId);

                if (results.Errors.Any())
                {
                    TempData["ErrorMessage"] = $"Upload completed with {results.Errors.Count} errors. {results.SuccessCount} records processed successfully.";
                    TempData["ValidationErrors"] = results.Errors;
                }
                else
                {
                    TempData["SuccessMessage"] = $"Successfully uploaded {results.SuccessCount} records.";
                }

                return RedirectToAction("GetAll");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error processing file: {ex.Message}";
                return RedirectToAction("BulkUpload");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadTemplate()
        {
            try
            {
                var templateData = await _bulkUploadService.GenerateTemplateAsync();

                return File(templateData,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "PersonTemplate.xlsx");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating template: {ex.Message}";
                return RedirectToAction("BulkUpload");
            }
        }

        // GET: Persons
        public async Task<IActionResult> Index()
        {
            

            return View(); // uses Views/Persons/Index.cshtml
        }
        public async Task<IActionResult> GetAll(
    [FromQuery] List<FilterCondition> filters,
    string? searchTerm = null,
    string? firstName=null,
    string? email = null,
    string? phone = null,
    string? nationalId = null,
    int? userType = null,
    int? statusId = null,
    string? createdBy = null,
    DateTime? createdFrom = null,
    DateTime? createdTo = null,
    string matchType = "and",
    int page = 1,
    int pageSize = 6,
    string sortBy = "CreatedAt",
    string sortOrder = "desc",
    bool showOverdueOnly = false
)
        {
            var query = _context.People.Include(p => p.Nationality)
                .Include(p => p.Requests)
                    .ThenInclude(r => r.Status)
                .AsQueryable();

            var followUpSettings = await _context.FollowUpSetting.FirstOrDefaultAsync();
            int maxFollowUps = followUpSettings?.MaxFollowUps ?? 0;
            int followUpIntervalDays = followUpSettings?.FollowUpIntervalDays ?? 0;

            query = ApplyDynamicFilters(query, filters, matchType);

            // Add overdue follow-up filter if requested
            if (showOverdueOnly)
            {
                var cutoffDate = DateTime.Now.Date.AddDays(-followUpIntervalDays);
                query = query.Where(p => p.Requests.Any(r =>
                    r.Status != null &&
                    r.Status.RequireFollowUp == true &&
                    r.FollowUpCount < maxFollowUps &&
                    (r.LastFollowUpDate == null || r.LastFollowUpDate.Value.Date <= cutoffDate) &&
                    !IsClosedStatus(r.Status.StatusName)));
            }

            var users = await _context.Users.ToListAsync();
            var userDictionary = users.ToDictionary(u => u.UserId, u => u.FullName);

            var statusOptions = await GetDropdownOptionsAsync("statusid");
            var userTypeOptions = await GetDropdownOptionsAsync("usertype");

            // Pass to view
            ViewBag.StatusOptions = statusOptions;
            ViewBag.UserTypeOptions = userTypeOptions;

            // MATCH TYPE LOGIC
            if (matchType == "or")
            {
                query = query.Where(p =>
                    (!string.IsNullOrWhiteSpace(searchTerm) && (
                        p.FirstName.Contains(searchTerm) ||
                       // p.LastName.Contains(searchTerm) ||
                        p.Email.Contains(searchTerm) ||
                        p.Phone.Contains(searchTerm) ||
                        p.NationalId.Contains(searchTerm))) ||
                    (!string.IsNullOrWhiteSpace(firstName) && p.FirstName.Contains(firstName)) ||
                    (!string.IsNullOrWhiteSpace(email) && p.Email.Contains(email)) ||
                    (!string.IsNullOrWhiteSpace(phone) && p.Phone.Contains(phone)) ||
                    (!string.IsNullOrWhiteSpace(nationalId) && p.NationalId.Contains(nationalId)) ||
                    (userType.HasValue && p.UserType == userType));
            }
            else if (matchType == "not")
            {
                if (!string.IsNullOrWhiteSpace(searchTerm))
                    query = query.Where(p =>
                        !p.FirstName.Contains(searchTerm) &&
                      //  !p.LastName.Contains(searchTerm) &&
                        !p.Email.Contains(searchTerm) &&
                        !p.Phone.Contains(searchTerm) &&
                        !p.NationalId.Contains(searchTerm));
                if (!string.IsNullOrWhiteSpace(email))
                    query = query.Where(p => !p.Email.Contains(email));
                if (!string.IsNullOrWhiteSpace(phone))
                    query = query.Where(p => !p.Phone.Contains(phone));
                if (!string.IsNullOrWhiteSpace(nationalId))
                    query = query.Where(p => !p.NationalId.Contains(nationalId));
                if (userType.HasValue)
                    query = query.Where(p => p.UserType != userType);
            }
            else // matchType == "and"
            {
                if (!string.IsNullOrWhiteSpace(searchTerm))
                    query = query.Where(p =>
                        p.FirstName.Contains(searchTerm) ||
                       // p.LastName.Contains(searchTerm) ||
                        p.Email.Contains(searchTerm) ||
                        p.Phone.Contains(searchTerm) ||
                        p.NationalId.Contains(searchTerm));
                if (!string.IsNullOrWhiteSpace(firstName))
                    query = query.Where(p => p.FirstName.Contains(firstName));

                if (!string.IsNullOrWhiteSpace(email))
                    query = query.Where(p => p.Email.Contains(email));
                if (!string.IsNullOrWhiteSpace(phone))
                    query = query.Where(p => p.Phone.Contains(phone));
                if (!string.IsNullOrWhiteSpace(nationalId))
                    query = query.Where(p => p.NationalId.Contains(nationalId));
                if (userType.HasValue)
                    query = query.Where(p => p.UserType == userType);
            }

            if (createdFrom.HasValue)
                query = query.Where(p => p.CreatedAt >= createdFrom);
            if (createdTo.HasValue)
                query = query.Where(p => p.CreatedAt <= createdTo.Value.AddDays(1));

            if (!string.IsNullOrWhiteSpace(createdBy))
            {
                var matchingUserIds = users
                    .Where(u => u.FullName.Contains(createdBy, StringComparison.OrdinalIgnoreCase))
                    .Select(u => u.UserId)
                    .ToList();
                query = query.Where(p => matchingUserIds.Contains(p.CreatedByCode));
            }

            var people = await query.ToListAsync();

            var personRequests = people.Select(p =>
            {
                var latestFollowUpRequest = p.Requests?
                    .Where(r => r.Status != null && r.Status.RequireFollowUp == true)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();

                var latestRequest = latestFollowUpRequest ?? p.Requests?
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();

                string statusName = "N/A";
                if (latestRequest?.StatusId is int sid && statusOptions.TryGetValue(sid, out var sname))
                {
                    statusName = sname;
                }

                var viewModel = new PersonRequestViewModel
                {
                    ID = latestRequest?.RequestId ?? 0,
                    PersonID = p.PersonId,
                    FirstName = p.FirstName,
                   // LastName = p.LastName,
                    Email = p.Email,
                    Phone = p.Phone,
                    NationalId = p.NationalId,
                    NationalityName = p.Nationality?.NationalityName ?? "N/A",

                    UserType = p.UserType,
                    Person_CreatedAt = p.CreatedAt,
                    Person_CreatedByCode = p.CreatedByCode,
                    Person_CreatedByName = userDictionary.GetValueOrDefault(p.CreatedByCode, "Unknown"),
                    Person_UpdatedByCode = p.UpdatedByCode,
                    Person_UpdatedAt = p.UpdatedAt,
                    Person_UpdatedByName = p.UpdatedByCode.HasValue
                        ? userDictionary.GetValueOrDefault(p.UpdatedByCode.Value, "N/A")
                        : "N/A",
                    Request_CreatedAt = latestRequest?.CreatedAt ?? DateTime.MinValue,
                    Request_CreatedByCode = latestRequest?.CreatedByCode ?? 0,
                    Request_CreatedByName = latestRequest?.CreatedByCode is int reqCreatedByCode
                        ? userDictionary.GetValueOrDefault(reqCreatedByCode, "Unknown")
                        : "Unknown",
                    Request_UpdatedAt = latestRequest?.UpdatedAt,
                    Request_UpdatedByCode = latestRequest?.UpdatedByCode,
                    Request_UpdatedByName = latestRequest?.UpdatedByCode is int reqUpdatedByCode
                        ? userDictionary.GetValueOrDefault(reqUpdatedByCode, "N/A")
                        : "N/A",
                    ReasonID = latestRequest?.ReasonID,
                    Comments = latestRequest?.Comments ?? string.Empty,
                    FollowUpCount = latestRequest?.FollowUpCount ?? 0,
                    LastFollowUpDate = latestRequest?.LastFollowUpDate,
                    StatusId = latestRequest?.StatusId,
                    StatusName = statusName,

                    // Set follow-up settings for ViewModel calculations
                    MaxFollowUps = maxFollowUps,
                    FollowUpIntervalDays = followUpIntervalDays
                };

                // Determine if request is closed (doesn't need follow-up)
                bool isClosedStatus = latestRequest?.Status?.StatusName != null &&
                                     IsClosedStatus(latestRequest.Status.StatusName);
                bool doesNotRequireFollowUp = latestRequest?.Status?.RequireFollowUp != true;
                bool maxFollowUpsReached = latestRequest?.FollowUpCount >= maxFollowUps;

                viewModel.IsRequestClosed = isClosedStatus || doesNotRequireFollowUp || maxFollowUpsReached;
                viewModel.CanOpenDetails = !viewModel.IsRequestClosed;

                // Set follow-up requirement status
                if (latestRequest?.Status?.RequireFollowUp == true &&
                    latestRequest.FollowUpCount < maxFollowUps &&
                    latestRequest.Status.StatusName != null &&
                    !IsClosedStatus(latestRequest.Status.StatusName))
                {
                    viewModel.RequiresFollowUp = true;
                }
                else
                {
                    viewModel.RequiresFollowUp = false;
                }

                return viewModel;
            }).ToList();

            // Apply status filter after creating view models (if needed)
            //if (statusId.HasValue)
            //    personRequests = personRequests.Where(p => p.StatusId == statusId).ToList();

            // Sorting logic
            personRequests = sortBy.ToLower() switch
            {
                "name" => sortOrder == "asc"
                    ? personRequests.OrderBy(p => p.FirstName).ToList()
                    : personRequests.OrderByDescending(p => p.FirstName).ToList(),
                "email" => sortOrder == "asc"
                    ? personRequests.OrderBy(p => p.Email).ToList()
                    : personRequests.OrderByDescending(p => p.Email).ToList(),
                "createdat" => sortOrder == "asc"
                    ? personRequests.OrderBy(p => p.Person_CreatedAt).ToList()
                    : personRequests.OrderByDescending(p => p.Person_CreatedAt).ToList(),
                "createdby" => sortOrder == "asc"
                    ? personRequests.OrderBy(p => p.Person_CreatedByName).ToList()
                    : personRequests.OrderByDescending(p => p.Person_CreatedByName).ToList(),
                "followup" => sortOrder == "asc"
                    ? personRequests.OrderBy(p => p.LastFollowUpDate ?? DateTime.MinValue).ToList()
                    : personRequests.OrderByDescending(p => p.LastFollowUpDate ?? DateTime.MinValue).ToList(),
                "followupurgency" => sortOrder == "asc"
                    ? personRequests.OrderBy(p => p.IsFollowUpOverdue ? 0 : 1)
                                   .ThenBy(p => p.LastFollowUpDate ?? DateTime.MinValue).ToList()
                    : personRequests.OrderByDescending(p => p.IsFollowUpOverdue ? 1 : 0)
                                   .ThenByDescending(p => p.LastFollowUpDate ?? DateTime.MinValue).ToList(),
                _ => personRequests.OrderByDescending(p => p.Person_CreatedAt).ToList()
            };

            // Pagination
            var totalCount = personRequests.Count;
            var paginatedResults = personRequests
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Better follow-up statistics calculation
            var overdueFollowUps = personRequests.Where(p => p.IsFollowUpOverdue).ToList();
            var pendingFollowUps = personRequests.Where(p => p.RequiresFollowUp && !p.IsFollowUpOverdue).ToList();

            // Additional statistics for dashboard
            var nearAutoClose = personRequests.Where(p => p.IsNearAutoClose).ToList();
            var totalFollowUpsNeeded = personRequests.Count(p => p.RequiresFollowUp);

            // ViewBag properties
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Email = email;
            ViewBag.Phone = phone;
            ViewBag.NationalId = nationalId;
            ViewBag.FirstName = firstName;
            ViewBag.UserType = userType;
            ViewBag.StatusId = statusId;
            ViewBag.CreatedBy = createdBy;
            ViewBag.CreatedFrom = createdFrom?.ToString("yyyy-MM-dd");
            ViewBag.CreatedTo = createdTo?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;
            ViewBag.SortBy = sortBy;
            ViewBag.SortOrder = sortOrder;
            ViewBag.MatchType = matchType;
            ViewBag.InitialFilters = filters;
            ViewBag.ShowOverdueOnly = showOverdueOnly;

            // Follow-up statistics for dashboard
            ViewBag.OverdueFollowUpCount = overdueFollowUps.Count;
            ViewBag.PendingFollowUpCount = pendingFollowUps.Count;
            ViewBag.NearAutoCloseCount = nearAutoClose.Count;
            ViewBag.TotalFollowUpsNeeded = totalFollowUpsNeeded;

            // Pass overdue items for notifications/alerts
            ViewBag.OverdueFollowUpItems = overdueFollowUps
                .OrderByDescending(p => p.LastFollowUpDate ?? p.Request_CreatedAt)
                .Take(10) // Limit for performance
                .ToList();

            return View(paginatedResults);
        }
       

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkFollowUpDone(int id)
        {
            try
            {
                var request = await _context.Requests.FindAsync(id);
                if (request == null)
                    return Json(new { success = false, message = "Request not found." });

                // Get current user ID
                var currentUserId = GetCurrentUserId();
                var userName = GetCurrentUserFullName();
                // Update follow-up details
                request.FollowUpCount++;
                request.LastFollowUpDate = DateTime.Now;
                request.UpdatedAt = DateTime.Now;
                request.UpdatedByCode = currentUserId;

                // Update comments
               // request.Comments += $"\nFollow-up #{request.FollowUpCount} completed on {DateTime.Now:G} by {userName}";
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    followUpCount = request.FollowUpCount,
                    lastFollowUpDate = request.LastFollowUpDate?.ToString("MMM dd, yyyy")
                });
            }
            catch (Exception ex)
            {
                // Log the error
                // _logger.LogError(ex, "Error marking follow-up for request {RequestId}", id);
                return Json(new
                {
                    success = false,
                    message = "An error occurred while updating the follow-up status."
                });
            }
        }
        /// <summary>
        /// Helper method to check if a status is closed
        /// </summary>
        private bool IsClosedStatus(string? statusName)
        {
            if (string.IsNullOrEmpty(statusName))
                return false;

            // Define your closed status names here
            var closedStatuses = new[] { "Closed", "Completed", "Resolved", "Cancelled" };
            return closedStatuses.Contains(statusName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get follow-up notifications for display
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFollowUpNotifications()
        {
            try
            {
                var notifications = await _FollowUpService.GetFollowUpNotificationsAsync();
                return Json(new { success = true, data = notifications });
            }
            catch (Exception ex)
            {
                // Log the exception if you have a logger
                // _logger.LogError(ex, "Error retrieving follow-up notifications");
                return Json(new { success = false, message = "Error retrieving notifications" });
            }
        }

        /// <summary>
        /// Process a single follow-up
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProcessFollowUp(int requestId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _FollowUpService.ProcessFollowUpAsync(requestId, currentUserId);

                return Json(new
                {
                    success = true,
                    message = "Follow-up processed successfully"
                });
            }
            catch (ArgumentException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                // Log the exception if you have a logger
                // _logger.LogError(ex, "Error processing follow-up for request {RequestId}", requestId);
                return Json(new { success = false, message = "An error occurred while processing the follow-up" });
            }
        }

        /// <summary>
        /// Process multiple follow-ups at once
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProcessBulkFollowUps([FromBody] List<int> requestIds)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var results = await _FollowUpService.ProcessBulkFollowUpsAsync(requestIds, currentUserId);

                var successCount = results.Count(r => r.Value);
                var failureCount = results.Count(r => !r.Value);

                return Json(new
                {
                    success = true,
                    message = $"Processed {successCount} follow-ups successfully, {failureCount} failed",
                    results = results
                });
            }
            catch (Exception ex)
            {
                // Log the exception if you have a logger
                // _logger.LogError(ex, "Error processing bulk follow-ups");
                return Json(new { success = false, message = "Error processing bulk follow-ups" });
            }
        }

        /// <summary>
        /// Get follow-up dashboard statistics
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFollowUpStats()
        {
            try
            {
                var overdueCount = await _FollowUpService.GetOverdueFollowUpCountAsync();
                var nearAutoCloseCount = await _FollowUpService.GetNearAutoCloseCountAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        overdueCount,
                        nearAutoCloseCount
                    }
                });
            }
            catch (Exception ex)
            {
                // Log the exception if you have a logger
                // _logger.LogError(ex, "Error retrieving follow-up statistics");
                return Json(new { success = false, message = "Error retrieving statistics" });
            }
        }

        /// <summary>
        /// Display follow-up notifications page
        /// </summary>
        public IActionResult FollowUpNotifications()
        {
            try
            {
                // Return the view - let the view handle async data loading via AJAX
                return View();
            }
            catch (Exception ex)
            {
                // Log the exception if you have a logger
                // _logger.LogError(ex, "Error loading follow-up notifications page");
                TempData["Error"] = "Error loading follow-up notifications";
                return View();
            }
        }

        /// <summary>
        /// Helper method to get current user ID - implement based on authentication system
        /// </summary>




        private string GetCurrentUserFullName()
        {
            var userId = GetCurrentUserId();

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);

            return user != null ? user.FullName : $"User {userId}";
        }

        private int GetCurrentUserId()
        {
            // Example implementation - adjust based on your auth system
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            throw new UnauthorizedAccessException("User not authenticated or invalid user ID");
        }

        //export yo pdf
        // Add this method to your controller class

        // Add this method to your controller class

        public async Task<IActionResult> ExportToPdf(
            [FromQuery] List<FilterCondition> filters,
            string? searchTerm = null,
            string? email = null,
            string? phone = null,
            string? nationalId = null,
            int? userType = null,
            int? statusId = null,
            string? createdBy = null,
            DateTime? createdFrom = null,
            DateTime? createdTo = null,
            string matchType = "and",
            string sortBy = "CreatedAt",
            string sortOrder = "desc"
        
        )
        {
            try
            {
                // Use the same filtering logic as GetAll method
                var query = _context.People
                    .Include(p => p.Requests)
                
                    .AsQueryable();

                query = ApplyDynamicFilters(query, filters, matchType);

                var users = await _context.Users.ToListAsync();
                var userDictionary = users.ToDictionary(u => u.UserId, u => u.FullName);

                var statusOptions = await GetDropdownOptionsAsync("statusid");
                var userTypeOptions = await GetDropdownOptionsAsync("usertype");

               
                // Apply the same filtering logic from GetAll
                if (matchType == "or")
                {
                    query = query.Where(p =>
                        (!string.IsNullOrWhiteSpace(searchTerm) && (
                            p.FirstName.Contains(searchTerm) ||
                            
                            p.Email.Contains(searchTerm) ||
                            p.Phone.Contains(searchTerm) ||
                            p.NationalId.Contains(searchTerm))) ||
                        (!string.IsNullOrWhiteSpace(email) && p.Email.Contains(email)) ||
                        (!string.IsNullOrWhiteSpace(phone) && p.Phone.Contains(phone)) ||
                        (!string.IsNullOrWhiteSpace(nationalId) && p.NationalId.Contains(nationalId)) ||
                        (userType.HasValue && p.UserType == userType));
                }
                else if (matchType == "not")
                {
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                        query = query.Where(p =>
                            !p.FirstName.Contains(searchTerm) &&
                           
                            !p.Email.Contains(searchTerm) &&
                            !p.Phone.Contains(searchTerm) &&
                            !p.NationalId.Contains(searchTerm));
                    if (!string.IsNullOrWhiteSpace(email))
                        query = query.Where(p => !p.Email.Contains(email));
                    if (!string.IsNullOrWhiteSpace(phone))
                        query = query.Where(p => !p.Phone.Contains(phone));
                    if (!string.IsNullOrWhiteSpace(nationalId))
                        query = query.Where(p => !p.NationalId.Contains(nationalId));
                    if (userType.HasValue)
                        query = query.Where(p => p.UserType != userType);
                }
                else // matchType == "and"
                {
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                        query = query.Where(p =>
                            p.FirstName.Contains(searchTerm) ||
                            
                            p.Email.Contains(searchTerm) ||
                            p.Phone.Contains(searchTerm) ||
                            p.NationalId.Contains(searchTerm));
                    if (!string.IsNullOrWhiteSpace(email))
                        query = query.Where(p => p.Email.Contains(email));
                    if (!string.IsNullOrWhiteSpace(phone))
                        query = query.Where(p => p.Phone.Contains(phone));
                    if (!string.IsNullOrWhiteSpace(nationalId))
                        query = query.Where(p => p.NationalId.Contains(nationalId));
                    if (userType.HasValue)
                        query = query.Where(p => p.UserType == userType);
                }

                if (createdFrom.HasValue)
                    query = query.Where(p => p.CreatedAt >= createdFrom);
                if (createdTo.HasValue)
                    query = query.Where(p => p.CreatedAt <= createdTo.Value.AddDays(1));

                if (!string.IsNullOrWhiteSpace(createdBy))
                {
                    var matchingUserIds = users
                        .Where(u => u.FullName.Contains(createdBy, StringComparison.OrdinalIgnoreCase))
                        .Select(u => u.UserId)
                        .ToList();
                    query = query.Where(p => matchingUserIds.Contains(p.CreatedByCode));
                }

                var people = await query.ToListAsync();

                var personRequests = people.Select(p =>
                {
                    var latestRequest = p.Requests?
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefault();

                    string statusName = "N/A";
                    if (latestRequest?.StatusId is int sid && statusOptions.TryGetValue(sid, out var sname))
                    {
                        statusName = sname;
                    }

                    string userTypeName = "N/A";
                    if (userTypeOptions.TryGetValue(p.UserType, out var utname))
                    {
                        userTypeName = utname;
                    }

                    return new PersonRequestViewModel
                    {
                        ID = latestRequest?.RequestId ?? 0,
                        PersonID = p.PersonId,
                        FirstName = p.FirstName,
                       // LastName = p.LastName,
                        Email = p.Email,
                        Phone = p.Phone,
                        NationalId = p.NationalId,
                        UserType = p.UserType,
                        Person_CreatedAt = p.CreatedAt,
                        Person_CreatedByCode = p.CreatedByCode,
                        Person_CreatedByName = userDictionary.GetValueOrDefault(p.CreatedByCode, "Unknown"),
                        Person_UpdatedByCode = p.UpdatedByCode,
                        Person_UpdatedAt = p.UpdatedAt,
                        Person_UpdatedByName = p.UpdatedByCode.HasValue
                            ? userDictionary.GetValueOrDefault(p.UpdatedByCode.Value, "N/A")
                            : "N/A",
                        Request_CreatedAt = latestRequest?.CreatedAt ?? DateTime.MinValue,
                        Request_CreatedByCode = latestRequest?.CreatedByCode ?? 0,
                        Request_CreatedByName = latestRequest?.CreatedByCode is int reqCreatedByCode
                            ? userDictionary.GetValueOrDefault(reqCreatedByCode, "Unknown")
                            : "Unknown",
                        Request_UpdatedAt = latestRequest?.UpdatedAt,
                        Request_UpdatedByCode = latestRequest?.UpdatedByCode,
                        Request_UpdatedByName = latestRequest?.UpdatedByCode is int reqUpdatedByCode
                            ? userDictionary.GetValueOrDefault(reqUpdatedByCode, "N/A")
                            : "N/A",
                        ReasonID = latestRequest.ReasonID,
                        Comments = latestRequest?.Comments ?? string.Empty,
                        FollowUpCount = latestRequest?.FollowUpCount ?? 0,
                        LastFollowUpDate = latestRequest?.LastFollowUpDate,
                        StatusId = latestRequest?.StatusId,
                      
                        StatusName = statusName,
                        HighSchoolId = p.HighSchoolId,
                        CertificateId = p.CertificateId,
                        //MajorId = p.MajorId,
                        HowDidYouKnowUsId = p.HowDidYouKnowUsId,

                        HighSchoolName = _context.LookUpHighSchools
    .FirstOrDefault(h => h.HighSchoolId == p.HighSchoolId)?.HighSchoolName ?? "N/A",

                        CertificateName = _context.LookUpHighSchoolCerts
    .FirstOrDefault(c => c.CertificateId == p.CertificateId)?.CertificateName ?? "N/A",

                        HowDidYouKnowUsName = _context.LookUpHowDidYouKnowUs
    .FirstOrDefault(k => k.HowDidYouKnowUsId == p.HowDidYouKnowUsId)?.HowDidYouKnowUs ?? "N/A",

    //                    MajorName = _context.LookupMajors
    //.FirstOrDefault(m => m.MajorId == p.MajorId)?.MajorInterest ?? "N/A",



                    };

                    // Add follow-up status indicators
                 
                }).ToList();

                // Apply sorting
                personRequests = sortBy.ToLower() switch
                {
                    "name" => sortOrder == "asc"
                        ? personRequests.OrderBy(p => p.FirstName).ToList()
                        : personRequests.OrderByDescending(p => p.FirstName).ToList(),
                    "email" => sortOrder == "asc"
                        ? personRequests.OrderBy(p => p.Email).ToList()
                        : personRequests.OrderByDescending(p => p.Email).ToList(),
                    "createdat" => sortOrder == "asc"
                        ? personRequests.OrderBy(p => p.Person_CreatedAt).ToList()
                        : personRequests.OrderByDescending(p => p.Person_CreatedAt).ToList(),
                    "createdby" => sortOrder == "asc"
                        ? personRequests.OrderBy(p => p.Person_CreatedByName).ToList()
                        : personRequests.OrderByDescending(p => p.Person_CreatedByName).ToList(),
                    _ => personRequests.OrderByDescending(p => p.Person_CreatedAt).ToList()
                };

                // Generate PDF
                var document = new PersonRequestsPdfDocument(personRequests, userTypeOptions);
                var pdfBytes = document.GeneratePdf();

                var fileName = $"PersonRequests_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                // Log the exception
                // You might want to use your logging framework here
                return BadRequest($"Error generating PDF: {ex.Message}");
            }
        }

     
        public async Task<IActionResult> ExportToExcel(List<int> personIds)
        {
            try
            {
                // Get user type options (assuming you have this somewhere)
                var userTypeOptions = new Dictionary<int, string>
        {
            { 1, "Lead" },
            { 2, "Guardian" },
            
            // user types here
        };

                // Create the service with dependency injection
                var exportService = new PersonRequestsExcelExportService(_context, userTypeOptions);

                // Generate the Excel file
                var fileBytes = await exportService.ExportToExcelAsync(personIds);

                // Return the file
                var fileName = $"PersonRequests_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                // Log the error
                // _logger.LogError(ex, "Error exporting person requests to Excel");
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        // Alternative method if you want to export all persons with requests
        public async Task<IActionResult> ExportAllPersonsWithRequests()
        {
            try
            {
                // Get all person IDs that have requests
                var personIds = await _context.Requests
                    .Select(r => r.PersonId)
                    .Distinct()
                    .ToListAsync();

                return await ExportToExcel(personIds);
            }
            catch (Exception ex)
            {
                // Log the error
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }

        // Method to export specific filtered persons
        public async Task<IActionResult> ExportFilteredPersons(string searchTerm = "", int? statusId = null, int? userType = null)
        {
            try
            {
                var query = _context.People.AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(p => p.FirstName.Contains(searchTerm) ||
                                          
                                           p.Email.Contains(searchTerm));
                }

                if (userType.HasValue)
                {
                    query = query.Where(p => p.UserType == userType.Value);
                }

                // If filtering by status, we need to join with requests
                if (statusId.HasValue)
                {
                    var personIdsWithStatus = await _context.Requests
                        .Where(r => r.StatusId == statusId.Value)
                        .Select(r => r.PersonId)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(p => personIdsWithStatus.Contains(p.PersonId));
                }

                var personIds = await query.Select(p => p.PersonId).ToListAsync();

                return await ExportToExcel(personIds);
            }
            catch (Exception ex)
            {
                // Log the error
                return StatusCode(500, "An error occurred while generating the Excel file.");
            }
        }
   


        private IQueryable<Person> ApplyTextFilter(IQueryable<Person> query, Expression<Func<Person, string>> selector, FilterCondition filter)
        {
            switch (filter.Operator)
            {
                case "starts_with": // Added for FirstName
                    return query.Where(p => EF.Functions.Like(EF.Property<string>(p, selector.GetPropertyAccess().Name), $"{filter.Value}%"));
                case "contains":
                    return query.Where(p => EF.Functions.Like(EF.Property<string>(p, selector.GetPropertyAccess().Name), $"%{filter.Value}%"));
                case "not_contains":
                    return query.Where(p => !EF.Functions.Like(EF.Property<string>(p, selector.GetPropertyAccess().Name), $"%{filter.Value}%"));
                case "equals":
                    return query.Where(p => EF.Property<string>(p, selector.GetPropertyAccess().Name) == filter.Value);
                case "not_equals":
                    return query.Where(p => EF.Property<string>(p, selector.GetPropertyAccess().Name) != filter.Value);
                default:
                    return query;
            }
        }

        private IQueryable<Person> ApplyNumericFilter(IQueryable<Person> query, Expression<Func<Person, int>> selector, string op, int value)
        {
            switch (op)
            {
                case "equals":
                    return query.Where(p => EF.Property<int>(p, selector.GetPropertyAccess().Name) == value);
                case "not_equals":
                    return query.Where(p => EF.Property<int>(p, selector.GetPropertyAccess().Name) != value);
                default:
                    return query;
            }
        }

        private IQueryable<Person> ApplyDynamicFilters(IQueryable<Person> query, List<FilterCondition> filters, string matchType)
        {
            if (filters == null || !filters.Any()) return query;

            foreach (var filter in filters)
            {
                query = ApplyIndividualFilter(query, filter);
            }

            return query;
        }
    
        private IQueryable<Person> ApplyIndividualFilter(IQueryable<Person> query, FilterCondition filter)
        {
            switch (filter.Field.ToLower())
            {
                case "firstname": // Added FirstName support
                    return ApplyTextFilter(query, p => p.FirstName, filter);
                case "email":
                    return ApplyTextFilter(query, p => p.Email, filter);
                case "phone":
                    return ApplyTextFilter(query, p => p.Phone, filter);
                case "nationalid":
                    return ApplyTextFilter(query, p => p.NationalId, filter);
                case "usertype":
                    if (int.TryParse(filter.Value, out var userTypeVal))
                        return ApplyNumericFilter(query, p => p.UserType, filter.Operator, userTypeVal);
                    break;
                case "statusid":
                    if (filter.Field.ToLower() == "statusid" && int.TryParse(filter.Value, out var statusVal))
                    {
                        if (filter.Operator.ToLower() == "equals")
                        {
                            // Test: Show people whose latest request has this exact status
                            return query.Where(p => p.Requests.Any() &&
                                               p.Requests.OrderByDescending(r => r.CreatedAt)
                                                        .First().StatusId == statusVal);
                        }
                        else if (filter.Operator.ToLower() == "not_equals")
                        {
                            // Test: Show people whose latest request does NOT have this status
                            return query.Where(p => p.Requests.Any() &&
                                               p.Requests.OrderByDescending(r => r.CreatedAt)
                                                        .First().StatusId != statusVal);
                        }
                    }
                    break;
            }
            return query;
        }




        [HttpGet]
        public async Task<Dictionary<int, string>> GetDropdownOptionsAsync(string field)
        {
            switch (field.ToLower())
            {
                case "usertype":
                    return new Dictionary<int, string>
            {
                { 1, "Lead" },
                { 2, "Guardian" }
            };
                case "statusid":
                    return await _context.LookUpStatusTypes
                        .ToDictionaryAsync(s => s.StatusId, s => s.StatusName);
                default:
                    return new Dictionary<int, string>();
            }
        }


        private string GetPropertyName<T>(Expression<Func<Person, T>> expression)
        {
            if (expression.Body is MemberExpression member)
                return member.Member.Name;

            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
                return unaryMember.Member.Name;

            throw new ArgumentException("Invalid expression type");
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPeople()
        {
            var people = await _context.People
                .ToListAsync();

            var userDictionary = await _context.Users
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var models = people.Select(person => new PersonRequestViewModel
            {
                PersonID = person.PersonId,
                FirstName = person.FirstName,
               // LastName = person.LastName,
                Email = person.Email,
                Phone = person.Phone,
                NationalId = person.NationalId,
                UserType = person.UserType,
                HighSchoolId = person.HighSchoolId,
                CertificateId = person.CertificateId,
                CityID = person.CityID,
                GradeID = person.GradeID,
                NationalityID = person.NationalityID,
                whatsApp = person.whatsApp,
                HowDidYouKnowUsId = person.HowDidYouKnowUsId,
                HowDidYouKnowUs_Other = string.IsNullOrEmpty(person.HowDidYouKnowUs_Other) ? null : person.HowDidYouKnowUs_Other,
                Person_CreatedByCode = person.CreatedByCode,
                Person_CreatedByName = userDictionary.GetValueOrDefault(person.CreatedByCode, "Unknown"),
                Person_UpdatedAt = person.UpdatedAt,
                Person_UpdatedByCode = person.UpdatedByCode,
                Person_UpdatedByName = person.UpdatedByCode.HasValue && userDictionary.TryGetValue(person.UpdatedByCode.Value, out var updater)
                    ? updater : "N/A"
            }).ToList();

            return View("GetAllPeople", models); 
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var person = await _context.People
                .Include(p => p.HighSchool)
                .Include(p => p.Certificate)
                .Include(p => p.HowDidYouKnowUs)
                .Include(p => p.City)
                .Include(p => p.Grade)
                .Include(p => p.Nationality)
                 .Include(p => p.Requests)  
        .ThenInclude(r => r.Status)
                .FirstOrDefaultAsync(p => p.PersonId == id);

            if (person == null)
                return NotFound();

            // Get person's major interests using your existing function
            var majorInterestIds = await GetPersonMajorInterestsAsync(person.PersonId);

            // Get the actual major names from LookupMajor table
            var majorInterests = new List<string>();
            if (majorInterestIds.Any())
            {
                majorInterests = await _context.LookupMajors
                    .Where(m => majorInterestIds.Contains(m.MajorId))
                    .Select(m => m.MajorInterest)
                    .ToListAsync();
            }

            // Store in ViewBag for use in the view
            ViewBag.MajorInterests = majorInterests;
            ViewBag.PrimaryMajor = majorInterests.FirstOrDefault() ?? "Not specified";
            ViewBag.AllMajorsString = majorInterests.Any() ? string.Join(", ", majorInterests) : "Not specified";
            // In your Details action
            var requestFollowUpData = new Dictionary<int, object>();

            foreach (var request in person.Requests)
            {
                try
                {
                    var canFollowUp = await _helper.CanFollowUpAsync(
                        request.StatusId.Value,
                        request.FollowUpCount.Value,
                        request.Status?.StatusName
                    );

                    var followUpSummary = await _helper.GetFollowUpSummaryAsync(
                        request.RequestId,
                        request.StatusId.Value,
                        request.LastFollowUpDate,
                        request.CreatedAt,
                        request.FollowUpCount.Value
                    );

                    // Combine the data
                    requestFollowUpData[request.RequestId] = new
                    {
                        CanFollowUp = canFollowUp,
                        RequiresFollowUp = followUpSummary.GetType().GetProperty("RequiresFollowUp")?.GetValue(followUpSummary) ?? false,
                        MaxFollowUpsReached = followUpSummary.GetType().GetProperty("MaxFollowUpsReached")?.GetValue(followUpSummary) ?? false,
                        StatusText = followUpSummary.GetType().GetProperty("StatusText")?.GetValue(followUpSummary) ?? "Unknown",
                        Settings = followUpSummary.GetType().GetProperty("Settings")?.GetValue(followUpSummary)
                    };
                }
                catch (Exception ex)
                {
                    // Fallback for any errors
                    requestFollowUpData[request.RequestId] = new
                    {
                        CanFollowUp = false,
                        RequiresFollowUp = false,
                        MaxFollowUpsReached = false,
                        StatusText = "Error loading follow-up info",
                        Settings = (object?)null
                    };
                }
            }

            ViewBag.RequestFollowUpData = requestFollowUpData;
            // Handle HowDidYouKnowUs display - show custom value if "Other" was selected
            string howDidYouKnowUsDisplay;
            if (person.HowDidYouKnowUsId == 8) // "Other" option
            {
                howDidYouKnowUsDisplay = !string.IsNullOrEmpty(person.HowDidYouKnowUs_Other)
                    ? person.HowDidYouKnowUs_Other
                    : "Other";
            }
            else
            {
                howDidYouKnowUsDisplay = person.HowDidYouKnowUs?.HowDidYouKnowUs ?? "Not specified";
            }
            ViewBag.HowDidYouKnowUsDisplay = howDidYouKnowUsDisplay;

            var requests = await _context.Requests
                .Where(r => r.PersonId == id)
                .Include(r => r.Reason) // Include the Reason lookup
                .ToListAsync();

            // Get Follow-Up Settings from DB
            var followUpSettings = await _context.FollowUpSetting.FirstOrDefaultAsync();
            ViewBag.MaxFollowUps = followUpSettings?.MaxFollowUps ?? 0; // Fallback to 0 if null 

            var userDictionary = await _context.Users
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);
            // Get all request IDs for this person to check follow-ups in bulk
            var requestIds = requests.Select(r => r.RequestId).ToList();

            // Get all follow-ups for these requests in one query
            var requestsWithFollowUps = await _context.FollowUp_Log
                .Where(f => requestIds.Contains(f.RequestId))
                .Select(f => f.RequestId)
                .Distinct()
                .ToListAsync();
            var requestViewModels = requests.Select(r => new RequestViewModel
            {
                RequestId = r.RequestId,
                ReasonID = r.ReasonID,
                ReasonDescription = r.Reason?.Reason_Description ?? "N/A",

                Comments = r.Comments ?? "",
                FollowUpCount = r.FollowUpCount??0,
                LastFollowUpDate = r.LastFollowUpDate,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                CreatedByName = userDictionary.TryGetValue(r.CreatedByCode, out var created) ? created : "Unknown",
                UpdatedByName = r.UpdatedByCode.HasValue && userDictionary.TryGetValue(r.UpdatedByCode.Value, out var updated) ? updated : "N/A",
                StatusId = r.StatusId,
                StatusName = _context.LookUpStatusTypes.FirstOrDefault(s => s.StatusId == r.StatusId)?.StatusName ?? "N/A",
                //  this property to indicate if request has follow-ups
                HasFollowUps = requestsWithFollowUps.Contains(r.RequestId)
            }).ToList();

            // Keep your existing ViewModel structure unchanged
            var viewModel = new PersonDetailsViewModel
            {
                Person = person,
                Requests = requestViewModels
            };

            return View(viewModel);
        }

        






        private bool PersonExists(int id)
        {
            return _context.People.Any(e => e.PersonId == id);
        }



       
        // 2. Add a helper method to get current academic settings
        private async Task<(int numberOfInterests, int academicSettingId)> GetCurrentAcademicSettingAsync()
        {
            var currentSetting = await _context.AcademicSettings
                .Where(a => a.IsActive == true)
                .OrderByDescending(a => a.Year)
                .ThenByDescending(a => a.AcademicSettingId)
                .FirstOrDefaultAsync();

            if (currentSetting == null)
            {
                // Default fallback
                return (1, 0);
            }

            return (currentSetting.NumberOfInterests, currentSetting.AcademicSettingId);
        }

        //private async Task ValidateUniqueness(PersonRequestViewModel model)
        //{
        //    var (numberOfInterests, academicSettingId) = await GetCurrentAcademicSettingAsync();

        //    if (!model.MajorId.HasValue || model.MajorId.Value == 0)
        //    {
        //        ModelState.AddModelError("FirstMajorInterestId", "First major interest is required.");
        //    }
        //    if (numberOfInterests >= 2)
        //    {
        //        // Second major is allowed
        //        if (model.MajorId.HasValue && model.SecondMajorId.HasValue &&
        //            model.MajorId.Value == model.SecondMajorId.Value)
        //        {
        //            ModelState.AddModelError("SecondMajorInterestId", "Second major interest must be different from the first.");
        //        }
        //    }
        //    else
        //    {
        //        // Only one major allowed, clear second major if set
        //        model.SecondMajorId = null;
        //    }

        //    // Check if email already exists (if not empty)
        //    if (!string.IsNullOrWhiteSpace(model.Email))
        //    {
        //        var emailExists = await _context.People
        //            .AnyAsync(p => p.Email.ToLower() == model.Email.ToLower());

        //        if (emailExists)
        //        {
        //            ModelState.AddModelError("Email", "This email address is already registered.");
        //        }
        //    }

        //    // Check if phone already exists (if not empty)
        //    if (!string.IsNullOrWhiteSpace(model.Phone))
        //    {
        //        var phoneExists = await _context.People
        //            .AnyAsync(p => p.Phone == model.Phone);

        //        if (phoneExists)
        //        {
        //            ModelState.AddModelError("Phone", "This phone number is already registered.");
        //        }
        //    }

        //    // Check if national ID already exists (if not empty)
        //    if (!string.IsNullOrWhiteSpace(model.NationalId))
        //    {
        //        var nationalIdExists = await _context.People
        //            .AnyAsync(p => p.NationalId == model.NationalId);

        //        if (nationalIdExists)
        //        {
        //            ModelState.AddModelError("NationalId", "This National ID is already registered.");
        //        }
        //    }

        //    //  National ID validation for Egyptian nationality
        //    if (!string.IsNullOrWhiteSpace(model.NationalId) && model.NationalityID.HasValue)
        //    {
        //        string? nationalIdError = null;

        //        if (model.NationalityID == 1) // Egyptian nationality
        //        {
        //            // Remove any spaces or special characters for validation
        //            var cleanNationalId = model.NationalId.Trim();

        //            if (cleanNationalId.Length != 14)
        //            {
        //                nationalIdError = "Egyptian National ID must be exactly 14 digits.";
        //            }
        //            else if (!cleanNationalId.All(char.IsDigit))
        //            {
        //                nationalIdError = "Egyptian National ID must contain only numbers.";
        //            }
        //            else
        //            {
        //                // Additional Egyptian National ID format validation
        //                // First digit should be 2 or 3 (born in 1900s or 2000s)
        //                var firstDigit = cleanNationalId[0];
        //                //if (firstDigit != '2' && firstDigit != '3')
        //                //{
        //                //    nationalIdError = "Invalid Egyptian National ID format. First digit should be 2 or 3.";
        //                //}

        //                //// Check birth date validity (positions 1-6: YYMMDD)
        //                //if (string.IsNullOrEmpty(nationalIdError))
        //                //{
        //                //    var yearPart = cleanNationalId.Substring(1, 2);
        //                //    var monthPart = cleanNationalId.Substring(3, 2);
        //                //    var dayPart = cleanNationalId.Substring(5, 2);

        //                //    if (!int.TryParse(monthPart, out int month) || month < 1 || month > 12)
        //                //    {
        //                //        nationalIdError = "Invalid month in Egyptian National ID.";
        //                //    }
        //                //    else if (!int.TryParse(dayPart, out int day) || day < 1 || day > 31)
        //                //    {
        //                //        nationalIdError = "Invalid day in Egyptian National ID.";
        //                //    }
        //                //}
        //            }
        //        }
        //        else // Foreign nationality
        //        {
        //            if (model.NationalId.Trim().Length < 5)
        //            {
        //                nationalIdError = "For foreign nationality, please enter a valid passport number (minimum 5 characters).";
        //            }
        //        }

        //        if (!string.IsNullOrEmpty(nationalIdError))
        //        {
        //            ModelState.AddModelError("NationalId", nationalIdError);
        //        }
        //    }

        //    // Conditional validation for required fields when StatusId = 1 (Interested)
        //    if (model.StatusId == 1)
        //    {
        //        if (!model.NationalityID.HasValue)
        //        {
        //            ModelState.AddModelError("NationalityID", "Nationality is required for interested status.");
        //        }

        //        if (string.IsNullOrWhiteSpace(model.NationalId))
        //        {
        //            ModelState.AddModelError("NationalId", "National ID/Passport is required for interested status.");
        //        }
        //    }
        //}

        //normalize method to remove the spaces while choosing other selection 

        private async Task ValidateUniqueness(PersonRequestViewModel model)
        {
            var (numberOfInterests, academicSettingId) = await GetCurrentAcademicSettingAsync();

            //if (!model.MajorId.HasValue || model.MajorId.Value == 0)
            //{
            //    ModelState.AddModelError("FirstMajorInterestId", "First major interest is required.");
            //}
            if (numberOfInterests >= 2)
            {
                // Second major is allowed
                if (model.MajorId.HasValue && model.SecondMajorId.HasValue &&
                    model.MajorId.Value == model.SecondMajorId.Value)
                {
                    ModelState.AddModelError("SecondMajorInterestId", "Second major interest must be different from the first.");
                }
            }
            else
            {
                // Only one major allowed, clear second major if set
                model.SecondMajorId = null;
            }

            // Enhanced uniqueness checking with person details for redirect
            Person existingPerson = null;
            string duplicateField = "";

            // Check if email already exists (if not empty)
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                existingPerson = await _context.People
                    .FirstOrDefaultAsync(p => p.Email.ToLower() == model.Email.ToLower());

                if (existingPerson != null)
                {
                    duplicateField = "Email";
                    var errorMessage = $"This email address is already registered for {existingPerson.FirstName} .";
                    ModelState.AddModelError("Email", errorMessage);

                    // Store the existing person ID for the view button
                    ViewBag.ExistingPersonId = existingPerson.PersonId;
                    ViewBag.ExistingPersonName = $"{existingPerson.FirstName} ";
                    ViewBag.DuplicateField = duplicateField;
                }
            }

            // Check if phone already exists (if not empty) - only if no email duplicate found
            if (existingPerson == null && !string.IsNullOrWhiteSpace(model.Phone))
            {
                existingPerson = await _context.People
                    .FirstOrDefaultAsync(p => p.Phone == model.Phone);

                if (existingPerson != null)
                {
                    duplicateField = "Phone";
                    var errorMessage = $"This phone number is already registered for {existingPerson.FirstName} .";
                    ModelState.AddModelError("Phone", errorMessage);

                    // Store the existing person ID for the view button
                    ViewBag.ExistingPersonId = existingPerson.PersonId;
                    ViewBag.ExistingPersonName = $"{existingPerson.FirstName}";
                    ViewBag.DuplicateField = duplicateField;
                }
            }

            // Check if national ID already exists (if not empty) - only if no previous duplicate found
            if (existingPerson == null && !string.IsNullOrWhiteSpace(model.NationalId))
            {
                existingPerson = await _context.People
                    .FirstOrDefaultAsync(p => p.NationalId == model.NationalId);

                if (existingPerson != null)
                {
                    duplicateField = "National ID";
                    var errorMessage = $"This National ID is already registered for {existingPerson.FirstName}.";
                    ModelState.AddModelError("NationalId", errorMessage);

                    // Store the existing person ID for the view button
                    ViewBag.ExistingPersonId = existingPerson.PersonId;
                    ViewBag.ExistingPersonName = $"{existingPerson.FirstName}";
                    ViewBag.DuplicateField = duplicateField;
                }
            }

            // If we found a duplicate, also check if it's the same person across multiple fields
            if (existingPerson != null)
            {
                var matchingFields = new List<string>();

                if (!string.IsNullOrWhiteSpace(model.Email) &&
                    existingPerson.Email?.ToLower() == model.Email.ToLower())
                {
                    matchingFields.Add("Email");
                }

                if (!string.IsNullOrWhiteSpace(model.Phone) &&
                    existingPerson.Phone == model.Phone)
                {
                    matchingFields.Add("Phone");
                }

                if (!string.IsNullOrWhiteSpace(model.NationalId) &&
                    existingPerson.NationalId == model.NationalId)
                {
                    matchingFields.Add("National ID");
                }

                // Store matching fields for display
                ViewBag.MatchingFields = string.Join(", ", matchingFields);

                // Create a comprehensive message for the user
                if (matchingFields.Count > 1)
                {
                    var fieldsText = string.Join(" and ", matchingFields);
                    ViewBag.DuplicateMessage = $"The {fieldsText} match an existing person: {existingPerson.FirstName} ";
                }
                else
                {
                    ViewBag.DuplicateMessage = $"The {duplicateField} matches an existing person: {existingPerson.FirstName}";
                }
            }

            // National ID validation for Egyptian nationality (rest of your existing validation code)
            if (!string.IsNullOrWhiteSpace(model.NationalId) && model.NationalityID.HasValue)
            {
                string? nationalIdError = null;

                if (model.NationalityID == 1) // Egyptian nationality
                {
                    // Remove any spaces or special characters for validation
                    var cleanNationalId = model.NationalId.Trim();

                    if (cleanNationalId.Length != 14)
                    {
                        nationalIdError = "Egyptian National ID must be exactly 14 digits.";
                    }
                    else if (!cleanNationalId.All(char.IsDigit))
                    {
                        nationalIdError = "Egyptian National ID must contain only numbers.";
                    }
                }
                else // Foreign nationality
                {
                    if (model.NationalId.Trim().Length < 5)
                    {
                        nationalIdError = "For foreign nationality, please enter a valid passport number (minimum 5 characters).";
                    }
                }

                if (!string.IsNullOrEmpty(nationalIdError))
                {
                    ModelState.AddModelError("NationalId", nationalIdError);
                }
            }

            // Conditional validation for required fields when StatusId = 1 (Interested)
            //if (model.StatusId == 1)
            //{
            //    if (!model.NationalityID.HasValue)
            //    {
            //        ModelState.AddModelError("NationalityID", "Nationality is required for interested status.");
            //    }

            //    if (string.IsNullOrWhiteSpace(model.NationalId))
            //    {
            //        ModelState.AddModelError("NationalId", "National ID/Passport is required for interested status.");
            //    }
            //}
        }
        private string Normalize(string input)
        {
            return string.Join(" ", input?.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
                         .ToLowerInvariant();
        }

        [HttpGet]
        public async Task<IActionResult> Create()

        {
            var model = new PersonRequestViewModel();

            // Get current user info
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);
            var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

            // Set the display values for the form
            model.Person_CreatedByCode = currentUserId;
            model.Person_CreatedByName = currentUserName;
            // Get current academic setting
            var currentAcademicSetting = await _context.AcademicSettings
                .FirstOrDefaultAsync(a => a.IsActive == true);

            if (currentAcademicSetting != null)
            {
                model.MaxNumberOfInterests = currentAcademicSetting.NumberOfInterests;
                model.CurrentAcademicSettingId = currentAcademicSetting.AcademicSettingId;
            }

            await LoadSelectLists(model);

            return View(model);
        }
        // POST: Persons/CreateWithRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
       
        public async Task<IActionResult> Create(PersonRequestViewModel model)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);
            var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

            // Set created by info
            model.Person_CreatedByCode = currentUserId;
            model.Request_CreatedByCode = currentUserId;
            model.Person_CreatedByName = currentUserName;
            model.Request_CreatedByName = currentUserName;

            // Academic settings
            var academicSettings = await GetCurrentAcademicSettingAsync();
            var numberOfInterests = academicSettings.numberOfInterests;
            var academicSettingId = academicSettings.academicSettingId;

            model.MaxNumberOfInterests = numberOfInterests;

            // Check uniqueness
            await ValidateUniqueness(model);

            // === Handle "Other" High School ===
            ViewBag.ExistingHighSchoolNames = await _context.LookUpHighSchools
    .Select(h => h.HighSchoolName)
    .ToListAsync();


            if (model.HighSchoolId == -1 && !string.IsNullOrWhiteSpace(model.SchoolOther))
            {
               

                var normalizedOther = Normalize(model.SchoolOther);
                // Check if a similar entry exists
                var highSchools = await _context.LookUpHighSchools.ToListAsync();
                var existingSchool = highSchools
                    .FirstOrDefault(h => Normalize(h.HighSchoolName) == normalizedOther);

                // var trimmedName = model.SchoolOther.Trim().ToLower();
                //var existingSchool = await _context.LookUpHighSchools
                //    .FirstOrDefaultAsync(h => h.HighSchoolName.Trim().ToLower() == trimmedName);

                if (existingSchool != null)
                {
                    model.HighSchoolId = existingSchool.HighSchoolId;
                    TempData["Toast"] = $"High school '{existingSchool.HighSchoolName}' already exists. Selected automatically.";
                }
                else
                {
                    var newHighSc = new LookUpHighSchool { HighSchoolName = model.SchoolOther.Trim() };
                    _context.LookUpHighSchools.Add(newHighSc);
                    await _context.SaveChangesAsync();
                    model.HighSchoolId = newHighSc.HighSchoolId;
                }
            }

            if (!ModelState.IsValid)
            {
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    foreach (var error in state.Errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"ModelState Error - Field: {key}, Error: {error.ErrorMessage}");
                    }
                }
                await LoadSelectLists(model);
                return View("Create", model);
            }

            try
            {
                // Create Person
                var person = new Person
                {
                    FirstName = model.FirstName,
                    //LastName = model.LastName,
                    Email = model.Email,
                    Phone = model.Phone,
                    NationalId = model.NationalId,
                    UserType = model.UserType,
                    HighSchoolId = model.HighSchoolId,
                    CertificateId = model.CertificateId,
                    HowDidYouKnowUsId = model.HowDidYouKnowUsId,
                    CreatedAt = DateTime.Now,
                    CreatedByCode = model.Person_CreatedByCode > 0 ? model.Person_CreatedByCode : currentUserId,
                    CityID = model.CityID,
                    GradeID = model.GradeID,
                    NationalityID = model.NationalityID,
                    whatsApp = model.whatsApp
                };

                // === Handle "Other" HowDidYouKnowUs ===
                if (model.HowDidYouKnowUsId == 8 && !string.IsNullOrWhiteSpace(model.HowDidYouKnowUs_Other))
                {
                    var trimmed = model.HowDidYouKnowUs_Other;

                    var existing = await _context.LookUpHowDidYouKnowUs
                        .FirstOrDefaultAsync(h => h.HowDidYouKnowUs.ToLower() == trimmed.ToLower());

                    if (existing != null)
                    {

                        person.HowDidYouKnowUsId = existing.HowDidYouKnowUsId;
                        person.HowDidYouKnowUs_Other = null;

                    }
                    else
                    {
                        person.HowDidYouKnowUsId = 8; // Keep as "Other"
                        person.HowDidYouKnowUs_Other = trimmed;
                    }
                }
                else
                {
                    person.HowDidYouKnowUsId = model.HowDidYouKnowUsId;
                    person.HowDidYouKnowUs_Other = null;
                }

                _context.People.Add(person);
                await _context.SaveChangesAsync();

                // Save Major Interests
                if (model.MajorId.HasValue && model.MajorId.Value > 0)
                {
                    _context.MajorPersons.Add(new MajorPerson
                    {
                        PersonID = person.PersonId,
                        MajorID = model.MajorId.Value,
                        Academic_Setting_ID = academicSettingId,
                        Priority = 1
                    });
                }

                if (numberOfInterests >= 2 && model.SecondMajorId.HasValue && model.SecondMajorId.Value > 0)
                {
                    _context.MajorPersons.Add(new MajorPerson
                    {
                        PersonID = person.PersonId,
                        MajorID = model.SecondMajorId.Value,
                        Academic_Setting_ID = academicSettingId,
                        Priority = 2
                    });
                }

             
                // === Handle "Other" Reason Description ===
                if (model.ReasonID == -1 && !string.IsNullOrWhiteSpace(model.ReasonOther))
                {
                    var normalizedOther = Normalize(model.ReasonOther);

                    // Fetch all existing reasons into memory for safe comparison
                    var existingReasons = await _context.Lookup_ReasonDescription.ToListAsync();

                    var existingReason = existingReasons
                        .FirstOrDefault(r => Normalize(r.Reason_Description.Trim().ToLower()) == normalizedOther);

                    if (existingReason != null)
                    {
                        model.ReasonID = existingReason.ReasonID;
                        TempData["Toast"] = $"Reason '{existingReason.Reason_Description}' already exists. Selected automatically.";

                    }
                    else
                    {
                        var newReason = new Lookup_ReasonDescription { Reason_Description = model.ReasonOther.Trim() };
                        _context.Lookup_ReasonDescription.Add(newReason);
                        await _context.SaveChangesAsync();
                        model.ReasonID = newReason.ReasonID;
                    }
                }

                // === Create Request only if ReasonID is valid ===
                if (model.ReasonID.HasValue && model.ReasonID.Value > 0)
                {
                    FollowUpSettings followUpSettings = null;
                    // Get selected status and its follow-up settings
                    var selectedStatus = await _context.LookUpStatusTypes
                        .Include(s => s.FollowUpSettings)
                        .FirstOrDefaultAsync(s => s.StatusId == model.StatusId);

                    if (selectedStatus != null && selectedStatus.FollowUp_SettingID.HasValue)
                    {
                        // Get the specific follow-up setting for this status
                        followUpSettings = await _context.FollowUpSetting
                            .FirstOrDefaultAsync(fs => fs.FollowUp_SettingID == selectedStatus.FollowUp_SettingID.Value);
                    }
                    if (selectedStatus == null)
                    {
                        ModelState.AddModelError("", "Invalid status selected.");
                        await LoadSelectLists(model);
                        return View("Create", model);
                    }
                    var request = new Request
                    {
                        PersonId = person.PersonId,
                        ReasonID = model.ReasonID.Value,
                        Comments = model.Comments,
                        FollowUpCount = 0,
                        CreatedAt = DateTime.Now,
                        CreatedByCode = currentUserId,
                        StatusId = model.StatusId ?? 0
                    
                    };

                    _context.Requests.Add(request);
                    await _context.SaveChangesAsync();

                }
                else
                {
                    TempData["Toast"] = "Request was not created because no valid reason was selected or added.";
                }

                return RedirectToAction("Details", new { id = person.PersonId });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                ModelState.AddModelError("", "An error occurred while saving. Please try again.");
                await LoadSelectLists(model);
                return View("Create", model);
            }
        }

     



        private async Task LoadSelectLists(PersonRequestViewModel model)
        {
            ViewData["CertificateId"] = new SelectList(await _context.LookUpHighSchoolCerts.ToListAsync(), "CertificateId", "CertificateName", model.CertificateId);
           // ViewData["HighSchoolId"] = new SelectList(await _context.LookUpHighSchools.ToListAsync(), "HighSchoolId", "HighSchoolName", model.HighSchoolId);

            var schools = await _context.LookUpHighSchools.ToListAsync();
            schools.Add(new LookUpHighSchool { HighSchoolId = -1, HighSchoolName = "Other" });
            ViewData["HighSchoolId"] = new SelectList(schools, "HighSchoolId", "HighSchoolName", model.HighSchoolId);

            //  ViewData["HowDidYouKnowUsId"] = new SelectList(await _context.LookUpHowDidYouKnowUs.ToListAsync(), "HowDidYouKnowUsId", "HowDidYouKnowUs", model.HowDidYouKnowUsId);


            var howList = await _context.LookUpHowDidYouKnowUs.ToListAsync();

            // Ensure "Other" exists only once
            if (!howList.Any(h => h.HowDidYouKnowUsId == 8))
            {
                howList.Add(new LookUpHowDidYouKnowU
                {
                    HowDidYouKnowUsId = 8,
                    HowDidYouKnowUs = "Other"
                });
            }

            ViewData["HowDidYouKnowUsId"] = new SelectList(howList, "HowDidYouKnowUsId", "HowDidYouKnowUs", model.HowDidYouKnowUsId);

            ViewData["StatusId"] = new SelectList(await _context.LookUpStatusTypes.ToListAsync(), "StatusId", "StatusName", model.StatusId);

            //Load majors for both dropdownsx  x

           var majors = await _context.LookupMajors.ToListAsync();
            ViewData["MajorId"] = new SelectList(majors, "MajorId", "MajorInterest", model.MajorId);
            ViewData["SecondMajorId"] = new SelectList(majors, "MajorId", "MajorInterest", model.SecondMajorId);

            ViewData["UserType"] = new SelectList(new List<SelectListItem>
            {
                 new SelectListItem { Value = "1", Text = "Lead" },
                new SelectListItem { Value = "2", Text = "Guardian" }
              }, "Value", "Text", model.UserType);

            var reasons = await _context.Lookup_ReasonDescription.ToListAsync();
            reasons.Add(new Lookup_ReasonDescription { ReasonID = -1, Reason_Description = "Other" });
            ViewData["ReasonID"] = new SelectList(reasons, "ReasonID", "Reason_Description", model.ReasonID);

            //ViewData["ReasonID"] = new SelectList(await _context.Lookup_ReasonDescription.ToListAsync(), "ReasonID", "Reason_Description", model.ReasonID);
            // NEW dropdowns
            ViewData["CityID"] = new SelectList(await _context.Lookup_City.ToListAsync(), "CityID", "CityName", model.CityID);
            ViewData["GradeID"] = new SelectList(await _context.LookUp_Grade.ToListAsync(), "GradeID", "GradeName", model.GradeID);
            ViewData["NationalityID"] = new SelectList(await _context.Lookup_Nationality.ToListAsync(), "NationalityID", "NationalityName", model.NationalityID);
        }


        //  Add method to get person's major interests for display/edit scenarios
        private async Task<List<int>> GetPersonMajorInterestsAsync(int personId, int? academicSettingId = null)
        {
            var query = _context.MajorPersons
                .Where(mp => mp.PersonID == personId);

            if (academicSettingId.HasValue)
            {
                query = query.Where(mp => mp.Academic_Setting_ID == academicSettingId.Value);
            }
            else
            {
                // Get the current academic setting inline
                var currentSetting = await _context.AcademicSettings
                    .Where(a => a.IsActive == true)
                    .OrderByDescending(a => a.Year)
                    .ThenByDescending(a => a.AcademicSettingId)
                    .FirstOrDefaultAsync();

                if (currentSetting != null)
                {
                    query = query.Where(mp => mp.Academic_Setting_ID == currentSetting.AcademicSettingId);
                }
            }

            // Filter out null values and convert to non-nullable int
            return await query
                .Where(mp => mp.MajorID.HasValue)  // Only include records where MajorID is not null
                .Select(mp => mp.MajorID.Value)    // Convert int? to int using .Value
                .ToListAsync();
        }
        //private async Task<List<int>> GetPersonMajorInterestsAsync(int personId, int? academicSettingId = null)
        //{
        //    var query = _context.MajorPersons
        //        .Where(mp => mp.PersonID == personId);

        //    if (academicSettingId.HasValue)
        //    {
        //        query = query.Where(mp => mp.Academic_Setting_ID == academicSettingId.Value);
        //    }
        //    else
        //    {
        //        // Get the current academic setting if not specified
        //        var currentSettings = await GetCurrentAcademicSettingAsync();
        //        query = query.Where(mp => mp.Academic_Setting_ID == currentSettings.academicSettingId);
        //    }

        //    return await query.Select(mp => mp.MajorID).ToListAsync();
        //}
        //////////////edit 
        ///
        [HttpGet]
        public async Task<IActionResult> EditWithRequest(int id)
        {
            var person = await _context.People
                .Include(p => p.Requests)
                .FirstOrDefaultAsync(p => p.PersonId == id);

            if (person == null)
                return NotFound();

            var userDictionary = await _context.Users
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var model = new PersonRequestViewModel
            {
                PersonID = person.PersonId,
                FirstName = person.FirstName,
                //LastName = person.LastName,
                Email = person.Email,
                Phone = person.Phone,
                NationalId = person.NationalId,
                UserType = person.UserType,
                HighSchoolId = person.HighSchoolId,
                CertificateId = person.CertificateId,
              //  MajorId = person.MajorId,
                
                Person_UpdatedAt = person.UpdatedAt,
                Person_UpdatedByCode = person.UpdatedByCode,
                Person_UpdatedByName = person.UpdatedByCode.HasValue && userDictionary.TryGetValue(person.UpdatedByCode.Value, out var pUpdater) ? pUpdater : "N/A",
                //ReasonID = person.Requests.FirstOrDefault()?.ReasonID ?? "",
                Comments = person.Requests.FirstOrDefault()?.Comments ?? "",
                FollowUpCount = person.Requests.FirstOrDefault()?.FollowUpCount ?? 0,
                StatusId = person.Requests.FirstOrDefault()?.StatusId,
                Person_CreatedByCode =person.Requests.FirstOrDefault()?.CreatedByCode ??0,
                Person_CreatedByName = person.Requests.Any() && userDictionary.TryGetValue(person.Requests.First().CreatedByCode,out var pCreator) 
                ? pCreator : "Unknown",
                Request_CreatedByCode = person.Requests.FirstOrDefault()?.CreatedByCode ?? 0,

                Request_CreatedByName = person.Requests.Any() && userDictionary.TryGetValue(person.Requests.First().CreatedByCode, out var rCreator)
                        ? rCreator
                        : "Unknown",


                Request_UpdatedAt = person.Requests.FirstOrDefault()?.UpdatedAt,
                Request_UpdatedByCode = person.Requests.FirstOrDefault()?.UpdatedByCode,
                Request_UpdatedByName = person.Requests.FirstOrDefault()?.UpdatedByCode.HasValue == true &&
                                        userDictionary.TryGetValue(person.Requests.FirstOrDefault()!.UpdatedByCode!.Value, out var rUpdater)
                                        ? rUpdater : "N/A",
                CityID = person.CityID,
                GradeID = person.GradeID,
                NationalityID = person.NationalityID,
                whatsApp = person.whatsApp,
                HowDidYouKnowUsId = person.HowDidYouKnowUsId,
                HowDidYouKnowUs_Other = !string.IsNullOrEmpty(person.HowDidYouKnowUs_Other)
            ? person.HowDidYouKnowUs_Other
            : null,
                //  HowDidYouKnowUs_Other = person.HowDidYouKnowUs_Other,



                Requests = person.Requests.Select(r => new RequestViewModel
                {
                    RequestId = r.RequestId,
                    ReasonID = r.ReasonID,
                    ReasonDescription_Other = r.Reason?.Reason_Description ?? "N/A",
                    ReasonDescription = r.Reason?.Reason_Description ?? "N/A",
                    Comments = r.Comments,
                    FollowUpCount = r.FollowUpCount??0,
                    StatusId = r.StatusId,
                    CreatedAt = r.CreatedAt,
                    CreatedByName = userDictionary.TryGetValue(r.CreatedByCode, out var created) ? created : "Unknown",
                    UpdatedByName = r.UpdatedByCode.HasValue && userDictionary.TryGetValue(r.UpdatedByCode.Value, out var updated) ? updated : "N/A",
                    
                    UpdatedAt = r.UpdatedAt,
                    UpdatedByCode = r.UpdatedByCode ?? 0, // safely use 0 as fallback
                    
                }).ToList()
            };
            //Fetch and assign existing majors
            var savedMajors = await GetPersonMajorInterestsAsync(person.PersonId);
            if (savedMajors.Count > 0)
                model.MajorId = savedMajors[0];
            if (savedMajors.Count > 1)
                model.SecondMajorId = savedMajors[1];
          
            await LoadSelectLists(model);
            return View("EditWithRequest", model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWithRequest(PersonRequestViewModel model)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);
            var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

            // Trim 'Other' values early to ensure consistent usage
            model.ReasonOther = model.ReasonOther?.Trim();
            model.HowDidYouKnowUs_Other = model.HowDidYouKnowUs_Other?.Trim();
            model.SchoolOther = model.SchoolOther?.Trim(); // Add this line

            // Get current academic setting for validation
            var academicSettings = await GetCurrentAcademicSettingAsync();
            var numberOfInterests = academicSettings.numberOfInterests;
            var academicSettingId = academicSettings.academicSettingId;

            model.MaxNumberOfInterests = numberOfInterests;

            // Add validation for uniqueness (similar to Create method)
            //await ValidateUniqueness(model);
            for (int i = 0; i < model.Requests.Count; i++)
            {
                var req = model.Requests[i];

                if (req.ReasonID == -1 && !string.IsNullOrWhiteSpace(req.ReasonDescription_Other))
                {
                    // Skip required validation on ReasonID for this case
                    ModelState.Remove($"Requests[{i}].ReasonID");
                }
                else if ((req.ReasonID == null || req.ReasonID == -1) && string.IsNullOrWhiteSpace(req.ReasonDescription_Other))
                {
                    // Both fields are missing: show manual error
                    ModelState.AddModelError($"Requests[{i}].ReasonDescription_Other", "Reason is required if no option is selected.");
                }
            }
            ModelState.Remove(nameof(model.ReasonID));
            ModelState.Remove(nameof(model.StatusId));

            //await ValidateUniqueness(model);
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).ToList();
                foreach (var r in model.Requests)
                {
                    Console.WriteLine($"DEBUG: RequestId={r.RequestId}, StatusId={r.StatusId}");
                }

                foreach (var entry in ModelState)
                {
                    if (entry.Key.Contains("StatusId"))
                    {
                        Console.WriteLine($"Key={entry.Key}, RawValue={entry.Value.RawValue}, Errors={string.Join(", ", entry.Value.Errors.Select(e => e.ErrorMessage))}");
                    }
                }
                ModelState.Clear();

                await LoadSelectLists(model);
                return View("EditWithRequest", model);
            }

            var person = await _context.People
                .Include(p => p.Requests)
                .FirstOrDefaultAsync(p => p.PersonId == model.PersonID);

            if (person == null)
                return NotFound();

            // === Handle "Other" High School ===
            if (model.HighSchoolId == -1 && !string.IsNullOrWhiteSpace(model.SchoolOther))
            {
                var trimmedName = model.SchoolOther.Trim().ToLower();

                var existingSchool = await _context.LookUpHighSchools
                    .FirstOrDefaultAsync(h => h.HighSchoolName.Trim().ToLower() == trimmedName);

                if (existingSchool != null)
                {
                    model.HighSchoolId = existingSchool.HighSchoolId;
                    TempData["Toast"] = $"High school '{existingSchool.HighSchoolName}' already exists. Selected automatically.";
                }
                else
                {
                    var newHighSc = new LookUpHighSchool { HighSchoolName = model.SchoolOther.Trim() };
                    _context.LookUpHighSchools.Add(newHighSc);
                    await _context.SaveChangesAsync();
                    model.HighSchoolId = newHighSc.HighSchoolId;
                }
            }

            // Update person
            person.FirstName = model.FirstName;
            //person.LastName = model.LastName;
            person.Email = model.Email;
            person.Phone = model.Phone;
            person.NationalId = model.NationalId;
            person.UserType = model.UserType;
            person.HighSchoolId = model.HighSchoolId;
            person.CertificateId = model.CertificateId ;
            person.CityID = model.CityID;
            person.GradeID = model.GradeID ;
            person.NationalityID = model.NationalityID;
            person.whatsApp = model.whatsApp; // Add this line if missing
            person.UpdatedAt = DateTime.Now;
            person.UpdatedByCode = currentUserId;

            // === Handle "Other" HowDidYouKnowUs ===
            if (model.HowDidYouKnowUsId == 8 && !string.IsNullOrWhiteSpace(model.HowDidYouKnowUs_Other))
            {
                var trimmed = model.HowDidYouKnowUs_Other.Trim();
                var existing = await _context.LookUpHowDidYouKnowUs
                    .FirstOrDefaultAsync(h => h.HowDidYouKnowUs.ToLower() == trimmed.ToLower());

                if (existing != null)
                {
                    person.HowDidYouKnowUsId = existing.HowDidYouKnowUsId;
                    person.HowDidYouKnowUs_Other = null;
                    TempData["Toast"] = $"'{existing.HowDidYouKnowUs}' already exists. Selected automatically.";
                }
                else
                {
                    person.HowDidYouKnowUsId = 8;
                    person.HowDidYouKnowUs_Other = trimmed;
                }
            }
            else
            {
                person.HowDidYouKnowUsId = model.HowDidYouKnowUsId;
                person.HowDidYouKnowUs_Other = null;
            }

            // Update each request from the form
            foreach (var requestVm in model.Requests)
            {
                var existingRequest = person.Requests.FirstOrDefault(r => r.RequestId == requestVm.RequestId);
                if (existingRequest == null)
                    continue;

                if (requestVm.ReasonID == -1 && !string.IsNullOrWhiteSpace(requestVm.ReasonDescription_Other))
                {
                    var trimmed = requestVm.ReasonDescription_Other.Trim();

                    var existingReason = await _context.Lookup_ReasonDescription
                        .FirstOrDefaultAsync(r => r.Reason_Description.ToLower() == trimmed.ToLower());

                    if (existingReason != null)
                    {
                        existingRequest.ReasonID = existingReason.ReasonID;
                        requestVm.ReasonDescription = existingReason.Reason_Description;
                        TempData["Toast"] = $"'{existingReason.Reason_Description}' already exists. Selected automatically.";
                    }
                    else
                    {
                        var newReason = new Lookup_ReasonDescription { Reason_Description = trimmed };
                        _context.Lookup_ReasonDescription.Add(newReason);
                        await _context.SaveChangesAsync();

                        existingRequest.ReasonID = newReason.ReasonID;
                        requestVm.ReasonDescription = newReason.Reason_Description;
                    }
                }
                else
                {
                    // Save ReasonID and ReasonDescription from dropdown
                    existingRequest.ReasonID = requestVm.ReasonID;
                  
                }

                // Always update these fields
                existingRequest.StatusId = requestVm.StatusId;
                existingRequest.Comments = requestVm.Comments;
                existingRequest.UpdatedAt = DateTime.Now;
                existingRequest.UpdatedByCode = currentUserId;
            }

            // Get current academic setting
            var currentSetting = await GetCurrentAcademicSettingAsync();

           
            var existingMajors = await _context.MajorPersons
                .Where(mp => mp.PersonID == model.PersonID && mp.Academic_Setting_ID == currentSetting.academicSettingId)
                .ToListAsync();

            _context.MajorPersons.RemoveRange(existingMajors);

            // Add the primary major if selected
            if (model.MajorId.HasValue && model.MajorId.Value > 0)
            {
                _context.MajorPersons.Add(new MajorPerson
                {
                    PersonID = model.PersonID,
                    MajorID = model.MajorId.Value,
                    Academic_Setting_ID = currentSetting.academicSettingId,
                    Priority = 1
                });
            }

            // Add the second major if selected and different from the primary (only if allowed)
            if (numberOfInterests >= 2 && model.SecondMajorId.HasValue && model.SecondMajorId.Value > 0 &&
                model.SecondMajorId != model.MajorId)
            {
                _context.MajorPersons.Add(new MajorPerson
                {
                    PersonID = model.PersonID,
                    MajorID = model.SecondMajorId.Value,
                    Academic_Setting_ID = currentSetting.academicSettingId,
                    Priority = 2
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = model.PersonID });
        }




        // GET: Edit Person Data Only
        [HttpGet]
        public async Task<IActionResult> EditPerson(int id)
        {
            var person = await _context.People
                .FirstOrDefaultAsync(p => p.PersonId == id);

            if (person == null)
                return NotFound();

            var userDictionary = await _context.Users
                 .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var model = new PersonRequestViewModel
            {
                PersonID = person.PersonId,
                FirstName = person.FirstName,
                //LastName = person.LastName,
                Email = person.Email,
                Phone = person.Phone,
                NationalId = person.NationalId,
                UserType = person.UserType,
                HighSchoolId = person.HighSchoolId,
                CertificateId = person.CertificateId,
                CityID = person.CityID,
                GradeID = person.GradeID,
                NationalityID = person.NationalityID,
                whatsApp = person.whatsApp,
                HowDidYouKnowUsId = person.HowDidYouKnowUsId,
                HowDidYouKnowUs_Other = !string.IsNullOrEmpty(person.HowDidYouKnowUs_Other)
                    ? person.HowDidYouKnowUs_Other
                    : null,
                Person_CreatedByCode=person.CreatedByCode,
                Person_CreatedByName = userDictionary.GetValueOrDefault(person.CreatedByCode, "Unknown"),
                Person_UpdatedAt = person.UpdatedAt,
                Person_UpdatedByCode = person.UpdatedByCode,
                Person_UpdatedByName = person.UpdatedByCode.HasValue && userDictionary.TryGetValue(person.UpdatedByCode.Value, out var updater)
                    ? updater : "N/A"
            };
            //Fetch and assign existing majors
            var savedMajors = await GetPersonMajorInterestsAsync(person.PersonId);
            if (savedMajors.Count > 0)
                model.MajorId = savedMajors[0];
            if (savedMajors.Count > 1)
                model.SecondMajorId = savedMajors[1];

            await LoadSelectLists(model);
            return View("EditPerson", model);
        }

        // POST: Edit Person Data Only
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPerson(PersonRequestViewModel model)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);

            // Trim 'Other' values early to ensure consistent usage
            model.HowDidYouKnowUs_Other = model.HowDidYouKnowUs_Other?.Trim();
            model.SchoolOther = model.SchoolOther?.Trim();

            // Get current academic setting for validation
            var academicSettings = await GetCurrentAcademicSettingAsync();
            var numberOfInterests = academicSettings.numberOfInterests;
            var academicSettingId = academicSettings.academicSettingId;

            model.MaxNumberOfInterests = numberOfInterests;

            // Remove validation for request-specific fields since we're only editing person data
            ModelState.Remove(nameof(model.ReasonID));
            ModelState.Remove(nameof(model.StatusId));
            ModelState.Remove(nameof(model.Comments));
            ModelState.Remove(nameof(model.Request_CreatedByCode));
            ModelState.Remove(nameof(model.Person_CreatedByCode));

         

            if (!ModelState.IsValid)
            {
                await LoadSelectLists(model);
                return View("EditPerson", model);
            }

            var person = await _context.People
                .FirstOrDefaultAsync(p => p.PersonId == model.PersonID);

            if (person == null)
                return NotFound();

            // === Handle "Other" High School ===
            if (model.HighSchoolId == -1 && !string.IsNullOrWhiteSpace(model.SchoolOther))
            {
                var trimmedName = model.SchoolOther.Trim().ToLower();

                var existingSchool = await _context.LookUpHighSchools
                    .FirstOrDefaultAsync(h => h.HighSchoolName.Trim().ToLower() == trimmedName);

                if (existingSchool != null)
                {
                    model.HighSchoolId = existingSchool.HighSchoolId;
                    TempData["Toast"] = $"High school '{existingSchool.HighSchoolName}' already exists. Selected automatically.";
                }
                else
                {
                    var newHighSchool = new LookUpHighSchool { HighSchoolName = model.SchoolOther.Trim() };
                    _context.LookUpHighSchools.Add(newHighSchool);
                    await _context.SaveChangesAsync();
                    model.HighSchoolId = newHighSchool.HighSchoolId;
                }
            }

            // === Handle "Other" HowDidYouKnowUs ===
            if (model.HowDidYouKnowUsId == 8 && !string.IsNullOrWhiteSpace(model.HowDidYouKnowUs_Other))
            {
                var trimmed = model.HowDidYouKnowUs_Other.Trim();
                var existing = await _context.LookUpHowDidYouKnowUs
                    .FirstOrDefaultAsync(h => h.HowDidYouKnowUs.ToLower() == trimmed.ToLower());

                if (existing != null)
                {
                    person.HowDidYouKnowUsId = existing.HowDidYouKnowUsId;
                    person.HowDidYouKnowUs_Other = null;
                    TempData["Toast"] = $"'{existing.HowDidYouKnowUs}' already exists. Selected automatically.";
                }
                else
                {
                    person.HowDidYouKnowUsId = 8;
                    person.HowDidYouKnowUs_Other = trimmed;
                }
            }
            else
            {
                person.HowDidYouKnowUsId = model.HowDidYouKnowUsId ?? 0;
                person.HowDidYouKnowUs_Other = null;
            }

            // Update person properties
            person.FirstName = model.FirstName;
           // person.LastName = model.LastName;
            person.Email = model.Email;
            person.Phone = model.Phone;
            person.NationalId = model.NationalId;
            person.UserType = model.UserType;
            person.HighSchoolId = model.HighSchoolId ?? 0;
            person.CertificateId = model.CertificateId ?? 0;
            person.CityID = model.CityID ?? 0;
            person.GradeID = model.GradeID ?? 0;
            person.NationalityID = model.NationalityID ?? 0;
            person.whatsApp = model.whatsApp;
            person.UpdatedAt = DateTime.Now;
            person.UpdatedByCode = currentUserId;

            // Handle Major Interests
            var currentSetting = await GetCurrentAcademicSettingAsync();

            // Remove existing majors for this person in the current academic setting
            var existingMajors = await _context.MajorPersons
                .Where(mp => mp.PersonID == model.PersonID && mp.Academic_Setting_ID == currentSetting.academicSettingId)
                .ToListAsync();

            _context.MajorPersons.RemoveRange(existingMajors);

            // Add the primary major if selected
            if (model.MajorId.HasValue && model.MajorId.Value > 0)
            {
                _context.MajorPersons.Add(new MajorPerson
                {
                    PersonID = model.PersonID,
                    MajorID = model.MajorId.Value,
                    Academic_Setting_ID = currentSetting.academicSettingId,
                    Priority = 1
                });
            }

            // Add the second major if selected and different from the primary (only if allowed)
            if (numberOfInterests >= 2 && model.SecondMajorId.HasValue && model.SecondMajorId.Value > 0 &&
                model.SecondMajorId != model.MajorId)
            {
                _context.MajorPersons.Add(new MajorPerson
                {
                    PersonID = model.PersonID,
                    MajorID = model.SecondMajorId.Value,
                    Academic_Setting_ID = currentSetting.academicSettingId,
                    Priority = 2
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Person data updated successfully.";
            return RedirectToAction("Details", new { id = model.PersonID });
        }


        // [HttpGet]
        // public async Task<IActionResult> GetAllRequests(
        //string? reasonFilter = null,
        //string? statusFilter = null,
        //string? createdByFilter = null,
        //string? followUpFilter = null,
        //DateTime? createdFrom = null,
        //DateTime? createdTo = null,
        //DateTime? lastFollowUpFrom = null,
        //DateTime? lastFollowUpTo = null,
        //int page = 1,
        //int pageSize = 10,
        //string sortBy = "CreatedAt",
        //string sortOrder = "desc")
        // {
        //     // Get follow-up settings
        //     var followUpSettings = await _context.FollowUpSetting.FirstOrDefaultAsync();

        //     // Get user dictionary for name lookup
        //     var userDictionary = await _context.Users
        //         .ToDictionaryAsync(u => u.UserId, u => u.FullName);

        //     // Build the base query
        //     var query = _context.Requests
        //         .Include(r => r.Person)
        //         .Include(r => r.Status)
        //         .Include(r => r.Reason)
        //         .Where(r => r.Person != null) // Only include requests with valid persons
        //         .AsQueryable();

        //     // Apply filters
        //     query = ApplyRequestFilters(query, reasonFilter, statusFilter, createdByFilter,
        //         followUpFilter, createdFrom, createdTo, lastFollowUpFrom, lastFollowUpTo);

        //     // Apply sorting
        //     query = ApplyRequestSorting(query, sortBy, sortOrder);

        //     // Create pagination request
        //     var PaginationModels = new PaginationModels
        //     {
        //         Page = page,
        //         PageSize = pageSize,
        //         SortBy = sortBy,
        //         SortOrder = sortOrder
        //     };

        //     // Apply pagination with projection to RequestViewModel
        //     var result = await _PaginationService.PaginateAsync(query, PaginationModels,
        //         r => new RequestViewModel
        //         {
        //             RequestId = r.RequestId,
        //             PersonId = r.PersonId,
        //             person_FullName = r.Person.FirstName,
        //             ReasonID = r.ReasonID,
        //             ReasonDescription = r.Reason != null ? r.Reason.Reason_Description : "N/A",
        //             Comments = r.Comments,
        //             FollowUpCount = r.FollowUpCount ?? 0,
        //             LastFollowUpDate = _context.FollowUp_Log
        //                 .Where(f => f.RequestId == r.RequestId)
        //                 .OrderByDescending(f => f.UpdatedAt)
        //                 .Select(f => f.UpdatedAt)
        //                 .FirstOrDefault(),
        //             FollowUpTypeName = _context.FollowUp_Log
        //                                 .Where(f => f.RequestId == r.RequestId)
        //                                 .OrderByDescending(f => f.UpdatedAt)
        //                                 .Select(f => f.FollowUpType.FollowUpName)
        //                                 .FirstOrDefault() ?? "N/A",
        //             CreatedAt = r.CreatedAt,
        //             CreatedbyCode = r.CreatedByCode,
        //             CreatedByName = userDictionary.GetValueOrDefault(r.CreatedByCode, "Unknown"),
        //             UpdatedAt = r.UpdatedAt,
        //             StatusId = r.StatusId,
        //             StatusName = r.Status != null ? r.Status.StatusName : "Unknown",
        //             HasFollowUps = _context.FollowUp_Log.Any(f => f.RequestId == r.RequestId)
        //         });

        //     // Calculate follow-up requirements for each request in the current page
        //     var followUpRequirements = new Dictionary<int, object>();

        //     if (followUpSettings != null)
        //     {
        //         foreach (var request in result.Items)
        //         {
        //             var requiresFollowUp = false;
        //             var isOverdue = false;
        //             var daysSinceLastFollowUp = 0;
        //             var nextFollowUpDue = DateTime.MinValue;

        //             // Only check follow-up requirements for requests with StatusId = 1
        //             if (request.StatusId == 1)
        //             {
        //                 // Calculate days since last follow-up or creation
        //                 var referenceDate = request.LastFollowUpDate ?? request.CreatedAt;
        //                 daysSinceLastFollowUp = (DateTime.Now - referenceDate).Days;

        //                 // Check if follow-up is required
        //                 var hasReachedMaxFollowUps = request.FollowUpCount >= followUpSettings.MaxFollowUps;
        //                 var isIntervalPassed = daysSinceLastFollowUp >= followUpSettings.FollowUpIntervalDays;

        //                 requiresFollowUp = !hasReachedMaxFollowUps && isIntervalPassed;

        //                 // Calculate next follow-up due date
        //                 nextFollowUpDue = referenceDate.AddDays(followUpSettings.FollowUpIntervalDays);
        //                 isOverdue = DateTime.Now > nextFollowUpDue && !hasReachedMaxFollowUps;
        //             }

        //             followUpRequirements[request.RequestId] = new
        //             {
        //                 RequiresFollowUp = requiresFollowUp,
        //                 IsOverdue = isOverdue,
        //                 DaysSinceLastFollowUp = daysSinceLastFollowUp,
        //                 NextFollowUpDue = nextFollowUpDue,
        //                 HasReachedMaxFollowUps = request.FollowUpCount >= (followUpSettings?.MaxFollowUps ?? 0)
        //             };
        //         }
        //     }

        //     // Prepare dropdown data
        //     ViewBag.AllStatuses = await _context.LookUpStatusTypes
        //         .OrderBy(s => s.StatusName)
        //         .Select(s => s.StatusName)
        //         .ToListAsync();

        //     ViewBag.AllReasons = await _context.Lookup_ReasonDescription
        //         .OrderBy(r => r.Reason_Description)
        //         .Select(r => r.Reason_Description)
        //         .ToListAsync();

        //     ViewBag.AllCreators = await _context.Users
        //         .OrderBy(u => u.FullName)
        //         .Select(u => u.FullName)
        //         .ToListAsync();

        //     // Pass data to view
        //     ViewBag.Pagination = result.Pagination;
        //     ViewBag.FollowUpRequirements = followUpRequirements;
        //     ViewBag.FollowUpSettings = followUpSettings;
        //     ViewBag.CurrentFilters = new
        //     {
        //         ReasonFilter = reasonFilter,
        //         StatusFilter = statusFilter,
        //         CreatedByFilter = createdByFilter,
        //         FollowUpFilter = followUpFilter,
        //         CreatedFrom = createdFrom,
        //         CreatedTo = createdTo,
        //         LastFollowUpFrom = lastFollowUpFrom,
        //         LastFollowUpTo = lastFollowUpTo,
        //         SortBy = sortBy,
        //         SortOrder = sortOrder
        //     };

        //     return View("GetAllRequests", result.Items);
        // }

        // Helper method to create fallback follow-up data
        private object CreateFallbackFollowUpData(string errorMessage)
        {
            return new
            {
                RequiresFollowUp = false,
                IsOverdue = false,
                MaxFollowUpsReached = false,
                StatusText = errorMessage,
                Settings = (object?)null,
                IsManualFollowUp = false,
                FollowUpType = "Unknown",
                CanFollowUp = false,
                IsScheduledFollowUp = false,
                IsProblemSolvingFollowUp = false,
                HasAnyFollowUps = false,
                DisplayText = errorMessage
            };
        }
        public async Task<IActionResult> GetAllRequests(
        string? reasonFilter = null,
        string? statusFilter = null,
        string? createdByFilter = null,
        string? followUpFilter = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        DateTime? lastFollowUpFrom = null,
        DateTime? lastFollowUpTo = null,
        int page = 1,
        int pageSize = 10,
        string sortBy = "CreatedAt",
        string sortOrder = "desc")
        {
            try
            {
                // Clean up filters
                reasonFilter = string.IsNullOrWhiteSpace(reasonFilter) ? null : reasonFilter;
                statusFilter = string.IsNullOrWhiteSpace(statusFilter) ? null : statusFilter;
                createdByFilter = string.IsNullOrWhiteSpace(createdByFilter) ? null : createdByFilter;
                followUpFilter = string.IsNullOrWhiteSpace(followUpFilter) ? null : followUpFilter;

                // ====== OPTIMIZED: Get all data in bulk with single database queries ======

                // Get all requests with necessary includes in one query
                var query = _context.Requests
                    .Include(r => r.Reason)
                    .Include(r => r.Status)
                        .ThenInclude(s => s.FollowUpSettings) // Include follow-up settings
                    .Include(r => r.Person)
                    .AsQueryable();

                // Apply basic filters to reduce dataset early
                if (!string.IsNullOrEmpty(reasonFilter))
                    query = query.Where(r => r.Reason.Reason_Description.Contains(reasonFilter));

                if (!string.IsNullOrEmpty(statusFilter))
                    query = query.Where(r => r.Status.StatusName.Contains(statusFilter));

                if (!string.IsNullOrEmpty(createdByFilter))
                {
                    var creatorUserIds = await _context.Users
                        .Where(u => u.FullName.Contains(createdByFilter))
                        .Select(u => u.UserId)
                        .ToListAsync();
                    query = query.Where(r => creatorUserIds.Contains(r.CreatedByCode));
                }

                // Date filters
                if (createdFrom.HasValue)
                    query = query.Where(r => r.CreatedAt >= createdFrom.Value);
                if (createdTo.HasValue)
                    query = query.Where(r => r.CreatedAt < createdTo.Value.Date.AddDays(1));
                if (lastFollowUpFrom.HasValue)
                    query = query.Where(r => r.LastFollowUpDate >= lastFollowUpFrom.Value);
                if (lastFollowUpTo.HasValue)
                    query = query.Where(r => r.LastFollowUpDate < lastFollowUpTo.Value.Date.AddDays(1));

                // Execute query once
                var allRequests = await query.ToListAsync();

                // ====== OPTIMIZED: Bulk process follow-up filtering ======
                var filteredRequests = new List<Request>();

                // Group requests by status to batch process follow-up logic
                var requestsByStatus = allRequests
                    .Where(r => r.StatusId.HasValue && r.Status != null)
                    .GroupBy(r => new { r.StatusId, r.Status.RequireFollowUp, r.Status.FollowUpSettings })
                    .ToList();

                foreach (var statusGroup in requestsByStatus)
                {
                    var statusId = statusGroup.Key.StatusId.Value;
                    var requiresFollowUp = statusGroup.Key.RequireFollowUp;
                    var settings = statusGroup.Key.FollowUpSettings;

                    // Skip if status doesn't require follow-up
                    if (!requiresFollowUp) continue;

                    foreach (var request in statusGroup)
                    {
                        try
                        {
                            bool shouldInclude = ShouldIncludeInFollowUpFilter(
                                request,
                                settings,
                                followUpFilter
                            );

                            if (shouldInclude)
                            {
                                filteredRequests.Add(request);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error filtering request {request.RequestId}: {ex.Message}");
                            // Include on error if no specific filter
                            if (string.IsNullOrEmpty(followUpFilter))
                                filteredRequests.Add(request);
                        }
                    }
                }

                // Apply sorting
                var sortedRequests = ApplySorting(filteredRequests, sortBy, sortOrder);

                // Apply pagination
                var totalCount = sortedRequests.Count;
                var paginatedRequests = sortedRequests
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // ====== OPTIMIZED: Bulk get user names ======
                var userIds = paginatedRequests
                    .SelectMany(r => new[] { r.CreatedByCode, r.UpdatedByCode ?? 0 })
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                var userDictionary = await _context.Users
                    .Where(u => userIds.Contains(u.UserId))
                    .ToDictionaryAsync(u => u.UserId, u => u.FullName);

                // ====== OPTIMIZED: Bulk get follow-up logs ======
                var requestIds = paginatedRequests.Select(r => r.RequestId).ToList();
                var requestsWithFollowUps = await _context.FollowUp_Log
                    .Where(f => requestIds.Contains(f.RequestId))
                    .Select(f => f.RequestId)
                    .Distinct()
                    .ToListAsync();

                // ====== OPTIMIZED: Bulk create follow-up data ======
                var requestFollowUpData = new Dictionary<int, object>();

                foreach (var request in paginatedRequests)
                {
                    try
                    {
                        var followUpData = CreateFollowUpData(request);
                        requestFollowUpData[request.RequestId] = followUpData;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error creating follow-up data for request {request.RequestId}: {ex.Message}");
                        requestFollowUpData[request.RequestId] = CreateFallbackFollowUpData("Error loading follow-up info");
                    }
                }

                // Create view models
                var requestViewModels = paginatedRequests.Select(r => new RequestViewModel
                {
                    RequestId = r.RequestId,
                    ReasonID = r.ReasonID,
                    ReasonDescription = r.Reason?.Reason_Description ?? "N/A",
                    Comments = r.Comments ?? "",
                    FollowUpCount = r.FollowUpCount ?? 0,
                    LastFollowUpDate = r.LastFollowUpDate,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    CreatedByName = userDictionary.TryGetValue(r.CreatedByCode, out var created) ? created : "Unknown",
                    UpdatedByName = r.UpdatedByCode.HasValue && userDictionary.TryGetValue(r.UpdatedByCode.Value, out var updated) ? updated : "N/A",
                    StatusId = r.StatusId ?? 0,
                    StatusName = r.Status?.StatusName ?? "N/A",
                    HasFollowUps = requestsWithFollowUps.Contains(r.RequestId),
                    person_FullName = r.Person?.FirstName ?? "N/A"
                }).ToList();

                // ====== REST OF YOUR EXISTING CODE FOR VIEWBAG SETUP ======
                // ... (pagination, filters, etc.)

                var paginationInfo = new PaginationInfo
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    StartItem = totalCount == 0 ? 0 : ((page - 1) * pageSize) + 1,
                    EndItem = Math.Min(page * pageSize, totalCount)
                };

                ViewBag.Pagination = paginationInfo;
                ViewBag.RequestFollowUpData = requestFollowUpData;
                // ... other ViewBag assignments

                return View("GetAllRequests", requestViewModels);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAllRequests: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while loading requests. Please try again.";
                return View("GetAllRequests", new List<RequestViewModel>());
            }
        }

        // Helper method for optimized follow-up filtering
        private bool ShouldIncludeInFollowUpFilter(Request request, FollowUpSettings settings, string followUpFilter)
        {
            if (settings == null) return false;

            var followUpCount = request.FollowUpCount ?? 0;
            var maxFollowUps = settings.MaxFollowUps;
            var intervalDays = settings.FollowUpIntervalDays;

            // Check max follow-ups reached
            bool maxReached = followUpCount >= maxFollowUps;

            // Check if overdue (only for scheduled follow-ups)
            bool isOverdue = false;
            if (intervalDays > 0)
            {
                var lastDate = request.LastFollowUpDate ?? request.CreatedAt;
                var daysSince = (DateTime.Now - lastDate).Days;
                isOverdue = daysSince >= intervalDays;
            }

            return followUpFilter switch
            {
                "requires_followup" => !maxReached,
                "overdue_followup" => isOverdue && !maxReached,
                "max_followups_reached" => maxReached,
                "with_followups" => followUpCount > 0,
                "no_followups" => followUpCount == 0,
                _ => !maxReached // Default: show if can still follow up
            };
        }

        // Helper method to create follow-up data without async calls
        private object CreateFollowUpData(Request request)
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
                    Settings = (object)null,
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

        // Helper method for sorting
        private List<Request> ApplySorting(List<Request> requests, string sortBy, string sortOrder)
        {
            return sortBy.ToLower() switch
            {
                "name" => sortOrder == "asc"
                    ? requests.OrderBy(r => r.Person?.FirstName ?? "").ToList()
                    : requests.OrderByDescending(r => r.Person?.FirstName ?? "").ToList(),
                "createdat" => sortOrder == "asc"
                    ? requests.OrderBy(r => r.CreatedAt).ToList()
                    : requests.OrderByDescending(r => r.CreatedAt).ToList(),
                "lastfollowup" => sortOrder == "asc"
                    ? requests.OrderBy(r => r.LastFollowUpDate ?? DateTime.MinValue).ToList()
                    : requests.OrderByDescending(r => r.LastFollowUpDate ?? DateTime.MinValue).ToList(),
                "followups" => sortOrder == "asc"
                    ? requests.OrderBy(r => r.FollowUpCount ?? 0).ToList()
                    : requests.OrderByDescending(r => r.FollowUpCount ?? 0).ToList(),
                _ => requests.OrderByDescending(r => r.CreatedAt).ToList()
            };
        }
        // 4. HELPER METHOD FOR APPLYING FILTERS
        private IQueryable<Request> ApplyRequestFilters(IQueryable<Request> query,
            string? reasonFilter, string? statusFilter, string? createdByFilter,
            string? followUpFilter, DateTime? createdFrom, DateTime? createdTo,
            DateTime? lastFollowUpFrom, DateTime? lastFollowUpTo)
        {
            // Reason filter
            if (!string.IsNullOrEmpty(reasonFilter))
                query = query.Where(r => r.Reason.Reason_Description == reasonFilter);

            // Status filter
            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(r => r.Status.StatusName == statusFilter);

            // Created by filter
            if (!string.IsNullOrEmpty(createdByFilter))
            {
                var userIds = _context.Users
                    .Where(u => u.FullName == createdByFilter)
                    .Select(u => u.UserId)
                    .ToList();
                query = query.Where(r => userIds.Contains(r.CreatedByCode));
            }

            // Created date filters
            if (createdFrom.HasValue)
                query = query.Where(r => r.CreatedAt >= createdFrom);

            if (createdTo.HasValue)
                query = query.Where(r => r.CreatedAt <= createdTo.Value.AddDays(1));

            // Last follow-up date filters
            if (lastFollowUpFrom.HasValue || lastFollowUpTo.HasValue)
            {
                var requestsWithFollowUps = _context.FollowUp_Log
                    .GroupBy(f => f.RequestId)
                    .Select(g => new
                    {
                        RequestId = g.Key,
                        LastFollowUpDate = g.Max(f => f.UpdatedAt)
                    });

                if (lastFollowUpFrom.HasValue)
                {
                    var validRequestIds = requestsWithFollowUps
                        .Where(f => f.LastFollowUpDate >= lastFollowUpFrom)
                        .Select(f => f.RequestId);
                    query = query.Where(r => validRequestIds.Contains(r.RequestId));
                }

                if (lastFollowUpTo.HasValue)
                {
                    var validRequestIds = requestsWithFollowUps
                        .Where(f => f.LastFollowUpDate <= lastFollowUpTo.Value.AddDays(1))
                        .Select(f => f.RequestId);
                    query = query.Where(r => validRequestIds.Contains(r.RequestId));
                }
            }

            // Follow-up specific filters
            if (!string.IsNullOrEmpty(followUpFilter))
            {
                var followUpSettings = _context.FollowUpSetting.FirstOrDefault();
                if (followUpSettings != null)
                {
                    switch (followUpFilter)
                    {
                        case "requires_followup":
                            query = query.Where(r => r.StatusId == 1 &&
                                r.FollowUpCount < followUpSettings.MaxFollowUps);
                            break;
                        case "overdue_followup":
                            var cutoffDate = DateTime.Now.AddDays(-followUpSettings.FollowUpIntervalDays);
                            query = query.Where(r => r.StatusId == 1 &&
                                r.FollowUpCount < followUpSettings.MaxFollowUps &&
                                (r.UpdatedAt <= cutoffDate ||
                                 !_context.FollowUp_Log.Any(f => f.RequestId == r.RequestId && f.UpdatedAt > cutoffDate)));
                            break;
                        case "max_followups_reached":
                            query = query.Where(r => r.FollowUpCount >= followUpSettings.MaxFollowUps);
                            break;
                        case "with_followups":
                            query = query.Where(r => _context.FollowUp_Log.Any(f => f.RequestId == r.RequestId));
                            break;
                        case "no_followups":
                            query = query.Where(r => !_context.FollowUp_Log.Any(f => f.RequestId == r.RequestId));
                            break;
                    }
                }
            }

            return query;
        }

        // 5. HELPER METHOD FOR APPLYING SORTING
        private IQueryable<Request> ApplyRequestSorting(IQueryable<Request> query, string sortBy, string sortOrder)
        {
            var isAscending = sortOrder?.ToLower() == "asc";

            return sortBy?.ToLower() switch
            {
                "name" => isAscending
                    ? query.OrderBy(r => r.Person.FirstName)
                    : query.OrderByDescending(r => r.Person.FirstName),
                "reason" => isAscending
                    ? query.OrderBy(r => r.Reason.Reason_Description)
                    : query.OrderByDescending(r => r.Reason.Reason_Description),
                "status" => isAscending
                    ? query.OrderBy(r => r.Status.StatusName)
                    : query.OrderByDescending(r => r.Status.StatusName),
                "followups" => isAscending
                    ? query.OrderBy(r => r.FollowUpCount)
                    : query.OrderByDescending(r => r.FollowUpCount),
                "lastfollowup" => isAscending
                    ? query.OrderBy(r => _context.FollowUp_Log
                        .Where(f => f.RequestId == r.RequestId)
                        .Max(f => (DateTime?)f.UpdatedAt) ?? DateTime.MinValue)
                    : query.OrderByDescending(r => _context.FollowUp_Log
                        .Where(f => f.RequestId == r.RequestId)
                        .Max(f => (DateTime?)f.UpdatedAt) ?? DateTime.MinValue),
                "createdby" => isAscending
                    ? query.OrderBy(r => r.CreatedByCode)
                    : query.OrderByDescending(r => r.CreatedByCode),
                _ => isAscending
                    ? query.OrderBy(r => r.CreatedAt)
                    : query.OrderByDescending(r => r.CreatedAt)
            };
        }



        [HttpGet]
        public async Task<IActionResult> EditRequest(int id)
        {
            var request = await _context.Requests
                .Include(r => r.Reason)
                .Include(r => r.Status)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null)
                return NotFound();

            // Check if request has any follow-ups - if yes, prevent editing
            var hasFollowUps = await _context.FollowUp_Log
                .AnyAsync(f => f.RequestId == id);

            if (hasFollowUps)
            {
                TempData["ErrorMessage"] = "This request cannot be edited because it has follow-up records.";
                return RedirectToAction("Details", new { id = request.PersonId });
            }

            var userDictionary = await _context.Users
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var model = new RequestViewModel
            {
                RequestId = request.RequestId,
                PersonId = request.PersonId,
                ReasonID = request.ReasonID,
                ReasonDescription = request.Reason?.Reason_Description ?? "N/A",
                Comments = request.Comments,
                FollowUpCount = request.FollowUpCount ?? 0,
                StatusId = request.StatusId,
                StatusName = request.Status?.StatusName ?? "N/A",
                CreatedAt = request.CreatedAt,
                CreatedbyCode = request.CreatedByCode,
                CreatedByName = userDictionary.TryGetValue(request.CreatedByCode, out var creator) ? creator : "Unknown",
                UpdatedAt = request.UpdatedAt,
                UpdatedByCode = request.UpdatedByCode ?? 0,
                UpdatedByName = request.UpdatedByCode.HasValue && userDictionary.TryGetValue(request.UpdatedByCode.Value, out var updater) ? updater : "N/A"
            };

            await LoadRequestSelectLists(model);
            return View("EditRequest", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRequest(RequestViewModel model)
        {
            // Check if request has any follow-ups before allowing edit
            var hasFollowUps = await _context.FollowUp_Log
                .AnyAsync(f => f.RequestId == model.RequestId);

            if (hasFollowUps)
            {
                TempData["ErrorMessage"] = "This request cannot be edited because it has follow-up records.";
                return RedirectToAction("Details", new { id = model.PersonId });
            }

            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(currentUserId);
            var currentUserName = user?.FullName ?? user?.Username ?? "Current User";

            // Trim 'Other' values early to ensure consistent usage
            model.ReasonDescription_Other = model.ReasonDescription_Other?.Trim();

            // Handle validation for ReasonID and ReasonDescription_Other
            if (model.ReasonID == -1 && !string.IsNullOrWhiteSpace(model.ReasonDescription_Other))
            {
                // Skip required validation on ReasonID for this case
                ModelState.Remove(nameof(model.ReasonID));
            }
            else if ((model.ReasonID == null || model.ReasonID == -1) && string.IsNullOrWhiteSpace(model.ReasonDescription_Other))
            {
                // Both fields are missing: show manual error
                ModelState.AddModelError(nameof(model.ReasonDescription_Other), "Reason is required if no option is selected.");
            }

            if (!ModelState.IsValid)
            {
                await LoadRequestSelectLists(model);
                return View("EditRequest", model);
            }

            var request = await _context.Requests
                .FirstOrDefaultAsync(r => r.RequestId == model.RequestId);

            if (request == null)
                return NotFound();

            // Handle "Other" reason description
            if (model.ReasonID == -1 && !string.IsNullOrWhiteSpace(model.ReasonDescription_Other))
            {
                var trimmed = model.ReasonDescription_Other.Trim();

                var existingReason = await _context.Lookup_ReasonDescription
                    .FirstOrDefaultAsync(r => r.Reason_Description.ToLower() == trimmed.ToLower());

                if (existingReason != null)
                {
                    request.ReasonID = existingReason.ReasonID;
                    TempData["Toast"] = $"'{existingReason.Reason_Description}' already exists. Selected automatically.";
                }
                else
                {
                    var newReason = new Lookup_ReasonDescription { Reason_Description = trimmed };
                    _context.Lookup_ReasonDescription.Add(newReason);
                    await _context.SaveChangesAsync();

                    request.ReasonID = newReason.ReasonID;
                }
            }
            else
            {
                // Save ReasonID from dropdown
                request.ReasonID = model.ReasonID;
            }

            // Update request fields
            request.StatusId = model.StatusId;
            request.Comments = model.Comments;
            request.UpdatedAt = DateTime.Now;
            request.UpdatedByCode = currentUserId;

            await _context.SaveChangesAsync();

            // Redirect back to person details
            return RedirectToAction("Details", new { id = model.PersonId });
        }
      
        private async Task LoadRequestSelectLists(RequestViewModel model)
        {
            // Load reasons for dropdown
            ViewBag.Reasons = await _context.Lookup_ReasonDescription
                .Select(r => new SelectListItem
                {
                    Value = r.ReasonID.ToString(),
                    Text = r.Reason_Description,
                    Selected = r.ReasonID == model.ReasonID
                })
                .ToListAsync();

            // Add "Other" option to reasons
            ViewBag.Reasons.Add(new SelectListItem
            {
                Value = "-1",
                Text = "Other",
                Selected = model.ReasonID == -1
            });

            // Load statuses for dropdown
            ViewBag.Statuses = await _context.LookUpStatusTypes
                .Select(s => new SelectListItem
                {
                    Value = s.StatusId.ToString(),
                    Text = s.StatusName,
                    Selected = s.StatusId == model.StatusId
                })
                .ToListAsync();
        }








    }
}
