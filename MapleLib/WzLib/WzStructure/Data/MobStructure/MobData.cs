using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;

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
        public bool DamagedByMob { get; set; }
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
        public int ChargeCount { get; set; }
        public bool HasAngerGauge { get; set; }
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
            data.DamagedByMob = InfoTool.GetInt(info["damagedByMob"], 0) > 0;
            data.Friendly = data.DamagedByMob;
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
            data.ChargeCount = InfoTool.GetInt(info["ChargeCount"], 0);
            data.HasAngerGauge = InfoTool.GetInt(info["AngerGauge"], 0) > 0;
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
                        SourceIndex = int.TryParse(skill.Name, out int sourceIndex) ? sourceIndex : data.SkillData.Count,
                        SkillAfter = InfoTool.GetInt(skill["skillAfter"], 0),
                        EffectAfter = InfoTool.GetInt(skill["effectAfter"], 0),
                        Skill = InfoTool.GetInt(skill["skill"], 0),
                        Action = InfoTool.GetInt(skill["action"], 0),
                        Level = InfoTool.GetInt(skill["level"], 0),
                        Priority = InfoTool.GetInt(skill["priority"], 0),
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
                        Type = InfoTool.GetInt(attack["type"], -1),
                        Action = (byte)InfoTool.GetInt(attack["action"], 0),
                        AttackCount = InfoTool.GetInt(attack["attackCount"], 0),
                        Magic = (byte)InfoTool.GetInt(attack["magic"], 0),
                        DeadlyAttack = (byte)InfoTool.GetInt(attack["deadlyAttack"], 0),
                        Knockback = (byte)InfoTool.GetInt(attack["knockback"], 0),
                        Rush = InfoTool.GetInt(attack["rush"], 0) > 0,
                        JumpAttack = InfoTool.GetInt(attack["jumpAttack"], 0) > 0,
                        Tremble = InfoTool.GetInt(attack["tremble"], 0) > 0,
                        BulletSpeed = InfoTool.GetInt(attack["bulletSpeed"], 0),
                        MpBurn = InfoTool.GetInt(attack["mpBurn"], 0),
                        Disease = InfoTool.GetInt(attack["disease"], 0),
                        Level = InfoTool.GetInt(attack["level"], 0),
                        ConMP = InfoTool.GetInt(attack["conMP"], 0),
                        HitEffectPath = GetMobAttackHitEffectPath(attack),
                        HasHitAttach = attack["bHitAttach"] != null
                                       || attack["attach"] != null
                                       || attack["hitAttach"] != null,
                        HitAttach = InfoTool.GetInt(
                                        attack["bHitAttach"]
                                        ?? attack["attach"]
                                        ?? attack["hitAttach"],
                                        0) > 0,
                        HasFacingAttach = attack["bFacingAttach"] != null
                                          || attack["bFacingAttatch"] != null
                                          || attack["attachfacing"] != null
                                          || attack["facingAttach"] != null,
                        FacingAttach = InfoTool.GetInt(
                                           attack["bFacingAttach"]
                                           ?? attack["bFacingAttatch"]
                                           ?? attack["attachfacing"]
                                           ?? attack["facingAttach"],
                                           0) > 0,
                        HasHitAfter = attack["hitAfter"] != null,
                        HitAfterMs = InfoTool.GetInt(attack["hitAfter"], 0)
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
            else if (data.DamagedByMob)
                data.HpDisplayType = MobHpDisplayType.Friendly;
            else if (mobId >= 9300184 && mobId <= 9300215) // Mulung TC mobs
                data.HpDisplayType = MobHpDisplayType.MulungTC;
            else if (!isBoss || data.PartyBonusMob)
                data.HpDisplayType = MobHpDisplayType.Normal;
            else
                data.HpDisplayType = MobHpDisplayType.None;

            return data;
        }

        private static string GetMobAttackHitEffectPath(WzSubProperty attack)
        {
            return GetMobAttackHitEffectPath(attack?["sHit"])
                   ?? GetMobAttackHitEffectPath(attack?["hit"]);
        }

        private static string GetMobAttackHitEffectPath(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            if (property is WzUOLProperty uolProperty)
            {
                return string.IsNullOrWhiteSpace(uolProperty.Value)
                    ? null
                    : uolProperty.Value;
            }

            if (property is WzStringProperty stringProperty)
            {
                string value = stringProperty.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            string sequencePath = GetMobAttackHitEffectSequencePath(property);
            if (!string.IsNullOrWhiteSpace(sequencePath))
            {
                return sequencePath;
            }

            if (property.WzProperties != null && property.WzProperties.Count > 0)
            {
                string[] preferredChildNames =
                {
                    "source",
                    "path",
                    "sHit",
                    "hit",
                    "effect",
                    "uol",
                    "value",
                    "0"
                };

                for (int i = 0; i < preferredChildNames.Length; i++)
                {
                    string value = GetMobAttackHitEffectPath(property[preferredChildNames[i]]);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                return null;
            }

            string fallback = property.GetString();
            return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
        }

        private static string GetMobAttackHitEffectSequencePath(WzImageProperty property)
        {
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return null;
            }

            var indexedPaths = new List<KeyValuePair<int, string>>();
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out int frameIndex))
                {
                    continue;
                }

                string childPath = GetMobAttackHitEffectPath(child);
                if (!string.IsNullOrWhiteSpace(childPath))
                {
                    indexedPaths.Add(new KeyValuePair<int, string>(frameIndex, childPath));
                }
            }

            if (indexedPaths.Count == 0)
            {
                return GetMobAttackHitEffectRecordSequencePath(property)
                       ?? GetMobAttackHitEffectNamedLeafSequencePath(property);
            }

            indexedPaths.Sort(static (left, right) => left.Key.CompareTo(right.Key));
            var paths = new List<string>(indexedPaths.Count);
            for (int i = 0; i < indexedPaths.Count; i++)
            {
                paths.Add(indexedPaths[i].Value);
            }

            AddMobAttackHitEffectMetadataTokens(property, paths);
            return string.Join("|", paths);
        }

        private static void AddMobAttackHitEffectMetadataTokens(WzImageProperty property, List<string> paths)
        {
            if (property == null || paths == null)
            {
                return;
            }

            InsertMobAttackHitEffectMetadataToken(property, paths, "hitAfter", "hitAfter");
            InsertMobAttackHitEffectMetadataToken(property, paths, "attach", "attach");
            InsertMobAttackHitEffectMetadataToken(property, paths, "bHitAttach", "attach");
            InsertMobAttackHitEffectMetadataToken(property, paths, "hitAttach", "attach");
            InsertMobAttackHitEffectMetadataToken(property, paths, "attachfacing", "attachfacing");
            InsertMobAttackHitEffectMetadataToken(property, paths, "bFacingAttach", "attachfacing");
            InsertMobAttackHitEffectMetadataToken(property, paths, "bFacingAttatch", "attachfacing");
            InsertMobAttackHitEffectMetadataToken(property, paths, "facingAttach", "attachfacing");
        }

        private static void InsertMobAttackHitEffectMetadataToken(
            WzImageProperty property,
            List<string> paths,
            string sourceName,
            string tokenName)
        {
            WzImageProperty metadataProperty = property?[sourceName];
            if (metadataProperty == null)
            {
                return;
            }

            string value = metadataProperty.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            paths.Insert(0, $"{tokenName}={value.Trim()}");
        }

        private static string GetMobAttackHitEffectNamedLeafSequencePath(WzImageProperty property)
        {
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return null;
            }

            var indexedPaths = new List<KeyValuePair<int, string>>();
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child == null
                    || child.WzProperties?.Count > 0 == true
                    || !TryParseMobAttackHitEffectNamedLeafFrameIndex(child.Name, out int frameIndex))
                {
                    continue;
                }

                string childPath = GetMobAttackHitEffectPath(child);
                if (!string.IsNullOrWhiteSpace(childPath))
                {
                    indexedPaths.Add(new KeyValuePair<int, string>(frameIndex, childPath));
                }
            }

            if (indexedPaths.Count == 0)
            {
                return null;
            }

            indexedPaths.Sort(static (left, right) => left.Key.CompareTo(right.Key));
            var paths = new List<string>(indexedPaths.Count);
            for (int i = 0; i < indexedPaths.Count; i++)
            {
                paths.Add(indexedPaths[i].Value);
            }

            return string.Join("|", paths);
        }

        private static bool TryParseMobAttackHitEffectNamedLeafFrameIndex(string name, out int frameIndex)
        {
            frameIndex = 0;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string normalizedName = name.Trim();
            string[] prefixes =
            {
                "source",
                "path",
                "sHit",
                "hit",
                "effect",
                "uol",
                "value",
                "target",
                "targetPath",
                "sourcePath",
                "srcPath"
            };

            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (!normalizedName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)
                    || normalizedName.Length <= prefix.Length)
                {
                    continue;
                }

                string suffix = normalizedName.Substring(prefix.Length).Trim();
                suffix = suffix.TrimStart('_', '-', '.', ':', '=', '[', '(', '{', '<').Trim();
                suffix = suffix.TrimEnd(']', ')', '}', '>').Trim();
                if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out frameIndex)
                    && frameIndex >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetMobAttackHitEffectRecordSequencePath(WzImageProperty property)
        {
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return null;
            }

            var indexedPaths = new List<KeyValuePair<int, string>>();
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child == null || child.WzProperties == null)
                {
                    continue;
                }

                if (!TryGetMobAttackHitEffectRecordFrameIndex(child, out int frameIndex))
                {
                    continue;
                }

                string childPath = GetMobAttackHitEffectRecordValuePath(child);
                if (!string.IsNullOrWhiteSpace(childPath))
                {
                    indexedPaths.Add(new KeyValuePair<int, string>(frameIndex, childPath));
                }
            }

            if (indexedPaths.Count == 0)
            {
                return null;
            }

            indexedPaths.Sort(static (left, right) => left.Key.CompareTo(right.Key));
            var paths = new List<string>(indexedPaths.Count);
            for (int i = 0; i < indexedPaths.Count; i++)
            {
                paths.Add(indexedPaths[i].Value);
            }

            return string.Join("|", paths);
        }

        private static bool TryGetMobAttackHitEffectRecordFrameIndex(WzImageProperty record, out int frameIndex)
        {
            frameIndex = 0;
            if (record == null)
            {
                return false;
            }

            if (TryParseMobAttackHitEffectRecordFrameIndex(record.Name, out frameIndex))
            {
                return true;
            }

            string[] frameFieldNames =
            {
                "frame",
                "frameIndex",
                "hitFrame",
                "sourceFrame",
                "index",
                "idx",
                "i",
                "nFrame",
                "nIndex",
                "key"
            };

            for (int i = 0; i < frameFieldNames.Length; i++)
            {
                string frameFieldValue = record[frameFieldNames[i]]?.GetString();
                if (TryParseMobAttackHitEffectRecordFrameIndex(frameFieldValue, out frameIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseMobAttackHitEffectRecordFrameIndex(string value, out int frameIndex)
        {
            frameIndex = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = value.Trim().Trim('"', '\'');
            if (int.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out frameIndex)
                && frameIndex >= 0)
            {
                return true;
            }

            int digitStart = -1;
            for (int i = 0; i < normalizedValue.Length; i++)
            {
                if (char.IsDigit(normalizedValue[i]))
                {
                    digitStart = digitStart < 0 ? i : digitStart;
                    continue;
                }

                if (TryParseMobAttackHitEffectRecordFrameIndexToken(
                        normalizedValue,
                        digitStart,
                        i - digitStart,
                        out frameIndex))
                {
                    return true;
                }

                digitStart = -1;
            }

            return TryParseMobAttackHitEffectRecordFrameIndexToken(
                normalizedValue,
                digitStart,
                normalizedValue.Length - digitStart,
                out frameIndex);
        }

        private static bool TryParseMobAttackHitEffectRecordFrameIndexToken(
            string value,
            int tokenStart,
            int tokenLength,
            out int frameIndex)
        {
            frameIndex = 0;
            if (string.IsNullOrWhiteSpace(value)
                || tokenStart < 0
                || tokenLength <= 0)
            {
                return false;
            }

            return int.TryParse(
                       value.Substring(tokenStart, tokenLength),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out frameIndex)
                   && frameIndex >= 0;
        }

        private static string GetMobAttackHitEffectRecordValuePath(WzImageProperty record)
        {
            if (record?.WzProperties == null)
            {
                return null;
            }

            string[] preferredChildNames =
            {
                "source",
                "path",
                "sHit",
                "hit",
                "effect",
                "uol",
                "value",
                "target",
                "targetPath",
                "sourcePath",
                "srcPath"
            };

            for (int i = 0; i < preferredChildNames.Length; i++)
            {
                string value = GetMobAttackHitEffectPath(record[preferredChildNames[i]]);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
        #endregion
    }
}
