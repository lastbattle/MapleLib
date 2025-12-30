using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapleLib.WzLib
{
	public class WzHeader
	{
        private const string DEFAULT_WZ_HEADER_COPYRIGHT = "Package file v1.0 Copyright 2002 Wizet, ZMS";

        private string ident;
        private string copyright;
        private ulong fsize;
        private uint fstart;

        public string Ident
        {
            get { return ident; }
            set { ident = value; }
        }

        /// <summary>
        /// see: DEFAULT_WZ_HEADER_COPYRIGHT
        /// </summary>
        public string Copyright
        {
            get { return copyright; }
            set { copyright = value; }
        }

        public ulong FSize
        {
            get { return fsize; }
            set { fsize = value; }
        }

		public uint FStart 
        {
            get { return fstart; }
            set { fstart = value; }
        }

        public void RecalculateFileStart()
        {
            fstart = (uint)(ident.Length + sizeof(ulong) + sizeof(uint) + copyright.Length + 1);
        }

		public static WzHeader GetDefault()
		{
            WzHeader header = new WzHeader
            {
                ident = "PKG1",
                copyright = DEFAULT_WZ_HEADER_COPYRIGHT,
                fstart = 60,
                fsize = 0
            };
            return header;
		}
	}
}