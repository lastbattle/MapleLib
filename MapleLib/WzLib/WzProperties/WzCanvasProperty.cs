using System.Collections.Generic;
using System.IO;
using MapleLib.WzLib.Util;
using System;
using System.Drawing;
using System.Diagnostics;

namespace MapleLib.WzLib.WzProperties {
    /// <summary>
    /// A property that can contain sub properties and has one png image
    /// </summary>
    public class WzCanvasProperty : WzExtended, IPropertyContainer {
        #region Constants
        /// <summary>
        /// The propertyname used for inlink
        /// </summary>
        public const string InlinkPropertyName = "_inlink";
        public const string OutlinkPropertyName = "_outlink";

        /// <summary>
        /// Optional external image resolver used by IMG filesystem consumers where WzFile parent
        /// relationships are unavailable for _outlink traversal.
        /// The input should be a category-rooted image path such as "Map/Map/Map0/_Canvas/010006121.img".
        /// </summary>
        public static Func<string, WzImage> ExternalImageResolver { get; set; }
        public const string OriginPropertyName = "origin";
        public const string HeadPropertyName = "head";
        public const string LtPropertyName = "lt";
        public const string AnimationDelayPropertyName = "delay";
        #endregion

        #region Fields
        internal WzPropertyCollection properties;
        internal WzPngProperty imageProp;
        internal string name;
        internal WzObject parent;
        //internal WzImage imgParent;
        #endregion

        #region Inherited Members
        public override void SetValue(object value) {
            imageProp.SetValue(value);
        }

        public override WzImageProperty DeepClone() {
            WzCanvasProperty clone = new WzCanvasProperty(name);
            foreach (WzImageProperty prop in properties) {
                clone.AddProperty(prop.DeepClone());
            }
            clone.imageProp = (WzPngProperty)imageProp.DeepClone();
            return clone;
        }

        public override object WzValue { get { return PngProperty; } }
        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get { return parent; } internal set { parent = value; } }
        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType { get { return WzPropertyType.Canvas; } }
        /// <summary>
        /// The properties contained in this property
        /// </summary>
        public override WzPropertyCollection WzProperties {
            get {
                return properties;
            }
        }
        /// <summary>
        /// The name of the property
        /// </summary>
        public override string Name { get { return name; } set { name = value; } }
        /// <summary>
        /// Gets a wz property by it's name
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <returns>The wz property with the specified name</returns>
        public override WzImageProperty this[string name] {
            get {
                if (string.Equals(name, "PNG", StringComparison.Ordinal))
                    return imageProp;

                return FindProperty(name, StringComparison.OrdinalIgnoreCase);
            }
            set {
                if (value != null) {
                    if (string.Equals(name, "PNG", StringComparison.Ordinal)) {
                        imageProp = (WzPngProperty)value;
                        return;
                    }
                    value.Name = name;
                    AddProperty(value);
                }
            }
        }

        public WzImageProperty GetProperty(string name) {
            return FindProperty(name, StringComparison.OrdinalIgnoreCase);
        }

        /// Gets a wz property by a path name
        /// </summary>
        /// <param name="path">path to property</param>
        /// <returns>the wz property with the specified name</returns>
        public override WzImageProperty GetFromPath(string path) {
            string[] segments = path.Split(new char[1] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) {
                return null;
            }

            if (segments[0] == "..") {
                return ((WzImageProperty)Parent)[path.Substring(name.IndexOf('/') + 1)];
            }

            WzImageProperty ret = this;
            foreach (string segment in segments) {
                if (segment == "PNG")
                    return imageProp;

                WzImageProperty iwp = FindProperty(ret.WzProperties, segment, StringComparison.Ordinal);
                if (iwp == null) {
                    return null;
                }

                ret = iwp;
            }

            return ret;
        }

        private WzImageProperty FindProperty(string propertyName, StringComparison comparisonType) {
            return FindProperty(properties, propertyName, comparisonType);
        }

        private static WzImageProperty FindProperty(WzPropertyCollection propertyCollection, string propertyName, StringComparison comparisonType) {
            if (propertyCollection == null || propertyName == null) {
                return null;
            }

            for (int i = 0; i < propertyCollection.Count; i++) {
                WzImageProperty property = propertyCollection[i];
                if (string.Equals(property.Name, propertyName, comparisonType)) {
                    return property;
                }
            }

            return null;
        }

