using System.ComponentModel.DataAnnotations;

namespace CRM.Models
{
    public class loginVM
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }=string.Empty;
    }
}
