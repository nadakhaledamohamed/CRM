using System.ComponentModel.DataAnnotations;

namespace CRM.Models
{
    public class MajorPerson
    {
        [Key]
        public int MajorPerson_ID { get; set; }

        public int PersonID { get; set; }
        public int? MajorID { get; set; }

        public int Academic_Setting_ID { get; set; }

        public int Priority { get; set; }


        // Navigation Properties (define relationships)
        public Person? Person { get; set; }     
        public LookupMajor? LookupMajor { get; set; }
        public AcademicSetting? AcademicSetting { get; set; }

    }
}
