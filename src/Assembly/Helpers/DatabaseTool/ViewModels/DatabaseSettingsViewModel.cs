using Assembly.Helpers.Database.Commands;
using Assembly.Helpers.Database.Models;
using Assembly.Metro.Dialogs;
using Newtonsoft.Json;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Assembly.Helpers.Database.ViewModels
{
    public class DatabaseSettingsViewModel : INotifyPropertyChanged
    {
        #region PrivateProperties

        private string _configFilePath;
        private HashSet<ConnectorType> _databaseTypes;
        private string _mapBackupPath;
        private bool _refresh;
        private string _selectedKey;
        private string _textBoxInput;
        private ObservableCollection<ConnectionViewModel> _connections = new ObservableCollection<ConnectionViewModel>();
        private ObservableCollection<string> _keyModifiers = new ObservableCollection<string>();
        private ConnectionViewModel _timestampCache = new ConnectionViewModel();

        #endregion

        /// <summary>
        /// Default constructor
        /// </summary>
        public DatabaseSettingsViewModel()
        {
            InitializeCommands();
            InitializeHandlers();
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

        /// <summary>
        /// Update collection whenever a property is changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ConnectionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ConnectionViewModel model in e.OldItems)
                {
                    model.PropertyChanged -= ModelPropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (ConnectionViewModel model in e.NewItems)
                {
                    model.PropertyChanged += ModelPropertyChanged;
                }
            }
        }

        /// <summary>
        /// Change refresh value to trigger an update
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Refresh = !Refresh;
        }

        #endregion

        #region PublicProperties

        [JsonIgnore]
        public bool Loaded { get; set; }

        /// <summary>
        /// Used to trigger an update when connection property is changed
        /// </summary>
        [JsonIgnore]
        public bool Refresh
        {
            get { return _refresh; }
            set
            {
                SetField(ref _refresh, value, "Refresh", true);
            }
        }

        /// <summary>
        /// A list of unique database types
        /// </summary>
        public HashSet<ConnectorType> DatabaseTypes
        {
            get { return _databaseTypes; }
            set
            {
                _databaseTypes = Enum.GetValues(typeof(ConnectorType)).Cast<ConnectorType>().ToHashSet();
            }
        }

        /// <summary>
        /// A collection of user-defined database connections
        /// </summary>
        public ObservableCollection<ConnectionViewModel> Connections
        {
            get { return _connections; }
            set
            {
                SetField(ref _connections, value, "Connections", true);
            }
        }

        /// <summary>
        /// The path to backup .map files to before downloading tag meta from a database
        /// </summary>
        public string MapBackupPath
        {
            get { return _mapBackupPath; }
            set
            {
                SetField(ref _mapBackupPath, value, "MapBackupPath", true);
            }
        }

        /// <summary>
        /// A user-defined connection to store last modified dates for tags
        /// </summary>
        public ConnectionViewModel TimestampCache
        {
            get { return _timestampCache; }
            set
            {
                SetField(ref _timestampCache, value, "TimestampCache", true);
            }
        }

        /// <summary>
        /// A collection of user-defined values to prepend to keys when uploading / downloading tag meta from a database
        /// </summary>
        public ObservableCollection<string> KeyModifiers
        {
            get { return _keyModifiers; }
            set
            {
                SetField(ref _keyModifiers, value, "KeyModifiers", true);
            }
        }

        /// <summary>
        /// Path to where a database config file is stored
        /// </summary>
        public string ConfigFilePath
        {
            get { return _configFilePath; }
            set
            {
                SetField(ref _configFilePath, value, "ConfigFilePath", true);
            }
        }

        /// <summary>
        /// Update key modifier text input after adding or removing keys
        /// </summary>
        [JsonIgnore]
        public string TextBoxInput
        {
            get { return _textBoxInput; }
            set
            {
                SetField(ref _textBoxInput, value, "TextBoxInput", true);
            }
        }

        /// <summary>
        /// The value of a selected key from the key modifiers combobox
        /// </summary>
        [JsonIgnore]
        public string SelectedKey
        {
            get { return _selectedKey; }
            set
            {
                SetField(ref _selectedKey, value, "SelectedKey", true);
            }
        }

        #endregion

        #region Commands

        [JsonIgnore]
        public RelayCommand AddConnectionCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand RemoveConnectionCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand TestConnectionCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand BackupDatabaseCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand SaveDatabaseCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand WipeDatabaseCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand StartServerCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand KillServerCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand AddKeyCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand RemoveKeyCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand SelectBackupPathCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand SelectConfigPathCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand SelectDatabasePathCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand ClearKeysCommand { get; private set; }
        [JsonIgnore]
        public RelayCommand ShowHideSettingsCommand { get; private set; }

        /// <summary>
        /// Add a new database connection
        /// </summary>
        /// <param name="connection">The connection being added</param>
        public void AddDatabaseConnection(object connection)
        {
            Connections.Add(new ConnectionViewModel());
        }

        /// <summary>
        /// Remove a database connection
        /// </summary>
        /// <param name="connection">The connection being removed</param>
        public void RemoveDatabaseConnection(object connection)
        {
            Connections.Remove((ConnectionViewModel)connection);
        }

        /// <summary>
        /// Add a key modifier to the KeyModifiers collection.
        /// </summary>
        /// <param name="modifier">The key modifier to add</param>
        public void AddKeyModifier(object modifier)
        {
            // Remove all whitespace and non-word characters, except for underscores
            var mod = Regex.Replace((string)modifier, @"[\W]+", "");

            // Check if object is empty
            if (mod != "")
            {
                // Check if key modifier already exists
                var existing = KeyModifiers.FirstOrDefault(k => k == mod);
                if (existing == null)
                {
                    // Add key modifier
                    KeyModifiers.Add(mod);
                }
            }

            // Clear the text input
            TextBoxInput = "";
        }

        /// <summary>
        /// Remove a key modifier from the KeyModifiers collection
        /// </summary>
        /// <param name="modifier">The key modifier to remove</param>
        public void RemoveKeyModifier(object modifier)
        {
            if (SelectedKey == "None" || SelectedKey == "Map Name")
            {
                return;
            }

            KeyModifiers.Remove(SelectedKey);
            SelectedKey = null;
        }

        /// <summary>
        /// Delete all key modifiers
        /// </summary>
        /// <param name="clear">The key modifier collection</param>
        public void ClearKeyModifiers(object keys)
        {
            // Prompt if user wants to continue
            var result = MetroMessageBox.Show("Delete Key Modifers", "You are about to delete all the custom keys, are you sure you want to continue?", MetroMessageBox.MessageBoxButtons.YesNo);

            if (result != MetroMessageBox.MessageBoxResult.Yes)
            {
                return;
            }

            KeyModifiers.Clear();

            KeyModifiers.Add("None");
            KeyModifiers.Add("Map Name");
        }

        /// <summary>
        /// Select a directory to backup .map files to
        /// </summary>
        /// <param name="path">The selected directory</param>
        public void SelectMapBackupPath(object path)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog
            {
                Description = "Map Backup Path",
                UseDescriptionForTitle = true
            };

            dialog.ShowDialog();

            MapBackupPath = dialog.SelectedPath;
        }

        /// <summary>
        /// Select the database path to load from
        /// </summary>
        /// <param name="connection">The database connection</param>
        private void SelectDatabasePath(object connection)
        {
            ConnectionViewModel database = (ConnectionViewModel)connection;

            // Get the .rdb name and location
            VistaOpenFileDialog dialog = new VistaOpenFileDialog();

            switch (database.DatabaseType)
            {
                case ConnectorType.Redis:
                    dialog.Filter = "Redis database files (*.rdb)|*.rdb";
                    break;
                default:
                    break;
            }

            dialog.ShowDialog();

            // Handle empty path when user cancels dialog
            if (dialog.FileName.Length == 0)
            {
                return;
            }

            dialog.OpenFile();

            var fullPath = dialog.FileName;
            database.DatabasePath = fullPath;
        }

        private void SelectConfigFilePath(object connection)
        {
            ConnectionViewModel database = (ConnectionViewModel)connection;

            // Get the .conf name and location
            VistaOpenFileDialog dialog = new VistaOpenFileDialog
            {
                Filter = "Config files (*.conf)|*.conf"
            };

            dialog.ShowDialog();

            // Handle empty path when user cancels dialog
            if (dialog.FileName.Length == 0)
            {
                return;
            }

            dialog.OpenFile();

            var fullPath = dialog.FileName;
            database.ConfigPath = fullPath;
        }

        /// <summary>
        /// Delete all entries inside a database
        /// </summary>
        /// <param name="connection">The database connection</param>
        public void WipeDatabase(object connection)
        {
            var database = (ConnectionViewModel)connection;

            // Test the connection
            if (!database.ConnectionIsSuccessful())
            {
                return;
            }

            // Retrieve the encrypted database password
            string _password = database.PasswordResolver();

            RedisConnector connector = new RedisConnector(database.Host, database.Port, Convert.ToInt32(database.Database), _password);

            // Prompt if user wants to continue
            var result = MetroMessageBox.Show("Wipe Database Contents", "You are about to delete everything from this database, are you sure you want to continue?", MetroMessageBox.MessageBoxButtons.YesNo);

            if (result != MetroMessageBox.MessageBoxResult.Yes)
            {
                return;
            }

            connector.ClearDatabaseContents();
        }

        public void SaveDatabase(object connection)
        {
            // Get the .rdb name and location
            VistaSaveFileDialog dialog = new VistaSaveFileDialog
            {
                Filter = "Redis database Files (*.rdb)|*.rdb",
                DefaultExt = "rdb"
            };

            dialog.ShowDialog();

            // Handle empty path when user cancels dialog
            if (dialog.FileName.Length == 0)
            {
                return;
            }

            var fullPath = dialog.FileName.Replace('\\', '/');
            var strCmdText = $"/C memurai-cli --rdb { fullPath }";
            
            Process.Start("CMD.exe", strCmdText);
        }

        /// <summary>
        /// Manually start a database server
        /// </summary>
        /// <param name="connection">The database connection</param>
        private void StartServer(object connection)
        {
            // TODO: provide options for different database types

            ConnectionViewModel database = (ConnectionViewModel)connection;
            string strCmdText;

            // Check if the connection is valid
            if (!database.IsValidConnection())
            {
                return;
            }

            var fullPath = database.DatabasePath ?? "";
            var fileName = Regex.Match(fullPath, @"[\w]+(.rdb)").Value;

            // Handle invalid paths
            if (fullPath != "" && ( !File.Exists(database.DatabasePath) || fileName.Length == 0) )
            {
                MetroMessageBox.Show("Path Error", "Please assign the correct path, or keep it empty to use the default location.");

                return;
            }

            // Retrieve the encrypted database password
            string _password = database.PasswordResolver();

            // Using the custom config path (or default if none), start a redis instance in the background
            // TODO: command line args should work for redis-server as well
            // TODO: terminate if db software is not installd (e.g. memurai.exe, redis-server)

            // Handle configuration with no database path
            // Handle configuration with no password
            if (fullPath != "")
            {
                var directory = fullPath.Replace(fileName, "").Replace('\\', '/');

                if (_password == "")
                {
                    strCmdText = $"/k memurai.exe { _configFilePath } --dbfilename { fileName } --dir { directory } --port { database.Port }";
                }
                else
                {
                    strCmdText = $"/k memurai.exe { _configFilePath } --dbfilename { fileName } --dir { directory } --port { database.Port } --requirepass { _password }";

                }

            }
            else
            {
                if (_password == "")
                {
                    strCmdText = $"/k memurai.exe { _configFilePath } --port { database.Port }";

                }
                else
                {
                    strCmdText = $"/k memurai.exe { _configFilePath } --port { database.Port } --requirepass { _password }";

                }
            }

            // Launch command prompt with a new redis server
            // TODO: terminate if port is in use
            Process p = new Process();
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "CMD.EXE",
                Arguments = strCmdText
            };

            p.StartInfo = psi;

            database.ProcessInfo = p;

            p.Start();
        }

        /// <summary>
        /// Kill a database server
        /// </summary>
        /// <param name="connection">The database connection</param>
        private void KillServer(object connection)
        {
            ConnectionViewModel database = (ConnectionViewModel)connection;
            
            // Check if a connection can be made with the database
            if (!database.ConnectionIsSuccessful())
            { 
                return;
            }

            // Check if the server is running
            if (database.ProcessInfo == null)
            {
                MetroMessageBox.Show("Server Error", "Server is not running");
                return;
            }

            database.ProcessInfo.CloseMainWindow();
        }

        /// <summary>
        /// Test the database connection
        /// </summary>
        /// <param name="connection">The database connection</param>
        public void TestDatabaseConnection(object connection)
        {
            ConnectionViewModel database = (ConnectionViewModel)connection;

            // Test the connection
            if (!database.ConnectionIsSuccessful())
            {
                return;
            }

            MetroMessageBox.Show("Success", "Connection Successful");
        }

        private void ShowHideAdvancedSettings(object connection)
        {
            var database = (ConnectionViewModel)connection;
            database.IsHidden = !database.IsHidden;
        }

        #endregion

        #region HelperMethods

        /// <summary>
        /// Assign all commands
        /// </summary>
        private void InitializeCommands()
        {
            SelectBackupPathCommand = new RelayCommand(SelectMapBackupPath);
            SelectConfigPathCommand = new RelayCommand(SelectConfigFilePath);

            AddConnectionCommand = new RelayCommand(AddDatabaseConnection);
            RemoveConnectionCommand = new RelayCommand(RemoveDatabaseConnection);
            TestConnectionCommand = new RelayCommand(TestDatabaseConnection);
            BackupDatabaseCommand = new RelayCommand(SaveDatabase);
            SaveDatabaseCommand = new RelayCommand(SaveDatabase);
            WipeDatabaseCommand = new RelayCommand(WipeDatabase);
            StartServerCommand = new RelayCommand(StartServer);
            KillServerCommand = new RelayCommand(KillServer);
            SelectDatabasePathCommand = new RelayCommand(SelectDatabasePath);

            AddKeyCommand = new RelayCommand(AddKeyModifier);
            RemoveKeyCommand = new RelayCommand(RemoveKeyModifier);
            ClearKeysCommand = new RelayCommand(ClearKeyModifiers);
            ShowHideSettingsCommand = new RelayCommand(ShowHideAdvancedSettings);
        }

        /// <summary>
        /// Assign all property changed handlers
        /// </summary>
        private void InitializeHandlers()
        {
            Connections.CollectionChanged += ConnectionsCollectionChanged;
            Connections.CollectionChanged += (sender, args) => SetField(ref _connections, sender as ObservableCollection<ConnectionViewModel>, "Connections", true);
            KeyModifiers.CollectionChanged += (sender, args) => SetField(ref _keyModifiers, sender as ObservableCollection<string>, "KeyModifiers", true);
            TimestampCache.PropertyChanged += (sender, args) => SetField(ref _timestampCache, sender as ConnectionViewModel, "TimestampCache", true);
        }

        #endregion
    }
}
