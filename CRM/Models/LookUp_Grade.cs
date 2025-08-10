using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Models
{
    public class LookUp_Grade
    {
        [Key]
        [Column("GradeID")]
        public int GradeID { get; set; }


        [StringLength(100)]
        public string GradeName { get; set; } = string.Empty;

        public virtual ICollection<Person> People { get; set; } = new List<Person>();
    }
}
