﻿/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System.Diagnostics;
using MapleLib.PacketLib;
using System.Text;
using MapleLib.MapleCryptoLib;
using System.Threading.Tasks;

namespace MapleLib.WzLib
{
    /// <summary>
    /// An interface for wz img properties
    /// </summary>
    public abstract class WzImageProperty : WzObject
    {
        #region Virtual\Abstrcat Members
        public virtual WzPropertyCollection WzProperties { get { return null; } }

        public virtual new WzImageProperty this[string name] { get { return null; } set { throw new NotImplementedException(); } }

        public virtual WzImageProperty GetFromPath(string path)
        {
            return null;
        }

        public abstract WzPropertyType PropertyType { get; }

        /// <summary>
        /// The image that this property is contained in
        /// </summary>
        public WzImage ParentImage
        {
            get
            {
                WzObject parent = Parent;
                while (parent != null)
                {
                    if (parent is WzImage) return (WzImage)parent;
                    else parent = parent.Parent;
                }
                return null;
            }
        }

        public override WzObjectType ObjectType { get { return WzObjectType.Property; } }

        public abstract void WriteValue(WzBinaryWriter writer);

        public abstract WzImageProperty DeepClone();

        public abstract void SetValue(object value);

        public override void Remove()
        {
            ((IPropertyContainer)Parent).RemoveProperty(this);
        }

        public virtual void ExportXml(StreamWriter writer, int level)
        {
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.OpenNamedTag(this.PropertyType.ToString(), this.Name, true));
            writer.WriteLine(XmlUtil.Indentation(level) + XmlUtil.CloseTag(this.PropertyType.ToString()));
        }

        public override WzFile WzFileParent
        {
            get { return ParentImage.WzFileParent; }
        }
        #endregion

        #region Extended Properties Parsing
        internal static void WritePropertyList(WzBinaryWriter writer, List<WzImageProperty> properties)
        {
            if (properties.Count == 1 && properties[0] is WzLuaProperty)
            {
                properties[0].WriteValue(writer);
            }
            else
            {
                writer.Write((ushort)0);
                writer.WriteCompressedInt(properties.Count);
                foreach (WzImageProperty imgProperty in properties)
                {
                    writer.WriteStringValue(imgProperty.Name, 0x00, 0x01);
                    if (imgProperty is WzExtended extended)
                        WriteExtendedValue(writer, extended);
                    else
                        imgProperty.WriteValue(writer);
                }
            }
        }

        internal static void DumpPropertyList(StreamWriter writer, int level, List<WzImageProperty> properties)
        {
            foreach (WzImageProperty prop in properties)
            {
                prop.ExportXml(writer, level + 1);
            }
        }

        /// <summary>
        /// Parses .lua property
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="reader"></param>
        /// <param name="parent"></param>
        /// <param name="parentImg"></param>
        /// <returns></returns>
        internal static WzLuaProperty ParseLuaProperty(long offset, WzBinaryReader reader, WzObject parent, WzImage parentImg)
        {
            // 28 71 4F EF 1B 65 F9 1F A7 48 8D 11 73 E7 F0 27 55 09 DD 3C 07 32 D7 38 21 57 84 70 C1 79 9A 3F 49 F7 79 03 41 F4 9D B9 1B 5F CF 26 80 3D EC 25 5F 9C 
            // [compressed int] [bytes]
            int length = reader.ReadCompressedInt();
            byte[] rawEncBytes = reader.ReadBytes(length);

            WzLuaProperty lua = new WzLuaProperty("Script", rawEncBytes) { Parent = parent };
            return lua;
        }

