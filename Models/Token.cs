using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_App.Models
{
    public class Token
    {
        //  Keys, relations
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }


        //  Token data
        public string Jti { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public bool Revoked { get; set; } = false;
    }
}
