using CRM.Models;
using CRM.ViewModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CRM.FuncModels;
using CRM.Extensions;

namespace CRM.Services
{
    public class FollowUpAutomationService : IFollowUpService
    {
        private readonly CallCenterContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FollowUpAutomationService(CallCenterContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Gets the current logged-in user's ID from HttpContext
        /// </summary>
        private int GetCurrentUserId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                return httpContext.User.GetUserId();
            }
            return 0;
        }

        /// <summary>
        /// Gets follow-up settings for a specific status
        /// </summary>
        private async Task<FollowUpSettings?> GetFollowUpSettingsForStatusAsync(int statusId)
        {
            var status = await _context.LookUpStatusTypes
                .Include(s => s.FollowUpSettings)
                .FirstOrDefaultAsync(s => s.StatusId == statusId);

            return status?.FollowUpSettings;
        }

        /// <summary>
        /// Checks if a status requires follow-up
        /// </summary>
        private async Task<bool> StatusRequiresFollowUpAsync(int statusId)
        {
            var status = await _context.LookUpStatusTypes
                .FirstOrDefaultAsync(s => s.StatusId == statusId);

            return status?.RequireFollowUp ?? false;
        }

        /// <summary>
        /// Gets all requests that need follow-up notifications
        /// </summary>
        public async Task<List<FollowUpNotificationViewModel>> GetFollowUpNotificationsAsync()
        {
            try
            {
                var result = await _context.Database
                    .SqlQuery<FollowUpNotificationViewModel>($"EXEC GetFollowUpNotifications")
                    .ToListAsync();

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error executing stored procedure: GetFollowUpNotifications");
                Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                throw;
            }
        }

