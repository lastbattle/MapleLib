namespace MapleLib.WzLib.WzStructure.Data.MobStructure
{
    /// <summary>
    /// Mob self-destruction data structure
    /// </summary>
    public class MobSelfDestructionData
    {
        public int Hp { get; set; }
        public short Action { get; set; } = -1;
        public int RemoveAfter { get; set; } = -1;
    }
}
