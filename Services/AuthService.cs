using API_App.Data;
using API_App.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace API_App.Services
{
    public class AuthService
    {
        private readonly AppDbContext _db;
        private readonly PasswordHasher<User> _hasher;
        private readonly JwtOptions _jwt;
        private readonly BruteCfg _bruteCfg;
        private readonly IMemoryCache _cache;

        public AuthService(AppDbContext db, PasswordHasher<User> hasher, IOptions<JwtOptions> jwt, IOptions<BruteCfg> bruteCfg, IMemoryCache cache)
        {
            _db = db;
            _hasher = hasher;
            _jwt = jwt.Value;
            _bruteCfg = bruteCfg.Value;
            _cache = cache;
        }

        //  Login()
        public async Task<(bool Success, string? Token, string? Error)> LoginAsync(string username, string password, string ip)
        {
            //Brute count attempts
            var bKey = $"bfp:{ip}";
            if (_cache.TryGetValue<BruteTracker>(bKey, out var bEntry)
                && bEntry!.LockedUntil.HasValue
                && bEntry.LockedUntil.Value > DateTime.UtcNow)
            {
                var minutesLeft = Math.Ceiling((bEntry.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
                return (false, null, $"Too many failed login attempts. IP is blocked for {minutesLeft} minuntes");
            }
            
            //User table data
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

            //if incorrect login or password
            if (user == null 
                || _hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            {
                BruteRegisterAttempt(bKey);
                return (false, null, "Wrong credentials");
            }

            //Brute reset
            _cache.Remove(bKey);

            //Token set
            var jti = Guid.NewGuid().ToString();
            var token = GenerateJwtToken(user, jti);
            await _db.Tokens.AddAsync(new Token
            {
                UserId = user.Id,
                Jti = jti,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwt.ExpireMinutes)
            });
            await _db.SaveChangesAsync();

            return (true, token, null);
        }
        //  Brute-force-prevention
        private class BruteTracker
        {
            public int FailedAttempts { get; set; }
            public DateTime? LockedUntil { get; set; }
        }
        private void BruteRegisterAttempt(string key)
        {
            var entry = _cache.Get<BruteTracker>(key) ?? new BruteTracker();
            entry.FailedAttempts++;

            if (entry.FailedAttempts >= _bruteCfg.MaxFailedLoginAttempts)
                entry.LockedUntil = DateTime.UtcNow.AddMinutes(_bruteCfg.LoginLockMinutes);

            _cache.Set(key, entry, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(_bruteCfg.LoginLockMinutes)
            });
        }


        //  Logout()
        public async Task<bool> LogoutAsync(string jti)
        {
            var token = await _db.Tokens.FirstOrDefaultAsync(t => t.Jti == jti);
            if (token is null)
                return false;

            token.Revoked = true;
            await _db.SaveChangesAsync();
            return true;
        }

        // JWT()
        private string GenerateJwtToken(User user, string jti)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, jti)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(_jwt.ExpireMinutes);

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
