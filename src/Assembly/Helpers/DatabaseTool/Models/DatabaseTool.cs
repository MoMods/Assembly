using Assembly.Helpers.Database.ViewModels;
using Assembly.Metro.Controls.PageTemplates.Games;
using Assembly.Metro.Controls.PageTemplates.Games.Components;
using Assembly.Metro.Controls.PageTemplates.Games.Components.MetaData;
using Assembly.Metro.Dialogs;
using Blamite.Blam;
using Blamite.IO;
using Blamite.Serialization;
using Blamite.Util;
using Ceras;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using Xceed.Wpf.AvalonDock.Layout;

namespace Assembly.Helpers.Database.Models
{
    public class DatabaseTool
    {
        #region PrivateProperties

        private ConnectionViewModel _connection = new ConnectionViewModel();
		private string _keyModifier;
		private bool _backupMaps;
		private bool _useTimestamps;
		private ConnectionViewModel _timestampCache = new ConnectionViewModel();
		private string _backupPath;
		private string _mapPath;
		private string _mapDirectory;
		private List<string> _maps;
		private string _baseMap;
		private RedisConnector _tagDatabaseConnection;
		private RedisConnector _timestampCacheConnection;
		private CerasSerializer _ceras;
		private HaloMap _map;
		private TagHierarchy _allTags = new TagHierarchy();
		private EngineDescription _buildInfo;
		private ICacheFile _cacheFile;
		private IStreamManager _mapManager;
		private Trie _stringIdTrie;
		private List<TagEntry> _tags = new List<TagEntry>();
		private List<string> _tagNames = new List<string>();
		string _mapName;
		private IEnumerable<RedisKey> _timestampKeys;
		private HashSet<string> _missingTagRefs = new HashSet<string>();

		#endregion

		#region Constructor

		/// <summary>
		/// Default Constructor
		/// </summary>
		/// <param name="connection">The database connection for the tag data</param>
		/// <param name="keyModifier">The key modifier value</param>
		/// <param name="backupMaps">Whether or not to backup maps before downloading from database</param>
		/// <param name="useTimestamps">Whether or not to use timestamps for uploading / downloading tag data</param>
		/// <param name="timestampCache">The database connection for the timestamp cache</param>
		/// <param name="backupPath">The directory to backup all maps to before downloading from database</param>
		/// <param name="mapPath">The path to the .map cache file that will be edited</param>
		/// <param name="mapDirectory">The directory to the collection of .map cache files to edit in batch</param>
		/// <param name="maps">The list of .map cache file names that will be modified during batch operations</param>
		/// <param name="baseMap">The .map cache file to start with (as a baseline) during batch operations</param>
		public DatabaseTool(ConnectionViewModel connection, string keyModifier, bool backupMaps, bool useTimestamps,
			ConnectionViewModel timestampCache, string backupPath, string mapPath, string mapDirectory = null,
			List<string> maps = null, string baseMap = null)
		{
			_connection = connection;
			_backupMaps = backupMaps;
			_useTimestamps = useTimestamps;
			_timestampCache = timestampCache;
			_backupPath = backupPath;
			_mapPath = mapPath;
			_mapDirectory = mapDirectory;
			_maps = maps;
			_baseMap = baseMap;

			// Configure key modifier
			switch(keyModifier)
			{
				case "None":
					_keyModifier = null;
					break;
				case "Map Name":
					_keyModifier = GetMapName(mapPath);
					break;
				default:
					_keyModifier = keyModifier;
					break;
			}

			// Connect to databases
			_tagDatabaseConnection = new RedisConnector(_connection.Host, _connection.Port, Convert.ToInt32(_connection.Database), _connection.PasswordResolver());
			_timestampCacheConnection = new RedisConnector(_timestampCache.Host, _timestampCache.Port, Convert.ToInt32(_timestampCache.Database), _timestampCache.PasswordResolver());

			// Initialize serializer
			_ceras = CerasSerializerConfig();
		}

        #endregion

        #region PublicMethods

