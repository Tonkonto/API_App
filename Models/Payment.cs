using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_App.Models
{
    public class Payment
    {
        //  Keys, relations
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        // Transaction data
        public long AmountCents { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    }
}