        public override void WriteValue(WzBinaryWriter writer) {
            writer.WriteStringValue("Canvas", WzImage.WzImageHeaderByte_WithoutOffset, WzImage.WzImageHeaderByte_WithOffset);
            writer.Write((byte)0);
            if (properties.Count > 0) // subproperty in the canvas
            {
                writer.Write((byte)1);
                WzImageProperty.WritePropertyList(writer, properties);
            }
            else {
                writer.Write((byte)0);
            }

            // Image info
            writer.WriteCompressedInt(PngProperty.Width);
            writer.WriteCompressedInt(PngProperty.Height);

            int formatValue = (int)PngProperty.Format;
            int format1 = formatValue & 0xFF; // Lower 8 bits
            int format2 = formatValue >> 8;   // Upper bits
            writer.WriteCompressedInt(format1);
            writer.WriteCompressedInt(format2);

            writer.Write((Int32)0);

            // Write image - use GetCompressedBytesForExtraction to convert listWz format
            // to standard zlib format, ensuring PNG can be read without the original WzKey
            byte[] bytes = PngProperty.GetCompressedBytesForExtraction(false);
            writer.Write(bytes.Length + 1);
            writer.Write((byte)0); // header? see WzImageProperty.ParseExtendedProp "0x00"
            writer.Write(bytes);
        }