        /// <summary>
        /// Download tag data to a single .map cache file
        /// </summary>
        /// <param name="mapPath">The .map cache file to download to</param>
        public void DownloadTagData(string mapPath)
		{
			// Backup .map cache file
			if (_backupMaps)
			{
				BackupMapCacheFile(mapPath, _backupPath);
			}

			// Initialize .map cache file
			OpenMapCacheFile(mapPath);

			// Get a collection of filterd tags based on key modifiers, timestamps, and cache file + database hits
			var tagList = FilterTagsForDownload();

			// Collect tags from database and write them to the .map cache file using the producer-consumer pattern
			var watch = System.Diagnostics.Stopwatch.StartNew();
			BlockingCollection<(TagEntry, IList<MetaField>, HashSet<string>)> tagData = new BlockingCollection<(TagEntry, IList<MetaField>, HashSet<string>)>(Environment.ProcessorCount * 4);
			int _items = 0;

			Task t1 = Task.Run(() => TagDataConsumer(tagData, _items));
			Task t2 = Task.Run(() => TagDataProducer(tagList, tagData));

			Task.WaitAll(t1, t2);

			var elapsedMS = watch.ElapsedMilliseconds;
			MetroMessageBox.Show($"Elapsed Milliseconds: { elapsedMS }");
		}

		public void UploadTagData(string mapPath)
		{
			// Initialize .map cache file
			OpenMapCacheFile(mapPath);

			// Get a collection of filterd tags based on key modifiers, timestamps, and cache file hits
			var tagList = FilterTagsForUpload();

			// Upload tag data ~ .006 seconds per tag read speed
			foreach (TagEntry tag in tagList)
			{
				// Extract tag data from .map cache file
				(IList<MetaField>, HashSet<string>) tagMeta = ExtractTag(tag);

				// Get the tag name, using the key modifier when available
				string tagName = GetModifiedTagName(tag);

				// Serialize the tag data and tag ref data and insert into database with a UNIX timestamp
				var serializedMetaList = _ceras.Serialize<IList<MetaField>>(tagMeta.Item1);
				var serializedTagRefList = _ceras.Serialize<HashSet<string>>(tagMeta.Item2);

				_tagDatabaseConnection.Database.HashSet(tagName, new HashEntry[] { 
					new HashEntry("tagMeta", serializedMetaList), 
					new HashEntry("tagRefs", serializedTagRefList),
					new HashEntry("lastModified", DateTimeOffset.UtcNow.ToUnixTimeSeconds()) });
			}

			MetroMessageBox.Show("Extraction Successful", "Extracted " + tagList.Count + " tag(s).");
		}

		/// <summary>
		/// Download tag data to multiple .map cache files
		/// </summary>
		public void DownloadTagDataBatch()
		{
			// Move the base map to the front of the list
			string _first = _maps.Find(m => m == _baseMap);
			_maps.Remove(_baseMap);
			_maps.Insert(0, _first);

			foreach (string mapPath in _maps)
			{
				_mapPath = mapPath;
				var _fullPath = Path.Combine(_mapDirectory, mapPath);
				DownloadTagData(_fullPath);
			}
		}

		/// <summary>
		/// Upload tag data from multiple .map cache files
		/// </summary>
		public void UploadTagDataBatch()
		{
			foreach (string mapPath in _maps)
			{
				_mapPath = mapPath;
				var _fullPath = Path.Combine(_mapDirectory, mapPath);
				UploadTagData(_fullPath);
			}
		}

        #endregion

        #region HelperMethods

        /// <summary>
        /// Open the .map cache file and configure initial values.
        /// </summary>
        /// <param name="mapPath">The full path of the .map cache file</param>
        private void OpenMapCacheFile(string mapPath)
		{
			var newCacheTab = new LayoutDocument
			{
				ContentId = mapPath,
				Title = "",
				ToolTip = mapPath
			};

			// Create a new HaloMap instance
			_map = new HaloMap(mapPath, newCacheTab, App.AssemblyStorage.AssemblySettings.HalomapTagSort, true);

			// Get relevant values for local use
			_allTags = _map.AllTags;
			_buildInfo = _map.BuildInfo;
			_cacheFile = _map.CacheFile;
			_mapManager = _map.MapManager;
			_stringIdTrie = _map.StringIdTrie;

			// Aggregate tags from .map cache file that are not null
			_tags = _allTags.Entries.Where(t => t != null).ToList();

			// Generate a list of .map cache file tag names
			_tagNames = _tags.Select(t => $"{ t.GroupName }:{ t.TagFileName }").ToList();

			// Extract the map name from the cache file
			_mapName = GetMapName(_mapPath);

			// Get timestamp keys for the current .map cache file
			if (_useTimestamps)
			{
				_timestampKeys = _timestampCacheConnection.Server.Keys(pattern: _mapName);
			}
		}

