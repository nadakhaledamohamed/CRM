using CRM.ViewModel;

namespace CRM.Services
{
    public interface IFollowUpService
    {
        Task<List<FollowUpNotificationViewModel>> GetFollowUpNotificationsAsync();
        Task ProcessFollowUpAsync(int requestId, int currentUserId);
        Task ProcessAutoClosureAsync();
        Task<int> GetOverdueFollowUpCountAsync();
    }
}
