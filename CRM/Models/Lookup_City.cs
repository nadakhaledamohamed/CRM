using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Models
{
    public class Lookup_City
    {

        [Key]
        [Column("CityID")]
        public int CityID { get; set; }

        [StringLength(100)]

        public string CityName { get; set; } = string.Empty;

        public virtual ICollection<Person> People { get; set; } = new List<Person>();
    }
}