		/// <summary>
		/// Backup the .map cache file to a designated directory
		/// </summary>
		/// <param name="mapPath">The full path of the original .map cache file</param>
		/// <param name="backupPath">The directory to backup to</param>
		private void BackupMapCacheFile(string mapPath, string backupPath) 
		{
			// Create a new timestamp
			var timestamp = DateTime.Now.ToString("yyyy-dd-M-HH-mm-ss");

			// Create a new path consisting of the backup directory, map name, and timestamp
			var targetPath = Path.Combine(backupPath, $"{ _mapName }_{ timestamp }.map");

			// Copy the .map cache file to the backup directory
			File.Copy(mapPath, targetPath, false);
		}

		/// <summary>
		/// Filter tag values based on key modifiers, timestamps, and cache file + database hits.
		/// </summary>
		/// <returns>Filtered collection of tag entries</returns>
		private List<TagEntry> FilterTagsForUpload()
		{
			var matches = new List<TagEntry>();

			if (_useTimestamps)
			{
				var timestampTags = _tags
					.Where(tag => _timestampKeys
					.Any(t => t.ToString().Replace($"{ _mapName }:", "") == (tag.GroupName + ":" + tag.TagFileName)))
					.ToList();

				if (_keyModifier != null)
				{
					matches = timestampTags
						.Where(tag => _timestampKeys
						.Any(t => (long)_timestampCacheConnection.Database.StringGet(t) > (long)_tagDatabaseConnection.Database.HashGetAll($"{ _keyModifier }:{ tag.GroupName }:{ tag.TagFileName }").Where(k => k.Name == "lastModified").ToList()[0].Value))
						.ToList();
				}
				else
				{
					matches = timestampTags
						.Where(tag => _timestampKeys
						.Any(t => (long)_timestampCacheConnection.Database.StringGet(t) > (long)_tagDatabaseConnection.Database.HashGetAll($"{ tag.GroupName }:{ tag.TagFileName }").Where(k => k.Name == "lastModified").ToList()[0].Value))
						.ToList();
				}
			}
			else
			{
				// Default filter
				matches = _tags;
			}

			return matches;
		}

		/// <summary>
		/// Filter tag values based on key modifiers, timestamps, and cache file + database hits.
		/// </summary>
		/// <returns>Filtered collection of tag entries</returns>
		private List<TagEntry> FilterTagsForDownload()
		{
			// Generate a list of tag names from .map cache file
			var tagNames = _tags.Select(t => t.GroupName + ":" + t.TagFileName).ToList();

			// Get database keys
			var databaseKeys = new List<RedisKey>();

			// Get database keys that match tags inside .map cache file, with an optional key modifier if present
			if (_keyModifier != null)
			{
				databaseKeys = _tagDatabaseConnection.Server.Keys(pattern: $"{ _keyModifier }*")
					.Where(k => tagNames
					.Any(t => t == k.ToString().Replace($"{ _keyModifier }:", "")))
					.ToList();
			}
			else
			{
				databaseKeys = _tagDatabaseConnection.Server.Keys()
					.Where(k => tagNames
					.Any(t => t == k.ToString()))
					.ToList();
			}

			// Filter database keys
			var matches = new List<RedisKey>();

			if (_useTimestamps)
			{
				// Filter keys based on timestamp
				matches = databaseKeys
					.Where(d => _timestampKeys
					.Any(t => t.ToString().Replace($"{ _mapName }:", "") == d.ToString()))
					.Where(d => _timestampKeys
					.Any(t => (long)_timestampCacheConnection.Database.StringGet(t) < (long)_tagDatabaseConnection.Database.HashGetAll(d).Where(k => k.Name == "lastModified").ToList()[0].Value))
					.ToList();
			}
			else
			{
				// Default filter
				matches = databaseKeys;
			}

			// Filter tag entries
			var tagList = _tags
				.Where(t => matches
				.Any(m => m.ToString() == t.GroupName + ":" + t.TagFileName))
				.ToList();

			return tagList;
		}

