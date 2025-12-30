using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapleLib.WzLib.WzProperties
{
    public class WzListEntry : IWzObject
    {
        public WzListEntry(string value)
        {
            this.value = value;
        }

        private string value;
        private WzListFile parentFile;

        public override IWzObject Parent
        {
            get
            {
                return parentFile;
            }
            internal set
            {
                parentFile = (WzListFile)value;
            }
        }

        public override string Name
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;
            }
        }

        public override object WzValue
        {
            get
            {
                return value;
            }
        }

        public override void Dispose()
        {
        }

        public override void Remove()
        {
            parentFile.WzListEntries.Remove(this);
        }

        public override WzObjectType ObjectType
        {
            get { return WzObjectType.List; }
        }

        public override IWzFile WzFileParent
        {
            get { return parentFile; }
        }
    }
}
