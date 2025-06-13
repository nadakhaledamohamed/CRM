using System.ComponentModel.DataAnnotations;

namespace CRM.Models
{
    public class PersonDetailsViewModel
    {
        public Person? Person { get; set; }
        public List<RequestViewModel>? Requests { get; set; }
    }

    public class RequestViewModel
    {
        [Key]
        public int RequestId { get; set; }

        [Required(ErrorMessage = "Description is required")]
        public string Description { get; set; } =string.Empty;
        public string Comments { get; set; } = string.Empty;
        public int FollowUpCount { get; set; }
        public DateTime? LastFollowUpDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; }=string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public int UpdatedByCode { get; set; }
        public string UpdatedByName { get; set; } = string.Empty;
        public int? StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
    }

}