		/// <summary>
		/// Take tags from tagData and write them to the .map cache file
		/// </summary>
		/// <param name="tagData">Blocking collection of tags to be processed</param>
		/// <param name="itemCount">Total number of tags written to file</param>
		private void TagDataConsumer(BlockingCollection<(TagEntry, IList<MetaField>, HashSet<string>)> tagData, int itemCount)
		{
			while (!tagData.IsCompleted)
			{
				List<(TagEntry, IList<MetaField>, HashSet<string>)> metaList = new List<(TagEntry, IList<MetaField>, HashSet<string>)>();

				try
				{
					// Create a batch of tags to process, limiting list size to core count
					// TODO: Find out if core count limit is best option or not
					for (int i = 0; i < Environment.ProcessorCount; i++)
						metaList.Add(tagData.Take());
				}
				catch (InvalidOperationException) { }

				// Write tag data to file in parallel
				// TODO: Find out if there are any unwanted consequences to writing to file in parallel
				Parallel.ForEach(metaList, ((TagEntry, IList<MetaField>, HashSet<string>) metaData) =>
				{
					if (metaData.Item1 != null && metaData.Item2 != null)
					{
						// Find tag refs that do not exist in the .map cache file's tag list
						var missingTagRefs = metaData.Item3.ToList().Except(_tagNames);

						if (missingTagRefs.Count() > 0)
						{
							// Add missing tag refs to a set
							_missingTagRefs.UnionWith(missingTagRefs);

							// Log missing tag refs
							foreach (string tagRef in missingTagRefs)
							{
								Console.WriteLine($"Tag: { metaData.Item1.GroupName }:{ metaData.Item1.TagFileName } is missing the following TagRef:\n\t{ tagRef }\n");
							}
						}

						WriteTag(metaData.Item1, metaData.Item2);
						itemCount++;
						Console.WriteLine($"Extracted: { itemCount } items");
					}
				});
			}
		}

		/// <summary>
		/// Get tags from database, deserialize and insert into list
		/// </summary>
		/// <param name="tagList">Collection of tags to get from database</param>
		/// <param name="tagData">Blocking collection of tags to be processed</param>
		private void TagDataProducer(List<TagEntry> tagList, BlockingCollection<(TagEntry, IList<MetaField>, HashSet<string>)> tagData)
		{
			while (tagList.Count != 0)
			{
				IList<MetaField> metaList = new List<MetaField>();
				HashSet<string> tagRefList = new HashSet<string>();

				// Take first item from list and remove it
				TagEntry tag = tagList[0];
				tagList.RemoveAt(0);
				
				// Get the tag from the database, using the key modifier when available
				var tagName = GetModifiedTagName(tag);
				var key = _tagDatabaseConnection.Database.HashGetAll(tagName);

				if (key != null)
				{
					// Deserialize the tag
					metaList = _ceras.Deserialize<IList<MetaField>>(key.Where(k => k.Name == "tagMeta").ToList()[0].Value);
					tagRefList = _ceras.Deserialize<HashSet<string>>(key.Where(k => k.Name == "tagRefs").ToList()[0].Value);
				}

				// Send tag to consumer
				(TagEntry, IList<MetaField>, HashSet<string>) metaData = (tag, metaList, tagRefList);
				tagData.Add(metaData);
			}
			tagData.CompleteAdding();
		}

		/// <summary>
		/// Extract tag data for a single tag from the .map cache file
		/// </summary>
		/// <param name="tag">The tag to be extracted</param>
		/// <returns>Tuple containing the collection of metafields and set of tagrefs</returns>
		private (IList<MetaField>, HashSet<string>) ExtractTag(TagEntry tag)
		{
			MetaExtractor metaExtractor = new MetaExtractor(_buildInfo, tag, _allTags, _cacheFile, _mapManager, _stringIdTrie);
			(IList<MetaField>, HashSet<string>) tagMeta = metaExtractor.StoreMeta(MetaStoreReader.LoadType.File);

			return tagMeta;
		}

