using Blamite.Blam;
using Blamite.IO;
using Blamite.Serialization;
using Blamite.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace Assembly.Metro.Controls.PageTemplates.Games.Components.MetaData
{
	/// <summary>
	/// A modified version of the MetaReader, this class reads MetaData from the .map cache file and stores it into a list of MetaField objects.
	/// Used for sending MetaData to an external source, such as a database.
	/// </summary>
	/// <returns>List[MetaField]</returns>
	class MetaStoreReader : IMetaStoreReader
	{
		public enum LoadType
		{
			File,
			Memory
		}

		private readonly ICacheFile _cache;
		private readonly StructureLayout _dataRefLayout;
		private readonly FieldChangeSet _ignoredFields;
		private readonly StructureLayout _tagBlockLayout;
		private readonly IStreamManager _streamManager;
		private readonly StructureLayout _tagRefLayout;
		private readonly LoadType _type;
		private IReader _reader;
		private HashSet<string> _tagRefs = new HashSet<string>();

		public MetaStoreReader(IStreamManager streamManager, long baseOffset, ICacheFile cache, EngineDescription buildInfo,
			LoadType type, FieldChangeSet ignore)
		{
			_streamManager = streamManager;
			BaseOffset = baseOffset;
			_cache = cache;
			_ignoredFields = ignore;
			_type = type;

			// Load layouts
			_tagBlockLayout = buildInfo.Layouts.GetLayout("tag block");
			_tagRefLayout = buildInfo.Layouts.GetLayout("tag reference");
			_dataRefLayout = buildInfo.Layouts.GetLayout("data reference");
		}

		public long BaseOffset { get; set; }

		public FlagData InsertFlags(FlagData field)
		{
			SeekToOffset(field.Offset);
			switch (field.Type)
			{
				case FlagsType.Flags8:
					field.Value = _reader.ReadByte();
					break;
				case FlagsType.Flags16:
					field.Value = _reader.ReadUInt16();
					break;
				case FlagsType.Flags32:
					field.Value = _reader.ReadUInt32();
					break;
				case FlagsType.Flags64:
					field.Value = _reader.ReadUInt64();
					break;
			}

			return field;
		}

		public CommentData InsertComment(CommentData field)
		{
			return field;
		}

		public EnumData InsertEnum(EnumData field)
		{
			SeekToOffset(field.Offset);
			switch (field.Type)
			{
				case EnumType.Enum8:
					field.Value = _reader.ReadSByte();
					break;
				case EnumType.Enum16:
					field.Value = _reader.ReadInt16();
					break;
				case EnumType.Enum32:
					field.Value = _reader.ReadInt32();
					break;
			}

			// Search for the corresponding option and select it
			EnumValue selected = null;
			foreach (EnumValue option in field.Values)
			{
				// Typecast the field value and the option value based upon the enum type
				switch (field.Type)
				{
					case EnumType.Enum8:
						if ((sbyte)option.Value == (sbyte)field.Value)
							selected = option;
						break;
					case EnumType.Enum16:
						if ((short)option.Value == (short)field.Value)
							selected = option;
						break;
					case EnumType.Enum32:
						if (option.Value == field.Value)
							selected = option;
						break;
				}
				if (selected != null)
					break;
			}
			if (selected == null)
			{
				// Nothing matched, so just add an option for it
				selected = new EnumValue(field.Value.ToString(), field.Value, "");
				field.Values.Add(selected);
			}
			field.SelectedValue = selected;

			return field;
		}

		public Uint8Data InsertUint8(Uint8Data field)
		{
			SeekToOffset(field.Offset);
			field.Value = _reader.ReadByte();

			return field;
		}

		public Int8Data InsertInt8(Int8Data field)
		{
			SeekToOffset(field.Offset);
			field.Value = _reader.ReadSByte();

			return field;
		}
		public Uint16Data InsertUint16(Uint16Data field)
		{
			SeekToOffset(field.Offset);
			field.Value = _reader.ReadUInt16();

			return field;
		}

		public Int16Data InsertInt16(Int16Data field)
		{
			SeekToOffset(field.Offset);
			field.Value = _reader.ReadInt16();

			return field;
		}

		public Uint32Data InsertUint32(Uint32Data field)
		{
			SeekToOffset(field.Offset);
			field.Value = _reader.ReadUInt32();

			return field;
		}

		public Int32Data InsertInt32(Int32Data field)
		{
			SeekToOffset(field.Offset);
			field.Value = _reader.ReadInt32();

			return field;
		}

		public Uint64Data InsertUint64(Uint64Data field)
		{
			SeekToOffset(field.Offset);
			field.Value = _reader.ReadUInt64();

			return field;
		}

		public Int64Data InsertInt64(Int64Data field)
		{
			SeekToOffset(field.Offset);
			field.Value = _reader.ReadInt64();

			return field;
		}

		public ColorData InsertColourInt(ColorData field)
		{
			SeekToOffset(field.Offset);

			var val = _reader.ReadUInt32();

			byte[] channels = BitConverter.GetBytes(val);

			field.Value = Color.FromArgb(field.Alpha ? channels[3] : (byte)0xFF, channels[2], channels[1], channels[0]);

			return field;
		}

		public ColorData InsertColourFloat(ColorData field)
		{
			SeekToOffset(field.Offset);

			Color scColor = Color.FromScRgb(field.Alpha ? _reader.ReadFloat() : 1, _reader.ReadFloat(), _reader.ReadFloat(), _reader.ReadFloat());
			//Color.ToString() doesnt display hex code when using scrgb, so gotta do this
			field.Value = Color.FromArgb(scColor.A, scColor.R, scColor.G, scColor.B);

			return field;
		}

		public StringData InsertString(StringData field)
		{
			SeekToOffset(field.Offset);
			switch (field.Type)
			{
				case StringType.ASCII:
					field.Value = _reader.ReadAscii(field.Size);
					break;

				case StringType.UTF16:
					field.Value = _reader.ReadUTF16(field.Size);
					break;
			}

			return field;
		}

		public StringIDData InsertStringID(StringIDData field)
		{
			SeekToOffset(field.Offset);
			field.Value = _cache.StringIDs.GetString(new StringID(_reader.ReadUInt32()));

			return field;
		}

		public RawData InsertRawData(RawData field)
		{
			SeekToOffset(field.Offset);
			field.DataAddress = field.FieldAddress;
			field.Value = FunctionHelpers.BytesToHexString(_reader.ReadBlock(field.Length));

			// copy data address value to use with serialization
			field.DataAddressValue = field.DataAddress;

			return field;
		}

		public DataRef InsertDataRef(DataRef field)
		{
			SeekToOffset(field.Offset);
			StructureValueCollection values = StructureReader.ReadStructure(_reader, _dataRefLayout);

			var length = (int)values.GetInteger("size");
			uint pointer = (uint)values.GetInteger("pointer");

			long expanded = _cache.PointerExpander.Expand(pointer);

			if (length > 0 && _cache.MetaArea.ContainsBlockPointer(expanded, length))
			{
				field.DataAddress = expanded;
				field.Length = length;

			}
			else
			{
				field.DataAddress = 0;
				field.Length = 0;
				field.Value = "";
			}

			// copy data address value to use with serialization
			field.DataAddressValue = field.DataAddress;

			// Read the DataRef contents and store them into a new DataRef object
			DataRef _field = (DataRef)field.CloneValue();
			_field = InsertDataRefContents(_field);
			
			return _field;
		}

		public TagRefData InsertTagRef(TagRefData field)
		{
			SeekToOffset(field.Offset);

			TagGroup tagGroup = null;
			DatumIndex index;
			if (field.WithGroup)
			{
				// Read the datum index based upon the layout
				StructureValueCollection values = StructureReader.ReadStructure(_reader, _tagRefLayout);
				index = new DatumIndex(values.GetInteger("datum index"));

				// Check the group, in case the datum index is null
				var magic = values.GetInteger("tag group magic");
				var str = CharConstant.ToString((int)magic);
				tagGroup = field.Tags.Groups.FirstOrDefault(c => c.TagGroupMagic == str);
			}
			else
			{
				// Just read the datum index at the current position
				index = DatumIndex.ReadFrom(_reader);
			}

			TagEntry tag = null;
			if (index.IsValid && index.Index < field.Tags.Entries.Count)
			{
				tag = field.Tags.Entries[index.Index];
				if (tag == null || tag.RawTag == null || tag.RawTag.Index != index)
					tag = null;
			}

			if (tag != null)
			{
				field.Group = field.Tags.Groups.FirstOrDefault(c => c.RawGroup == tag.RawTag.Group);
				field.Value = tag;

				// assign group and tag names to custom fields so that they can be accessed by serializer
				field.GroupName = tag.GroupName;
				field.TagFileName = tag.TagFileName;

				// assign tag ref full name to list for reference when writing back to .map cache file
				var _tagRefName = $"{ field.GroupName }:{ field.TagFileName }";
				_tagRefs.Add(_tagRefName);
			}
			else
			{
				field.Group = tagGroup;
				field.Value = null;
			}

			return field;
		}

		public Float32Data InsertFloat32(Float32Data field)
		{
			SeekToOffset(field.Offset);
			field.Value = _reader.ReadFloat();

			return field;
		}

		public Vector2Data InsertPoint2(Vector2Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();

			return field;
		}

		public Vector3Data InsertPoint3(Vector3Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();
			field.C = _reader.ReadFloat();

			return field;
		}

		public Vector2Data InsertVector2(Vector2Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();

			return field;
		}

		public Vector3Data InsertVector3(Vector3Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();
			field.C = _reader.ReadFloat();

			return field;
		}

		public Vector4Data InsertVector4(Vector4Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();
			field.C = _reader.ReadFloat();
			field.D = _reader.ReadFloat();

			return field;
		}

		public Point2Data InsertPoint2(Point2Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();

			return field;
		}

		public Point3Data InsertPoint3(Point3Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();
			field.C = _reader.ReadFloat();

			return field;
		}

		public Plane2Data InsertPlane2(Plane2Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();
			field.C = _reader.ReadFloat();

			return field;
		}

		public Plane3Data InsertPlane3(Plane3Data field) // same function name for vec4
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();
			field.C = _reader.ReadFloat();
			field.D = _reader.ReadFloat();

			return field;
		}

		public DegreeData InsertDegree(DegreeData field)
		{
			SeekToOffset(field.Offset);
			field.Radian = _reader.ReadFloat();

			return field;
		}

		public Degree2Data InsertDegree2(Degree2Data field)
		{
			SeekToOffset(field.Offset);
			field.RadianA = _reader.ReadFloat();
			field.RadianB = _reader.ReadFloat();

			return field;
		}

		public Degree3Data InsertDegree3(Degree3Data field)
		{
			SeekToOffset(field.Offset);
			field.RadianA = _reader.ReadFloat();
			field.RadianB = _reader.ReadFloat();
			field.RadianC = _reader.ReadFloat();

			return field;
		}

		public Vector3Data InsertPlane2(Vector3Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();
			field.C = _reader.ReadFloat();

			return field;
		}

		public Vector4Data InsertPlane3(Vector4Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadFloat();
			field.B = _reader.ReadFloat();
			field.C = _reader.ReadFloat();
			field.D = _reader.ReadFloat();

			return field;
		}

		public RectangleData InsertRect16(RectangleData field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadInt16();
			field.B = _reader.ReadInt16();
			field.C = _reader.ReadInt16();
			field.D = _reader.ReadInt16();

			return field;
		}

		public Quaternion16Data InsertQuat16(Quaternion16Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadInt16();
			field.B = _reader.ReadInt16();
			field.C = _reader.ReadInt16();
			field.D = _reader.ReadInt16();

			return field;
		}

		public Point16Data InsertPoint16(Point16Data field)
		{
			SeekToOffset(field.Offset);
			field.A = _reader.ReadInt16();
			field.B = _reader.ReadInt16();

			return field;
		}

		public TagBlockData InsertTagBlock(TagBlockData field)
		{
			SeekToOffset(field.Offset);
			StructureValueCollection values = StructureReader.ReadStructure(_reader, _tagBlockLayout);

			var length = (int)values.GetInteger("entry count");
			uint pointer = (uint)values.GetInteger("pointer");
			long expanded = _cache.PointerExpander.Expand(pointer);

			// Make sure the pointer looks valid
			if (length < 0 || !_cache.MetaArea.ContainsBlockPointer(expanded, (int)(length * field.ElementSize)))
			{
				length = 0;
				pointer = 0;
				expanded = 0;
			}

			if (expanded != field.FirstElementAddress)
				field.FirstElementAddress = expanded;

			field.Length = length;

			if (field != null)
				field = ReadTagBlockChildren(field);

			return field;
		}

		public ShaderRef InsertShaderRef(ShaderRef field)
		{
			SeekToOffset(field.Offset);
			if (_cache.ShaderStreamer != null)
				field.Shader = _cache.ShaderStreamer.ReadShader(_reader, field.Type);

			return field;
		}

		public RangeUint16Data InsertRangeUint16(RangeUint16Data field)
		{
			SeekToOffset(field.Offset);
			field.Min = _reader.ReadUInt16();
			field.Max = _reader.ReadUInt16();

			return field;
		}

		public RangeFloat32Data InsertRangeFloat32(RangeFloat32Data field)
		{
			SeekToOffset(field.Offset);
			field.Min = _reader.ReadFloat();
			field.Max = _reader.ReadFloat();

			return field;
		}

		public RangeDegreeData InsertRangeDegree(RangeDegreeData field)
		{
			SeekToOffset(field.Offset);
			field.RadianMin = _reader.ReadFloat();
			field.RadianMax = _reader.ReadFloat();

			return field;
		}

		/// <summary>
		/// Call the relevant method depending on the MetaField object type.
		/// This prevents the need of adding a new method to each MetaField object class in order for it to use the IMetaStoreReader interface.
		/// </summary>
		/// <param name="field">The MetaField being read</param>
		/// <returns>MetaField object containing all data read from the field</returns>
		public MetaField AcceptField(MetaField field)
		{
			switch (field)
			{
				case FlagData flag:
					field = InsertFlags(flag);
					break;
				case CommentData comment:
					field = InsertComment(comment);
					break;
				case EnumData enums:
					field = InsertEnum(enums);
					break;
				case Uint8Data uint8:
					field = InsertUint8(uint8);
					break;
				case Int8Data int8:
					field = InsertInt8(int8);
					break;
				case Uint16Data uint16:
					field = InsertUint16(uint16);
					break;
				case Int16Data int16:
					field = InsertInt16(int16);
					break;
				case Uint32Data uint32:
					field = InsertUint32(uint32);
					break;
				case Int32Data int32:
					field = InsertInt32(int32);
					break;
				case Uint64Data uint64:
					field = InsertUint64(uint64);
					break;
				case ColorData color:
					if (color.DataType == "int")
					{
						field = InsertColourInt(color);
						break;
					}
					else
					{
						field = InsertColourFloat(color);
						break;
					}
				case StringData str:
					field = InsertString(str);
					break;
				case StringIDData stringId:
					field = InsertStringID(stringId);
					break;
				case DataRef dataref: // rawdata is the same?
					field = InsertDataRef(dataref);
					break;
				case RawData raw:
					field = InsertRawData(raw);
					break;
				case TagRefData tagref:
					field = InsertTagRef(tagref);
					break;
				case TagBlockData block:
					field = InsertTagBlock(block);
					break;
				case Float32Data float32:
					field = InsertFloat32(float32);
					break;
				case Vector2Data vec2:
					if (vec2.Type == "point2")
					{
						field = InsertPoint2(vec2);
						break;
					}
					else
					{
						field = InsertVector2(vec2);
						break;
					}
				case Vector3Data vec3:
					if (vec3.Type == "point3")
					{
						field = InsertPoint3(vec3); // no difference between point3 and vector3; same function name for plane2
						break;
					}
					else
					{
						field = InsertVector3(vec3);
						break;
					}
				case Vector4Data vec4:
					field = InsertVector4(vec4); // same function name for plane3
					break;
				case Point2Data point2:
					field = InsertPoint2(point2);
					break;
				case Point3Data point3:
					field = InsertPoint3(point3);
					break;
				case Plane2Data plane2:
					field = InsertPlane2(plane2);
					break;
				case Plane3Data plane3:
					field = InsertPlane3(plane3);
					break;
				case DegreeData deg:
					field = InsertDegree(deg);
					break;
				case Degree2Data deg2:
					field = InsertDegree2(deg2);
					break;
				case Degree3Data deg3:
					field = InsertDegree3(deg3);
					break;
				case RectangleData rect:
					field = InsertRect16(rect);
					break;
				case Quaternion16Data quat:
					field = InsertQuat16(quat);
					break;
				case Point16Data point16:
					field = InsertPoint16(point16);
					break;
				case ShaderRef shade:
					field = InsertShaderRef(shade);
					break;
				case RangeUint16Data range16:
					field = InsertRangeUint16(range16);
					break;
				case RangeFloat32Data range32:
					field = InsertRangeFloat32(range32);
					break;
				case RangeDegreeData rangedeg:
					field = InsertRangeDegree(rangedeg);
					break;
				default:
					field = null;
					break;
			}

			return field;
		}

		private MetaField ReadField(MetaField field)
		{
			MetaField result = field;

			// create new instance of field value to prevent global changes / overrides
			field = field.CloneValue();

			// Update the field's memory address
			var valueField = field as ValueField;
			if (valueField != null)
			{
				valueField.FieldAddress = BaseOffset + valueField.Offset;
				if (_type == LoadType.File)
					valueField.FieldAddress = _cache.MetaArea.OffsetToPointer((int)valueField.FieldAddress);
			}

			// Read its contents if it hasn't changed (or if change detection is disabled)
			if (_ignoredFields == null || !_ignoredFields.HasChanged(field))
			{
				result = AcceptField(field);
			}

			return result;
		}

		/// <summary>
		/// Reads all fields contained within a tag and stores them inside a list.
		/// </summary>
		/// <param name="fields">The fields being read</param>
		/// <returns>Tuple containing the collection of metafields and set of tagrefs</returns>
		public (IList<MetaField>, HashSet<string>) ReadFields(IList<MetaField> fields)
		{
			IList<MetaField> allFields = new List<MetaField>();

			bool opened = OpenReader();
			if (_reader == null)
				return (allFields, _tagRefs);

			try
			{
				// ReSharper disable ForCanBeConvertedToForeach
				for (int i = 0; i < fields.Count; i++)
				{
					allFields.Add(ReadField(fields[i]));
				}

				// ReSharper restore ForCanBeConvertedToForeach
			}
			finally
			{
				if (opened)
					CloseReader();
			}

			return (allFields, _tagRefs);
		}

		/// <summary>
		/// Reads all TagBlockData children and stores them inside the TagBlockData.PageCollection property.
		/// This allows all data from child MetaFields to be stored inside the parent TagBlockData object.
		/// </summary>
		/// <param name="block">The TagBlockData object being read</param>
		/// <returns>TagBlockData object</returns>
		public TagBlockData ReadTagBlockChildren(TagBlockData block)
		{
			if (!block.HasChildren || block.CurrentIndex < 0)
				return block;

			bool opened = OpenReader();
			if (_reader == null)
				return block;

			try
			{
				// Calculate the base offset to read from
				long oldBaseOffset = BaseOffset;
				long dataOffset = block.FirstElementAddress;
				if (_type == LoadType.File)
					dataOffset = (uint)_cache.MetaArea.PointerToOffset(dataOffset);
				int _pageCount = block.Pages.Count;

				// Add each TagBlockPage to the PageCollection
				block.PageCollection = AddTagBlockPages(block, _pageCount, dataOffset);

				BaseOffset = oldBaseOffset;
			}
			finally
			{
				if (opened)
					CloseReader();
			}

			return block;
		}

		/// <summary>
		/// Iterate though every page inside the TagBlockData object and return the results as a list of TagBlockPage objects.
		/// </summary>
		/// <param name="block">The TagBlockData object</param>
		/// <param name="pageCount">The number of pages to iterate through</param>
		/// <param name="offset">The offset of the TagBlockData object</param>
		/// <returns>List[TagBlockPage]</returns>
		public IList<TagBlockPage> AddTagBlockPages(TagBlockData block, int pageCount, long offset)
		{
			IList<TagBlockPage> pageCollection = new List<TagBlockPage>();

			for (int i = 0; i < pageCount; i++)
			{
				BaseOffset = (offset + i * block.ElementSize);

				// Add the page to the pageCollection object
				TagBlockPage page = AddTagBlockPage(block, i);
				pageCollection.Add(page);

			}

			return pageCollection;
		}

		/// <summary>
		/// Create a single TagBlockPage and return it.
		/// </summary>
		/// <param name="block">The TagBlockData object</param>
		/// <param name="pageNum">The current page number</param>
		/// <returns>TagBlockPage</returns>
		public TagBlockPage AddTagBlockPage(TagBlockData block, int pageNum)
		{
			// Get the number of fields inside the page
			TagBlockPage oldPage = block.Pages[pageNum];
			int _fieldLength = oldPage.Fields.Length;

			TagBlockPage page = new TagBlockPage(pageNum, _fieldLength);

			// Get the data for all MetaField objects within the page
			page.FieldValues = AddMetaFields(block, page, _fieldLength);

			return page;
		}

		/// <summary>
		/// Visit every MetaField within the page and return them inside an array.
		/// </summary>
		/// <param name="block">The TagBlockData object</param>
		/// <param name="page">The current page number</param>
		/// <param name="fieldLength">The number of MetaField objects within each page</param>
		/// <returns>MetaField[]</returns>
		public MetaField[] AddMetaFields(TagBlockData block, TagBlockPage page, int fieldLength)
		{
			MetaField[] fields = new MetaField[fieldLength];

			for (int j = 0; j < fieldLength; j++)
			{
				fields[j] = ReadField(page.Fields[j] ?? block.Template[j]);
			}

			return fields;
		}

		public DataRef InsertDataRefContents(DataRef field)
		{
			if (field.Length < 0)
				return field;

			bool opened = OpenReader();
			if (_reader == null)
				return field;

			try
			{
				// Calculate the base offset to read from
				long oldBaseOffset = BaseOffset;
				long dataOffset = field.DataAddress;
				if (_type == LoadType.File)
					dataOffset = (uint)_cache.MetaArea.PointerToOffset(dataOffset);

				_reader.SeekTo(dataOffset);

				switch (field.Format)
				{
					default:
						byte[] data = _reader.ReadBlock(field.Length);
						field.Value = FunctionHelpers.BytesToHexString(data);
						break;
					case "asciiz":
						field.Value = _reader.ReadAscii(field.Length);
						break;
				}

				BaseOffset = oldBaseOffset;
			}
			finally
			{
				if (opened)
					CloseReader();
			}

			return field;
		}

		/// <summary>
		///     Opens the file for reading and sets _reader to the stream. Must be done before any I/O operations are performed.
		/// </summary>
		/// <returns>false if the file was already open.</returns>
		private bool OpenReader()
		{
			if (_reader == null)
			{
				_reader = _streamManager.OpenRead();
				return true;
			}
			return false;
		}

		private void CloseReader()
		{
			if (_reader != null)
			{
				_reader.Dispose();
				_reader = null;
			}
		}

		private void SeekToOffset(uint offset)
		{
			_reader.SeekTo(BaseOffset + offset);
		}
	}
}
