using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Models
{

    [Table("Lookup_ReasonDescription")]
    public class Lookup_ReasonDescription
    {
        

        [Key]
        [Column("ReasonID")]
        public int ReasonID { get; set; }
        [StringLength(250)]
        public string Reason_Description { get; set; } = string.Empty;
        public ICollection<Request>? Requests { get; set; }
    }
}
