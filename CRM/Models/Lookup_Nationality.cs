using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Models
{
    public class Lookup_Nationality
    {
        [Key]

        [Column("NationalityID")]
        public int NationalityID { get; set; }
        [StringLength(150)]
        public string NationalityName { get; set; } = string.Empty;

        public virtual ICollection<Person> People { get; set; } = new List<Person>();
    }
}
