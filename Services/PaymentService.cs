using API_App.Data;
using API_App.Models;
using Microsoft.EntityFrameworkCore;

namespace API_App.Services
{
    public class PaymentService
    {
        private readonly AppDbContext _db;
        private const long PaymentAmount = 110; // Cents. Hardcoded const – Specs demand

        public PaymentService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<(bool Success, string? Message, long? NewBalance)> MakePaymentAsync(int userId)
        {
            //Transaction begin
            await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            //SQL query
            var user = await _db.Users
                .FromSqlRaw("SELECT * FROM \"Users\" WHERE \"Id\" = {0} FOR UPDATE", userId)
                .FirstOrDefaultAsync();

            //Validation
            if (user is null)
                return (false, "Wrong credentials", null);
            if (user.BalanceCents < PaymentAmount)
                return (false, "Insufficient funds", user.BalanceCents);


            //Withdrawal operation [Users]
            user.BalanceCents -= PaymentAmount;

            //Payment data [Payments]
            var payment = new Payment
            {
                UserId = user.Id,
                AmountCents = PaymentAmount,
                TimeStamp = DateTime.UtcNow
            };


            //Transaction end
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return (true, "Payment successful", user.BalanceCents);
        }
    }
}
