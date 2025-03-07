using System;
using System.Security.Authentication;
using StackExchange.Redis;

namespace VideoActive.Services
{
    public class ValkeyService
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public ValkeyService(string connectionString)
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.Ssl = true;  // Enable SSL
            options.SslProtocols = SslProtocols.Tls12;  // Force TLS 1.2 (or Tls13 if supported)
            options.AbortOnConnectFail = false;  // Prevent connection failures on startup

            // Ignore certificate name mismatch (for local dev)
            // The RemoteCertificateNameMismatch error happens when the SSL/TLS certificate's hostname does not match the server's hostname
            options.CertificateValidation += (sender, certificate, chain, sslPolicyErrors) => true;

            _redis = ConnectionMultiplexer.Connect(options);
            _db = _redis.GetDatabase();
        }

        public void SetValue(string key, string value)
        {
            _db.StringSet(key, value);
        }

        public string GetValue(string key)
        {
            return _db.StringGet(key).ToString() ?? string.Empty;
        }

        // Subscribe to a channel
        public void Subscribe(string channel, Action<RedisChannel, RedisValue> handler)
        {
            var sub = _redis.GetSubscriber();
            sub.Subscribe(channel, handler);
        }

        // Publish a message to a channel, the message parameter is json serialized
        public void Publish(string channel, string message)
        {
            var sub = _redis.GetSubscriber();
            sub.Publish(channel, message);
        }
        
    }
}
