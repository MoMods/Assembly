using System.Windows;

namespace Assembly.Metro.Controls.PageTemplates.Games.Components.MetaData
{
	public class TagRefData : ValueField
	{
		private readonly TagHierarchy _allTags;
		private string _tagFileName;
		private bool _withGroup;
		private TagGroup _group;
		private string _groupName;
		private Visibility _showTagOptions;
		private TagEntry _value;

		// empty constructor to be used for serialization/deserialization
		public TagRefData() : base()
		{
		}

		public TagRefData(string name, uint offset, long address, TagHierarchy allTags, Visibility showTagOptions, bool withGroup,
			uint pluginLine, string tooltip)
			: base(name, offset, address, pluginLine, tooltip)
		{
			_allTags = allTags;
			_withGroup = withGroup;
			_showTagOptions = showTagOptions;
		}

		public TagEntry Value
		{
			get { return _value; }
			set
			{
				_value = value;
				NotifyPropertyChanged("Value");
			}
		}

		public Visibility ShowTagOptions
		{
			get { return _showTagOptions; }
			set
			{
				_showTagOptions = value;
				NotifyPropertyChanged("ShowTagOptions");
			}
		}

		public TagGroup Group
		{
			get { return _group; }
			set
			{
				_group = value;
				NotifyPropertyChanged("Group");
			}
		}

		// store group name into tag to be used by serializer
		public string GroupName
		{
			get { return _groupName; }
			set { _groupName = value; }
		}

		// store withgroup value into tag to be used by serializer
		public bool WithGroup
		{
			get { return _withGroup; }
			set { _withGroup = value; }
		}

		public TagHierarchy Tags
		{
			get { return _allTags; }
		}

		// store tag name into tag to be used by serializer
		public string TagFileName
		{
			get { return _tagFileName; }
			set { _tagFileName = value; }
		}

		public bool CanJump
		{
			get { return _value != null && !_value.IsNull; }
		}

		public override void Accept(IMetaFieldVisitor visitor)
		{
			visitor.VisitTagRef(this);
		}

		public override MetaField CloneValue()
		{
			var result = new TagRefData(Name, Offset, FieldAddress, _allTags, _showTagOptions, _withGroup, PluginLine, ToolTip);
			result.Group = _group;
			result.Value = _value;
			return result;
		}
	}
}