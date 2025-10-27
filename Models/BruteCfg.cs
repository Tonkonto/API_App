namespace API_App.Models
{
    // Brute-force-prevention cfg
    public class BruteCfg
    {
        public int MaxFailedLoginAttempts { get; set; }
        public int LoginLockMinutes { get; set; }
    }
}
