using Ceras;
using System.Windows.Media;

namespace Assembly.Metro.Controls.PageTemplates.Games.Components.MetaData
{
	/// <summary>
	///     Base class for color data.
	/// </summary>
	public class ColorData : ValueField
	{
		private string _dataType;
		private bool _alpha;
		// exclude color type: _value since it breaks the serializer with a NotSupportedException error due to the MarshalAs wrapper
		[Exclude]
		private Color _value;

		// empty constructor to be used for serialization/deserialization
		public ColorData() : base()
		{
		}

		public ColorData(string name, uint offset, long address, bool alpha, string dataType, Color value,
			uint pluginLine, string tooltip)
			: base(name, offset, address, pluginLine, tooltip)
		{
			_value = value;
			_alpha = alpha;
			_dataType = dataType;
		}

		public bool Alpha
		{
			get { return _alpha; }
			set
			{
				_alpha = value;
				NotifyPropertyChanged("Alpha");
			}
		}

		public string DataType
		{
			get { return _dataType; }
			set
			{
				_dataType = value;
				NotifyPropertyChanged("DataType");
			}
		}

		public Color Value
		{
			get { return _value; }
			set
			{
				_value = value;
				NotifyPropertyChanged("Value");
			}
		}

		public override void Accept(IMetaFieldVisitor visitor)
		{
			if (DataType == "color32")
				visitor.VisitColourInt(this);
			else
				visitor.VisitColourFloat(this);
		}

		public override MetaField CloneValue()
		{
			return new ColorData(Name, Offset, FieldAddress, Alpha, DataType, Value, PluginLine, ToolTip);
		}
	}
}