        internal static WzPropertyCollection ParsePropertyList(long offset, WzBinaryReader reader, WzObject parent, WzImage parentImg)
        {
            int entryCount = reader.ReadCompressedInt();
            WzPropertyCollection properties = new WzPropertyCollection(parent);
            for (int i = 0; i < entryCount; i++)
            {
                string name = reader.ReadStringBlock(offset);
                byte ptype = reader.ReadByte();
                switch (ptype) // header value
                {
                    case 0:
                        properties.Add(new WzNullProperty(name) { Parent = parent });
                        break;
                    case 11:
                    case 2:
                        properties.Add(new WzShortProperty(name, reader.ReadInt16()) { Parent = parent });
                        break;
                    case 3:
                    case 19:
                        properties.Add(new WzIntProperty(name, reader.ReadCompressedInt()) { Parent = parent });
                        break;
                    case 20:
                        properties.Add(new WzLongProperty(name, reader.ReadLong()) { Parent = parent });
                        break;
                    case 4:
                        byte type = reader.ReadByte();
                        if (type == 0x80)
                            properties.Add(new WzFloatProperty(name, reader.ReadSingle()) { Parent = parent });
                        else if (type == 0)
                            properties.Add(new WzFloatProperty(name, 0f) { Parent = parent });
                        break;
                    case 5:
                        properties.Add(new WzDoubleProperty(name, reader.ReadDouble()) { Parent = parent });
                        break;
                    case 8:
                        properties.Add(new WzStringProperty(name, reader.ReadStringBlock(offset)) { Parent = parent });
                        break;
                    case 9:
                        long eob = reader.ReadUInt32() + reader.BaseStream.Position;
                        WzImageProperty exProp = ParseExtendedProp(reader, offset, eob, name, parent, parentImg);
                        properties.Add(exProp);
                        if (reader.BaseStream.Position != eob)
                        {
                            reader.BaseStream.Position = eob;
                        }
                        break;
                    default:
                        throw new Exception("Unknown property type at ParsePropertyList, ptype = " + ptype);
                }
            }
            return properties;
        }

        internal static WzExtended ParseExtendedProp(WzBinaryReader reader, long offset, long endOfBlock, string name, WzObject parent, WzImage imgParent)
        {
            switch (reader.ReadByte())
            {
                case 0x01:
                case WzImage.WzImageHeaderByte_WithOffset:
                    return ExtractMore(reader, offset, endOfBlock, name, reader.ReadStringAtOffset(offset + reader.ReadInt32()), parent, imgParent);
                case 0x00:
                case WzImage.WzImageHeaderByte_WithoutOffset:
                    return ExtractMore(reader, offset, endOfBlock, name, "", parent, imgParent);
                default:
                    throw new System.Exception("Invalid byte read at ParseExtendedProp");
            }
        }

