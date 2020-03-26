using StackExchange.Redis;
using System.Collections.Generic;
using System.Linq;

namespace Assembly.Helpers.Database.Models
{
    /// <summary>
    /// A database connector class managing connections with Redis.
    /// </summary>
    class RedisConnector : IDatabaseConnector
    {
        private ConnectionMultiplexer _connection;
        private IDatabase _database;
        private IServer _server;
        private bool _connectionSuccess;
        private ConfigurationOptions _config;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="host">The database host name. Defaults to localhost.</param>
        /// <param name="port">The database port number. Defaults to 6379.</param>
        /// <param name="database">The database number. Defaults to 0.</param>
        /// <param name="password">The (optional) database password.</param>
        public RedisConnector(string host = "localhost", int port = 6379, int database = 0, string password = "")
        {
            _config = new ConfigurationOptions
            {
                // allow usage of method: FlushDatabase
                AllowAdmin = true,

                DefaultDatabase = database,
                Password = password,
                EndPoints =
                {
                    { host, port }
                }
            };

            // create database and server instances only if connection is successful
            bool success = TestConnection();
            if (success == true)
            {
                _database = _connection.GetDatabase(database);
                _server = _connection.GetServer($"{ host }:{ port }");
            }
        }

        // TODO: add method to backup database and restore

        /// <summary>
        /// Delete all keys inside the database, or only those that start with a special key modifier.
        /// </summary>
        /// <param name="keyModifier">The key to delete by. Set to empty if none.</param>
        public void ClearDatabaseContents(string keyModifier = null)
        {
            if (keyModifier == null)
            {
                _server.FlushDatabase();
            }
            else
            {
                foreach (var key in _server.Keys(pattern: $"{ keyModifier }*"))
                {
                    _database.KeyDelete(key);
                }
            }
        }

        /// <summary>
        /// Download a serialized byte array from a defined key and optional key modifier in redis.
        /// </summary>
        /// <param name="key">The key to retrieve the value from</param>
        /// <param name="keyModifier">Optional key modifier</param>
        public object DownloadFromDatabase(string key, string keyModifier = null)
        {
            object _data;

            if (keyModifier != null)
            {
                _data = _database.StringGet($"{ keyModifier }:{ key }");
            }
            else
            {
                _data = _database.StringGet(key);
            }

            return _data;
        }

        /// <summary>
        /// Upload a serialized byte array to a defined key and optional key modifier in redis.
        /// </summary>
        /// <param name="key">The key to store the value to</param>
        /// <param name="value">The value being stored</param>
        /// <param name="keyModifier">Optional key modifier</param>
        public void UploadToDatabase(string key, byte[] value, string keyModifier = null)
        {
            if (_connectionSuccess == true)
            {
                if (keyModifier != null)
                {
                    _database.StringSet($"{ keyModifier }:{ key }", value);
                }
                else
                {
                    _database.StringSet(key, value);
                }
            }
        }
        
        public IEnumerable<RedisKey> GetKeysByPattern(string keyPattern)
        {
            var result = _server.Keys(pattern: $"{ keyPattern }*");

            return result;
        }

        public IDatabase Database
        {
            get { return _database; }
        }

        public IServer Server
        {
            get { return _server; }
        }

        public bool TestConnection()
        {
            try
            {
                _connection = ConnectionMultiplexer.Connect(_config);
                _connectionSuccess = true;
            }
            catch (RedisConnectionException)
            {
                _connectionSuccess = false;
            }

            return _connectionSuccess;
        }
    }
}
