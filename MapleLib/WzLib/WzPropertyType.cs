namespace MapleLib.WzLib
{
	public enum WzPropertyType
	{
		#region Regular
		Null,
		Short,
		Int,
        Long,
		Float,
		Double,
		String,
		#endregion

		#region Extended
		SubProperty,
		Canvas,
		Vector,
		Convex,
		Sound,
		Raw,
		UOL,
		Lua,
		#endregion

		#region Png
		PNG
		#endregion
	}
}