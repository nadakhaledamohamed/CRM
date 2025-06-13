namespace CRM.Models
{
    public class ListPersonReq_ViewModel
    {
        public Person Person { get; set; } = new();
        public List<RequestViewModel> Requests { get; set; } = new();
    }
}
