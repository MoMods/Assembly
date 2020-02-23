using Assembly.Helpers;
using Assembly.Helpers.Plugins;
using Assembly.Metro.Controls.PageTemplates.Games.Components.MetaData;
using Blamite.Blam;
using Blamite.IO;
using Blamite.Plugins;
using Blamite.Serialization;
using Blamite.Util;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

namespace Assembly.Metro.Controls.PageTemplates.Games.Components
{
    /// <summary>
    /// A modified version of the MetaEditor class.
    /// Responsible for extracting MetaData to and from an external source, such as a database.
    /// </summary>
    class MetaExtractor
    {
        private TagEntry _tag;
        private readonly TagHierarchy _tags;
        private readonly Trie _stringIdTrie;
        private readonly ICacheFile _cache;
        private readonly IStreamManager _fileManager;
        private string _pluginPath;
        private string _fallbackPluginPath;
        private ThirdGenPluginVisitor _pluginVisitor;
        private FieldChangeSet _fileChanges;
        private readonly EngineDescription _buildInfo;
        private static ReaderWriterLockSlim lock_ = new ReaderWriterLockSlim();

        public MetaExtractor(EngineDescription buildInfo, TagEntry tag, TagHierarchy tags,
            ICacheFile cache, IStreamManager streamManager, Trie stringIDTrie)
        {
            _tag = tag;
            _tags = tags;
            _buildInfo = buildInfo;
            _cache = cache;
            _fileManager = streamManager;
            _stringIdTrie = stringIDTrie;

            // Load Plugin Path
            string groupName = VariousFunctions.SterilizeTagGroupName(CharConstant.ToString(tag.RawTag.Group.Magic)).Trim();
            _pluginPath = string.Format("{0}\\{1}\\{2}.xml", VariousFunctions.GetApplicationLocation() + @"Plugins",
                _buildInfo.Settings.GetSetting<string>("plugins"), groupName);

            if (_buildInfo.Settings.PathExists("fallbackPlugins"))
                _fallbackPluginPath = string.Format("{0}\\{1}\\{2}.xml", VariousFunctions.GetApplicationLocation() + @"Plugins",
                    _buildInfo.Settings.GetSetting<string>("fallbackPlugins"), groupName);
        }

        /// <summary>
        /// Iterates through every MetaField within the tag and returns the results as a list of MetaField objects.
        /// </summary>
        /// <param name="type">How the data is being loaded</param>
        /// <returns>Tuple containing the collection of metafields and set of tagrefs</returns>
        public (IList<MetaField>, HashSet<string>) StoreMeta(MetaStoreReader.LoadType type)
        {
            string pluginpath = _pluginPath;

            if (!File.Exists(pluginpath))
                pluginpath = _fallbackPluginPath;

            // Set the stream manager and base offset to use based upon the LoadType
            IStreamManager streamManager = null;
            long baseOffset = 0;
            streamManager = _fileManager;
            baseOffset = (uint)_tag.RawTag.MetaLocation.AsOffset();

            // Load Plugin File
            using (XmlReader xml = XmlReader.Create(pluginpath))
            {
                _pluginVisitor = new ThirdGenPluginVisitor(_tags, _stringIdTrie, _cache.MetaArea,
                    App.AssemblyStorage.AssemblySettings.PluginsShowInvisibles);
                AssemblyPluginLoader.LoadPlugin(xml, _pluginVisitor);
            };

            _fileChanges = new FieldChangeSet();

            // Call the MetaStoreReader and store results inside a list
            var metaStore = new MetaStoreReader(streamManager, baseOffset, _cache, _buildInfo, type, _fileChanges);
            (IList<MetaField>, HashSet<string>) metaStorage = metaStore.ReadFields(_pluginVisitor.Values);

            return metaStorage;
        }

        /// <summary>
        /// Iterate through every MetaField that has been imported and write them to the .map cache file.
        /// </summary>
        /// <param name="type">How the data is being saved</param>
        /// <param name="metaList">The list containing all imported MetaField objects</param>
        /// <param name="onlyUpdateChanged">Only update the tag if changes have occured</param>
        /// <param name="showActionDialog">Show a message dialog after changes are complete</param>
        public void RetrieveMeta(MetaStoreWriter.SaveType type, IList<MetaField> metaList, bool onlyUpdateChanged, bool showActionDialog = true)
        {
            if (type == MetaStoreWriter.SaveType.File)
            {
                string pluginpath = _pluginPath;

                if (!File.Exists(pluginpath))
                    pluginpath = _fallbackPluginPath;

                using (IStream stream = _fileManager.ParallelOpenReadWrite())
                {
                    // Visit each MetaField within the tag and update its values 
                    var metaSync = new MetaStoreWriter(stream, (uint)_tag.RawTag.MetaLocation.AsOffset(), _cache, _buildInfo, type,
                        _fileChanges, _stringIdTrie);
                    metaSync.WriteFields(metaList);
                    _cache.SaveChanges(stream); // VERY SLOW (91% of time goes to this operation)
                }

                //lock_.EnterWriteLock();
                //try
                //{
                //    using (IStream stream = _fileManager.OpenReadWrite())
                //    {
                //        // Visit each MetaField within the tag and update its values 
                //        var metaSync = new MetaStoreWriter(stream, (uint)_tag.RawTag.MetaLocation.AsOffset(), _cache, _buildInfo, type,
                //            _fileChanges, _stringIdTrie);
                //        metaSync.WriteFields(metaList);
                //        _cache.SaveChanges(stream); // VERY SLOW (91% of time goes to this operation)
                //    }
                //}
                //finally
                //{
                //    lock_.ExitWriteLock();
                //}
              
                //if (showActionDialog)
                //    MetroMessageBox.Show("Meta Saved", "The metadata has been saved back to the original file.");
            }
        }
    }
}