        /// <summary>
        /// Processes a follow-up for a specific request using status-specific settings
        /// </summary>
        public async Task ProcessFollowUpAsync(int requestId, int currentUserId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var request = await _context.Requests
                    .Include(r => r.Status)
                    .ThenInclude(s => s.FollowUpSettings) // Include the follow-up settings
                    .FirstOrDefaultAsync(r => r.RequestId == requestId);

                if (request == null)
                    throw new ArgumentException($"Request with ID {requestId} not found");

                if (request.Status == null)
                    throw new InvalidOperationException($"Request {requestId} has no status assigned");

                // Check if this status requires follow-up
                if (!request.Status.RequireFollowUp)
                    throw new InvalidOperationException($"Status '{request.Status.StatusName}' does not require follow-up");

                // Get settings for this specific status
                var settings = request.Status.FollowUpSettings;
                if (settings == null)
                    throw new InvalidOperationException($"No follow-up settings configured for status '{request.Status.StatusName}'");

                // Check if max follow-ups reached for this status
                if (request.FollowUpCount >= settings.MaxFollowUps)
                    throw new InvalidOperationException($"Maximum follow-ups ({settings.MaxFollowUps}) already reached for request {requestId}");

                if (IsClosedStatus(request.Status?.StatusName))
                    throw new InvalidOperationException($"Cannot follow up on closed request {requestId}");

                request.FollowUpCount++;
                request.LastFollowUpDate = DateTime.Now;
                request.UpdatedAt = DateTime.Now;
                request.UpdatedByCode = currentUserId;

                // Add a comment to track the follow-up
                var followUpMessage = $"Follow-up #{request.FollowUpCount} completed on {DateTime.Now:yyyy-MM-dd HH:mm}";
                request.Comments = string.IsNullOrEmpty(request.Comments)
                    ? followUpMessage
                    : $"{request.Comments}\n\n{followUpMessage}";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Processes multiple follow-ups in bulk
        /// </summary>
        public async Task<Dictionary<int, bool>> ProcessBulkFollowUpsAsync(List<int> requestIds, int currentUserId)
        {
            var results = new Dictionary<int, bool>();

            foreach (var requestId in requestIds)
            {
                try
                {
                    await ProcessFollowUpAsync(requestId, currentUserId);
                    results[requestId] = true;
                }
                catch
                {
                    results[requestId] = false;
                }
            }

            return results;
        }

        /// <summary>
        /// Automatically closes requests that have exceeded the follow-up limit (using status-specific settings)
        /// </summary>
        public async Task ProcessAutoClosureAsync()
        {
            // Find the "Auto-Closed" or "Closed" status ID
            var autoClosedStatus = await _context.LookUpStatusTypes
                .FirstOrDefaultAsync(s => s.StatusName.ToLower().Contains("closed") ||
                                         s.StatusName.ToLower().Contains("auto"));

            if (autoClosedStatus == null)
            {
                throw new InvalidOperationException("No 'Closed' or 'Auto-Closed' status found in LookUpStatusTypes");
            }

            // Get all requests that need to be auto-closed based on their status-specific settings
            var requestsToClose = await (from request in _context.Requests
                                         join status in _context.LookUpStatusTypes on request.StatusId equals status.StatusId
                                         join settings in _context.FollowUpSetting on status.FollowUp_SettingID equals settings.FollowUp_SettingID
                                         where status.RequireFollowUp == true
                                         && settings.AutoCloseDays > 0 // Only if auto-close is enabled for this status
                                         && request.FollowUpCount >= settings.MaxFollowUps
                                         && request.LastFollowUpDate.HasValue
                                         && request.LastFollowUpDate.Value.Date <= DateTime.Now.Date.AddDays(-settings.AutoCloseDays)
                                         && !IsClosedStatus(status.StatusName)
                                         select new { request, settings }).ToListAsync();

            if (!requestsToClose.Any()) return;

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var item in requestsToClose)
                {
                    var request = item.request;
                    var settings = item.settings;

                    request.StatusId = autoClosedStatus.StatusId;
                    request.UpdatedAt = DateTime.Now;
                    request.UpdatedByCode = null; // System update

                    // Add auto-closure comment
                    var autoCloseMessage = $"Request automatically closed on {DateTime.Now:yyyy-MM-dd HH:mm} after {settings.MaxFollowUps} follow-ups with no response (Auto-close after {settings.AutoCloseDays} days).";
                    request.Comments = string.IsNullOrEmpty(request.Comments)
                        ? autoCloseMessage
                        : $"{request.Comments}\n\n{autoCloseMessage}";
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Gets count of overdue follow-ups using status-specific settings
        /// </summary>
        public async Task<int> GetOverdueFollowUpCountAsync()
        {
            return await (from request in _context.Requests
                          join status in _context.LookUpStatusTypes on request.StatusId equals status.StatusId
                          join settings in _context.FollowUpSetting on status.FollowUp_SettingID equals settings.FollowUp_SettingID
                          where status.RequireFollowUp == true
                          && request.FollowUpCount < settings.MaxFollowUps
                          && settings.FollowUpIntervalDays > 0 // Only count if interval is set
                          && (request.LastFollowUpDate == null
                              ? request.CreatedAt.Date <= DateTime.Now.Date.AddDays(-settings.FollowUpIntervalDays)
                              : request.LastFollowUpDate.Value.Date <= DateTime.Now.Date.AddDays(-settings.FollowUpIntervalDays))
                          && !IsClosedStatus(status.StatusName)
                          select request).CountAsync();
        }

        /// <summary>
        /// Gets count of requests near auto-closure using status-specific settings
        /// </summary>
        public async Task<int> GetNearAutoCloseCountAsync()
        {
            return await (from request in _context.Requests
                          join status in _context.LookUpStatusTypes on request.StatusId equals status.StatusId
                          join settings in _context.FollowUpSetting on status.FollowUp_SettingID equals settings.FollowUp_SettingID
                          where status.RequireFollowUp == true
                          && settings.AutoCloseDays > 0 // Only count if auto-close is enabled
                          && request.FollowUpCount >= settings.MaxFollowUps
                          && request.LastFollowUpDate.HasValue
                          && request.LastFollowUpDate.Value.Date <= DateTime.Now.Date.AddDays(-(settings.AutoCloseDays - 2)) // 2 days before auto-close
                          && request.LastFollowUpDate.Value.Date > DateTime.Now.Date.AddDays(-settings.AutoCloseDays)
                          && !IsClosedStatus(status.StatusName)
                          select request).CountAsync();
        }

        /// <summary>
        /// Checks if a request is overdue for follow-up based on its status settings
        /// </summary>
        public async Task<bool> IsRequestOverdueAsync(int requestId)
        {
            var request = await _context.Requests
                .Include(r => r.Status)
                .ThenInclude(s => s.FollowUpSettings)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request?.Status?.RequireFollowUp != true || request.Status.FollowUpSettings == null)
                return false;

            var settings = request.Status.FollowUpSettings;
            if (settings.FollowUpIntervalDays <= 0) return false;

            var lastDate = request.LastFollowUpDate ?? request.CreatedAt;
            return (DateTime.Now - lastDate).Days > settings.FollowUpIntervalDays;
        }

        /// <summary>
        /// Gets the next follow-up date for a request based on its status settings
        /// </summary>
        public async Task<DateTime?> GetNextFollowUpDateAsync(int requestId)
        {
            var request = await _context.Requests
                .Include(r => r.Status)
                .ThenInclude(s => s.FollowUpSettings)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request?.Status?.RequireFollowUp != true || request.Status.FollowUpSettings == null)
                return null;

            var settings = request.Status.FollowUpSettings;
            if (settings.FollowUpIntervalDays <= 0) return null;

            var lastDate = request.LastFollowUpDate ?? request.CreatedAt;
            return lastDate.AddDays(settings.FollowUpIntervalDays);
        }

        /// <summary>
        /// Helper method to determine if a status represents a closed state
        /// </summary>
        private static bool IsClosedStatus(string? statusName)
        {
            if (string.IsNullOrEmpty(statusName)) return false;

            var closedKeywords = new[] { "Closed", "Completed", "Resolved", "Cancelled" };
            return closedKeywords.Any(keyword => statusName.ToLower().Contains(keyword.ToLower()));
        }
    }
}