        internal static WzExtended ExtractMore(WzBinaryReader reader, long offset, long eob, string name, string iname, WzObject parent, WzImage imgParent)
        {
            if (iname == "")
            {
                iname = reader.ReadString();
            }
            switch (iname)
            {
                case "Property":
                    {
                        WzSubProperty subProp = new WzSubProperty(name) { Parent = parent };
                        reader.BaseStream.Position += 2; // Reserved?
                        subProp.AddProperties(WzImageProperty.ParsePropertyList(offset, reader, subProp, imgParent));
                        return subProp;
                    }
                case "Canvas":
                    {
                        WzCanvasProperty canvasProp = new WzCanvasProperty(name) { Parent = parent };
                        reader.BaseStream.Position++;
                        if (reader.ReadByte() == 1)
                        {
                            reader.BaseStream.Position += 2;
                            canvasProp.AddProperties(WzImageProperty.ParsePropertyList(offset, reader, canvasProp, imgParent));
                        }
                        canvasProp.PngProperty = new WzPngProperty(reader, imgParent.ParseEverything) { Parent = canvasProp };
                        return canvasProp;
                    }
                case "Shape2D#Vector2D":
                    {
                        WzVectorProperty vecProp = new WzVectorProperty(name) { Parent = parent };
                        vecProp.X = new WzIntProperty("X", reader.ReadCompressedInt()) { Parent = vecProp };
                        vecProp.Y = new WzIntProperty("Y", reader.ReadCompressedInt()) { Parent = vecProp };
                        return vecProp;
                    }
                case "Shape2D#Convex2D":
                    {
                        WzConvexProperty convexProp = new WzConvexProperty(name) { Parent = parent };
                        int convexEntryCount = reader.ReadCompressedInt();
                        convexProp.WzProperties.Capacity = convexEntryCount;
                        for (int i = 0; i < convexEntryCount; i++)
                        {
                            convexProp.AddProperty(ParseExtendedProp(reader, offset, 0, name, convexProp, imgParent));
                        }
                        return convexProp;
                    }
                case "Sound_DX8":
                    {
                        WzBinaryProperty soundProp = new WzBinaryProperty(name, reader, imgParent.ParseEverything) { Parent = parent };
                        return soundProp;
                    }
                case "UOL":
                    {
                        reader.BaseStream.Position++;
                        switch (reader.ReadByte())
                        {
                            case 0:
                                return new WzUOLProperty(name, reader.ReadString()) { Parent = parent };
                            case 1:
                                return new WzUOLProperty(name, reader.ReadStringAtOffset(offset + reader.ReadInt32())) { Parent = parent };
                        }
                        throw new Exception("Unsupported UOL type");
                    }
                case WzRawDataProperty.RAW_DATA_HEADER:  // GMS v220++
                    {
                        // GMS v255+
                        // UI_000.wz\Login.img\RaceSelect_new\Back0\3\skeleton.skel 
                        // UI_000.wz\Login.img\RaceSelect_new\Back0\16\skeleton.skel
                        // UI_000.wz\Login.img\RaceSelect_new\Back0\1005\Sia.skel
                        var type = reader.ReadByte();

                        var rawDataProp = new WzRawDataProperty(name, reader, type) { Parent = parent };

                        // type 0: do nothing
                        // type 1: similar to CanvasProperty that has binary data (the PNG) and sub properties (the origin, _hash, etc.)
                        if (type == 1)
                        {
                            if (reader.ReadByte() == 1)
                            {
                                reader.BaseStream.Position += 2; // Reserved?
                                rawDataProp.AddProperties(ParsePropertyList(offset, reader, rawDataProp, imgParent));
                            }
                        }

                        // all types: parse the binary data (similar to parse WzPngProperty for WzCanvasProperty)
                        rawDataProp.Parse(imgParent.ParseEverything); // TODO: be able to display this raw data in HaRepacker
                        return rawDataProp;
                    }
                case WzVideoProperty.CANVAS_VIDEO_HEADER: // KMST v1181
                    {
                        WzVideoProperty videoProperty = new WzVideoProperty(name, reader);
                        videoProperty.Parent = parent;

                        reader.BaseStream.Position++;
                        if (reader.ReadByte() == 1)
                        {
                            reader.BaseStream.Position += 2;
                            videoProperty.AddProperties(WzImageProperty.ParsePropertyList(offset, reader, videoProperty, imgParent));
                        }
                        videoProperty.Parse(imgParent.ParseEverything); // TODO:  be able to display this Canvas#Video in HaRepacker
                        return videoProperty;
                    }
                default:
                    throw new Exception("Unknown iname: " + iname);
            }
        }

        internal static void WriteExtendedValue(WzBinaryWriter writer, WzExtended property)
        {
            writer.Write((byte)9);

            long beforePos = writer.BaseStream.Position;
            writer.Write((Int32)0); // Placeholder
            property.WriteValue(writer);

            int len = (int)(writer.BaseStream.Position - beforePos);
            long newPos = writer.BaseStream.Position;
            writer.BaseStream.Position = beforePos;
            writer.Write(len - 4);
            writer.BaseStream.Position = newPos;
        }
        #endregion

        #region Custom Members
        /// <summary>
        /// Gets the linked WzImageProperty via WzUOLProperty
        /// </summary>
        /// <returns></returns>
        public WzImageProperty GetLinkedWzImageProperty()
        {
            WzImageProperty thisWzImage = this;
            while ((thisWzImage is WzUOLProperty))
            {
                if ((thisWzImage as WzUOLProperty).LinkValue is WzImageProperty newWzImage)
                    thisWzImage = newWzImage;
                else // broken link
                    return this;
            }
            return thisWzImage;
        }
        #endregion
    }
}