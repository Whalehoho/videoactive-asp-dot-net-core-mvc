using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using VideoActive.Services;
using VideoActive.Models;
using Microsoft.EntityFrameworkCore;

namespace VideoActive.WebSocketHandlers
{
    public class DirectCallHandler
    {
        // private static readonly string _valkeyConnectionString;
        // private static readonly ValkeyService _valkeyService;
        private static ApplicationDbContext _context;

        public static void Initialize(ApplicationDbContext context)
        {
            _context = context;
        }

        static DirectCallHandler()
        {
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            // _valkeyConnectionString = configuration.GetValue<string>("Valkey:ConnectionString");
            // _valkeyService = new(_valkeyConnectionString);
            
        }

        // A: [B, C], B: [A], C: [A] (for testing)
        private static readonly Dictionary<string, List<string>> clientContacts = new()
        {
            { "A", new List<string> { "B", "C" } },
            { "B", new List<string> { "A" } },
            { "C", new List<string> { "A" } }
        };

        // Add client A, B, C to clientPools for testing
        private static readonly Dictionary<string, WebSocket?> clientPools = new()
        {
            { "A", null },
            { "B", null },
            { "C", null }
        };

        private static ConcurrentDictionary<string, WebSocket> activeSockets = new();

        

        public static async Task HandleWebSocketAsync(WebSocket socket, string? clientId)
        {
            // Console.WriteLine($"valkeyConnectionString: {_valkeyConnectionString}");
            // Console.WriteLine($"valkeyConnection: {_valkeyService}");
            // Console.WriteLine($"Getting value from valkey: {_valkeyService.GetValue("test-key")}");

            if (clientId is null)
            {
                Console.WriteLine("Client ID not provided.");
                return;
            }

            // Check if client is already connected
            if (clientPools.ContainsKey(clientId) && clientPools[clientId] != null)
            {
                Console.WriteLine($"Client {clientId} is already connected.");
                return;
            }
            // Add client to clientPools
            clientPools[clientId] = socket;
            // Set now to valkey
            // _valkeyService.SetValue(clientId, DateTime.Now.ToString());
           

            // Broadcast online contacts to all clients
            await Online(clientId);

            // Handle incoming messages
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
                        clientPools[clientId] = null;
                        // _valkeyService.SetValue(clientId, "");
                        await Offline(clientId); //notify all clients that this client is offline
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                    }
                    else
                    {
                        Console.WriteLine($"Received message from {clientId}");
                        messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        if (result.EndOfMessage)
                        {
                            var message = messageBuilder.ToString();
                            // Console.WriteLine($"Message: {message}");
                            await ForwardMessage(socket, message);
                            messageBuilder.Clear();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReceiveMessages Error: {ex.Message}");
            }
        }

        private static async Task SendMessageToTargetClient(string targetClientId, string message)
        {
            if (clientPools.TryGetValue(targetClientId, out WebSocket? targetSocket) && targetSocket?.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await targetSocket.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
            else
            {
                Console.WriteLine($"Target client {targetClientId} not found or not connected.");
            }
        }


        private static async Task ForwardMessage(WebSocket sender, string message)
        {
            if(clientPools.FirstOrDefault(x => x.Value == sender).Key is string senderId)
            {
                // Console.WriteLine($"Forwarding message from {senderId}");
                var messageObject = JsonConvert.DeserializeObject<dynamic>(message);
                // Console.WriteLine($"Message object: {messageObject}");
                var targetClientId = messageObject?.to.ToString();
                // Console.WriteLine($"Target client ID: {targetClientId}");

                // Fetch target client's socket and send the message
                await SendMessageToTargetClient(targetClientId, message);

            }
        }

        public static async Task Online(string clientId)
        {

            var contacts = await _context.Relationships
            .Where(r => (r.UserId == int.Parse(clientId) || r.FriendId == int.Parse(clientId)) && r.Status == RelationshipStatus.Accepted)
            .Select(r => new
            {
                ContactId = r.UserId == int.Parse(clientId) ? r.FriendId : r.UserId,
                ContactName = r.UserId == int.Parse(clientId) ? r.Friend.Username : r.User.Username
            })
            .ToListAsync();
            //iterate and print the contacts
            foreach (var contact in contacts)
            {
                Console.WriteLine($"Contact: {contact}");
            }
            //Contact: { ContactId = 2, ContactName = whale hoho }
            //Contact: { ContactId = 3, ContactName = 蓝鲸吼 }

            // Get my username
            var myUsername = await _context.Users
                .Where(u => u.UID == int.Parse(clientId))
                .Select(u => u.Username)
                .FirstOrDefaultAsync();

            // Iterate through clientPools to find contact, if found, tell them that the client is online
            foreach (var contact in contacts)
            {
                if (clientPools.TryGetValue(contact.ContactId.ToString(), out WebSocket? contactSocket) && contactSocket?.State == WebSocketState.Open)
                {
                    var message = JsonConvert.SerializeObject(new
                    {
                        type = "contact-online",
                        contact = new
                        {
                            contactId = clientId,
                            contactName = myUsername
                        }
                    });

                    var messageBuffer = Encoding.UTF8.GetBytes(message);
                    await contactSocket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }

            // Use contacts to get user's online contacts from clientPools (socket is not null and state is open) and tell user that they are online
            if (clientPools.TryGetValue(clientId, out WebSocket? clientSocket) && clientSocket?.State == WebSocketState.Open)
            {
                var onlineContacts = contacts.Where(c => clientPools.TryGetValue(c.ContactId.ToString(), out WebSocket? contactSocket) && contactSocket?.State == WebSocketState.Open)
                    .Select(c => new
                    {
                        contactId = c.ContactId,
                        contactName = c.ContactName
                    });

                var message = JsonConvert.SerializeObject(new
                {
                    type = "online-contacts",
                    contacts = onlineContacts
                });

                var messageBuffer = Encoding.UTF8.GetBytes(message);
                await clientSocket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public static async Task Offline(string clientId)
        {
            var contacts = await _context.Relationships
            .Where(r => (r.UserId == int.Parse(clientId) || r.FriendId == int.Parse(clientId)) && r.Status == RelationshipStatus.Accepted)
            .Select(r => new
            {
                ContactId = r.UserId == int.Parse(clientId) ? r.FriendId : r.UserId,
                ContactName = r.UserId == int.Parse(clientId) ? r.Friend.Username : r.User.Username
            })
            .ToListAsync();

            // Get my username
            var myUsername = await _context.Users
                .Where(u => u.UID == int.Parse(clientId))
                .Select(u => u.Username)
                .FirstOrDefaultAsync();

            foreach (var contact in contacts)
            {
                if (clientPools.TryGetValue(contact.ContactId.ToString(), out WebSocket? contactSocket) && contactSocket?.State == WebSocketState.Open)
                {
                    var message = JsonConvert.SerializeObject(new
                    {
                        type = "contact-offline",
                        contact = new
                        {
                            contactId = clientId,
                            contactName = myUsername
                        }
                    });

                    var messageBuffer = Encoding.UTF8.GetBytes(message);
                    await contactSocket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

    }
}
