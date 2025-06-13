using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace CRM.Models
{
    [NotMapped]
    public class PersonRequestViewModel
{


        [Key]

        public int ID { get; set; }
        [Key]
        public int PersonID { get; set; }
        // Person fields
        [Required]
    public string FirstName { get; set; } = string.Empty;


    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;

    [Required]
    public int UserType { get; set; }

        public int? StatusId { get; set; }

     
        public int? HighSchoolId { get; set; }
    public int? CertificateId { get; set; }
    public int? MajorId { get; set; }
    public int? HowDidYouKnowUsId { get; set; }

        public string? HighSchoolName { get; set; }
        public string? CertificateName { get; set; }
        public string? HowDidYouKnowUsName { get; set; }
        public string? MajorName { get; set; }
        public string? StatusName { get; set; }
        public string? UserTypeName => UserType == 1 ? "Applicant" : "Guardian";

        public string Description { get; set; } =string.Empty;

        public string? Comments { get; set; } = string.Empty;
      public int FollowUpCount { get; set; } = 0;
      public DateTime? LastFollowUpDate { get; set; }
     



        public DateTime? Person_UpdatedAt { get; set; }
        public DateTime? Request_UpdatedAt { get; set; }

        public int? Person_UpdatedByCode { get; set; }
        public string? Request_UpdatedByName { get; set; }

        public int? Request_UpdatedByCode { get; set; }
        public string? Person_UpdatedByName { get; set; }

        [Required(ErrorMessage = "Creation date is required")]
        [Display(Name = "Created At")]
        public DateTime Person_CreatedAt { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Creation date is required")]
        [Display(Name = "Created At")]
        public DateTime Request_CreatedAt { get; set; } = DateTime.Now;
        [Display(Name = "Created By")]
      

        [Required]
        public int Person_CreatedByCode { get; set; }

        [BindNever]
        public string? Person_CreatedByName { get; set; }
        [Required]
        public int Request_CreatedByCode { get; set; }
        public string? Request_CreatedByName { get; set; }

        public List<RequestViewModel> Requests { get; set; } = new();

    }
}