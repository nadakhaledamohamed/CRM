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
        public string? person_FullName { get; set; }
        public int PersonId { get; set; }
        //public string Description { get; set; } = string.Empty;
        public string? ReasonDescription { get; set; }

        //[Required(ErrorMessage = "Description is required")]
        public int? ReasonID { get; set; }
    
        public string? ReasonDescription_Other { get; set; }

        public string? Comments { get; set; } 
        public int FollowUpCount { get; set; }
        public DateTime? LastFollowUpDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; }=string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public int UpdatedByCode { get; set; }
        public string UpdatedByName { get; set; } = string.Empty;
        
        public int? CreatedbyCode { get; set; }
        public string? CreatedByCodeName { get; set; }

        public int? StatusId { get; set; }
        public string? StatusName { get; set; }
        public int? FollowUpType_ID { get; set; }
        public string? FollowUpTypeName { get; set; }
        public bool HasFollowUps { get; set; }


    }

}
