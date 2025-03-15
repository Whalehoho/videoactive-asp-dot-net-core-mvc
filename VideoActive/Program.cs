using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.IO;
using VideoActive.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using VideoActive.WebSocketHandlers;
using VideoActive.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Add PostgreSQL Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Set up Kestrel to listen on port 5000
builder.WebHost.UseUrls("http://localhost:5000");

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// Register AuthService
builder.Services.AddScoped<AuthService>();

// ✅ CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins("http://localhost:3001", "https://8e7f-2001-f40-98e-ab91-f84f-df17-f719-6909.ngrok-free.app") // 👈 Specify allowed frontend URL. "*" does not allow credentials if credentials: "include" is used. Must explicitly specify.
              .AllowCredentials() // 👈 Allow cookies/tokens to be sent
                .AllowAnyHeader()
                .AllowAnyMethod();
    });
});

// ✅ JWT Authentication Configuration
// Add authentication services
// ✅ Add Authentication services
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
     options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme; // ✅ Redirect to /Admin/Login
})
.AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login"; // Redirect to login page
        options.AccessDeniedPath = "/Admin/Login"; // Handle unauthorized access
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Session expires after 30 minutes
    }

) // ✅ Cookie authentication
.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    options.CallbackPath = "/auth/google/callback"; // ✅ Set callback URL
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Key"])), // ✅ Use your secret key
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"], // ✅ Use your issuer
        ValidateAudience = true,
        ValidAudience = builder.Configuration["JwtSettings:Audience"], // ✅ Use your audience
        ValidateLifetime = true
    };
});

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer(); // Required for WebSockets

var app = builder.Build();

// ✅ Correct Middleware Order
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ✅ Enable Forwarded Headers Middleware, equivalent to app.set('trust proxy', 1) in Express
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    RequireHeaderSymmetry = false, // ✅ Needed for proxies like Ngrok
    ForwardLimit = null // ✅ Allow multiple proxies
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowSpecificOrigin");

app.UseAuthentication(); // 🔹 Ensure authentication runs before authorization
app.UseAuthorization();

// ✅ WebSocket Support
app.UseWebSockets();

app.Use(async (context, next) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var path = context.Request.Path.Value;
        var query = context.Request.Query;

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        

        if (path == "/ws/direct")
        {
            Console.WriteLine("Direct call");
            string? clientId = query.ContainsKey("clientId") ? query["clientId"].ToString() : null;
            string? authToken = query.ContainsKey("authToken") ? query["authToken"].ToString() : null;
            Console.WriteLine($"Client ID: {clientId}");
            Console.WriteLine($"Auth Token: {authToken}");

            using (var scope = app.Services.CreateScope())
            {
                var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                var user = await authService.GetUserFromToken(authToken);

                if (user == null)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid or expired token", CancellationToken.None);
                    return;
                }

                Console.WriteLine($"User ID: {user.UID}");
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                DirectCallHandler.Initialize(dbContext);
                await DirectCallHandler.HandleWebSocketAsync(socket, user.UID.ToString());
            }
        }
        else if (path == "/ws/random")
        {
            Console.WriteLine("Random call");
            string? authToken = query.ContainsKey("authToken") ? query["authToken"].ToString() : null;

            using (var scope = app.Services.CreateScope())
            {
                var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                var user = await authService.GetUserFromToken(authToken);

                if (user == null)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid or expired token", CancellationToken.None);
                    return;
                }
                await RandomCallHandler.HandleWebSocketAsync(socket, user.UID.ToString());
            }

        }
        else
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid path", CancellationToken.None);
        }
    }
    else
    {
        await next();
    }
});


// ✅ Configure static files & MVC routes
app.UseStaticFiles();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
