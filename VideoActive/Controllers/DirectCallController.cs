using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;

namespace VideoActive.WebSocketHandlers
{
    public class DirectCallHandler
    {
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

        

        public static async Task HandleWebSocketAsync(WebSocket socket, string? clientId)
        {
            if (clientId is null)
            {
                Console.WriteLine("Client ID not provided.");
                return;
            }

            // Add client to clientPools
            clientPools[clientId] = socket;

            // Broadcast online contacts to all clients
            await BroadcastOnlineContacts();

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
                        await BroadcastOnlineContacts();
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

        public static async Task BroadcastOnlineContacts()
        {
            // Get all online clients' contact lists and notify them
            foreach (var (contactId, contactSocket) in clientPools)
            {
                // Ensure the contact is online and has contacts associated with them
                if (contactSocket?.State == WebSocketState.Open && clientContacts.TryGetValue(contactId, out List<string>? contacts))
                {

                    // Filter contacts to include only those who are online
                    var onlineContacts = contacts
                        .Where(contact => clientPools.TryGetValue(contact, out var socket) && socket?.State == WebSocketState.Open)
                        .ToList();

                    // Create a message with all the contacts' online status
                    var message = JsonConvert.SerializeObject(new
                    {
                        type = "contacts-online",
                        clientId = contactId, // We're sending this list for each client
                        contacts = onlineContacts
                    });

                    var messageBuffer = Encoding.UTF8.GetBytes(message);
                    await contactSocket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

    }
}
