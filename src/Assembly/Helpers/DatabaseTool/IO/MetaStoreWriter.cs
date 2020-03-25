using Blamite.Blam;
using Blamite.IO;
using Blamite.Serialization;
using Blamite.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assembly.Metro.Controls.PageTemplates.Games.Components.MetaData
{
	/// <summary>
	/// A modified version of MetaWriter, this class writes MetaData to the .map cache file.
	/// Used when the MetaData is being imported from an external source, such as a database.
	/// </summary>
	internal class MetaStoreWriter : IMetaFieldVisitor
    {
		public enum SaveType
		{
			File,
			Memory
		}

		private readonly ICacheFile _cache;
		private readonly FieldChangeSet _changes;
		private readonly StructureLayout _dataRefLayout;
		private readonly StructureLayout _tagBlockLayout;
		private readonly Trie _stringIdTrie;
		private readonly StructureLayout _tagRefLayout;
		private readonly SaveType _type;
		private readonly IWriter _writer;
		private long _baseOffset;

		private bool _pokeTemplateFields = true;

		/// <summary>
		///     Save meta to the Blam Cache File
		/// </summary>
		public MetaStoreWriter(IWriter writer, long baseOffset, ICacheFile cache, EngineDescription buildInfo, SaveType type,
			FieldChangeSet changes, Trie stringIdTrie)
		{
			_writer = writer;
			_baseOffset = baseOffset;
			_cache = cache;
			_type = type;
			_changes = changes;
			_stringIdTrie = stringIdTrie;

			// Load layouts
			_tagBlockLayout = buildInfo.Layouts.GetLayout("tag block");
			_tagRefLayout = buildInfo.Layouts.GetLayout("tag reference");
			_dataRefLayout = buildInfo.Layouts.GetLayout("data reference");
		}
		public void VisitFlags(FlagData field)
		{
			SeekToOffset(field.Offset);
			switch (field.Type)
			{
				case FlagsType.Flags8:
					_writer.WriteByte((byte)field.Value);
					break;

				case FlagsType.Flags16:
					_writer.WriteUInt16((ushort)field.Value);
					break;

				case FlagsType.Flags32:
					_writer.WriteUInt32((uint)field.Value);
					break;

				case FlagsType.Flags64:
					_writer.WriteUInt64(field.Value);
					break;
			}
		}

		public void VisitComment(CommentData field)
		{
		}

		public void VisitEnum(EnumData field)
		{
			SeekToOffset(field.Offset);
			switch (field.Type)
			{
				case EnumType.Enum8:
					_writer.WriteSByte((sbyte)field.Value);
					break;

				case EnumType.Enum16:
					_writer.WriteInt16((short)field.Value);
					break;

				case EnumType.Enum32:
					_writer.WriteInt32(field.Value);
					break;
			}
		}

		public void VisitUint8(Uint8Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteByte(field.Value);
		}

		public void VisitInt8(Int8Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteSByte(field.Value);
		}

		public void VisitUint16(Uint16Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteUInt16(field.Value);
		}

		public void VisitInt16(Int16Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteInt16(field.Value);
		}

		public void VisitUint32(Uint32Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteUInt32(field.Value);
		}

		public void VisitInt32(Int32Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteInt32(field.Value);
		}

		public void VisitUint64(Uint64Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteUInt64(field.Value);
		}

		public void VisitInt64(Int64Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteInt64(field.Value);
		}

		public void VisitFloat32(Float32Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.Value);
		}

		public void VisitColourInt(ColorData field)
		{
			SeekToOffset(field.Offset);

			byte[] channels = new byte[] { field.Value.B, field.Value.G, field.Value.R, field.Value.A };

			_writer.WriteUInt32(BitConverter.ToUInt32(channels, 0));
		}

		public void VisitColourFloat(ColorData field)
		{
			SeekToOffset(field.Offset);

			if (field.Alpha)
				_writer.WriteFloat(field.Value.ScA);
			_writer.WriteFloat(field.Value.ScR);
			_writer.WriteFloat(field.Value.ScG);
			_writer.WriteFloat(field.Value.ScB);
		}

		public void VisitTagBlock(TagBlockData field)
		{
			var values = new StructureValueCollection();

			bool isValid = _cache.MetaArea.ContainsBlockPointer(field.FirstElementAddress, (int)(field.Length * field.ElementSize));

			values.SetInteger("entry count", isValid ? (uint)field.Length : 0);

			uint cont = _cache.PointerExpander.Contract(field.FirstElementAddress);

			values.SetInteger("pointer", isValid ? cont : 0);

			SeekToOffset(field.Offset);
			StructureWriter.WriteStructure(values, _tagBlockLayout, _writer);
		}

		public void VisitTagBlockEntry(WrappedTagBlockEntry field)
		{
		}

		public void VisitString(StringData field)
		{
			SeekToOffset(field.Offset);
			switch (field.Type)
			{
				case StringType.ASCII:
					_writer.WriteAscii(field.Value);
					break;

				case StringType.UTF16:
					_writer.WriteUTF16(field.Value);
					break;
			}
		}

		public void VisitStringID(StringIDData field)
		{
			SeekToOffset(field.Offset);

			// Temporary fix
			// TODO: test this out more and remove if not necessary
			if (field.Value == null)
			{
				return;
			}

			if (_stringIdTrie.Contains(field.Value))
			{
				StringID sid = _cache.StringIDs.FindStringID(field.Value);
				_writer.WriteUInt32(sid.Value);
			}
			else if (_type == SaveType.File)
			{
				StringID sid = _cache.StringIDs.AddString(field.Value);
				_stringIdTrie.Add(field.Value);
				_writer.WriteUInt32(sid.Value);
			}
			else
			{
				_writer.WriteUInt32(StringID.Null.Value);
			}
		}

		public void VisitRawData(RawData field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteBlock(FunctionHelpers.HexStringToBytes(field.Value), 0, field.Length);
		}

		public void VisitDataRef(DataRef field)
		{
			var values = new StructureValueCollection();
			bool isValid = _cache.MetaArea.ContainsBlockPointer(field.DataAddressValue, field.Length);
			values.SetInteger("size", isValid ? (uint)field.Length : 0);

			uint cont = _cache.PointerExpander.Contract(field.DataAddressValue);

			values.SetInteger("pointer", isValid ? cont : 0);

			SeekToOffset(field.Offset);

			StructureWriter.WriteStructure(values, _dataRefLayout, _writer);

			if (isValid)
			{
				// Go to the data location
				long offset = field.DataAddressValue;
				if (_type == SaveType.File)
					offset = _cache.MetaArea.PointerToOffset(offset);
				_writer.SeekTo(offset);

				// Build the data
				byte[] buffer = new byte[field.Length];
				byte[] bytes;

				switch (field.Format)
				{
					default:
						bytes = FunctionHelpers.HexStringToBytes(field.Value);
						break;
					case "asciiz":
						bytes = Encoding.GetEncoding(28591).GetBytes(field.Value);
						break;
				}

				Array.Copy(bytes, buffer, bytes.Length > field.Length ? field.Length : bytes.Length);
				_writer.WriteBlock(buffer, 0, buffer.Length);
			}
		}

		public void VisitTagRef(TagRefData field)
		{
			SeekToOffset(field.Offset);

			// Fix the TagRefData pointer to match that in the map's cache file
			field = FixTagRef(field);

			if (field.WithGroup)
			{
				var values = new StructureValueCollection();
				if (field.Value != null)
				{
					//hax
					if (field.Value.RawTag == null)
					{
						values.SetInteger("tag group magic", (uint)field.Group.RawGroup.Magic);
						values.SetInteger("datum index", 0xFFFFFFFF);
					}
					else
					{
						values.SetInteger("tag group magic", (uint)field.Value.RawTag.Group.Magic);
						values.SetInteger("datum index", field.Value.RawTag.Index.Value);
					}
				}
				else
				{
					values.SetInteger("tag group magic", 0xFFFFFFFF);
					values.SetInteger("datum index", 0xFFFFFFFF);
				}
				StructureWriter.WriteStructure(values, _tagRefLayout, _writer);
			}
			else
			{
				_writer.WriteUInt32(field.Value == null ? 0xFFFFFFFF : field.Value.RawTag.Index.Value);
			}
		}

		public void VisitPoint2(Vector2Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
		}

		public void VisitPoint3(Vector3Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
			_writer.WriteFloat(field.C);
		}

		public void VisitVector2(Vector2Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
		}

		public void VisitVector3(Vector3Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
			_writer.WriteFloat(field.C);
		}

		public void VisitVector4(Vector4Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
			_writer.WriteFloat(field.C);
			_writer.WriteFloat(field.D);
		}

		public void VisitPoint2(Point2Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
		}

		public void VisitPoint3(Point3Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
			_writer.WriteFloat(field.C);
		}

		public void VisitPlane2(Plane2Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
			_writer.WriteFloat(field.C);
		}

		public void VisitPlane3(Plane3Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
			_writer.WriteFloat(field.C);
			_writer.WriteFloat(field.D);
		}

		public void VisitDegree(DegreeData field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.Radian);
		}

		public void VisitDegree2(Degree2Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.RadianA);
			_writer.WriteFloat(field.RadianB);
		}

		public void VisitDegree3(Degree3Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.RadianA);
			_writer.WriteFloat(field.RadianB);
			_writer.WriteFloat(field.RadianC);
		}

		public void VisitPlane2(Vector3Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
			_writer.WriteFloat(field.C);
		}

		public void VisitPlane3(Vector4Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.A);
			_writer.WriteFloat(field.B);
			_writer.WriteFloat(field.C);
			_writer.WriteFloat(field.D);
		}

		public void VisitRect16(RectangleData field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteInt16(field.A);
			_writer.WriteInt16(field.B);
			_writer.WriteInt16(field.C);
			_writer.WriteInt16(field.D);
		}

		public void VisitQuat16(Quaternion16Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteInt16(field.A);
			_writer.WriteInt16(field.B);
			_writer.WriteInt16(field.C);
			_writer.WriteInt16(field.D);
		}

		public void VisitPoint16(Point16Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteInt16(field.A);
			_writer.WriteInt16(field.B);
		}

		public void VisitShaderRef(ShaderRef field)
		{
			// skip it for now
			// TODO: look at implementing a write function for this
			SeekToOffset(field.Offset);
		}

		public void VisitRangeUint16(RangeUint16Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteUInt16(field.Min);
			_writer.WriteUInt16(field.Max);
		}

		public void VisitRangeFloat32(RangeFloat32Data field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.Min);
			_writer.WriteFloat(field.Max);
		}

		public void VisitRangeDegree(RangeDegreeData field)
		{
			SeekToOffset(field.Offset);
			_writer.WriteFloat(field.RadianMin);
			_writer.WriteFloat(field.RadianMax);
		}

		public void WriteFields(IList<MetaField> fields)
		{
			foreach (MetaField t in fields)
				WriteField(t);
		}

		private void WriteField(MetaField field)
		{
			if (_changes == null || _changes.HasChanged(field))
				field.Accept(this);

			var block = field as TagBlockData;
			if (block != null)
				WriteTagBlockChildren(block);
		}

		public void WriteTagBlockChildren(TagBlockData field)
		{
			if (field.CurrentIndex < 0 || !field.HasChildren)
				return;

			// Get the base address and convert it to an offset if we're writing to the file
			long newBaseOffset = field.FirstElementAddress;
			if (_type == SaveType.File)
				newBaseOffset = _cache.MetaArea.PointerToOffset(newBaseOffset);

			// Save the old base offset and set the base offset to the block's base
			long oldBaseOffset = _baseOffset;
			_baseOffset = newBaseOffset;

			// Write each page
			int _oldIndex = field.CurrentIndex;
			bool _oldPokeTemplates = _pokeTemplateFields;
			for (int i = 0; i < field.Length; i++)
			{
				// If we're saving everything, then change the active page so the values get loaded from the file
				if (_changes == null && field.CurrentIndex != i)
					field.CurrentIndex = i;

				// If we're not saving everything, then we can only poke template fields in block
				// if the current indices all line up
				if (i != _oldIndex)
					_pokeTemplateFields = false;

				// Get each field in the PageCollection and write it
				TagBlockPage page = field.PageCollection[i];
				for (int j = 0; j < page.FieldValues.Length; j++)
				{
					MetaField pageField = page.FieldValues[j];
					// The field in the PageCollection takes precedence over the field in the block's template
					if (pageField == null && (_changes == null || _pokeTemplateFields))
						pageField = field.Template[j]; // Get it from the template
					if (pageField != null)
						WriteField(pageField);
				}

				// Advance to the next chunk
				_baseOffset += field.ElementSize;
				_pokeTemplateFields = _oldPokeTemplates;
			}
			if (!Equals(field.CurrentIndex, _oldIndex))
				field.CurrentIndex = _oldIndex;

			// Restore the old base offset
			_baseOffset = oldBaseOffset;
		}

		private void SeekToOffset(uint offset)
		{
			_writer.SeekTo(_baseOffset + offset);
		}

		/// <summary>
		/// Replaces the imported TagRefData.Value property with one residing in the map's cache file.
		/// This is necessary in order to have the TagRefData point to the correct location, otherwise the result can be null.
		/// </summary>
		/// <param name="field">The TagRefData object that is being imported</param>
		/// <returns>Updated TagRefData object</returns>
		public TagRefData FixTagRef(TagRefData field)
		{
			// TODO: Try to reuse / modify the TagContainerInjector.FixTagReferences method instead

			// If the tag group exists, search for tag
			if (field.GroupName != null)
			{
				// Search for the tag inside the cache that has the same name and group as the imported TagRefData object
				ITag tag = _cache.Tags.FindTagByName(field.TagFileName, field.GroupName, _cache.FileNames);

				// If the tag was found, update the TagRefData object
				if (tag != null)
				{
					// Create a new TagEntry using the newly found tag
					TagEntry entry = new TagEntry(tag, field.GroupName, field.TagFileName);

					// Change the imported TagRefData.Value property to the newly created TagEntry
					field.Value = entry;
				}
			}
			
			// TODO: store any tags that were not found and display to user.
			// This can happen if the map does not contain the tag, or the original name has been altered so that it does not match the imported tag.

			return field;
		}
	}
}
