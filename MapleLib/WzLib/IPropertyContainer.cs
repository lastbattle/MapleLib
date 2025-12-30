using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapleLib.WzLib
{
	public interface IPropertyContainer
	{
		void AddProperty(WzImageProperty prop);
		void AddProperties(WzPropertyCollection props);

        void RemoveProperty(string propertyName);
        void RemoveProperty(WzImageProperty prop);

		void ClearProperties();

        WzPropertyCollection WzProperties { get; }
        WzImageProperty this[string name] { get; set; }
	}
}