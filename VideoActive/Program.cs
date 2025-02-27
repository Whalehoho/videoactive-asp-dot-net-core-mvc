using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using VideoActive.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Set up Kestrel to listen on port 5000
builder.WebHost.UseUrls("http://localhost:5000");

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // 👈 Specify allowed frontend URL
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
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme; // ✅ Use Google for challenges
})
.AddCookie() // ✅ Cookie authentication
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

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowSpecificOrigin");

app.UseAuthentication(); // 🔹 Ensure authentication runs before authorization
app.UseAuthorization();

// ✅ WebSocket Support
app.UseWebSockets();

// Dictionary to store connected WebSocket clients
Dictionary<string, WebSocket> connectedClients = new Dictionary<string, WebSocket>();

// ✅ WebSocket Handling Middleware
app.Use(async (context, next) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        var clientId = Guid.NewGuid().ToString();
        connectedClients.Add(clientId, socket);

        var clientIdMessage = new { type = "client-id", clientId = clientId };
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(clientIdMessage))),
            WebSocketMessageType.Text, true, CancellationToken.None);

        await BroadcastConnectedClients();
        await HandleWebSocketConnection(socket, clientId);
    }
    else
    {
        await next();
    }
});

// ✅ WebSocket Connection Handling
async Task HandleWebSocketConnection(WebSocket socket, string clientId)
{
    var buffer = new byte[8192];
    var messageBuilder = new StringBuilder();

    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                connectedClients.Remove(clientId);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the client", CancellationToken.None);
                await BroadcastConnectedClients();
            }
            else
            {
                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    var message = messageBuilder.ToString();
                    try
                    {
                        await BroadcastMessageToClients(clientId, message);
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error parsing JSON: {ex.Message}");
                    }

                    messageBuilder.Clear();
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WebSocket Error: {ex.Message}");
    }
}

// ✅ Broadcast message to all connected WebSocket clients
async Task BroadcastMessageToClients(string fromClientId, string message)
{
    foreach (var client in connectedClients)
    {
        if (client.Key != fromClientId)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await client.Value.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

// ✅ Send the list of connected WebSocket clients
async Task BroadcastConnectedClients()
{
    var clientListMessage = new { type = "connected-clients", clients = connectedClients.Keys.ToList() };
    var message = JsonConvert.SerializeObject(clientListMessage);

    foreach (var client in connectedClients)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await client.Value.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}

// ✅ Configure static files & MVC routes
app.UseStaticFiles();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
