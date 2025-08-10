using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace CRM.Models
{
    [NotMapped]
    public partial class PersonRequestViewModel
    
    {


        [Key]

        public int ID { get; set; }
        [Key]
        public int PersonID { get; set; }
        // Person fields
        [Required]
        public string FirstName { get; set; } = string.Empty;

        //[Required(ErrorMessage = "Name is required")]
      //  public string? LastName { get; set; }

    [EmailAddress]
       // [Required(ErrorMessage = "Email is required")]
        public string? Email { get; set; }


        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^[\d\s\-\+\(\)]+$", ErrorMessage = "Please enter a valid phone number")]
        public string? Phone { get; set; } 

        public string? NationalId { get; set; }

        [Required(ErrorMessage = "user Type is required")]
        public int UserType { get; set; }

        [Required(ErrorMessage = "Status is required")]
        public int? StatusId { get; set; }

     
        public int? HighSchoolId { get; set; }
      
        public int? CertificateId { get; set; }

    
        //[Required(ErrorMessage = "HowDidYouKnowUs is required")]
        public int? HowDidYouKnowUsId { get; set; }
        public string? HowDidYouKnowUsDisplay { get; set; }//to view data in details page
        public string? HighSchoolName { get; set; }
        public string? CertificateName { get; set; }
        public string? HowDidYouKnowUsName { get; set; }
        public string? MajorName { get; set; }
        public string? StatusName { get; set; }

        //[Required(ErrorMessage = "user type is required")]
        public string? UserTypeName => UserType == 1 ? "Lead" : "Guardian";

        [Required(ErrorMessage = "Reason Description is required")]
        public int? ReasonID { get; set; } // This is the reason for the request

        public string? Comments { get; set; } 
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

        public DateTime Request_CreatedAt { get; set; } =DateTime.Now;
        [Display(Name = "Created By")]
      

        [Required]
        public int Person_CreatedByCode { get; set; }

        [BindNever]
        public string? Person_CreatedByName { get; set; }
        [Required]
        public int Request_CreatedByCode { get; set; }
        public string? Request_CreatedByName { get; set; }

     
        public List<RequestViewModel> Requests { get; set; } = new();



        //[Required(ErrorMessage = "First major interest is required")]
        public int? MajorId { get; set; }
        public int? SecondMajorId { get; set; } // Optional

        // Add these for display purposes
        public int MaxNumberOfInterests { get; set; } = 1; // Will be set from Academic_Setting
        public int CurrentAcademicSettingId { get; set; }


        /// <summary>
        /// ////new feilds 
        /// </summary>


        // Lookup fields for City, Grade, and Nationality
        [Display(Name = "City")]
        public int? CityID { get; set; }
        public string? CityName { get; set; }

        [Display(Name = "Grade")]
        public int? GradeID { get; set; }
        public string? GradeName { get; set; }

        [Display(Name = "Nationality")]
        public int? NationalityID { get; set; }
        public string? NationalityName { get; set; }



        //others for drop down lists
        public string? SchoolOther { get; set; }
       
        public string? ReasonOther { get; set; }
        public string? HowDidYouKnowUs_Other { get; set; } // stored in Person

        [RegularExpression(@"^[\d\s\-\+\(\)]+$", ErrorMessage = "Please enter a valid whatsApp number")]
        public string? whatsApp { get; set; }

        ////follow uppppppppppp
        //public bool IsFollowUpOverdue { get; set; }
        public bool RequiresFollowUp { get; set; }
        //[NotMapped]
        //public int MaxFollowUps { get; set; }
        //[NotMapped]
        //public int FollowUpIntervalDays { get; set; }

        // public bool RequiresFollowUp => StatusId == 1;
        //to close no need follow up requests after 3 follow ups 
        public bool IsRequestClosed { get; set; }
        public bool CanOpenDetails { get; set; }
    }
}