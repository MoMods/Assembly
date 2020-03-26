using Assembly.Helpers.Database.Models;
using Assembly.Metro.Dialogs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;

namespace Assembly.Helpers.Database.ViewModels
{
    public class ConnectionViewModel : INotifyPropertyChanged
    {
        #region PrivateProperties

        private string _name;
        private ConnectorType _databaseType;
        private string _keyModifier;
        private bool _useTimestamps;
        private string _passwordKey;
        private string _host;
        private int _port;
        private string _database;
        private bool _isHidden;
        private string _databasePath;
        private Process _processInfo;
        private string _configPath;

        #endregion

        /// <summary>
        /// Default constructor
        /// </summary>
        public ConnectionViewModel()
        {
            Name = "Name";
            Host = "localhost";
            Port = 6379;
            Database = "0";
            DatabaseType = ConnectorType.Redis;
            UseTimestamps = false;
            KeyModifier = null;
            PasswordKey = "";
        }

        #region EventHandlers

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, string propertyName, bool overrideChecks = false)
        {
            if (!overrideChecks)
                if (EqualityComparer<T>.Default.Equals(field, value))
                    return false;

            field = value;
            OnPropertyChanged(propertyName);

            return true;
        }

        #endregion

        #region PublicProperties

        public string Name
        {
            get { return _name; }
            set
            {
                SetField(ref _name, value, "Name", true);
            }
        }

        public ConnectorType DatabaseType
        {
            get { return _databaseType; }
            set
            {
                SetField(ref _databaseType, value, "DatabaseType", true);
            }
        }
        public bool UseTimestamps
        {
            get { return _useTimestamps; }
            set
            {
                SetField(ref _useTimestamps, value, "UseTimestamps", true);
            }
        }

        public string KeyModifier
        {
            get { return _keyModifier; }
            set
            {
                SetField(ref _keyModifier, value, "KeyModifier", true);
            }
        }

        public string PasswordKey
        {
            get { return _passwordKey; }
            set
            {
                SetField(ref _passwordKey, value, "PasswordKey", true);
            }
        }

        public string Host
        {
            get { return _host; }
            set
            {
                SetField(ref _host, value, "Host", true);
            }
        }

        public int Port
        {
            get { return _port; }
            set
            {
                SetField(ref _port, value, "Port", true);
            }
        }

        public string Database
        {
            get { return _database; }
            set
            {
                SetField(ref _database, value, "Database");
            }
        }

        public bool IsHidden
        {
            get { return _isHidden; }
            set
            {
                SetField(ref _isHidden, value, "IsHidden");
            }
        }

        public string DatabasePath
        {
            get { return _databasePath; }
            set
            {
                SetField(ref _databasePath, value, "DatabasePath");
            }
        }

        [JsonIgnore]
        public Process ProcessInfo
        {
            get { return _processInfo; }
            set
            {
                SetField(ref _processInfo, value, "ProcessInfo");
            }
        }

        public string ConfigPath
        {
            get { return _configPath; }
            set
            {
                SetField(ref _configPath, value, "ConfigPath");
            }
        }

        #endregion

        #region HelperMethods

        /// <summary>
        /// Check if a connection can be made to the database
        /// </summary>
        /// <param name="this">The database connection</param>
        /// <returns>Connection result</returns>
        public bool ConnectionIsSuccessful()
        {
            bool _isSuccess = false;

            // Check if the connection is valid
            if (!this.IsValidConnection())
            {
                return _isSuccess;
            }

            // Retrieve the encrypted database password
            string _password = this.PasswordResolver();

            RedisConnector _connector = new RedisConnector(this.Host, this.Port, Convert.ToInt32(this.Database), _password);

            // Check if database is up and running
            if (!IsDatabaseUp(_connector))
            {
                return _isSuccess;
            }

            _isSuccess = true;

            return _isSuccess;
        }

        /// <summary>
        /// Check if the database connection is up
        /// </summary>
        /// <param name="connector">The database connection</param>
        /// <returns>Connection result</returns>
        private bool IsDatabaseUp(RedisConnector connector)
        {
            bool _success = false;

            // Check if the connection is valid
            bool success = connector.TestConnection();

            if (success == false)
            {
                MetroMessageBox.Show("Connection Error", "Could not connect to the database");
                return _success;
            }

            _success = true;

            return _success;
        }

        /// <summary>
        /// Check if the database connection parameters are valid
        /// </summary>
        /// <param name="connection">The database connection</param>
        /// <returns>Parameters result</returns>
        public bool IsValidConnection()
        {
            bool _valid;

            // Check if inputs are valid
            // TODO: test should return false port value is empty
            if (this.Host.Length == 0 || this.Port < 0 || this.Database.Length == 0)
            {
                _valid = false;

                MetroMessageBox.Show("Configuration Error", "Please make sure you have a valid Host, Port, and Database Name configured");
            }
            else
            {
                _valid = true;
            }

            return _valid;
        }

        /// <summary>
        /// Retrieve encrypted password from .config file
        /// </summary>
        /// <param name="key">Password key name</param>
        /// <returns></returns>
        public string PasswordResolver()
        {
            string returnValue = "";

            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[this.PasswordKey];

            if (settings != null)
            {
                returnValue = settings.ConnectionString;
            }

            return returnValue;
        }

        #endregion
    }
}
