namespace MapleLib.ClientLib
{
    /// <summary>
    /// The localisation number for each regional MapleStory version.
    /// </summary>
    public enum MapleStoryLocalisation : int
    {
        MapleStoryKorea = 1, 
        MapleStoryKoreaTespia = 2,
        Unknown3 = 3,
        Unknown4 = 4,
        MapleStoryTespia = 5,
        Unknown6 = 6,
        MapleStorySEA = 7,
        MapleStoryGlobal = 8,
        MapleStoryEurope = 9,

        Not_Known = 999,

        // TODO: other values
    }
}

/* 2199 
 * Not sure, this might be a value for something else

enum NMLOCALEID
{
    kLocaleID_Null = 0x0,
    kLocaleID_KR = 0x1,
    kLocaleID_KR_Test = 0x10000001,
    kLocaleID_JP = 0x100,
    kLocaleID_CN = 0x101,
    kLocaleID_TW = 0x102,
    kLocaleID_TH = 0x103,
    kLocaleID_VN = 0x104,
    kLocaleID_SG = 0x105,
    kLocaleID_ID = 0x106,
    kLocaleID_CN2 = 0x107,
    kLocaleID_ID2 = 0x108,
    kLocaleID_US = 0x200,
    kLocaleID_EU = 0x300,
    kLocaleID_RU = 0x301,
    kLocaleID_BR = 0x400,
    kLocaleID_CN_CNC = 0x111,
    kLocaleID_CN_CT = 0x112,
};*/