using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoActive.Models;

namespace VideoActive.WebSocketHandlers
{
    public class RandomCallHandler
    {
        private static readonly List<WebSocket> waitingClients = new();
        private static readonly Dictionary<string, (WebSocket caller, WebSocket callee)> activePairs = new();
        private static readonly Dictionary<WebSocket, string> clientPairIds = new();
        private static readonly Dictionary<string, WebSocket> clientSockets = new();
        private static readonly DbContextOptions<ApplicationDbContext> _dbOptions;

        // Static constructor to initialize DB options
        static RandomCallHandler()
        {
            var connectionString = ConfigHelper.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            _dbOptions = optionsBuilder.Options;
        }

        public static async Task HandleWebSocketAsync(WebSocket socket, string clientId)
        {
            Console.WriteLine($"Client connected: {clientId}");
            lock (waitingClients)
            {
                waitingClients.Add(socket);
                clientSockets[clientId] = socket;
            }
            Console.WriteLine("Client added to random call queue.");

            // Attempt to pair clients if two are available
            if (waitingClients.Count >= 2)
            {
                WebSocket client1, client2;

                lock (waitingClients)
                {
                    client1 = waitingClients[0];
                    client2 = waitingClients[1];
                    waitingClients.RemoveRange(0, 2);
                }

                var pairId = Guid.NewGuid().ToString();
                activePairs[pairId] = (client1, client2);
                clientPairIds[client1] = pairId;
                clientPairIds[client2] = pairId;

                // Assign roles: client1 is caller, client2 is callee
                await NotifyPair(client1, pairId, "caller");
                await NotifyPair(client2, pairId, "callee");

                // Log the start call
                var callerId = clientSockets.FirstOrDefault(x => x.Value == client1).Key;
                var calleeId = clientSockets.FirstOrDefault(x => x.Value == client2).Key;

                // Create new DbContext instance
                await using var dbContext = new ApplicationDbContext(_dbOptions);
                var callLog = new CallLog
                {
                    CallerId = int.Parse(callerId),
                    CalleeId = int.Parse(calleeId),
                    CallTime = DateTime.UtcNow,
                    CallType = "random"
                };
                dbContext.CallLogs.Add(callLog);
                await dbContext.SaveChangesAsync();
                

            }

            await ReceiveMessages(socket, clientId);
        }

        private static async Task ReceiveMessages(WebSocket socket, string clientId)
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
                        lock (waitingClients)
                        {
                            waitingClients.Remove(socket);
                        }
                        await CleanupDisconnectedClient(socket);
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);

                        // Log the end call
                        // Create new DbContext instance
                        await using var dbContext = new ApplicationDbContext(_dbOptions);
                        var callLog = await dbContext.CallLogs
                            .FirstOrDefaultAsync(c => c.CallerId == int.Parse(clientId) && c.EndTime == null);
                        
                        if (callLog != null)
                        {
                            callLog.EndTime = DateTime.UtcNow;
                            await dbContext.SaveChangesAsync();
                        }


                        break;
                    }
                    else
                    {
                        messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        if (result.EndOfMessage)
                        {
                            var message = messageBuilder.ToString();
                            await ForwardMessage(socket, message);
                            messageBuilder.Clear();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RandomCall Error: {ex.Message}");
            }
        }

        private static async Task NotifyPair(WebSocket client, string pairId, string role)
        {
            var message = JsonConvert.SerializeObject(new
            {
                type = "match-found",
                pairId = pairId,
                role = role
            });

            var buffer = Encoding.UTF8.GetBytes(message);
            await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);

        }

        private static async Task ForwardMessage(WebSocket sender, string message)
        {
            // 1. Find the pair ID for the sender
            if (clientPairIds.TryGetValue(sender, out string? pairId) && 
                activePairs.TryGetValue(pairId, out var pair))
            {
                // 2. Determine the receiver based on the pair
                WebSocket receiver = (pair.caller == sender) ? pair.callee : pair.caller;

                // 3. Forward the message to the receiver
                if (receiver.State == WebSocketState.Open)
                {
                    var buffer = Encoding.UTF8.GetBytes(message);
                    await receiver.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

        private static async Task CleanupDisconnectedClient(WebSocket socket)
        {
            if (clientPairIds.TryGetValue(socket, out string? pairId))
            {
                clientPairIds.Remove(socket);

                if (activePairs.TryGetValue(pairId, out var pair))
                {
                    WebSocket otherClient = (pair.caller == socket) ? pair.callee : pair.caller;

                    if (otherClient.State == WebSocketState.Open)
                    {
                        var message = JsonConvert.SerializeObject(new { type = "peer-disconnected" });
                        var buffer = Encoding.UTF8.GetBytes(message);
                        await otherClient.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);


                    }

                    activePairs.Remove(pairId);

                   
                }
            }

            // Remove the clientId from the clientSockets dictionary
            var clientId = clientSockets.FirstOrDefault(x => x.Value == socket).Key;
            if (clientId != null)
            {
                clientSockets.Remove(clientId);
            }
        }

        public static class ConfigHelper
        {
            public static string GetConnectionString(string name = "DefaultConnection")
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory) // or Directory.GetCurrentDirectory() depending on setup
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                return configuration.GetConnectionString(name);
            }
        }

    }
}
