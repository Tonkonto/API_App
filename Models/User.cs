using System.ComponentModel.DataAnnotations;

namespace API_App.Models
{
    public class User
    {
        //  Keys, relations
        [Key]
        public int Id { get; set; }
        
        //  User data
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        
        //  Wallet data
        public long BalanceCents { get; set; }
    }
}
