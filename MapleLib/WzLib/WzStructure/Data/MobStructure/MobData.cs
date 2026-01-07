using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;

namespace MapleLib.WzLib.WzStructure.Data.MobStructure
{
    /// <summary>
    /// Stores parsed mob data from WZ files.
    /// This is shared across all instances of the same mob ID.
    /// Based on mob data extraction structure.
    /// </summary>
    public class MobData
    {
        #region Basic Properties
        public int MobId { get; set; }
        public byte Level { get; set; }
        public byte Category { get; set; }
        public byte RareItemDropLevel { get; set; }
        public byte IgnoreFieldOut { get; set; }
        public byte OnlyNormalAttack { get; set; }
        #endregion

        #region Flags
        public bool IsBoss { get; set; }
        public bool FirstAttack { get; set; }
        public byte ExplosiveReward { get; set; }
        public byte PublicReward { get; set; }
        public byte Undead { get; set; }
        public bool Friendly { get; set; }
        public byte Escort { get; set; }
        public byte GetCP { get; set; }
        public bool PartyBonusMob { get; set; }
        public bool DualGauge { get; set; }
        public byte NoDoom { get; set; }
        public byte RemoveOnMiss { get; set; }
        public byte SummonType { get; set; }
        public byte BodyAttack { get; set; }
        public bool NoFlip { get; set; }
        #endregion

        #region Stats
        public short Eva { get; set; }
        public short Acc { get; set; }
        public int MaxHP { get; set; }
        public int MaxMP { get; set; }
        public int Exp { get; set; }
        public int Pushed { get; set; }
        public int PADamage { get; set; }
        public int MADamage { get; set; }
        public int RemoveAfter { get; set; }
        public int FixedDamage { get; set; } = -1;
        public int Buff { get; set; } = -1;
        public int PDDamage { get; set; }
        public int MDDamage { get; set; }
        public int CharismaEXP { get; set; }
        public int WillEXP { get; set; }
        public int PDRate { get; set; }
        public int MDRate { get; set; }
        public int HpRecovery { get; set; }
        public int MpRecovery { get; set; }
        public string ElemAttr { get; set; } = "";
        #endregion

        #region Movement
        public bool CanFly { get; set; }
        public bool IsMobile { get; set; }
        public bool CanJump { get; set; }
        public short Speed { get; set; }
        public short FlySpeed { get; set; }
        #endregion

        #region HP Display
        public short HpTagColor { get; set; }
        public short HpTagBgColor { get; set; }
        public MobHpDisplayType HpDisplayType { get; set; }
        #endregion

        #region Complex Data
        public List<int> ReviveData { get; set; } = new List<int>();
        public List<MobSkillData> SkillData { get; set; } = new List<MobSkillData>();
        public List<MobAttackData> AttackData { get; set; } = new List<MobAttackData>();
        public MobSelfDestructionData SelfDestruction { get; set; }
        public MobBanishData Banish { get; set; }
        #endregion

