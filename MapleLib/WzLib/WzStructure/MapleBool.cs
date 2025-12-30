using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace MapleLib.WzLib.WzStructure
{
    [DataContract]
    public struct MapleBool //I know I could have used the nullable bool.
    {
        public const byte NotExist = 0;
        public const byte False = 1;
        public const byte True = 2;

        [DataMember]
        private byte val { get; set; }
        public static implicit operator MapleBool(byte value)
        {
            return new MapleBool
            {
                val = value
            };
        }

        public static implicit operator MapleBool(bool? value)
        {
            return new MapleBool
            {
                val = value == null ? MapleBool.NotExist : (bool)value ? MapleBool.True : MapleBool.False
            };
        }

        public static implicit operator bool(MapleBool value)
        {
            return value == MapleBool.True;
        }

        public static implicit operator byte(MapleBool value)
        {
            return value.val;
        }

        public override bool Equals(object obj)
        {
            return obj is MapleBool ? ((MapleBool)obj).val.Equals(val) : false;
        }

        public override int GetHashCode()
        {
            return val.GetHashCode();
        }

        public static bool operator ==(MapleBool a, MapleBool b)
        {
            return a.val.Equals(b.val);
        }

        public static bool operator ==(MapleBool a, bool b)
        {
            return (b && (a.val == MapleBool.True)) || (!b && (a.val == MapleBool.False));
        }

        public static bool operator !=(MapleBool a, MapleBool b)
        {
            return !a.val.Equals(b.val);
        }

        public static bool operator !=(MapleBool a, bool b)
        {
            return (b && (a.val != MapleBool.True)) || (!b && (a.val != MapleBool.False));
        }

        public bool HasValue
        {
            get
            {
                return val != NotExist;
            }
        }

        public bool Value
        {
            get
            {
                switch (val)
                {
                    case False:
                        return false;
                    case True:
                        return true;
                    case NotExist:
                    default:
                        throw new Exception("Tried to get value of nonexistant MapleBool");
                }
            }
        }
    }
}