		/// <summary>
		/// Write a tag to the .map cache file
		/// </summary>
		/// <param name="tag">The tag to write</param>
		/// <param name="metaList">The tag's field values</param>
		private void WriteTag(TagEntry tag, IList<MetaField> metaList)
		{
			MetaExtractor extractor = new MetaExtractor(_buildInfo, tag, _allTags, _cacheFile, _mapManager, _stringIdTrie);
			extractor.RetrieveMeta(MetaStoreWriter.SaveType.File, metaList, false);
		}

		/// <summary>
		/// Convert a .map cache file path to it's display name, while excluding the file type.
		/// </summary>
		/// <param name="mapPath">The .map cache file path</param>
		/// <returns>New map name string</returns>
		private string GetMapName(string mapPath)
		{
			var mapName = Regex.Match(mapPath, @"([\w]+)(\.map)$").Value;
			return Regex.Replace(mapName, @"(\.map)$", "");
		}

		/// <summary>
		/// Create a new tag name, using the key modifier value when available.
		/// </summary>
		/// <param name="tag">The tag entry value</param>
		/// <returns>New tag name string</returns>
		private string GetModifiedTagName(TagEntry tag)
		{
			return _keyModifier != null ? _keyModifier + ":" + tag.GroupName + ":" + tag.TagFileName : tag.GroupName + ":" + tag.TagFileName;
		}

		/// <summary>
		/// Ceras serializer configuration options
		/// </summary>
		/// <returns>New ceras serializer object</returns>
		private CerasSerializer CerasSerializerConfig()
		{
			SerializerConfig config = new SerializerConfig();

			config.DefaultTargets = TargetMember.AllPublic | TargetMember.AllPrivate | TargetMember.All;

			// _metaArea is null when serialized, so exclude properties that access it to avoid a NullReferenceException
			config.ConfigType<TagBlockData>()
				.ConfigMember<long>(p => p.FirstElementAddress).Exclude()
				.ConfigMember<string>(p => p.FirstElementAddressHex).Exclude();
			config.ConfigType<RawData>()
				.ConfigMember<long>(p => p.DataAddress).Exclude()
				.ConfigMember<string>(p => p.DataAddressHex).Exclude();
			config.ConfigType<DataRef>()
				.ConfigMember<long>(p => p.DataAddress).Exclude()
				.ConfigMember<string>(p => p.DataAddressHex).Exclude();

			// exclude color type: value since it breaks the serializer with a NotSupportedException error due to the MarshalAs wrapper
			config.ConfigType<ColorData>()
				.ConfigMember<Color>(p => p.Value).Exclude();

			// exclude enumvalue type: selectedvalue since it breaks the serializer with a NullReferenceException
			config.ConfigType<EnumData>()
				.ConfigMember<EnumValue>(p => p.SelectedValue).Exclude();

			var ceras = new CerasSerializer(config);

			return ceras;
		}

        #endregion

        // Async tag downloader / writer
        //async Task DownloadKeysParallelAsync()
        //{
        //	List<Task<(TagEntry, IList<MetaField>)>> tasks = new List<Task<(TagEntry, IList<MetaField>)>>();

        //	foreach (TagEntry tag in tagList)
        //	{
        //		var key = new HashEntry[2];
        //		var tagName = tag.GroupName + ":" + tag.TagFileName;
        //		key = redis.Database.HashGetAll(tagName);

        //		if (key != null)
        //		{
        //			var metaList = ceras.Deserialize<IList<MetaField>>(key.Where(k => k.Name == "tagMeta").ToList()[0].Value);
        //			tasks.Add(Task.Run(() => (tag, metaList)));
        //		}
        //	}

        //	var results = await Task.WhenAll(tasks);

        //	Parallel.ForEach(results, ((TagEntry, IList<MetaField>) item) =>
        //	{
        //		WriteTags(item.Item1, item.Item2);
        //	});
        //}
    }
}