        public override void ExportXml(StreamWriter writer, int level) {
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.OpenNamedTag("WzCanvas", this.Name, false, false) +
            XmlUtil.Attrib("width", PngProperty.Width.ToString()) +
            XmlUtil.Attrib("height", PngProperty.Height.ToString(), true, false));
            WzImageProperty.DumpPropertyList(writer, level, this.WzProperties);
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.CloseTag("WzCanvas"));
        }

        /// <summary>
        /// Dispose the object
        /// </summary>
        public override void Dispose() {
            name = null;
            imageProp.Dispose();
            imageProp = null;
            foreach (WzImageProperty prop in properties) {
                prop.Dispose();
            }
            properties.Clear();
            properties = null;
        }
        #endregion

        #region Custom Members

        /// <summary>
        /// Gets the 'origin' position of the Canvas
        /// If not available, it defaults to xy of 0, 0
        /// </summary>
        /// <returns></returns>
        public PointF GetCanvasOriginPosition() {
            WzVectorProperty originPos = (WzVectorProperty)this[OriginPropertyName];
            if (originPos != null)
                return new PointF(originPos.X.Value, originPos.Y.Value);

            return new PointF(0, 0);
        }

        public void SetCanvasOriginPosition(PointF pointF) {
            PointF pointXY = GetCanvasOriginPosition();
            if (pointXY.X != 0 && pointXY.Y != 0) {
                WzVectorProperty originPos = (WzVectorProperty)this[OriginPropertyName];
                originPos.X.SetValue(pointF.X);
                originPos.Y.SetValue(pointF.Y);
            }
            else
                throw new Exception(string.Format("'{0}' property is not available", OriginPropertyName));
        }

        /// <summary>
        /// Gets the 'head' position of the Canvas
        /// If not available, it defaults to xy of 0, 0
        /// </summary>
        /// <returns></returns>
        public PointF GetCanvasHeadPosition() {
            WzVectorProperty headPos = (WzVectorProperty)this[HeadPropertyName];
            if (headPos != null)
                return new PointF(headPos.X.Value, headPos.Y.Value);

            return new PointF(0, 0);
        }

        /// <summary>
        /// Gets the 'head' position of the Canvas
        /// If not available, it defaults to xy of 0, 0
        /// </summary>
        /// <returns></returns>
        public PointF GetCanvasLtPosition() {
            WzVectorProperty headPos = (WzVectorProperty)this[LtPropertyName];
            if (headPos != null)
                return new PointF(headPos.X.Value, headPos.Y.Value);

            return new PointF(0, 0);
        }

        /// <summary>
        /// Gets whether this WzCanvasProperty contains an '_inlink' for modern maplestory version. v150++
        /// </summary>
        /// <returns></returns>
        public bool ContainsInlinkProperty() {
            return this[InlinkPropertyName] != null;
        }
        /// <summary>
        /// Gets whether this WzCanvasProperty contains an '_outlink' for modern maplestory version. v150++
        /// </summary>
        /// <returns></returns>
        public bool ContainsOutlinkProperty() {
            return this[OutlinkPropertyName] != null;
        }

        /// <summary>
        /// Gets the '_inlink' WzCanvasProperty of this.
        /// 
        /// '_inlink' is not implemented as part of WzCanvasProperty as I dont want to override existing Wz structure. 
        /// It will be handled via HaRepackerMainPanel instead.
        /// </summary>
        /// <returns></returns>
        public Bitmap GetLinkedWzCanvasBitmap() {
            return GetLinkedWzImageProperty().GetBitmap();
        }

        /// <summary>
        /// Gets the '_inlink' or '_outlink' WzImageProperty of this.
        /// 
        /// '_inlink' is not implemented as part of WzCanvasProperty so as to not override existing Wz structure. 
        /// It will be handled via HaRepackerMainPanel instead.
        /// </summary>
        /// <returns></returns>
        public WzImageProperty GetLinkedWzImageProperty()
        {
            string _inlink = ((WzStringProperty)this[InlinkPropertyName])?.Value; // could get nexon'd here. In case they place an _inlink that's not WzStringProperty
            string _outlink = ((WzStringProperty)this[OutlinkPropertyName])?.Value; // could get nexon'd here. In case they place an _outlink that's not WzStringProperty

            if (!string.IsNullOrEmpty(_inlink))
            {
                var current = this.Parent; // first object to work with
                while (current != null)
                {
                    if (current is WzImage wzImageParent) // keep looping if its not a WzImage
                    {
                        if (wzImageParent.GetFromPath(_inlink) is WzImageProperty property)
                        {
                            return property;
                        }
                        // No need to continue; assuming only the nearest WzImage is relevant.
                        break;
                    }
                    current = current.Parent;
                }
                Debug.WriteLine("Could not resolve _inlink path: " + _inlink);
            }
            else if (!string.IsNullOrEmpty(_outlink)) 
            {
                var current = this.Parent;
                WzFile wzFileParent = null;
                while (current != null)
                {
                    if (current is WzDirectory dir)
                    {
                        wzFileParent = dir.wzFile;
                        // No need to continue; wzFile is shared.
                        break;
                    }
                    current = current.Parent;
                }

                if (wzFileParent != null)
                {
                    // TODO
                    // Given the way it is structured, it might possibility also point to a different WZ file (i.e NPC.wz instead of Mob.wz).
                    // Mob001.wz/8800103.img/8800103.png has an outlink to "Mob/8800141.img/8800141.png"
                    // https://github.com/lastbattle/Harepacker-resurrected/pull/142

                    string prefixWz = GetWzFileAlphaPrefix(wzFileParent.Name) + "/"; // remove ended numbers and .wz from wzfile name

                    WzObject foundProperty;

                    if (_outlink.StartsWith(prefixWz, StringComparison.OrdinalIgnoreCase)) {
                        // fixed root path
                        string fileNameWithoutExtension = wzFileParent.Name.EndsWith(".wz", StringComparison.OrdinalIgnoreCase)
                            ? wzFileParent.Name.Substring(0, wzFileParent.Name.Length - 3)
                            : wzFileParent.Name;
                        string realpath = fileNameWithoutExtension + "/" + _outlink.Substring(prefixWz.Length);
                        foundProperty = wzFileParent.GetObjectFromPath(realpath);
                    }
                    else {
                        // If its a 64-bit wz file format, with "_Canvas".
                        // parse that instead, the canvas will never be in the data wz directory.
                        // TODO: Move this into the loader instead.
                        if (WzFileManager.fileManager != null && WzFileManager.fileManager.Is64Bit) {
                            // _outlink = 'Map/Back/_Canvas/snowyDarkrock.img/back/0'
                            bool bIsCanvasDir = WzFileManager.ContainsCanvasDirectory(_outlink);
                            if (bIsCanvasDir)
                            {
                                string canvasFolderBase = WzFileManager.NormaliseWzCanvasDirectory(_outlink);  // "map", "map/back"

                                WzFileManager.fileManager.LoadCanvasSection(canvasFolderBase, wzFileParent.MapleVersion);
                            }
                            else
                            {
                                Debug.WriteLine($"{FullPath} has an _outlink that does not contain '{WzFileManager.CANVAS_DIRECTORY_NAME}'");
                            }
                        }

                        // Get from path
                        foundProperty = wzFileParent.GetObjectFromPath(_outlink);
                    }
                    if (foundProperty != null && foundProperty is WzImageProperty property) {
                        return property;
                    }
                }

                WzImageProperty externallyResolvedProperty = ResolveLinkedImagePropertyFromExternalResolver(_outlink);
                if (externallyResolvedProperty != null)
                {
                    return externallyResolvedProperty;
                }

                Debug.WriteLine("Could not resolve _outlink path: " + _outlink);
            }
            return this;
        }

        private static string GetWzFileAlphaPrefix(string wzFileName)
        {
            if (string.IsNullOrEmpty(wzFileName))
            {
                return string.Empty;
            }

            int end = wzFileName.EndsWith(".wz", StringComparison.OrdinalIgnoreCase)
                ? wzFileName.Length - 3
                : wzFileName.Length;

            while (end > 0 && char.IsDigit(wzFileName[end - 1]))
            {
                end--;
            }

            return wzFileName.Substring(0, end);
        }

        private static WzImageProperty ResolveLinkedImagePropertyFromExternalResolver(string outlinkPath)
        {
            if (string.IsNullOrWhiteSpace(outlinkPath) || ExternalImageResolver == null)
            {
                return null;
            }

            string normalizedPath = outlinkPath.Replace('\\', '/').Trim('/');
            int imagePathEnd = FindImagePathEnd(normalizedPath);
            if (imagePathEnd < 0)
            {
                return null;
            }

            string imagePath = normalizedPath.Substring(0, imagePathEnd);
            WzImage image = ExternalImageResolver(imagePath);
            if (image == null)
            {
                return null;
            }

            if (!image.Parsed)
            {
                image.ParseImage();
            }

            int propertyPathStart = imagePathEnd + 1;
            if (propertyPathStart >= normalizedPath.Length)
            {
                return null;
            }

            string propertyPath = normalizedPath.Substring(propertyPathStart);
            return image.GetFromPath(propertyPath) as WzImageProperty;
        }

        private static int FindImagePathEnd(string normalizedPath)
        {
            int segmentStart = 0;
            while (segmentStart < normalizedPath.Length)
            {
                int slashIndex = normalizedPath.IndexOf('/', segmentStart);
                int segmentEnd = slashIndex < 0 ? normalizedPath.Length : slashIndex;
                int segmentLength = segmentEnd - segmentStart;

                if (segmentLength >= 4 &&
                    string.Compare(normalizedPath, segmentEnd - 4, ".img", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return segmentEnd;
                }

                if (slashIndex < 0)
                {
                    break;
                }

                segmentStart = slashIndex + 1;
            }

            return -1;
        }

        /// <summary>
        /// The png image for this canvas property
        /// </summary>
        public WzPngProperty PngProperty {
            get {
                return imageProp;
            }
            set {
                imageProp = value;
            }
        }

        /// <summary>
        /// Creates a blank WzCanvasProperty
        /// </summary>
        public WzCanvasProperty() {
            this.properties = new WzPropertyCollection(this);
        }
        /// <summary>
        /// Creates a WzCanvasProperty with the specified name
        /// </summary>
        /// <param name="name">The name of the property</param>
        public WzCanvasProperty(string name) {
            this.name = name;

            this.properties = new WzPropertyCollection(this);
        }
        /// <summary>
        /// Adds a property to the property list of this property
        /// </summary>
        /// <param name="prop">The property to add</param>
        public void AddProperty(WzImageProperty prop) {
            prop.Parent = this;
            properties.Add(prop);
        }
        public void AddProperties(WzPropertyCollection props) {
            foreach (WzImageProperty prop in props) {
                AddProperty(prop);
            }
        }
        /// <summary>
        /// Remove a property
        /// </summary>
        /// <param name="name">Name of Property</param>
        public void RemoveProperty(WzImageProperty prop) {
            prop.Parent = null;
            properties.Remove(prop);
        }

        /// <summary>
        /// Remove a property by its name
        /// </summary>
        /// <param name="name">Name of Property</param>
        public void RemoveProperty(string propertyName)
        {
            WzImageProperty prop = this[propertyName];
            if (prop != null) {
                RemoveProperty(prop);
            }
        }

        /// <summary>
        /// Clears the list of properties
        /// </summary>
        public void ClearProperties() {
            foreach (WzImageProperty prop in properties) prop.Parent = null;
            properties.Clear();
        }
        #endregion

        #region Cast Values

        public override Bitmap GetBitmap() {
            return imageProp.GetImage(false);
        }
        #endregion
    }
}
