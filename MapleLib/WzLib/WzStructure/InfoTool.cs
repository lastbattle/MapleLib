using System;
using System.Drawing;
using System.IO;
using MapleLib.WzLib.WzProperties;

namespace MapleLib.WzLib.WzStructure
{
    public static class InfoTool
    {
        // WzObject.GetX throws NotImplementedException for a property type
        // without that view. A UOL node instead throws InvalidDataException
        // when its link is missing or cyclic. Every caller here is a consumer
        // of authored content, so either shape degrades to the accessor
        // default instead of aborting the whole map load.
        private static T ReadOrDefault<T>(WzImageProperty source, Func<WzImageProperty, T> read, T fallback)
        {
            if (source == null)
                return fallback;

            try
            {
                return read(source);
            }
            catch (Exception ex) when (ex is NotImplementedException or InvalidDataException)
            {
                return fallback;
            }
        }

        #region String
        public static string GetString(this WzImageProperty source)
        {
            return ReadOrDefault(source, static property => property.GetString(), null);
        }

        public static WzStringProperty SetString(string value)
        {
            return new WzStringProperty("", value);
        }

        public static string GetOptionalString(this WzImageProperty source)
        {
            return ReadOrDefault(source, static property => property.GetString(), null);
        }

        public static WzStringProperty SetOptionalString(string value)
        {
            return value == null ? null : SetString(value);
        }
        #endregion


        #region Double
        public static double GetDouble(this WzImageProperty source)
        {
            return ReadOrDefault(source, static property => property.GetDouble(), 0d);
        }

        public static WzDoubleProperty SetDouble(double value)
        {
            return new WzDoubleProperty("", value);
        }
        #endregion


        #region Integer
        public static int GetInt(this WzImageProperty source, int default_ = 0)
        {
            return ReadOrDefault(source, static property => property.GetInt(), default_);
        }

        public static WzIntProperty SetInt(int value)
        {
            return new WzIntProperty("", value);
        }

        public static int? GetOptionalInt(this WzImageProperty source, int? default_ = null)
        {
            return ReadOrDefault(source, static property => (int?)property.GetInt(), default_);
        }

        public static WzIntProperty SetOptionalInt(int? value)
        {
            return value.HasValue ? SetInt(value.Value) : null;
        }
        #endregion

        #region Translated Integer
        public static int? GetOptionalTranslatedInt(this WzImageProperty source)
        {
            string str = InfoTool.GetOptionalString(source);
            if (str == null) return null;
            return int.TryParse(str, out int value) ? value : (int?)null;
        }

        public static WzStringProperty SetOptionalTranslatedInt(int? value)
        {
            if (value.HasValue)
            {
                return SetString(value.Value.ToString());
            }
            else
            {
                return null;
            }
        }
        #endregion

        #region Rectangle
        private static WzImageProperty TryGetChild(WzImageProperty source, string name)
        {
            if (source == null)
                return null;

            try
            {
                return source[name];
            }
            catch (InvalidDataException)
            {
                // UOL with a broken or cyclic link has no child view.
                return null;
            }
        }

        /// <summary>
        /// Gets System.Drawing.Rectangle from "lt" and "rb"
        /// </summary>
        /// <param name="parentSource"></param>
        /// <returns></returns>
        public static Rectangle GetLtRbRectangle(this WzImageProperty parentSource)
        {
            bool hasChildren;
            try
            {
                hasChildren = parentSource?.WzProperties != null;
            }
            catch (InvalidDataException)
            {
                hasChildren = false;
            }

            if (!hasChildren)
                return Rectangle.Empty;

            WzVectorProperty lt = InfoTool.GetOptionalVector(TryGetChild(parentSource, "lt"));
            WzVectorProperty rb = InfoTool.GetOptionalVector(TryGetChild(parentSource, "rb"));

            int left = lt.X?.Value ?? 0;
            int top = lt.Y?.Value ?? 0;
            int right = rb.X?.Value ?? 0;
            int bottom = rb.Y?.Value ?? 0;

            Rectangle rectangle = new Rectangle(
                left,
                top,
                right - left,
                bottom - top);
            return rectangle;
        }

        /// <summary>
        /// Sets the "lt" and "rb" value in a WzImageProperty parentSource
        /// derived from Rectangle
        /// </summary>
        /// <param name="parentSource"></param>
        /// <param name="rect"></param>
        public static void SetLtRbRectangle(this WzImageProperty parentSource, Rectangle rect)
        {
            parentSource["lt"] = InfoTool.SetVector(rect.X, rect.Y);
            parentSource["rb"] = InfoTool.SetVector(rect.X + rect.Width, rect.Y + rect.Height);
        }
        #endregion

        #region Vector
        /// <summary>
        /// Gets the vector value of the WzImageProperty
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static WzVectorProperty GetVector(this WzImageProperty source)
        {
            // Same zero fallback GetOptionalVector already applies to a null
            // node, so call sites that dereference the result keep working.
            return source as WzVectorProperty ?? new WzVectorProperty(String.Empty, 0, 0);
        }

        /// <summary>
        /// Sets vector
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static WzVectorProperty SetVector(float x, float y)
        {
            return new WzVectorProperty("", x, y);
        }


        /// <summary>
        /// Gets an optional Vector. 
        /// Returns x = 0, and y = 0 if the WzImageProperty is not found.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static WzVectorProperty GetOptionalVector(this WzImageProperty source)
        {
            return GetVector(source);
        }

        #endregion

        #region Long
        public static long GetLong(this WzImageProperty source)
        {
            return ReadOrDefault(source, static property => property.GetLong(), 0L);
        }

        public static WzLongProperty SetLong(long value)
        {
            return new WzLongProperty("", value);
        }

        public static long? GetOptionalLong(this WzImageProperty source)
        {
            return ReadOrDefault(source, static property => (long?)property.GetLong(), null);
        }

        public static WzLongProperty SetOptionalLong(long? value)
        {
            return value.HasValue ? SetLong(value.Value) : null;
        }
        #endregion

        #region Boolean
        public static bool GetBool(this WzImageProperty source)
        {
            return ReadOrDefault(source, static property => property.GetInt() == 1, false);
        }

        public static WzIntProperty SetBool(bool value)
        {
            return new WzIntProperty("", value ? 1 : 0);
        }

        public static MapleBool GetOptionalBool(this WzImageProperty source)
        {
            return ReadOrDefault<MapleBool>(source, static property => property.GetInt() == 1, MapleBool.NotExist);
        }

        public static WzIntProperty SetOptionalBool(this MapleBool value)
        {
            if (value.HasValue)
            {
                if (value.Value == true)
                {
                    return SetBool(value.Value);
                }
            }
            return null;
        }
        #endregion


        #region Float
        public static float GetFloat(this WzImageProperty source)
        {
            return ReadOrDefault(source, static property => property.GetFloat(), 0f);
        }

        public static WzFloatProperty SetFloat(float value)
        {
            return new WzFloatProperty("", value);
        }

        public static float? GetOptionalFloat(this WzImageProperty source)
        {
            return ReadOrDefault(source, static property => (float?)property.GetFloat(), null);
        }

        public static WzFloatProperty SetOptionalFloat(float? value)
        {
            return value.HasValue ? SetFloat(value.Value) : null;
        }
        #endregion
    }
}
