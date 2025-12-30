namespace MapleLib.WzLib.WzStructure.Data.MobStructure
{
    /// <summary>
    /// Mob banish data structure
    /// </summary>
    public class MobBanishData
    {
        public byte BanType { get; set; }
        public string BanMsg { get; set; } = "";
        public int BanMapField { get; set; } = -1;
        public string BanMapPortal { get; set; } = "sp";
    }
}
