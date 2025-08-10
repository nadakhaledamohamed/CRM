using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices.Marshalling;

namespace CRM.Models
{
    [Table("Lookup_FollowUpType")]
    public class Lookup_FollowUpType
    {
        [Key]
        [Column("FollowUpType_ID")]
        public int FollowUpType_ID { get; set; }

        [StringLength(150)]
        public string FollowUpName { get; set; } = string.Empty;

        public virtual ICollection<FollowUp_Log> FollowUpLog { get; set; } = new List<FollowUp_Log>();
    }
}