        #region Parsing
        /// <summary>
        /// Parse mob data from WZ image
        /// </summary>
        /// <param name="mobImage">The mob's WZ image</param>
        /// <param name="mobId">The mob ID</param>
        /// <returns>Parsed MobData</returns>
        public static MobData Parse(WzImage mobImage, int mobId)
        {
            if (mobImage == null)
                return null;

            var data = new MobData { MobId = mobId };

            WzSubProperty info = (WzSubProperty)mobImage["info"];
            if (info == null)
                return data;

            // Get linked mob image if exists
            WzImage linkMobImage = mobImage;
            int link = InfoTool.GetInt(info["link"], 0);
            if (link != 0)
            {
                // Try to get linked image from parent directory
                WzDirectory parentDir = mobImage.Parent as WzDirectory;
                if (parentDir != null)
                {
                    string linkImgName = string.Format("{0}{1}.img", link < 1000000 ? "0" : "", link);
                    WzImage linkedImage = parentDir.GetImageByName(linkImgName);
                    if (linkedImage != null)
                    {
                        if (!linkedImage.Parsed)
                            linkedImage.ParseImage();
                        linkMobImage = linkedImage;
                        // Update info to linked mob's info
                        var linkedInfo = (WzSubProperty)linkedImage["info"];
                        if (linkedInfo != null)
                            info = linkedInfo;
                    }
                }
            }

            // Parse basic properties
            data.Level = (byte)InfoTool.GetInt(info["level"], 0);
            data.Category = (byte)InfoTool.GetInt(info["category"], 0);
            data.RareItemDropLevel = (byte)InfoTool.GetInt(info["rareItemDropLevel"], 0);
            data.IgnoreFieldOut = (byte)InfoTool.GetInt(info["ignoreFieldOut"], 0);
            data.OnlyNormalAttack = (byte)InfoTool.GetInt(info["onlyNormalAttack"], 0);

            // Parse flags
            bool isBoss = InfoTool.GetInt(info["boss"], 0) > 0 ||
                          mobId == 8810018 || mobId == 8810118 || mobId == 9410066;
            data.IsBoss = isBoss;
            data.FirstAttack = InfoTool.GetInt(info["firstAttack"], 0) > 0 ||
                               mobId == 9300275 || mobId == 9300282;
            data.ExplosiveReward = (byte)InfoTool.GetInt(info["explosiveReward"], 0);
            data.PublicReward = (byte)InfoTool.GetInt(info["publicReward"], 0);
            data.Undead = (byte)InfoTool.GetInt(info["undead"], 0);
            data.Friendly = InfoTool.GetInt(info["damagedByMob"], 0) > 0;
            data.Escort = (byte)InfoTool.GetInt(info["escort"], 0);
            data.GetCP = (byte)InfoTool.GetInt(info["getCP"], 0);
            data.PartyBonusMob = InfoTool.GetInt(info["partyBonusMob"], 0) > 0;
            data.DualGauge = InfoTool.GetInt(info["dualGauge"], 0) > 0;
            data.NoDoom = (byte)InfoTool.GetInt(info["noDoom"], 0);
            data.RemoveOnMiss = (byte)InfoTool.GetInt(info["removeOnMiss"], 0);
            data.SummonType = (byte)InfoTool.GetInt(info["summonType"], 0);
            data.BodyAttack = (byte)InfoTool.GetInt(info["bodyAttack"], 0);
            data.NoFlip = InfoTool.GetInt(info["noFlip"], 0) == 1;

            // Parse stats
            data.Eva = (short)InfoTool.GetInt(info["eva"], 0);
            data.Acc = (short)InfoTool.GetInt(info["acc"], 0);
            data.MaxHP = InfoTool.GetInt(info["maxHP"], 0);
            data.MaxMP = InfoTool.GetInt(info["maxMP"], 0);
            data.Exp = InfoTool.GetInt(info["exp"], 0);
            data.Pushed = InfoTool.GetInt(info["pushed"], 0);
            data.PADamage = InfoTool.GetInt(info["PADamage"], 0);
            data.MADamage = InfoTool.GetInt(info["MADamage"], 0);
            data.RemoveAfter = InfoTool.GetInt(info["removeAfter"], 0);
            data.FixedDamage = InfoTool.GetInt(info["fixedDamage"], -1);
            data.Buff = InfoTool.GetInt(info["buff"], -1);
            data.PDDamage = InfoTool.GetInt(info["PDDamage"], 0);
            data.MDDamage = InfoTool.GetInt(info["MDDamage"], 0);
            data.CharismaEXP = InfoTool.GetInt(info["charismaEXP"], 0);
            data.WillEXP = InfoTool.GetInt(info["willEXP"], 0);
            data.PDRate = InfoTool.GetInt(info["PDRate"], 0);
            data.MDRate = InfoTool.GetInt(info["MDRate"], 0);
            data.HpRecovery = InfoTool.GetInt(info["hpRecovery"], 0);
            data.MpRecovery = InfoTool.GetInt(info["mpRecovery"], 0);

            var elemAttr = info["elemAttr"];
            data.ElemAttr = elemAttr != null ? InfoTool.GetString(elemAttr) : "";

            // Parse revive data
            WzSubProperty reviveData = (WzSubProperty)info["revive"];
            if (reviveData != null)
            {
                foreach (WzImageProperty mobid in reviveData.WzProperties)
                {
                    data.ReviveData.Add(InfoTool.GetInt(mobid));
                }
            }

            // Parse skill data
            WzSubProperty skillData = (WzSubProperty)info["skill"];
            if (skillData != null)
            {
                foreach (WzSubProperty skill in skillData.WzProperties)
                {
                    var mobSkill = new MobSkillData
                    {
                        SkillAfter = InfoTool.GetInt(skill["skillAfter"], 0),
                        EffectAfter = InfoTool.GetInt(skill["effectAfter"], 0),
                        Skill = InfoTool.GetInt(skill["skill"], 0),
                        Action = InfoTool.GetInt(skill["action"], 0),
                        Level = InfoTool.GetInt(skill["level"], 0),
                        PreSkillIndex = (byte)InfoTool.GetInt(skill["preSkillIndex"], 0),
                        PreSkillCount = (byte)InfoTool.GetInt(skill["preSkillCount"], 0),
                        OnlyFsm = InfoTool.GetInt(skill["onlyFsm"], 0) > 0,
                        SkillForbid = InfoTool.GetInt(skill["skillForbid"], 0)
                    };
                    data.SkillData.Add(mobSkill);
                }
            }

            // Parse self-destruction data
            WzSubProperty selfDData = (WzSubProperty)info["selfDestruction"];
            if (selfDData != null)
            {
                data.SelfDestruction = new MobSelfDestructionData
                {
                    Hp = InfoTool.GetInt(selfDData["hp"], 0),
                    Action = (short)InfoTool.GetInt(selfDData["action"], -1),
                    RemoveAfter = InfoTool.GetInt(selfDData["removeAfter"], -1)
                };
            }

            // Parse HP tag colors for bosses
            if (isBoss)
            {
                data.HpTagColor = (short)InfoTool.GetInt(info["hpTagColor"], 0);
                data.HpTagBgColor = (short)InfoTool.GetInt(info["hpTagBgcolor"], 0);
            }

            // Parse banish data
            WzSubProperty banishData = (WzSubProperty)info["ban"];
            if (banishData != null)
            {
                data.Banish = new MobBanishData
                {
                    BanType = (byte)InfoTool.GetInt(banishData["banType"], 0),
                    BanMsg = InfoTool.GetOptionalString(banishData["banMsg"]) ?? "",
                    BanMapField = InfoTool.GetInt(banishData.GetFromPath("banMap/0/field"), -1),
                    BanMapPortal = InfoTool.GetOptionalString(banishData.GetFromPath("banMap/0/portal")) ?? "sp"
                };
            }

            // Parse attack data
            WzSubProperty attackDataProp = (WzSubProperty)info["attack"];
            if (attackDataProp != null)
            {
                foreach (WzSubProperty attack in attackDataProp.WzProperties)
                {
                    if (!byte.TryParse(attack.Name, out byte attackNum))
                        continue;

                    var mobAttack = new MobAttackData
                    {
                        AttackNum = attackNum,
                        Action = (byte)InfoTool.GetInt(attack["action"], 0),
                        Magic = (byte)InfoTool.GetInt(attack["magic"], 0),
                        DeadlyAttack = (byte)InfoTool.GetInt(attack["deadlyAttack"], 0),
                        Knockback = (byte)InfoTool.GetInt(attack["knockback"], 0),
                        BulletSpeed = InfoTool.GetInt(attack["bulletSpeed"], 0),
                        MpBurn = InfoTool.GetInt(attack["mpBurn"], 0),
                        Disease = InfoTool.GetInt(attack["disease"], 0),
                        Level = InfoTool.GetInt(attack["level"], 0),
                        ConMP = InfoTool.GetInt(attack["conMP"], 0)
                    };
                    data.AttackData.Add(mobAttack);
                }
            }

            // Parse movement capabilities from linked mob image
            foreach (WzImageProperty imgdir in linkMobImage.WzProperties)
            {
                string imgDirName = imgdir.Name;
                if (imgDirName == "fly")
                {
                    data.CanFly = true;
                    data.IsMobile = true;
                }
                else if (imgDirName == "jump")
                {
                    data.CanJump = true;
                    data.IsMobile = true;  // Jumping mobs can also walk
                }
                else if (imgDirName == "move" || imgDirName == "walk")
                {
                    data.IsMobile = true;
                }
            }

            // Parse speed
            data.Speed = (short)InfoTool.GetInt(info["speed"], 0);
            data.FlySpeed = (short)InfoTool.GetInt(info["flySpeed"], 0);

            // Calculate HP display type
            if (data.DualGauge)
                data.HpDisplayType = MobHpDisplayType.DualGauge;
            else if (data.HpTagColor > 0)
                data.HpDisplayType = MobHpDisplayType.Boss;
            else if (data.Friendly)
                data.HpDisplayType = MobHpDisplayType.Friendly;
            else if (mobId >= 9300184 && mobId <= 9300215) // Mulung TC mobs
                data.HpDisplayType = MobHpDisplayType.MulungTC;
            else if (!isBoss || data.PartyBonusMob)
                data.HpDisplayType = MobHpDisplayType.Normal;
            else
                data.HpDisplayType = MobHpDisplayType.None;

            return data;
        }
        #endregion
    }
}
