using Assembly.Helpers.Database.Commands;
using Assembly.Helpers.Database.Models;
using Assembly.Metro.Dialogs;
using Newtonsoft.Json;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Assembly.Helpers.Database.ViewModels
{
    public class DatabaseToolViewModel : INotifyPropertyChanged
    {
        #region PrivateProperties

        private Settings _assemblySettings = new Settings();
        private DatabaseSettingsViewModel _databaseSettings = new DatabaseSettingsViewModel();
        private string _mapPath;
        private string _mapDirectory;
        private ObservableCollection<ConnectionViewModel> _connections;
        private ObservableCollection<string> _keyModifiers;
        private bool _useTimestampsSingle;
        private bool _useTimestampsBatch;
        private bool _backupMapsSingle;
        private bool _backupMapsBatch;
        private ObservableCollection<string> _maps = new ObservableCollection<string>();
        private ConnectionViewModel _selectedConnectionSingle;
        private ConnectionViewModel _selectedConnectionBatch;
        private string _selectedKeySingle;
        private string _selectedKeyBatch;
        private string _selectedMap;
        private bool _singleSelected;

        #endregion

        #region Setup

        /// <summary>
        /// Load database settings data
        /// </summary>
        public void Load()
        {
            // Get File Path
            string jsonString = null;
            if (File.Exists("AssemblySettings.ason"))
                jsonString = File.ReadAllText("AssemblySettings.ason");

            try
            {
                if (jsonString == null)
                    _assemblySettings = new Settings();
                else
                    _assemblySettings = JsonConvert.DeserializeObject<Settings>(jsonString) ?? new Settings();
            }
            catch (JsonSerializationException)
            {
                _assemblySettings = new Settings();
            }

            // Get database settings
            _databaseSettings = _assemblySettings.DatabaseSettings;

            // Dispose of assembly settings
            _assemblySettings = null;
        }

        public DatabaseToolViewModel()
        {
            Load();
            InitializeCommands();
        }

        #endregion

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

        /// <summary>
        /// Whether or not the single-update tab is selected
        /// </summary>
        public bool SingleSelected
        {
            get { return _singleSelected; }
            set
            {
                SetField(ref _singleSelected, value, "SingleSelected", true);
            }
        }

        /// <summary>
        /// The selected .map file path
        /// </summary>
        public string MapPath
        {
            get { return _mapPath; }
            set
            {
                SetField(ref _mapPath, value, "MapPath", true);
            }
        }

        /// <summary>
        /// The selected map directory path
        /// </summary>
        public string MapDirectory
        {
            get { return _mapDirectory; }
            set
            {
                SetField(ref _mapDirectory, value, "MapDirectory", true);
            }
        }

        /// <summary>
        /// Database settings data retrieved from AssemblySettings.ason
        /// </summary>
        public DatabaseSettingsViewModel DatabaseSettings
        {
            get { return _databaseSettings; }
            set
            {
                SetField(ref _databaseSettings, value, "DatabaseSettings", true);
            }
        }

        /// <summary>
        /// The available database connections
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
        /// The available key modifiers
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
        /// Whether or not to use timestamps for read / write operations
        /// </summary>
        public bool UseTimestampsSingle
        {
            get { return _useTimestampsSingle; }
            set
            {
                SetField(ref _useTimestampsSingle, value, "UseTimestampsSingle", true);
            }
        }

        /// <summary>
        /// Whether or not to use timestamps for read / write operations
        /// </summary>
        public bool UseTimestampsBatch
        {
            get { return _useTimestampsBatch; }
            set
            {
                SetField(ref _useTimestampsBatch, value, "UseTimestampsBatch", true);
            }
        }

        /// <summary>
        /// Whether or not to backup maps before read / write operations
        /// </summary>
        public bool BackupMapsSingle
        {
            get { return _backupMapsSingle; }
            set
            {
                SetField(ref _backupMapsSingle, value, "BackupMapsSingle", true);
            }
        }

        /// <summary>
        /// Whether or not to backup maps before read / write operations
        /// </summary>
        public bool BackupMapsBatch
        {
            get { return _backupMapsBatch; }
            set
            {
                SetField(ref _backupMapsBatch, value, "BackupMapsBatch", true);
            }
        }

        /// <summary>
        /// The .map files in the selected map directory
        /// </summary>
        public ObservableCollection<string> Maps
        {
            get { return _maps; }
            set
            {
                SetField(ref _maps, value, "Maps", true);
            }
        }

        /// <summary>
        /// The selected database connection
        /// </summary>
        public ConnectionViewModel SelectedConnectionSingle
        {
            get { return _selectedConnectionSingle; }
            set
            {
                SetField(ref _selectedConnectionSingle, value, "SelectedConnectionSingle", true);
            }
        }

        /// <summary>
        /// The selected database connection
        /// </summary>
        public ConnectionViewModel SelectedConnectionBatch
        {
            get { return _selectedConnectionBatch; }
            set
            {
                SetField(ref _selectedConnectionBatch, value, "SelectedConnectionBatch", true);
            }
        }

        /// <summary>
        /// The selected key modifier
        /// </summary>
        public string SelectedKeySingle
        {
            get { return _selectedKeySingle; }
            set
            {
                SetField(ref _selectedKeySingle, value, "SelectedKeySingle", true);
            }
        }

        /// <summary>
        /// The selected key modifier
        /// </summary>
        public string SelectedKeyBatch
        {
            get { return _selectedKeyBatch; }
            set
            {
                SetField(ref _selectedKeyBatch, value, "SelectedKeyBatch", true);
            }
        }

        /// <summary>
        /// The selected baseline map
        /// </summary>
        public string SelectedMap
        {
            get { return _selectedMap; }
            set
            {
                SetField(ref _selectedMap, value, "SelectedMap", true);
            }
        }

        #endregion

        #region Commands

        public RelayCommand SelectMapPathCommand { get; private set; }
        public RelayCommand SelectMapDirectoryCommand { get; private set; }
        public RelayCommand UploadCommand { get; private set; }
        public RelayCommand DownloadCommand { get; private set; }
        public RelayCommand RefreshCommand { get; private set; }
      
        /// <summary>
        /// The .map file to read / write from
        /// </summary>
        /// <param name="path">File path</param>
        private void SelectMapPath(object path)
        {
            // Get the .conf name and location
            VistaOpenFileDialog dialog = new VistaOpenFileDialog
            {
                Filter = "Map files (*.map)|*.map"
            };

            dialog.ShowDialog();

            // Handle empty path when user cancels dialog
            if (dialog.FileName.Length == 0)
            {
                return;
            }

            //dialog.OpenFile();

            MapPath = dialog.FileName;
        }

        /// <summary>
        /// The .map directory to read / write from
        /// </summary>
        /// <param name="directory">Directory path</param>
        private void SelectMapDirectory(object directory)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog
            {
                Description = "Map Backup Path",
                UseDescriptionForTitle = true
            };

            dialog.ShowDialog();

            MapDirectory = dialog.SelectedPath;
            GetMaps(MapDirectory);
        }

        /// <summary>
        /// Upload tag metadata from .map file(s) to database
        /// </summary>
        /// <param name="connection">Database connection</param>
        public void UploadToDatabase(object connection)
        {
            string message;

            // Check which tab is selected
            if (SingleSelected)
            {
                // Handle empty database name
                if (SelectedConnectionSingle == null)
                {
                    MetroMessageBox.Show("Name Error", "No connection name provided");
                    return;
                }

                // Check if the map backup path is valid
                if (BackupMapsSingle == true && !Directory.Exists(DatabaseSettings.MapBackupPath))
                {
                    MetroMessageBox.Show("Path Error", "Backup directory is not valid");
                    return;
                }

                // Test database connection
                if (!SelectedConnectionSingle.ConnectionIsSuccessful())
                {
                    return;
                }
                
                // Check if timestamp cache connection is available
                if (UseTimestampsSingle == true && !DatabaseSettings.TimestampCache.ConnectionIsSuccessful())
                {
                    MetroMessageBox.Show("Connection Error", "Could not connect to timestamp database");
                    return;
                }

                DatabaseTool tool = new DatabaseTool(SelectedConnectionSingle, SelectedKeySingle, BackupMapsSingle, UseTimestampsSingle, 
                    DatabaseSettings.TimestampCache, DatabaseSettings.MapBackupPath, MapPath);

                tool.UploadTagData(MapPath);

                message = $"Connection Name: {SelectedConnectionSingle.Name}\nKey: {SelectedKeySingle}\nTimestamps: {UseTimestampsSingle}\nBackup: {BackupMapsSingle}";

            }
            else
            {
                // Handle empty database name
                if (SelectedConnectionBatch == null)
                {
                    MetroMessageBox.Show("Name Error", "No connection name provided");
                    return;
                }

                // Check if the map backup path is valid
                if (BackupMapsBatch == true && !Directory.Exists(DatabaseSettings.MapBackupPath))
                {
                    MetroMessageBox.Show("Path Error", "Backup directory is not valid");
                    return;
                }

                // Test database connection
                if (!SelectedConnectionBatch.ConnectionIsSuccessful())
                {
                    return;
                }

                // Check if timestamp cache connection is available
                if (UseTimestampsBatch == true && !DatabaseSettings.TimestampCache.ConnectionIsSuccessful())
                {
                    MetroMessageBox.Show("Connection Error", "Could not connect to timestamp database");
                    return;
                }

                DatabaseTool tool = new DatabaseTool(SelectedConnectionBatch, SelectedKeyBatch, BackupMapsBatch, UseTimestampsBatch,
                    DatabaseSettings.TimestampCache, DatabaseSettings.MapBackupPath, MapPath, MapDirectory, Maps.ToList(), SelectedMap);

                tool.UploadTagDataBatch();

                message = $"Connection Name: {SelectedConnectionBatch.Name}\nKey: {SelectedKeyBatch}\nTimestamps: {UseTimestampsBatch}\nBackup: {BackupMapsBatch}";
            }

            MetroMessageBox.Show(message);
        }

        /// <summary>
        /// Download tag metadata from database to .map file(s)
        /// </summary>
        /// <param name="connection">Database connection</param>
        public void DownloadFromDatabase(object connection)
        {
            string message;

            // Check which tab is selected
            if (SingleSelected)
            {
                // Handle empty database name
                if (SelectedConnectionSingle == null)
                {
                    MetroMessageBox.Show("Name Error", "No connection name provided");
                    return;
                }

                // Check if the map backup path is valid
                if (BackupMapsSingle == true && !Directory.Exists(DatabaseSettings.MapBackupPath))
                {
                    MetroMessageBox.Show("Path Error", "Backup directory is not valid");
                    return;
                }

                // Test database connection
                if (!SelectedConnectionSingle.ConnectionIsSuccessful())
                {
                    return;
                }

                // Check if timestamp cache connection is available
                if (UseTimestampsSingle == true && !DatabaseSettings.TimestampCache.ConnectionIsSuccessful())
                {
                    MetroMessageBox.Show("Connection Error", "Could not connect to timestamp database");
                    return;
                }

                DatabaseTool tool = new DatabaseTool(SelectedConnectionSingle, SelectedKeySingle, BackupMapsSingle, UseTimestampsSingle,
                    DatabaseSettings.TimestampCache, DatabaseSettings.MapBackupPath, MapPath);

                tool.DownloadTagData(MapPath);

                message = $"Connection Name: {SelectedConnectionSingle.Name}\nKey: {SelectedKeySingle}\nTimestamps: {UseTimestampsSingle}\nBackup: {BackupMapsSingle}";

            }
            else
            {
                // Handle empty database name
                if (SelectedConnectionBatch == null)
                {
                    MetroMessageBox.Show("Name Error", "No connection name provided");
                    return;
                }

                // Check if the map backup path is valid
                if (BackupMapsBatch == true && !Directory.Exists(DatabaseSettings.MapBackupPath))
                {
                    MetroMessageBox.Show("Path Error", "Backup directory is not valid");
                    return;
                }

                // Test database connection
                if (!SelectedConnectionBatch.ConnectionIsSuccessful())
                {
                    return;
                }

                // Check if timestamp cache connection is available
                if (UseTimestampsBatch == true && !DatabaseSettings.TimestampCache.ConnectionIsSuccessful())
                {
                    MetroMessageBox.Show("Connection Error", "Could not connect to timestamp database");
                    return;
                }

                DatabaseTool tool = new DatabaseTool(SelectedConnectionBatch, SelectedKeyBatch, BackupMapsBatch, UseTimestampsBatch,
                    DatabaseSettings.TimestampCache, DatabaseSettings.MapBackupPath, MapPath, SelectedMap);

                tool.DownloadTagDataBatch();

                message = $"Connection Name: {SelectedConnectionBatch.Name}\nKey: {SelectedKeyBatch}\nTimestamps: {UseTimestampsBatch}\nBackup: {BackupMapsBatch}";
            }

            MetroMessageBox.Show(message);
        }
        
        /// <summary>
        /// Reload database settings
        /// </summary>
        /// <param name="settings">Database settings</param>
        public void RefreshDatabaseSettings(object settings)
        {
            Load();
            DatabaseSettings = _databaseSettings;
        }

        #endregion

        #region HelperMethods

        /// <summary>
        /// Assign all commands
        /// </summary>
        private void InitializeCommands()
        {
            SelectMapPathCommand = new RelayCommand(SelectMapPath);
            SelectMapDirectoryCommand = new RelayCommand(SelectMapDirectory);
            UploadCommand = new RelayCommand(UploadToDatabase);
            DownloadCommand = new RelayCommand(DownloadFromDatabase);
            RefreshCommand = new RelayCommand(RefreshDatabaseSettings);
        }

        /// <summary>
        /// Add shortened map names to map directory list
        /// </summary>
        /// <param name="mapDirectory">The selected map directory</param>
        private void GetMaps(string mapDirectory)
        {
            // Check if directory exists
            if (!Directory.Exists(mapDirectory))
            {
                return;
            }

            var maps = Directory.GetFiles(mapDirectory).ToList();

            // Extract end-value for each map (e.g. forge_halo.map)
            foreach (string map in maps)
            {
                var mapName = Regex.Match(map, @"[\w]+(.map)$").Value;
                
                // Skip empty matches
                if (mapName.Length == 0)
                {
                    return;
                }
                
                Maps.Add(mapName);
            }
        }

        #endregion
    }
}
