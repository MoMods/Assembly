namespace Assembly.Metro.Controls.PageTemplates.Games.Components.MetaData
{
    /// <summary>
    /// A modified version of IMetaReader.
    /// Returns the field object instead of returning void.
    /// Used by the MetaStoreReader class for writing MetaData to an external source, such as a database.
    /// </summary>
    interface IMetaStoreReader
    {
        ColorData InsertColourFloat(ColorData field);
        ColorData InsertColourInt(ColorData field);
        DataRef InsertDataRef(DataRef field);
        DegreeData InsertDegree(DegreeData field);
        Degree2Data InsertDegree2(Degree2Data field);
        Degree3Data InsertDegree3(Degree3Data field);
        EnumData InsertEnum(EnumData field);
        FlagData InsertFlags(FlagData field);
        Float32Data InsertFloat32(Float32Data field);
        Int16Data InsertInt16(Int16Data field);
        Int32Data InsertInt32(Int32Data field);
        Int64Data InsertInt64(Int64Data field);
        Int8Data InsertInt8(Int8Data field);
        Plane2Data InsertPlane2(Plane2Data field);
        Vector3Data InsertPlane2(Vector3Data field);
        Plane3Data InsertPlane3(Plane3Data field);
        Vector4Data InsertPlane3(Vector4Data field);
        Point16Data InsertPoint16(Point16Data field);
        Point2Data InsertPoint2(Point2Data field);
        Vector2Data InsertPoint2(Vector2Data field);
        Point3Data InsertPoint3(Point3Data field);
        Vector3Data InsertPoint3(Vector3Data field);
        Quaternion16Data InsertQuat16(Quaternion16Data field);
        RangeDegreeData InsertRangeDegree(RangeDegreeData field);
        RangeFloat32Data InsertRangeFloat32(RangeFloat32Data field);
        RangeUint16Data InsertRangeUint16(RangeUint16Data field);
        RawData InsertRawData(RawData field);
        RectangleData InsertRect16(RectangleData field);
        ShaderRef InsertShaderRef(ShaderRef field);
        StringData InsertString(StringData field);
        StringIDData InsertStringID(StringIDData field);
        TagBlockData InsertTagBlock(TagBlockData field);
        TagRefData InsertTagRef(TagRefData field);
        Uint16Data InsertUint16(Uint16Data field);
        Uint32Data InsertUint32(Uint32Data field);
        Uint64Data InsertUint64(Uint64Data field);
        Uint8Data InsertUint8(Uint8Data field);
        Vector2Data InsertVector2(Vector2Data field);
        Vector3Data InsertVector3(Vector3Data field);
        Vector4Data InsertVector4(Vector4Data field);
    }
}