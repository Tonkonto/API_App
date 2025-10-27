using API_App.Data;
using API_App.Models;
using API_App.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

internal class Program
{
    private static void Main(string[] args)
    {
        var webAppBuilder = WebApplication.CreateBuilder(args);
        ServicesSetup(webAppBuilder);

        var webApp = webAppBuilder.Build();
        MiddlewareSetup(webApp);
        EndpointsMap(webApp);

        webApp.Run();
    }

    private static void ServicesSetup(WebApplicationBuilder builder)
    {
        //DB context
        var dbConnStr = builder.Configuration.GetConnectionString("Connection1");
        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dbConnStr));

        //JWT Options
        var jwtJsConf = builder.Configuration.GetSection("Jwt");
        builder.Services.Configure<JwtOptions>(jwtJsConf);
        var jwtOptions = jwtJsConf.Get<JwtOptions>()!;
        var key = Encoding.UTF8.GetBytes(jwtOptions.Key);
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        //JWT Auth
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
            });

        //Services
          //Brute-force-prevention
        builder.Services.AddMemoryCache();
        builder.Services.Configure<BruteCfg>(
            builder.Configuration.GetSection("Brute"));
           
        builder.Services.AddAuthorization();
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<PaymentService>();
        builder.Services.AddScoped<PasswordHasher<User>>();
    }

    private static void MiddlewareSetup(WebApplication webApp)
    {
        webApp.UseAuthentication();
        webApp.UseAuthorization();
    }
    
    private static void EndpointsMap(WebApplication webApp)
    {
        //GET
        webApp.MapGet("/", () => "Hello, World!");

        //POST
        webApp.MapPost("/login", Login).AllowAnonymous();
        webApp.MapPost("/logout", Logout).RequireAuthorization();
        webApp.MapPost("/payment", Payment).RequireAuthorization();
        if (webApp.Environment.IsDevelopment())
            webApp.MapPost("/create-user", CreateUser).AllowAnonymous();
    }


    //  Login
    public record LoginRequest(string Username, string Password);
    private static async Task<IResult> Login(HttpContext ctx, AuthService auth, LoginRequest req)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "default";
        var (ok, token, error) = await auth.LoginAsync(req.Username, req.Password, ip);

        return ok
            ? Results.Ok(new { access_token = token })
            : Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized);
    }
    //  Logout
    private static async Task<IResult> Logout(ClaimsPrincipal user, AuthService auth)
    {
        var jti = user.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (string.IsNullOrEmpty(jti))
            return Results.BadRequest("Invalid token");

        var ok = await auth.LogoutAsync(jti);
        return ok
            ? Results.Ok(new { message = "Logged out" })
            : Results.NotFound();
    }
    //  Payment
    private static async Task<IResult> Payment(ClaimsPrincipal user, PaymentService payments)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
            return Results.BadRequest("Invalid token claims");
        int userId = int.Parse(sub);

        var (Success, Message, NewBalance) = await payments.MakePaymentAsync(userId);
        string newBalance_usd = $"{NewBalance / 100m:0.00}";

        return Success
            ? Results.Ok(new { Message, balance_usd = newBalance_usd })
            : Results.BadRequest(new { Message });
    }
    //  CreateUser
    public record CreateUserRequest(string Username, string Password);
    private static async Task<IResult> CreateUser(AppDbContext db, PasswordHasher<User> hasher, HttpRequest request, CreateUserRequest req, IConfiguration config)
    {
        //key identification
        if (!request.Headers.TryGetValue("AdminKey", out var providedKey))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        //key validation
        var expectedKey = config["debug:admin_key"];
        if (providedKey != expectedKey)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        
        //user exists?
        if (await db.Users.AnyAsync(u => u.Username == req.Username))
            return Results.Conflict("User already exists.");
        //new user
        var user = new User
        {
            Username = req.Username,
            PasswordHash = hasher.HashPassword(null!, req.Password),
            BalanceCents = 800  // Hardcoded – Specs demand
        };
        //add to db
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        var location = $"/users/{user.Id}"; //placeholder
        return Results.Created(location, new
        {
            username = user.Username,
            balance_usd = $"{user.BalanceCents / 100m:0.00}"
        });
    }
}