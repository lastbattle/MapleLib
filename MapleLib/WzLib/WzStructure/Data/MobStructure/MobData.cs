using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
        public bool NotAttack { get; set; }
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
        public int SpeakWidth { get; set; }
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
        public List<int> DamagedBySelectedMob { get; set; } = new List<int>();
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
            data.NotAttack = InfoTool.GetInt(info["notAttack"], 0) > 0;
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
            data.SpeakWidth = InfoTool.GetInt(info["nWidth"], InfoTool.GetInt(info["width"], 0));

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

            WzSubProperty damagedBySelectedMob = (WzSubProperty)info["damagedBySelectedMob"];
            if (damagedBySelectedMob != null)
            {
                foreach (WzImageProperty mobid in damagedBySelectedMob.WzProperties)
                {
                    int selectedMobId = InfoTool.GetInt(mobid);
                    if (selectedMobId > 0)
                    {
                        data.DamagedBySelectedMob.Add(selectedMobId);
                    }
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
                string value = NormalizeMobAttackHitEffectEncodedPathSyntax(uolProperty.Value);
                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : value;
            }

            if (property is WzStringProperty stringProperty)
            {
                string rawValue = stringProperty.GetString();
                string value = NormalizeMobAttackHitEffectEncodedPathSyntax(rawValue);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                string structuredSequenceValue = GetMobAttackHitEffectStructuredRecordSequencePath(rawValue);
                if (!string.IsNullOrWhiteSpace(structuredSequenceValue))
                {
                    return structuredSequenceValue;
                }

                string structuredValue = GetMobAttackHitEffectStructuredRecordValuePath(rawValue);
                return string.IsNullOrWhiteSpace(structuredValue) ? value : structuredValue;
            }

            string sequencePath = GetMobAttackHitEffectSequencePath(property);
            if (!string.IsNullOrWhiteSpace(sequencePath))
            {
                return sequencePath;
            }

            if (property.WzProperties != null && property.WzProperties.Count > 0)
            {
                string headerValuePath = GetMobAttackHitEffectHeaderValueRecordPath(property);
                if (!string.IsNullOrWhiteSpace(headerValuePath))
                {
                    return headerValuePath;
                }

                string[] preferredChildNames =
                {
                    "source",
                    "path",
                    "sHit",
                    "hit",
                    "effect",
                    "uol",
                    "m_Data",
                    "mData",
                    "_bstr_t",
                    "bstr_t",
                    "Ztl_bstr_t",
                    "ZtlBstr",
                    "ZtlBstrT",
                    "Data_t",
                    "data_t",
                    "Data",
                    "value",
                    "data",
                    "payload",
                    "valueData",
                    "valuePayload",
                    "payloadValue",
                    "payloadData",
                    "targetValue",
                    "pathValue",
                    "uolData",
                    "uolValueData",
                    "uolPayload",
                    "sourceValue",
                    "sourcePayload",
                    "hitValue",
                    "hitPayload",
                    "recordValue",
                    "recordData",
                    "recordValues",
                    "recordPayload",
                    "recordText",
                    "rowValue",
                    "rowData",
                    "rowPayload",
                    "entryValue",
                    "entryData",
                    "entryPayload",
                    "itemValue",
                    "itemData",
                    "itemPayload",
                    "raw",
                    "rawValue",
                    "rawData",
                    "rawPayload",
                    "body",
                    "content",
                    "text",
                    "json",
                    "sourceData",
                    "targetData",
                    "targetPayload",
                    "pathData",
                    "pathText",
                    "pathPayload",
                    "uolValue",
                    "uolText",
                    "hitData",
                    "hitText",
                    "sHitData",
                    "clientValue",
                    "clientData",
                    "clientPayload",
                    "clientString",
                    "clientStringValue",
                    "clientStringData",
                    "clientStringPayload",
                    "stringValue",
                    "rawString",
                    "m_wstr",
                    "mWstr",
                    "m_str",
                    "mStr",
                    "bstr",
                    "bstrValue",
                    "bstrData",
                    "bstrPayload",
                    "wstr",
                    "wstrValue",
                    "wstrData",
                    "wstrPayload",
                    "assetValue",
                    "assetData",
                    "assetPayload",
                    "target",
                    "targetPath",
                    "sourcePath",
                    "sourceText",
                    "srcPath",
                    "hitPath",
                    "sHitPath",
                    "effectPath",
                    "assetPath",
                    "uolPath",
                    "targetUol",
                    "targetUOL",
                    "targetUolPath",
                    "targetUOLPath",
                    "sourceUol",
                    "sourceUOL",
                    "sourceUolPath",
                    "sourceUOLPath",
                    "clientPath",
                    "clientUol",
                    "clientUOL",
                    "clientUolPath",
                    "clientUOLPath",
                    "hitUol",
                    "hitUOL",
                    "hitUolPath",
                    "hitUOLPath",
                    "hitRoot",
                    "hitRootPath",
                    "hitRootUol",
                    "hitRootUOL",
                    "hitRootUolPath",
                    "hitRootUOLPath",
                    "sHitUol",
                    "sHitUOL",
                    "sHitUolPath",
                    "sHitUOLPath",
                    "sHitRoot",
                    "sHitRootPath",
                    "sHitRootUol",
                    "sHitRootUOL",
                    "sHitRootUolPath",
                    "sHitRootUOLPath",
                    "mobAttackInfoHit",
                    "mobAttackInfoHitPath",
                    "mobAttackInfoHitRoot",
                    "mobAttackInfoHitRootPath",
                    "mobAttackInfoHitRootUol",
                    "mobAttackInfoHitRootUOL",
                    "mobAttackInfoHitRootUolPath",
                    "mobAttackInfoHitRootUOLPath",
                    "mobAttackInfoSHit",
                    "mobAttackInfoSHitPath",
                    "mobAttackInfoSHitRoot",
                    "mobAttackInfoSHitRootPath",
                    "mobAttackInfoSHitRootUol",
                    "mobAttackInfoSHitRootUOL",
                    "mobAttackInfoSHitRootUolPath",
                    "mobAttackInfoSHitRootUOLPath",
                    "frames",
                    "frame",
                    "sources",
                    "sourceList",
                    "sequence",
                    "entries",
                    "entry",
                    "rows",
                    "row",
                    "records",
                    "record",
                    "values",
                    "items",
                    "sHitFrames",
                    "hitFrames",
                    "0"
                };

                for (int i = 0; i < preferredChildNames.Length; i++)
                {
                    string value = GetMobAttackHitEffectPath(property[preferredChildNames[i]]);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return AddMobAttackHitEffectMetadataTokensToPath(property, value);
                    }
                }

                string encodedNameValue = GetMobAttackHitEffectEncodedNameValuePath(property, preferredChildNames);
                if (!string.IsNullOrWhiteSpace(encodedNameValue))
                {
                    return encodedNameValue;
                }

                return null;
            }

            string fallback = NormalizeMobAttackHitEffectEncodedPathSyntax(property.GetString());
            return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
        }

        private static string GetMobAttackHitEffectSequencePath(WzImageProperty property)
        {
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return null;
            }

            var indexedPaths = new List<(int FrameIndex, int RowOrder, string Path)>();
            int rowOrder = 0;
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child == null || !TryParseMobAttackHitEffectRecordFrameIndex(child.Name, out int frameIndex))
                {
                    continue;
                }

                string childPath = GetMobAttackHitEffectPath(child);
                if (!string.IsNullOrWhiteSpace(childPath))
                {
                    indexedPaths.Add((frameIndex, rowOrder++, childPath));
                }
            }

            if (indexedPaths.Count == 0)
            {
                return GetMobAttackHitEffectRecordSequencePath(property)
                       ?? GetMobAttackHitEffectNamedLeafSequencePath(property)
                       ?? GetMobAttackHitEffectNumericIndexedNameValueSequencePath(property)
                       ?? GetMobAttackHitEffectEncodedNamedLeafSequencePath(property);
            }

            indexedPaths.Sort(static (left, right) =>
            {
                int frameComparison = left.FrameIndex.CompareTo(right.FrameIndex);
                return frameComparison != 0 ? frameComparison : left.RowOrder.CompareTo(right.RowOrder);
            });
            var paths = new List<string>(indexedPaths.Count);
            for (int i = 0; i < indexedPaths.Count; i++)
            {
                paths.Add(indexedPaths[i].Path);
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

        private static string AddMobAttackHitEffectMetadataTokensToPath(WzImageProperty property, string path)
        {
            if (property == null || string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            var paths = new List<string>(
                path.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            if (paths.Count == 0)
            {
                return path;
            }

            AddMobAttackHitEffectMetadataTokens(property, paths);
            return string.Join("|", paths);
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

            string value = NormalizeMobAttackHitEffectEncodedPathSyntax(GetMobAttackHitEffectScalarString(metadataProperty));
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

            AddMobAttackHitEffectMetadataTokens(property, paths);
            return string.Join("|", paths);
        }

        private static bool TryParseMobAttackHitEffectNamedLeafFrameIndex(string name, out int frameIndex)
        {
            frameIndex = 0;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string normalizedName = NormalizeMobAttackHitEffectEncodedPathSyntax(name).Trim();
            string[] prefixes =
            {
                "source",
                "path",
                "sHit",
                "hit",
                "effect",
                "uol",
                "m_Data",
                "mData",
                "_bstr_t",
                "bstr_t",
                "Ztl_bstr_t",
                "ZtlBstr",
                "ZtlBstrT",
                "Data_t",
                "data_t",
                "Data",
                "value",
                "data",
                "payload",
                "valueData",
                "valuePayload",
                "payloadValue",
                "payloadData",
                "targetValue",
                "pathValue",
                "uolData",
                "uolValueData",
                "uolPayload",
                "sourceValue",
                "sourcePayload",
                "hitValue",
                "hitPayload",
                "recordValue",
                "recordData",
                "recordValues",
                "recordPayload",
                "recordText",
                "rowValue",
                "rowData",
                "rowPayload",
                "entryValue",
                "entryData",
                "entryPayload",
                "itemValue",
                "itemData",
                "itemPayload",
                "raw",
                "rawValue",
                "rawData",
                "rawPayload",
                "body",
                "content",
                "text",
                "json",
                "sourceData",
                "targetData",
                "targetPayload",
                "pathData",
                "pathText",
                "pathPayload",
                "uolValue",
                "uolText",
                "hitData",
                "hitText",
                "sHitData",
                "clientValue",
                "clientData",
                "clientPayload",
                "clientString",
                "clientStringValue",
                "clientStringData",
                "clientStringPayload",
                "stringValue",
                "rawString",
                "m_wstr",
                "mWstr",
                "bstr",
                "bstrValue",
                "bstrData",
                "bstrPayload",
                "wstr",
                "wstrValue",
                "wstrData",
                "wstrPayload",
                "assetValue",
                "assetData",
                "assetPayload",
                "target",
                "targetPath",
                "sourcePath",
                "sourceText",
                "srcPath",
                "hitPath",
                "sHitPath",
                "effectPath",
                "assetPath",
                "uolPath",
                "targetUol",
                "targetUOL",
                "targetUolPath",
                "targetUOLPath",
                "sourceUol",
                "sourceUOL",
                "sourceUolPath",
                "sourceUOLPath",
                "clientPath",
                "clientUol",
                "clientUOL",
                "clientUolPath",
                "clientUOLPath",
                "clientString",
                "clientStringValue",
                "clientStringData",
                "clientStringPayload",
                "stringValue",
                "rawString",
                "m_wstr",
                "mWstr",
                "bstr",
                "bstrValue",
                "bstrData",
                "bstrPayload",
                "wstr",
                "wstrValue",
                "wstrData",
                "wstrPayload",
                "hitUol",
                "hitUOL",
                "hitUolPath",
                "hitUOLPath",
                "hitRoot",
                "hitRootPath",
                "hitRootUol",
                "hitRootUOL",
                "hitRootUolPath",
                "hitRootUOLPath",
                "sHitUol",
                "sHitUOL",
                "sHitUolPath",
                "sHitUOLPath",
                "sHitRoot",
                "sHitRootPath",
                "sHitRootUol",
                "sHitRootUOL",
                "sHitRootUolPath",
                "sHitRootUOLPath",
                "mobAttackInfoHit",
                "mobAttackInfoHitPath",
                "mobAttackInfoHitRoot",
                "mobAttackInfoHitRootPath",
                "mobAttackInfoHitRootUol",
                "mobAttackInfoHitRootUOL",
                "mobAttackInfoHitRootUolPath",
                "mobAttackInfoHitRootUOLPath",
                "mobAttackInfoSHit",
                "mobAttackInfoSHitPath",
                "mobAttackInfoSHitRoot",
                "mobAttackInfoSHitRootPath",
                "mobAttackInfoSHitRootUol",
                "mobAttackInfoSHitRootUOL",
                "mobAttackInfoSHitRootUolPath",
                "mobAttackInfoSHitRootUOLPath"
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

        private static string GetMobAttackHitEffectEncodedNamedLeafSequencePath(WzImageProperty property)
        {
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return null;
            }

            string[] allowedNames =
            {
                "source",
                "path",
                "sHit",
                "hit",
                "effect",
                "uol",
                "value",
                "data",
                "payload",
                "targetValue",
                "pathValue",
                "uolData",
                "sourceValue",
                "hitValue",
                "recordValue",
                "clientString",
                "clientStringValue",
                "clientStringData",
                "clientStringPayload",
                "stringValue",
                "rawString",
                "m_wstr",
                "mWstr",
                "m_str",
                "mStr",
                "bstr",
                "bstrValue",
                "bstrData",
                "bstrPayload",
                "wstr",
                "wstrValue",
                "wstrData",
                "wstrPayload",
                "target",
                "targetPath",
                "sourcePath",
                "srcPath",
                "hitPath",
                "sHitPath",
                "effectPath",
                "assetPath",
                "uolPath",
                "targetUol",
                "targetUOL",
                "targetUolPath",
                "targetUOLPath",
                "sourceUol",
                "sourceUOL",
                "sourceUolPath",
                "sourceUOLPath",
                "clientPath",
                "clientUol",
                "clientUOL",
                "clientUolPath",
                "clientUOLPath",
                "clientString",
                "clientStringValue",
                "clientStringData",
                "clientStringPayload",
                "stringValue",
                "rawString",
                "m_wstr",
                "mWstr",
                "bstr",
                "bstrValue",
                "bstrData",
                "bstrPayload",
                "wstr",
                "wstrValue",
                "wstrData",
                "wstrPayload",
                "hitUol",
                "hitUOL",
                "hitUolPath",
                "hitUOLPath",
                "hitRoot",
                "hitRootPath",
                "hitRootUol",
                "hitRootUOL",
                "hitRootUolPath",
                "hitRootUOLPath",
                "sHitUol",
                "sHitUOL",
                "sHitUolPath",
                "sHitUOLPath",
                "sHitRoot",
                "sHitRootPath",
                "sHitRootUol",
                "sHitRootUOL",
                "sHitRootUolPath",
                "sHitRootUOLPath",
                "mobAttackInfoHit",
                "mobAttackInfoHitPath",
                "mobAttackInfoHitRoot",
                "mobAttackInfoHitRootPath",
                "mobAttackInfoHitRootUol",
                "mobAttackInfoHitRootUOL",
                "mobAttackInfoHitRootUolPath",
                "mobAttackInfoHitRootUOLPath",
                "mobAttackInfoSHit",
                "mobAttackInfoSHitPath",
                "mobAttackInfoSHitRoot",
                "mobAttackInfoSHitRootPath",
                "mobAttackInfoSHitRootUol",
                "mobAttackInfoSHitRootUOL",
                "mobAttackInfoSHitRootUolPath",
                "mobAttackInfoSHitRootUOLPath"
            };

            var indexedPaths = new List<KeyValuePair<int, string>>();
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child == null
                    || child.WzProperties?.Count > 0 == true
                    || !TryReadMobAttackHitEffectEncodedNamedLeafValue(
                        child.Name,
                        allowedNames,
                        out int frameIndex,
                        out string value))
                {
                    continue;
                }

                indexedPaths.Add(new KeyValuePair<int, string>(frameIndex, value));
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

            AddMobAttackHitEffectMetadataTokens(property, paths);
            return string.Join("|", paths);
        }

        private static string GetMobAttackHitEffectNumericIndexedNameValueSequencePath(WzImageProperty property)
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
                    || !TryReadMobAttackHitEffectNumericIndexedNameValue(
                        child.Name,
                        out int frameIndex,
                        out string value))
                {
                    continue;
                }

                indexedPaths.Add(new KeyValuePair<int, string>(frameIndex, value));
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

            AddMobAttackHitEffectMetadataTokens(property, paths);
            return string.Join("|", paths);
        }

        private static bool TryReadMobAttackHitEffectEncodedNamedLeafValue(
            string name,
            string[] allowedNames,
            out int frameIndex,
            out string value)
        {
            frameIndex = 0;
            value = null;
            if (string.IsNullOrWhiteSpace(name) || allowedNames == null || allowedNames.Length == 0)
            {
                return false;
            }

            string normalizedName = NormalizeMobAttackHitEffectEncodedPathSyntax(name).Trim();
            string[] delimiters = { "=>", "->", "=", ":" };
            for (int i = 0; i < delimiters.Length; i++)
            {
                string delimiter = delimiters[i];
                int delimiterIndex = normalizedName.IndexOf(delimiter, StringComparison.Ordinal);
                if (delimiterIndex <= 0 || delimiterIndex + delimiter.Length >= normalizedName.Length)
                {
                    continue;
                }

                string fieldName = normalizedName.Substring(0, delimiterIndex).Trim();
                fieldName = fieldName.Trim('"', '\'', '`', '[', '(', '{', '<').Trim();
                fieldName = fieldName.TrimEnd(']', ')', '}', '>').Trim();
                if (!TryParseMobAttackHitEffectNamedLeafFrameIndex(fieldName, out frameIndex)
                    || !IsMobAttackHitEffectAllowedIndexedFieldName(fieldName, allowedNames))
                {
                    continue;
                }

                value = NormalizeMobAttackHitEffectEncodedPathSyntax(
                        normalizedName.Substring(delimiterIndex + delimiter.Length))
                    .Trim()
                    .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }

        private static bool IsMobAttackHitEffectAllowedIndexedFieldName(string fieldName, string[] allowedNames)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            string normalizedFieldName = NormalizeMobAttackHitEffectEncodedPathSyntax(fieldName).Trim();
            for (int i = 0; i < allowedNames.Length; i++)
            {
                string allowedName = allowedNames[i];
                if (!normalizedFieldName.StartsWith(allowedName, StringComparison.OrdinalIgnoreCase)
                    || normalizedFieldName.Length <= allowedName.Length)
                {
                    continue;
                }

                char delimiter = normalizedFieldName[allowedName.Length];
                if (delimiter == '_'
                    || delimiter == '-'
                    || delimiter == '.'
                    || delimiter == ':'
                    || delimiter == '='
                    || delimiter == '['
                    || delimiter == '('
                    || delimiter == '{'
                    || delimiter == '<')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadMobAttackHitEffectNumericIndexedNameValue(
            string name,
            out int frameIndex,
            out string value)
        {
            frameIndex = 0;
            value = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string normalizedName = NormalizeMobAttackHitEffectEncodedPathSyntax(name).Trim();
            string[] delimiters = { "=>", "->", "=", ":" };
            for (int i = 0; i < delimiters.Length; i++)
            {
                string delimiter = delimiters[i];
                int delimiterIndex = normalizedName.IndexOf(delimiter, StringComparison.Ordinal);
                if (delimiterIndex <= 0 || delimiterIndex + delimiter.Length >= normalizedName.Length)
                {
                    continue;
                }

                string frameToken = normalizedName.Substring(0, delimiterIndex)
                    .Trim()
                    .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
                if (!TryParseMobAttackHitEffectPlainNumericFrameToken(frameToken, out frameIndex))
                {
                    continue;
                }

                value = NormalizeMobAttackHitEffectEncodedPathSyntax(
                        normalizedName.Substring(delimiterIndex + delimiter.Length))
                    .Trim()
                    .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }

        private static string GetMobAttackHitEffectRecordSequencePath(WzImageProperty property)
        {
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return null;
            }

            var indexedPaths = new List<(int FrameIndex, int RowOrder, string Path)>();
            int rowOrder = 0;
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

                List<string> childPaths = GetMobAttackHitEffectRecordValuePaths(child);
                if (childPaths.Count > 0)
                {
                    for (int i = 0; i < childPaths.Count; i++)
                    {
                        indexedPaths.Add((frameIndex, rowOrder++, childPaths[i]));
                    }
                }
            }

            if (indexedPaths.Count == 0)
            {
                return null;
            }

            indexedPaths.Sort(static (left, right) =>
            {
                int frameComparison = left.FrameIndex.CompareTo(right.FrameIndex);
                return frameComparison != 0 ? frameComparison : left.RowOrder.CompareTo(right.RowOrder);
            });
            var paths = new List<string>(indexedPaths.Count);
            for (int i = 0; i < indexedPaths.Count; i++)
            {
                paths.Add(indexedPaths[i].Path);
            }

            AddMobAttackHitEffectMetadataTokens(property, paths);
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

            if (record.WzProperties != null)
            {
                foreach (WzImageProperty child in record.WzProperties)
                {
                    if (TryReadMobAttackHitEffectEncodedNamedValue(
                            child?.Name,
                            frameFieldNames,
                            out string encodedFrameValue)
                        && TryParseMobAttackHitEffectRecordFrameIndex(encodedFrameValue, out frameIndex))
                    {
                        return true;
                    }
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

            string normalizedValue = NormalizeMobAttackHitEffectEncodedPathSyntax(value).Trim().Trim('"', '\'');
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

        private static List<string> GetMobAttackHitEffectRecordValuePaths(WzImageProperty record)
        {
            var paths = new List<string>();
            if (record?.WzProperties == null)
            {
                return paths;
            }

            string headerValuePath = GetMobAttackHitEffectHeaderValueRecordPath(record);
            if (!string.IsNullOrWhiteSpace(headerValuePath))
            {
                paths.Add(headerValuePath);
                AddMobAttackHitEffectMetadataTokens(record, paths);
                return paths;
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
                "data",
                "payload",
                "targetValue",
                "pathValue",
                "uolData",
                "sourceValue",
                "hitValue",
                "recordValue",
                "target",
                "targetPath",
                "sourcePath",
                "srcPath",
                "hitPath",
                "sHitPath",
                "effectPath",
                "assetPath",
                "uolPath",
                "targetUol",
                "targetUOL",
                "targetUolPath",
                "targetUOLPath",
                "sourceUol",
                "sourceUOL",
                "sourceUolPath",
                "sourceUOLPath",
                "clientPath",
                "clientUol",
                "clientUOL",
                "clientUolPath",
                "clientUOLPath",
                "hitUol",
                "hitUOL",
                "hitUolPath",
                "hitUOLPath",
                "hitRoot",
                "hitRootPath",
                "hitRootUol",
                "hitRootUOL",
                "hitRootUolPath",
                "hitRootUOLPath",
                "sHitUol",
                "sHitUOL",
                "sHitUolPath",
                "sHitUOLPath",
                "sHitRoot",
                "sHitRootPath",
                "sHitRootUol",
                "sHitRootUOL",
                "sHitRootUolPath",
                "sHitRootUOLPath",
                "mobAttackInfoHit",
                "mobAttackInfoHitPath",
                "mobAttackInfoHitRoot",
                "mobAttackInfoHitRootPath",
                "mobAttackInfoHitRootUol",
                "mobAttackInfoHitRootUOL",
                "mobAttackInfoHitRootUolPath",
                "mobAttackInfoHitRootUOLPath",
                "mobAttackInfoSHit",
                "mobAttackInfoSHitPath",
                "mobAttackInfoSHitRoot",
                "mobAttackInfoSHitRootPath",
                "mobAttackInfoSHitRootUol",
                "mobAttackInfoSHitRootUOL",
                "mobAttackInfoSHitRootUolPath",
                "mobAttackInfoSHitRootUOLPath"
            };

            for (int i = 0; i < preferredChildNames.Length; i++)
            {
                string value = GetMobAttackHitEffectPath(record[preferredChildNames[i]]);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    paths.Add(value);
                    break;
                }
            }

            if (paths.Count == 0)
            {
                string numericTupleValue = GetMobAttackHitEffectNumericTupleRecordValuePath(record);
                if (!string.IsNullOrWhiteSpace(numericTupleValue))
                {
                    paths.Add(numericTupleValue);
                }
            }

            if (paths.Count == 0)
            {
                string encodedNameValue = GetMobAttackHitEffectEncodedNameValuePath(record, preferredChildNames);
                if (!string.IsNullOrWhiteSpace(encodedNameValue))
                {
                    paths.Add(encodedNameValue);
                }
            }

            if (paths.Count > 0)
            {
                AddMobAttackHitEffectMetadataTokens(record, paths);
            }

            return paths;
        }

        private static string GetMobAttackHitEffectHeaderValueRecordPath(WzImageProperty record)
        {
            if (record?.WzProperties == null || record.WzProperties.Count == 0)
            {
                return null;
            }

            var paths = new List<string>();
            foreach (string recordText in EnumerateMobAttackHitEffectHeaderValueRecordTexts(record))
            {
                string sequencePath = GetMobAttackHitEffectStructuredRecordSequencePath(recordText);
                if (!string.IsNullOrWhiteSpace(sequencePath))
                {
                    paths.Add(sequencePath);
                    continue;
                }

                string valuePath = GetMobAttackHitEffectStructuredRecordValuePath(recordText);
                if (!string.IsNullOrWhiteSpace(valuePath))
                {
                    paths.Add(valuePath);
                }
            }

            if (!record.WzProperties.Any(static child => IsMobAttackHitEffectHeaderListFieldName(child?.Name)))
            {
                foreach (string recordText in EnumerateMobAttackHitEffectStructuredRecordTextRows(record))
                {
                    string sequencePath = GetMobAttackHitEffectStructuredRecordSequencePath(recordText);
                    if (!string.IsNullOrWhiteSpace(sequencePath))
                    {
                        paths.Add(sequencePath);
                        continue;
                    }

                    string valuePath = GetMobAttackHitEffectStructuredRecordValuePath(recordText);
                    if (!string.IsNullOrWhiteSpace(valuePath))
                    {
                        paths.Add(valuePath);
                    }
                }
            }

            return paths.Count == 0 ? null : string.Join("|", paths);
        }

        private static IEnumerable<string> EnumerateMobAttackHitEffectHeaderValueRecordTexts(WzImageProperty record)
        {
            foreach ((IReadOnlyList<string> Headers, IReadOnlyList<string> Values) in EnumerateMobAttackHitEffectHeaderValueRecordRows(record)
                         .Concat(EnumerateMobAttackHitEffectColumnValueRecordRows(record)))
            {
                int fieldCount = Math.Min(Headers.Count, Values.Count);
                if (fieldCount <= 0)
                {
                    continue;
                }

                var fields = new List<string>(fieldCount);
                for (int i = 0; i < fieldCount; i++)
                {
                    string header = Headers[i];
                    string value = Values[i];
                    if (string.IsNullOrWhiteSpace(header) || string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    fields.Add($"{header}={value}");
                }

                if (fields.Count > 0)
                {
                    yield return string.Join(";", fields);
                }
            }
        }

        private static IEnumerable<string> EnumerateMobAttackHitEffectStructuredRecordTextRows(WzImageProperty record)
        {
            if (record?.WzProperties == null)
            {
                yield break;
            }

            foreach (WzImageProperty child in record.WzProperties)
            {
                if (!IsMobAttackHitEffectValueListFieldName(child?.Name))
                {
                    continue;
                }

                foreach (string recordText in EnumerateMobAttackHitEffectStructuredRecordTextRowsFromValueNode(child))
                {
                    if (!string.IsNullOrWhiteSpace(recordText))
                    {
                        yield return recordText;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateMobAttackHitEffectStructuredRecordTextRowsFromValueNode(WzImageProperty node)
        {
            if (node == null)
            {
                yield break;
            }

            string scalarText = GetMobAttackHitEffectScalarString(node);
            if (!string.IsNullOrWhiteSpace(scalarText))
            {
                string normalizedText = NormalizeMobAttackHitEffectEncodedPathSyntaxForRecordSplitting(scalarText).Trim();
                bool yieldedBracketedRows = false;
                if (normalizedText.Length >= 4 && normalizedText[0] == '[' && normalizedText[^1] == ']')
                {
                    foreach (string nestedRowText in EnumerateMobAttackHitEffectTopLevelBracketGroups(normalizedText))
                    {
                        if (!string.IsNullOrWhiteSpace(nestedRowText))
                        {
                            yieldedBracketedRows = true;
                            yield return nestedRowText;
                        }
                    }
                }

                if (yieldedBracketedRows)
                {
                    yield break;
                }

                if (normalizedText.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                {
                    foreach (string line in normalizedText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string trimmedLine = TrimMobAttackHitEffectRecordTextFieldToken(line);
                        if (!string.IsNullOrWhiteSpace(trimmedLine))
                        {
                            yield return trimmedLine;
                        }
                    }

                    yield break;
                }

                yield return normalizedText;
                yield break;
            }

            if (node.WzProperties == null || node.WzProperties.Count == 0)
            {
                yield break;
            }

            var indexedChildren = new SortedDictionary<int, WzImageProperty>();
            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child == null
                    || string.IsNullOrWhiteSpace(child.Name)
                    || !int.TryParse(
                        NormalizeMobAttackHitEffectStructuredFieldName(child.Name),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int index)
                    || index < 0)
                {
                    continue;
                }

                indexedChildren[index] = child;
            }

            IEnumerable<WzImageProperty> rowNodes = indexedChildren.Count > 0
                ? indexedChildren.Values
                : node.WzProperties;
            foreach (WzImageProperty rowNode in rowNodes)
            {
                if (rowNode?.WzProperties?.Count > 0 == true)
                {
                    bool yieldedNestedHeaderValueRow = false;
                    foreach (string nestedRecordText in EnumerateMobAttackHitEffectHeaderValueRecordTexts(rowNode))
                    {
                        if (!string.IsNullOrWhiteSpace(nestedRecordText))
                        {
                            yieldedNestedHeaderValueRow = true;
                            yield return nestedRecordText;
                        }
                    }

                    if (yieldedNestedHeaderValueRow)
                    {
                        continue;
                    }

                    string namedRowText = BuildMobAttackHitEffectStructuredRecordTextFromNamedFields(rowNode);
                    if (!string.IsNullOrWhiteSpace(namedRowText))
                    {
                        yield return namedRowText;
                    }

                    continue;
                }

                string rowText = TrimMobAttackHitEffectRecordTextFieldTokenForRecordSplitting(GetMobAttackHitEffectScalarString(rowNode));
                if (!string.IsNullOrWhiteSpace(rowText))
                {
                    yield return rowText;
                }
            }
        }

        private static string BuildMobAttackHitEffectStructuredRecordTextFromNamedFields(WzImageProperty rowNode)
        {
            if (rowNode?.WzProperties == null || rowNode.WzProperties.Count == 0)
            {
                return null;
            }

            var fields = new List<string>();
            foreach (WzImageProperty fieldNode in rowNode.WzProperties)
            {
                string fieldName = fieldNode?.Name;
                if (!IsMobAttackHitEffectStructuredKnownFieldName(fieldName))
                {
                    continue;
                }

                string fieldValue = TrimMobAttackHitEffectRecordTextFieldToken(GetMobAttackHitEffectScalarString(fieldNode));
                if (string.IsNullOrWhiteSpace(fieldValue))
                {
                    continue;
                }

                fields.Add($"{fieldName}={fieldValue}");
            }

            return fields.Any(field => IsMobAttackHitEffectStructuredPathFieldName(field.Split('=')[0]))
                ? string.Join(";", fields)
                : null;
        }

        private static IEnumerable<(IReadOnlyList<string> Headers, IReadOnlyList<string> Values)> EnumerateMobAttackHitEffectColumnValueRecordRows(
            WzImageProperty record)
        {
            if (record?.WzProperties == null)
            {
                yield break;
            }

            var pathColumns = new List<(string Header, IReadOnlyList<string> Values)>();
            var frameColumns = new List<(string Header, IReadOnlyList<string> Values)>();
            var metadataColumns = new List<(string Header, IReadOnlyList<string> Values)>();
            foreach (WzImageProperty child in record.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                string header = child.Name;
                if (IsMobAttackHitEffectHeaderListFieldName(header) || IsMobAttackHitEffectValueListFieldName(header))
                {
                    continue;
                }

                IReadOnlyList<string> values = ReadMobAttackHitEffectColumnValues(child);
                if (values.Count == 0)
                {
                    continue;
                }

                if (IsMobAttackHitEffectStructuredPathFieldName(header))
                {
                    pathColumns.Add((header, values));
                }
                else if (IsMobAttackHitEffectStructuredFrameFieldName(header))
                {
                    frameColumns.Add((header, values));
                }
                else if (IsMobAttackHitEffectStructuredHitAfterFieldName(header)
                         || IsMobAttackHitEffectStructuredHitAttachFieldName(header)
                         || IsMobAttackHitEffectStructuredFacingAttachFieldName(header))
                {
                    metadataColumns.Add((header, values));
                }
            }

            if (pathColumns.Count == 0)
            {
                yield break;
            }

            int maxRowCount = pathColumns
                .Concat(frameColumns)
                .Concat(metadataColumns)
                .Select(static column => column.Values.Count)
                .DefaultIfEmpty(0)
                .Max();
            for (int rowIndex = 0; rowIndex < maxRowCount; rowIndex++)
            {
                var headers = new List<string>();
                var values = new List<string>();
                foreach ((string Header, IReadOnlyList<string> Values) column in frameColumns
                             .Concat(metadataColumns)
                             .Concat(pathColumns))
                {
                    string value = rowIndex < column.Values.Count
                        ? column.Values[rowIndex]
                        : column.Values.Count == 1
                            ? column.Values[0]
                            : null;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    headers.Add(column.Header);
                    values.Add(value);
                }

                if (headers.Any(IsMobAttackHitEffectStructuredPathFieldName))
                {
                    yield return (headers, values);
                }
            }
        }

        private static IReadOnlyList<string> ReadMobAttackHitEffectColumnValues(WzImageProperty node)
        {
            if (node == null)
            {
                return Array.Empty<string>();
            }

            IReadOnlyList<string> indexedValues = ReadMobAttackHitEffectDelimitedOrIndexedList(node, preferredDelimiter: '\0');
            string text = GetMobAttackHitEffectScalarString(node);
            if (string.IsNullOrWhiteSpace(text) || text.IndexOfAny(new[] { '\r', '\n' }) < 0)
            {
                return indexedValues;
            }

            string[] lineValues = text
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(TrimMobAttackHitEffectRecordTextFieldToken)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            return lineValues.Length > 1 ? lineValues : indexedValues;
        }

        private static IEnumerable<(IReadOnlyList<string> Headers, IReadOnlyList<string> Values)> EnumerateMobAttackHitEffectHeaderValueRecordRows(
            WzImageProperty record)
        {
            if (record?.WzProperties == null)
            {
                yield break;
            }

            foreach (WzImageProperty headerNode in record.WzProperties)
            {
                if (!IsMobAttackHitEffectHeaderListFieldName(headerNode?.Name))
                {
                    continue;
                }

                IReadOnlyList<string> headers = ReadMobAttackHitEffectDelimitedOrIndexedList(headerNode, preferredDelimiter: '\0');
                if (headers.Count == 0 || !ContainsMobAttackHitEffectStructuredPathHeader(headers))
                {
                    continue;
                }

                char preferredDelimiter = DetectMobAttackHitEffectListDelimiter(
                    GetMobAttackHitEffectScalarString(headerNode));
                foreach (WzImageProperty valueNode in record.WzProperties)
                {
                    if (!IsMobAttackHitEffectValueListFieldName(valueNode?.Name))
                    {
                        continue;
                    }

                    foreach (IReadOnlyList<string> values in EnumerateMobAttackHitEffectValueRows(valueNode, preferredDelimiter, headers))
                    {
                        if (values.Count > 0)
                        {
                            yield return (headers, values);
                        }
                    }
                }
            }
        }

        private static bool ContainsMobAttackHitEffectStructuredPathHeader(IReadOnlyList<string> headers)
        {
            if (headers == null)
            {
                return false;
            }

            for (int i = 0; i < headers.Count; i++)
            {
                if (IsMobAttackHitEffectStructuredPathFieldName(headers[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<IReadOnlyList<string>> EnumerateMobAttackHitEffectValueRows(
            WzImageProperty node,
            char preferredDelimiter,
            IReadOnlyList<string> headers = null)
        {
            if (node == null)
            {
                yield break;
            }

            bool yieldedBracketedRow = false;
            foreach (IReadOnlyList<string> row in EnumerateMobAttackHitEffectBracketedValueRows(
                         GetMobAttackHitEffectScalarString(node),
                         preferredDelimiter))
            {
                if (row.Count > 0)
                {
                    yieldedBracketedRow = true;
                    yield return row;
                }
            }

            if (yieldedBracketedRow)
            {
                yield break;
            }

            if (node.WzProperties != null && node.WzProperties.Count > 0)
            {
                var indexedChildren = new SortedDictionary<int, WzImageProperty>();
                foreach (WzImageProperty child in node.WzProperties)
                {
                    if (child == null
                        || string.IsNullOrWhiteSpace(child.Name)
                        || !int.TryParse(
                            NormalizeMobAttackHitEffectStructuredFieldName(child.Name),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int index)
                        || index < 0)
                    {
                        continue;
                    }

                    indexedChildren[index] = child;
                }

                if (indexedChildren.Count > 0)
                {
                    bool yieldedNestedRow = false;
                    foreach (WzImageProperty child in indexedChildren.Values)
                    {
                        if (child?.WzProperties?.Count > 0)
                        {
                            IReadOnlyList<string> row = ReadMobAttackHitEffectNamedFieldRow(child, headers);
                            if (row.Count == 0)
                            {
                                row = ReadMobAttackHitEffectDelimitedOrIndexedList(child, preferredDelimiter);
                            }

                            if (row.Count > 0)
                            {
                                yieldedNestedRow = true;
                                yield return row;
                            }
                        }
                    }

                    if (yieldedNestedRow)
                    {
                        yield break;
                    }

                    var indexedValues = new List<string>(indexedChildren.Count);
                    bool hasDelimitedRowText = false;
                    foreach (WzImageProperty child in indexedChildren.Values)
                    {
                        string value = TrimMobAttackHitEffectRecordTextFieldTokenForRecordSplitting(
                            GetMobAttackHitEffectScalarString(child));
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            continue;
                        }

                        indexedValues.Add(value);
                        hasDelimitedRowText |= DetectMobAttackHitEffectListDelimiter(value) != '\0';
                    }

                    if (indexedValues.Count > 0)
                    {
                        if (!hasDelimitedRowText)
                        {
                            yield return indexedValues;
                            yield break;
                        }

                        for (int i = 0; i < indexedValues.Count; i++)
                        {
                            IReadOnlyList<string> row = SplitMobAttackHitEffectDelimitedList(
                                indexedValues[i],
                                preferredDelimiter);
                            if (row.Count > 0)
                            {
                                yield return row;
                            }
                        }

                        yield break;
                    }
                }

                foreach (WzImageProperty child in node.WzProperties)
                {
                    if (child?.WzProperties?.Count > 0 != true
                        || string.IsNullOrWhiteSpace(child.Name)
                        || int.TryParse(
                            NormalizeMobAttackHitEffectStructuredFieldName(child.Name),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out _))
                    {
                        continue;
                    }

                    IReadOnlyList<string> row = ReadMobAttackHitEffectNamedFieldRow(child, headers);
                    if (row.Count > 0)
                    {
                        yield return row;
                    }
                }
            }

            IReadOnlyList<string> values = ReadMobAttackHitEffectDelimitedOrIndexedList(node, preferredDelimiter);
            if (values.Count > 0)
            {
                yield return values;
            }
        }

        private static IReadOnlyList<string> ReadMobAttackHitEffectNamedFieldRow(
            WzImageProperty node,
            IReadOnlyList<string> headers)
        {
            if (node?.WzProperties == null || node.WzProperties.Count == 0 || headers == null || headers.Count == 0)
            {
                return Array.Empty<string>();
            }

            var values = new List<string>(headers.Count);
            for (int i = 0; i < headers.Count; i++)
            {
                WzImageProperty fieldNode = FindMobAttackHitEffectNamedFieldChild(node, headers[i]);
                string value = TrimMobAttackHitEffectRecordTextFieldToken(
                    GetMobAttackHitEffectScalarString(fieldNode));
                values.Add(value ?? string.Empty);
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values;
                }
            }

            return Array.Empty<string>();
        }

        private static WzImageProperty FindMobAttackHitEffectNamedFieldChild(WzImageProperty node, string fieldName)
        {
            if (node?.WzProperties == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return null;
            }

            string normalizedFieldName = NormalizeMobAttackHitEffectStructuredFieldName(fieldName);
            foreach (WzImageProperty child in node.WzProperties)
            {
                if (string.Equals(
                        NormalizeMobAttackHitEffectStructuredFieldName(child?.Name),
                        normalizedFieldName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        private static IReadOnlyList<string> ReadMobAttackHitEffectDelimitedOrIndexedList(
            WzImageProperty node,
            char preferredDelimiter)
        {
            if (node == null)
            {
                return Array.Empty<string>();
            }

            if (node.WzProperties != null && node.WzProperties.Count > 0)
            {
                var indexedValues = new SortedDictionary<int, string>();
                foreach (WzImageProperty child in node.WzProperties)
                {
                    if (child == null
                        || string.IsNullOrWhiteSpace(child.Name)
                        || !int.TryParse(
                            NormalizeMobAttackHitEffectStructuredFieldName(child.Name),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int index)
                        || index < 0)
                    {
                        continue;
                    }

                    string value = TrimMobAttackHitEffectRecordTextFieldToken(
                        GetMobAttackHitEffectScalarString(child));
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        indexedValues[index] = value;
                    }
                }

                if (indexedValues.Count > 0)
                {
                    return new List<string>(indexedValues.Values);
                }
            }

            string text = GetMobAttackHitEffectScalarString(node);
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            return SplitMobAttackHitEffectDelimitedList(text, preferredDelimiter);
        }

        private static IReadOnlyList<string> SplitMobAttackHitEffectDelimitedList(
            string text,
            char preferredDelimiter)
        {
            string normalizedText = NormalizeMobAttackHitEffectEncodedPathSyntaxForRecordSplitting(text).Trim();
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return Array.Empty<string>();
            }

            char delimiter = preferredDelimiter != '\0'
                ? preferredDelimiter
                : DetectMobAttackHitEffectListDelimiter(normalizedText);
            if (delimiter == '\0')
            {
                string value = TrimMobAttackHitEffectRecordTextFieldToken(normalizedText);
                return string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value };
            }

            var values = new List<string>();
            foreach (string segment in SplitMobAttackHitEffectDelimitedListSegments(normalizedText, delimiter))
            {
                string value = TrimMobAttackHitEffectRecordTextFieldToken(segment);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }

            return values;
        }

        private static IEnumerable<IReadOnlyList<string>> EnumerateMobAttackHitEffectBracketedValueRows(
            string text,
            char preferredDelimiter)
        {
            string normalizedText = NormalizeMobAttackHitEffectEncodedPathSyntax(text).Trim();
            if (string.IsNullOrWhiteSpace(normalizedText)
                || normalizedText.Length < 4
                || normalizedText[0] != '['
                || normalizedText[normalizedText.Length - 1] != ']')
            {
                yield break;
            }

            bool yieldedNestedRow = false;
            foreach (string nestedRowText in EnumerateMobAttackHitEffectTopLevelBracketGroups(normalizedText))
            {
                IReadOnlyList<string> row = SplitMobAttackHitEffectDelimitedList(nestedRowText, preferredDelimiter);
                if (row.Count > 0)
                {
                    yieldedNestedRow = true;
                    yield return row;
                }
            }

            if (yieldedNestedRow)
            {
                yield break;
            }

            IReadOnlyList<string> scalarRow = SplitMobAttackHitEffectDelimitedList(
                normalizedText.Trim('[', ']'),
                preferredDelimiter);
            if (scalarRow.Count > 0)
            {
                yield return scalarRow;
            }
        }

        private static IEnumerable<string> EnumerateMobAttackHitEffectTopLevelBracketGroups(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            bool inQuote = false;
            char quoteChar = '\0';
            int depth = 0;
            int groupStart = -1;
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if ((current == '"' || current == '\'') && !IsMobAttackHitEffectEscapedQuote(text, i))
                {
                    if (!inQuote)
                    {
                        inQuote = true;
                        quoteChar = current;
                    }
                    else if (quoteChar == current)
                    {
                        inQuote = false;
                        quoteChar = '\0';
                    }

                    continue;
                }

                if (inQuote)
                {
                    continue;
                }

                if (current == '[' || current == '{' || current == '(')
                {
                    depth++;
                    if (depth == 2)
                    {
                        groupStart = i + 1;
                    }

                    continue;
                }

                if (current != ']' && current != '}' && current != ')')
                {
                    continue;
                }

                if (depth == 2 && groupStart >= 0 && i > groupStart)
                {
                    yield return text.Substring(groupStart, i - groupStart);
                    groupStart = -1;
                }

                depth = Math.Max(0, depth - 1);
            }
        }

        private static IEnumerable<string> SplitMobAttackHitEffectDelimitedListSegments(
            string text,
            char delimiter)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            int segmentStart = 0;
            bool inQuote = false;
            char quoteChar = '\0';
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if ((current == '"' || current == '\'') && !IsMobAttackHitEffectEscapedQuote(text, i))
                {
                    if (!inQuote)
                    {
                        inQuote = true;
                        quoteChar = current;
                    }
                    else if (quoteChar == current)
                    {
                        inQuote = false;
                        quoteChar = '\0';
                    }

                    continue;
                }

                if (inQuote)
                {
                    continue;
                }

                if (IsMobAttackHitEffectOpeningCompositeDelimiter(current))
                {
                    depth++;
                    continue;
                }

                if (IsMobAttackHitEffectClosingCompositeDelimiter(current))
                {
                    depth = Math.Max(0, depth - 1);
                    continue;
                }

                if (depth > 0 || current != delimiter)
                {
                    continue;
                }

                if (i > segmentStart)
                {
                    yield return text.Substring(segmentStart, i - segmentStart);
                }

                segmentStart = i + 1;
            }

            if (segmentStart < text.Length)
            {
                yield return text.Substring(segmentStart);
            }
        }

        private static char DetectMobAttackHitEffectListDelimiter(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return '\0';
            }

            if (ContainsMobAttackHitEffectUnquotedDelimiter(value, '\t'))
            {
                return '\t';
            }

            if (ContainsMobAttackHitEffectUnquotedDelimiter(value, '|'))
            {
                return '|';
            }

            if (ContainsMobAttackHitEffectUnquotedDelimiter(value, ','))
            {
                return ',';
            }

            if (ContainsMobAttackHitEffectUnquotedDelimiter(value, ';'))
            {
                return ';';
            }

            return '\0';
        }

        private static bool ContainsMobAttackHitEffectUnquotedDelimiter(string value, char delimiter)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            bool inQuote = false;
            char quoteChar = '\0';
            int depth = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if ((current == '"' || current == '\'') && !IsMobAttackHitEffectEscapedQuote(value, i))
                {
                    if (!inQuote)
                    {
                        inQuote = true;
                        quoteChar = current;
                    }
                    else if (quoteChar == current)
                    {
                        inQuote = false;
                        quoteChar = '\0';
                    }

                    continue;
                }

                if (inQuote)
                {
                    continue;
                }

                if (IsMobAttackHitEffectOpeningCompositeDelimiter(current))
                {
                    depth++;
                    continue;
                }

                if (IsMobAttackHitEffectClosingCompositeDelimiter(current))
                {
                    depth = Math.Max(0, depth - 1);
                    continue;
                }

                if (depth == 0 && current == delimiter)
                {
                    return true;
                }
            }

            return false;
        }

        private static string StripMobAttackHitEffectOuterJsonObjectShell(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            return trimmed.Length >= 2
                   && trimmed[0] == '{'
                   && trimmed[^1] == '}'
                   && IsMobAttackHitEffectCompositeShellBalanced(trimmed, '{', '}')
                ? trimmed.Substring(1, trimmed.Length - 2)
                : trimmed;
        }

        private static bool IsMobAttackHitEffectCompositeShellBalanced(string value, char open, char close)
        {
            bool inQuote = false;
            char quoteChar = '\0';
            int depth = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if ((current == '"' || current == '\'') && !IsMobAttackHitEffectEscapedQuote(value, i))
                {
                    if (!inQuote)
                    {
                        inQuote = true;
                        quoteChar = current;
                    }
                    else if (quoteChar == current)
                    {
                        inQuote = false;
                        quoteChar = '\0';
                    }

                    continue;
                }

                if (inQuote)
                {
                    continue;
                }

                if (current == open)
                {
                    depth++;
                }
                else if (current == close)
                {
                    depth--;
                    if (depth == 0 && i < value.Length - 1)
                    {
                        return false;
                    }
                }
            }

            return depth == 0;
        }

        private static bool IsMobAttackHitEffectOpeningCompositeDelimiter(char value)
        {
            return value == '[' || value == '{' || value == '(';
        }

        private static bool IsMobAttackHitEffectClosingCompositeDelimiter(char value)
        {
            return value == ']' || value == '}' || value == ')';
        }

        private static bool IsMobAttackHitEffectEscapedQuote(string value, int quoteIndex)
        {
            if (string.IsNullOrEmpty(value) || quoteIndex <= 0 || quoteIndex >= value.Length)
            {
                return false;
            }

            int slashCount = 0;
            for (int i = quoteIndex - 1; i >= 0 && value[i] == '\\'; i--)
            {
                slashCount++;
            }

            return slashCount % 2 == 1;
        }

        private static bool IsMobAttackHitEffectHeaderListFieldName(string name)
        {
            string normalizedName = NormalizeMobAttackHitEffectStructuredFieldName(name)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);
            return string.Equals(normalizedName, "header", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "headers", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "column", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "columns", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "field", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "fields", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "fieldname", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "fieldnames", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "keynames", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "key", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "keys", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "name", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "names", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "propertynames", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "fieldkeys", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "schema", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "schemas", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "schemafield", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "schemafields", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "fieldlist", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "fieldlabels", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "columnnames", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMobAttackHitEffectValueListFieldName(string name)
        {
            string normalizedName = NormalizeMobAttackHitEffectStructuredFieldName(name)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);
            return string.Equals(normalizedName, "value", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "values", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "rowvalue", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "rowvalues", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "rowdata", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "row", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "rows", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "recordvalue", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "recordvalues", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "record", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "records", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "entry", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "entries", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "fieldvalue", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "fieldvalues", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "data", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "payload", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "raw", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "rawdata", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "recorddata", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "rowpayload", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "entrypayload", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "itempayload", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "content", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "body", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "cell", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "cells", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "cellvalue", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "cellvalues", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "tuple", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "tuples", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "argument", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "arguments", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "args", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "item", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedName, "items", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetMobAttackHitEffectScalarString(WzImageProperty property)
        {
            if (property == null || property.WzProperties?.Count > 0 == true)
            {
                return null;
            }

            if (property is WzUOLProperty uolProperty)
            {
                return uolProperty.Value;
            }

            try
            {
                return property.GetString();
            }
            catch (NotImplementedException)
            {
                return null;
            }
        }

        private static string TrimMobAttackHitEffectRecordTextFieldToken(string value)
        {
            return NormalizeMobAttackHitEffectEncodedPathSyntax(value)
                .Trim()
                .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
        }

        private static string TrimMobAttackHitEffectRecordTextFieldTokenForRecordSplitting(string value)
        {
            return NormalizeMobAttackHitEffectEncodedPathSyntaxForRecordSplitting(value)
                .Trim()
                .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
        }

        private static string GetMobAttackHitEffectNumericTupleRecordValuePath(WzImageProperty record)
        {
            if (record?.WzProperties == null || record.WzProperties.Count == 0)
            {
                return null;
            }

            string firstValue = null;
            string valueSlot = null;
            foreach (WzImageProperty child in record.WzProperties)
            {
                if (child == null
                    || !TryParseMobAttackHitEffectPlainNumericFrameToken(child.Name, out int tupleIndex))
                {
                    continue;
                }

                string value = GetMobAttackHitEffectPath(child);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (firstValue == null)
                {
                    firstValue = value;
                }

                if (tupleIndex == 1 && valueSlot == null)
                {
                    valueSlot = value;
                }

                if (LooksLikeMobAttackHitEffectPathValue(value))
                {
                    return value;
                }
            }

            return valueSlot ?? firstValue;
        }

        private static string GetMobAttackHitEffectStructuredRecordValuePath(string recordText)
        {
            if (string.IsNullOrWhiteSpace(recordText))
            {
                return null;
            }

            string pathValue = null;
            var metadataTokens = new List<string>();
            foreach ((string FieldName, string FieldValue) in EnumerateMobAttackHitEffectStructuredRecordFields(recordText))
            {
                if (string.IsNullOrWhiteSpace(FieldName) || string.IsNullOrWhiteSpace(FieldValue))
                {
                    continue;
                }

                if (IsMobAttackHitEffectStructuredHitAfterFieldName(FieldName))
                {
                    metadataTokens.Add($"hitAfter={FieldValue.Trim()}");
                    continue;
                }

                if (IsMobAttackHitEffectStructuredHitAttachFieldName(FieldName))
                {
                    metadataTokens.Add($"attach={FieldValue.Trim()}");
                    continue;
                }

                if (IsMobAttackHitEffectStructuredFacingAttachFieldName(FieldName))
                {
                    metadataTokens.Add($"attachfacing={FieldValue.Trim()}");
                    continue;
                }

                if (!IsMobAttackHitEffectStructuredPathFieldName(FieldName))
                {
                    continue;
                }

                string normalizedValue = NormalizeMobAttackHitEffectEncodedPathSyntax(FieldValue)
                    .Trim()
                    .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
                if (string.IsNullOrWhiteSpace(normalizedValue))
                {
                    continue;
                }

                if (pathValue == null || LooksLikeMobAttackHitEffectPathValue(normalizedValue))
                {
                    pathValue = normalizedValue;
                }
            }

            if (string.IsNullOrWhiteSpace(pathValue))
            {
                return null;
            }

            if (metadataTokens.Count == 0)
            {
                return pathValue;
            }

            metadataTokens.Add(pathValue);
            return string.Join("|", metadataTokens);
        }

        private static string GetMobAttackHitEffectStructuredRecordSequencePath(string recordText)
        {
            if (string.IsNullOrWhiteSpace(recordText))
            {
                return null;
            }

            var records = new List<(int FrameIndex, int RowOrder, string Path)>();
            int? currentFrameIndex = null;
            string currentPath = null;
            var currentMetadataTokens = new List<string>();
            string pendingNextPath = null;
            var pendingNextMetadataTokens = new List<string>();
            int rowOrder = 0;

            foreach ((string FieldName, string FieldValue) in EnumerateMobAttackHitEffectStructuredRecordFields(recordText))
            {
                if (string.IsNullOrWhiteSpace(FieldName) || string.IsNullOrWhiteSpace(FieldValue))
                {
                    continue;
                }

                if (TryReadMobAttackHitEffectStructuredFrameIndex(FieldName, FieldValue, out int parsedFrameIndex))
                {
                    bool hadCurrentFrame = currentFrameIndex.HasValue;
                    string pathBeforeFrame = currentPath;
                    List<string> metadataBeforeFrame = currentMetadataTokens.Count == 0
                        ? null
                        : new List<string>(currentMetadataTokens);
                    if (hadCurrentFrame)
                    {
                        FlushCurrentStructuredRecord();
                    }

                    currentFrameIndex = parsedFrameIndex;
                    currentPath = pendingNextPath ?? (hadCurrentFrame ? null : pathBeforeFrame);
                    currentMetadataTokens.Clear();
                    if (pendingNextMetadataTokens.Count > 0)
                    {
                        currentMetadataTokens.AddRange(pendingNextMetadataTokens);
                    }
                    else if (!hadCurrentFrame && metadataBeforeFrame != null)
                    {
                        currentMetadataTokens.AddRange(metadataBeforeFrame);
                    }

                    pendingNextPath = null;
                    pendingNextMetadataTokens.Clear();
                    continue;
                }

                if (IsMobAttackHitEffectStructuredHitAfterFieldName(FieldName))
                {
                    AddCurrentOrPendingStructuredRecordMetadata($"hitAfter={FieldValue.Trim()}");
                    continue;
                }

                if (IsMobAttackHitEffectStructuredHitAttachFieldName(FieldName))
                {
                    AddCurrentOrPendingStructuredRecordMetadata($"attach={FieldValue.Trim()}");
                    continue;
                }

                if (IsMobAttackHitEffectStructuredFacingAttachFieldName(FieldName))
                {
                    AddCurrentOrPendingStructuredRecordMetadata($"attachfacing={FieldValue.Trim()}");
                    continue;
                }

                if (!IsMobAttackHitEffectStructuredPathFieldName(FieldName))
                {
                    continue;
                }

                string normalizedValue = NormalizeMobAttackHitEffectEncodedPathSyntax(FieldValue)
                    .Trim()
                    .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
                if (string.IsNullOrWhiteSpace(normalizedValue))
                {
                    continue;
                }

                if (currentPath == null || LooksLikeMobAttackHitEffectPathValue(normalizedValue))
                {
                    if (currentFrameIndex.HasValue && !string.IsNullOrWhiteSpace(currentPath))
                    {
                        pendingNextPath = normalizedValue;
                        pendingNextMetadataTokens.Clear();
                    }
                    else
                    {
                        currentPath = normalizedValue;
                    }
                }
            }

            FlushCurrentStructuredRecord();

            if (records.Count < 2)
            {
                return null;
            }

            records.Sort(static (left, right) =>
            {
                int frameComparison = left.FrameIndex.CompareTo(right.FrameIndex);
                return frameComparison != 0 ? frameComparison : left.RowOrder.CompareTo(right.RowOrder);
            });

            var paths = new List<string>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                paths.Add(records[i].Path);
            }

            return string.Join("|", paths);

            void FlushCurrentStructuredRecord()
            {
                if (!currentFrameIndex.HasValue || string.IsNullOrWhiteSpace(currentPath))
                {
                    return;
                }

                string path = currentPath;
                if (currentMetadataTokens.Count > 0)
                {
                    var pathTokens = new List<string>(currentMetadataTokens.Count + 1);
                    pathTokens.AddRange(currentMetadataTokens);
                    pathTokens.Add(currentPath);
                    path = string.Join("|", pathTokens);
                }

                records.Add((currentFrameIndex.Value, rowOrder++, path));
                currentFrameIndex = null;
                currentPath = null;
                currentMetadataTokens.Clear();
            }

            void AddCurrentOrPendingStructuredRecordMetadata(string token)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(pendingNextPath))
                {
                    pendingNextMetadataTokens.Add(token);
                    return;
                }

                currentMetadataTokens.Add(token);
            }
        }

        private static IEnumerable<(string FieldName, string FieldValue)> EnumerateMobAttackHitEffectStructuredRecordFields(
            string recordText)
        {
            string normalizedRecord = StripMobAttackHitEffectOuterJsonObjectShell(
                NormalizeMobAttackHitEffectEncodedPathSyntaxForRecordSplitting(recordText));
            if (string.IsNullOrWhiteSpace(normalizedRecord))
            {
                yield break;
            }

            bool yieldedBracketedRecords = false;
            if (normalizedRecord.Length >= 4 && normalizedRecord[0] == '[' && normalizedRecord[^1] == ']')
            {
                foreach (string nestedRecordText in EnumerateMobAttackHitEffectTopLevelBracketGroups(normalizedRecord))
                {
                    foreach ((string FieldName, string FieldValue) in EnumerateMobAttackHitEffectStructuredRecordFields(nestedRecordText))
                    {
                        yieldedBracketedRecords = true;
                        yield return (FieldName, FieldValue);
                    }
                }
            }

            if (yieldedBracketedRecords)
            {
                yield break;
            }

            var segments = new List<string>(SplitMobAttackHitEffectStructuredRecordSegments(normalizedRecord));
            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                string normalizedSegment = segments[segmentIndex].Trim().Trim('`', '[', ']', '(', ')', '{', '}', '<', '>');
                if (string.IsNullOrWhiteSpace(normalizedSegment))
                {
                    continue;
                }

                bool yieldedDelimitedField = false;
                string[] delimiters = { "=>", "->", "=", ":" };
                for (int i = 0; i < delimiters.Length; i++)
                {
                    string delimiter = delimiters[i];
                    int delimiterIndex = IndexOfMobAttackHitEffectUnquotedDelimiter(normalizedSegment, delimiter);
                    if (delimiterIndex <= 0 || delimiterIndex + delimiter.Length >= normalizedSegment.Length)
                    {
                        continue;
                    }

                    string fieldName = normalizedSegment.Substring(0, delimiterIndex)
                        .Trim()
                        .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
                    string fieldValue = normalizedSegment.Substring(delimiterIndex + delimiter.Length)
                        .Trim()
                        .Trim('`', '[', ']', '(', ')', '{', '}', '<', '>');
                    if (!string.IsNullOrWhiteSpace(fieldName) && !string.IsNullOrWhiteSpace(fieldValue))
                    {
                        yieldedDelimitedField = true;
                        yield return (fieldName, fieldValue);
                    }

                    break;
                }

                if (yieldedDelimitedField
                    || segmentIndex >= segments.Count - 1
                    || !IsMobAttackHitEffectStructuredKnownFieldName(normalizedSegment))
                {
                    continue;
                }

                string adjacentValue = segments[segmentIndex + 1]
                    .Trim()
                    .Trim('`', '[', ']', '(', ')', '{', '}', '<', '>');
                if (!string.IsNullOrWhiteSpace(adjacentValue))
                {
                    yield return (normalizedSegment, adjacentValue);
                    segmentIndex++;
                }
            }
        }

        private static bool IsMobAttackHitEffectStructuredKnownFieldName(string fieldName)
        {
            string normalizedFieldName = NormalizeMobAttackHitEffectStructuredFieldName(fieldName);
            return IsMobAttackHitEffectStructuredPathFieldName(normalizedFieldName)
                   || IsMobAttackHitEffectStructuredHitAfterFieldName(normalizedFieldName)
                   || IsMobAttackHitEffectStructuredHitAttachFieldName(normalizedFieldName)
                   || IsMobAttackHitEffectStructuredFacingAttachFieldName(normalizedFieldName)
                   || IsMobAttackHitEffectStructuredFrameFieldName(normalizedFieldName)
                   || string.Equals(normalizedFieldName, "ownerMobId", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "mobTemplateId", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryReadMobAttackHitEffectStructuredFrameIndex(
            string fieldName,
            string fieldValue,
            out int frameIndex)
        {
            frameIndex = 0;
            if (!IsMobAttackHitEffectStructuredFrameFieldName(fieldName))
            {
                return false;
            }

            return TryParseMobAttackHitEffectRecordFrameIndex(fieldValue, out frameIndex);
        }

        private static bool IsMobAttackHitEffectStructuredFrameFieldName(string fieldName)
        {
            string normalizedFieldName = NormalizeMobAttackHitEffectStructuredFieldName(fieldName);
            return string.Equals(normalizedFieldName, "frame", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "frameIndex", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "hitFrame", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "sourceFrame", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "index", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "idx", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "i", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "nFrame", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "nIndex", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "key", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMobAttackHitEffectStructuredPathFieldName(string fieldName)
        {
            string normalizedFieldName = NormalizeMobAttackHitEffectStructuredFieldName(fieldName);
            string[] pathFieldNames =
            {
                "source",
                "path",
                "sHit",
                "hit",
                "effect",
                "uol",
                "value",
                "data",
                "payload",
                "valueData",
                "valuePayload",
                "payloadValue",
                "payloadData",
                "targetValue",
                "targetData",
                "targetPayload",
                "pathValue",
                "pathData",
                "pathText",
                "pathPayload",
                "pathString",
                "uolData",
                "uolValue",
                "uolValueData",
                "uolText",
                "uolPayload",
                "uolString",
                "sourceValue",
                "sourceData",
                "sourceText",
                "sourcePayload",
                "sourceString",
                "hitValue",
                "hitData",
                "hitText",
                "hitPayload",
                "hitString",
                "recordValue",
                "recordData",
                "recordValues",
                "recordText",
                "recordPayload",
                "rowValue",
                "rowData",
                "rowPayload",
                "entryValue",
                "entryData",
                "entryPayload",
                "itemValue",
                "itemData",
                "itemPayload",
                "raw",
                "rawValue",
                "rawData",
                "rawPayload",
                "body",
                "content",
                "text",
                "json",
                "sHitData",
                "clientValue",
                "clientData",
                "clientPayload",
                "clientString",
                "clientStringValue",
                "clientStringData",
                "clientStringPayload",
                "stringValue",
                "rawString",
                "m_wstr",
                "mWstr",
                "bstr",
                "bstrValue",
                "bstrData",
                "bstrPayload",
                "wstr",
                "wstrValue",
                "wstrData",
                "wstrPayload",
                "assetValue",
                "assetData",
                "assetPayload",
                "target",
                "targetPath",
                "sourcePath",
                "srcPath",
                "hitPath",
                "sHitPath",
                "sHitString",
                "effectPath",
                "assetPath",
                "uolPath",
                "targetUol",
                "targetUOL",
                "targetUolPath",
                "targetUOLPath",
                "sourceUol",
                "sourceUOL",
                "sourceUolPath",
                "sourceUOLPath",
                "clientPath",
                "clientUol",
                "clientUOL",
                "clientUolPath",
                "clientUOLPath",
                "hitUol",
                "hitUOL",
                "hitUolPath",
                "hitUOLPath",
                "sHitUol",
                "sHitUOL",
                "sHitUolPath",
                "sHitUOLPath",
                "mobAttackInfoHit",
                "mobAttackInfoHitPath",
                "mobAttackInfoSHit",
                "mobAttackInfoSHitPath"
            };

            for (int i = 0; i < pathFieldNames.Length; i++)
            {
                if (string.Equals(normalizedFieldName, pathFieldNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMobAttackHitEffectStructuredHitAfterFieldName(string fieldName)
        {
            string normalizedFieldName = NormalizeMobAttackHitEffectStructuredFieldName(fieldName);
            return string.Equals(normalizedFieldName, "hitAfter", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> SplitMobAttackHitEffectStructuredRecordSegments(string recordText)
        {
            if (string.IsNullOrWhiteSpace(recordText))
            {
                yield break;
            }

            int segmentStart = 0;
            bool inQuote = false;
            char quoteChar = '\0';
            int depth = 0;
            for (int i = 0; i < recordText.Length; i++)
            {
                char current = recordText[i];
                if ((current == '"' || current == '\'') && !IsMobAttackHitEffectEscapedQuote(recordText, i))
                {
                    if (!inQuote)
                    {
                        inQuote = true;
                        quoteChar = current;
                    }
                    else if (quoteChar == current)
                    {
                        inQuote = false;
                        quoteChar = '\0';
                    }

                    continue;
                }

                if (inQuote)
                {
                    continue;
                }

                if (IsMobAttackHitEffectOpeningCompositeDelimiter(current))
                {
                    depth++;
                    continue;
                }

                if (IsMobAttackHitEffectClosingCompositeDelimiter(current))
                {
                    depth = Math.Max(0, depth - 1);
                    continue;
                }

                if (depth > 0 || !IsMobAttackHitEffectStructuredRecordSegmentDelimiter(current))
                {
                    continue;
                }

                if (i > segmentStart)
                {
                    yield return recordText.Substring(segmentStart, i - segmentStart);
                }

                segmentStart = i + 1;
            }

            if (segmentStart < recordText.Length)
            {
                yield return recordText.Substring(segmentStart);
            }
        }

        private static bool IsMobAttackHitEffectStructuredRecordSegmentDelimiter(char character)
        {
            return character == '|'
                   || character == ';'
                   || character == '&'
                   || character == ','
                   || character == '\r'
                   || character == '\n'
                   || character == '\t';
        }

        private static int IndexOfMobAttackHitEffectUnquotedDelimiter(string value, string delimiter)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(delimiter))
            {
                return -1;
            }

            bool inQuote = false;
            char quoteChar = '\0';
            int depth = 0;
            for (int i = 0; i <= value.Length - delimiter.Length; i++)
            {
                char current = value[i];
                if ((current == '"' || current == '\'') && !IsMobAttackHitEffectEscapedQuote(value, i))
                {
                    if (!inQuote)
                    {
                        inQuote = true;
                        quoteChar = current;
                    }
                    else if (quoteChar == current)
                    {
                        inQuote = false;
                        quoteChar = '\0';
                    }

                    continue;
                }

                if (inQuote)
                {
                    continue;
                }

                if (IsMobAttackHitEffectOpeningCompositeDelimiter(current))
                {
                    depth++;
                    continue;
                }

                if (IsMobAttackHitEffectClosingCompositeDelimiter(current))
                {
                    depth = Math.Max(0, depth - 1);
                    continue;
                }

                if (depth == 0 && string.CompareOrdinal(value, i, delimiter, 0, delimiter.Length) == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsMobAttackHitEffectStructuredHitAttachFieldName(string fieldName)
        {
            string normalizedFieldName = NormalizeMobAttackHitEffectStructuredFieldName(fieldName);
            return string.Equals(normalizedFieldName, "attach", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "bHitAttach", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "hitAttach", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMobAttackHitEffectStructuredFacingAttachFieldName(string fieldName)
        {
            string normalizedFieldName = NormalizeMobAttackHitEffectStructuredFieldName(fieldName);
            return string.Equals(normalizedFieldName, "attachfacing", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "bFacingAttach", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "bFacingAttatch", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedFieldName, "facingAttach", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMobAttackHitEffectStructuredFieldName(string fieldName)
        {
            string normalizedFieldName = NormalizeMobAttackHitEffectEncodedPathSyntax(fieldName)
                .Trim()
                .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
            if (string.IsNullOrWhiteSpace(normalizedFieldName))
            {
                return string.Empty;
            }

            int suffixStart = -1;
            for (int i = normalizedFieldName.Length - 1; i >= 0; i--)
            {
                char current = normalizedFieldName[i];
                if (current == '.'
                    || current == '/'
                    || current == '\\'
                    || current == '['
                    || current == ']'
                    || current == '('
                    || current == ')'
                    || current == '{'
                    || current == '}'
                    || current == '<'
                    || current == '>')
                {
                    suffixStart = i + 1;
                    break;
                }
            }

            return suffixStart > 0 && suffixStart < normalizedFieldName.Length
                ? normalizedFieldName.Substring(suffixStart).Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>')
                : normalizedFieldName;
        }

        private static bool LooksLikeMobAttackHitEffectPathValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = NormalizeMobAttackHitEffectEncodedPathSyntax(value).Trim();
            return normalizedValue.IndexOf('/', StringComparison.Ordinal) >= 0
                   || normalizedValue.IndexOf('\\') >= 0
                   || normalizedValue.Contains(".img", StringComparison.OrdinalIgnoreCase)
                   || normalizedValue.StartsWith("../", StringComparison.Ordinal)
                   || normalizedValue.StartsWith("./", StringComparison.Ordinal)
                   || normalizedValue.StartsWith("source", StringComparison.OrdinalIgnoreCase)
                   || normalizedValue.StartsWith("hit", StringComparison.OrdinalIgnoreCase)
                   || normalizedValue.StartsWith("sHit", StringComparison.OrdinalIgnoreCase)
                   || normalizedValue.StartsWith("mobAttackInfo", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseMobAttackHitEffectPlainNumericFrameToken(string value, out int frameIndex)
        {
            frameIndex = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = NormalizeMobAttackHitEffectEncodedPathSyntax(value)
                .Trim()
                .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
            return int.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out frameIndex)
                   && frameIndex >= 0;
        }

        private static string GetMobAttackHitEffectEncodedNameValuePath(
            WzImageProperty property,
            string[] allowedNames)
        {
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return null;
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (TryReadMobAttackHitEffectEncodedNamedValue(
                        child?.Name,
                        allowedNames,
                        out string value))
                {
                    return value;
                }
            }

            return null;
        }

        private static bool TryReadMobAttackHitEffectEncodedNamedValue(
            string name,
            string[] allowedNames,
            out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(name) || allowedNames == null || allowedNames.Length == 0)
            {
                return false;
            }

            string normalizedName = NormalizeMobAttackHitEffectEncodedPathSyntax(name).Trim();
            string[] delimiters = { "=>", "->", "=", ":" };
            for (int i = 0; i < delimiters.Length; i++)
            {
                string delimiter = delimiters[i];
                int delimiterIndex = normalizedName.IndexOf(delimiter, StringComparison.Ordinal);
                if (delimiterIndex <= 0 || delimiterIndex + delimiter.Length >= normalizedName.Length)
                {
                    continue;
                }

                string fieldName = normalizedName.Substring(0, delimiterIndex).Trim();
                fieldName = fieldName.Trim('"', '\'', '`', '[', '(', '{', '<').Trim();
                fieldName = fieldName.TrimEnd(']', ')', '}', '>').Trim();
                if (!IsMobAttackHitEffectAllowedFieldName(fieldName, allowedNames))
                {
                    continue;
                }

                value = NormalizeMobAttackHitEffectEncodedPathSyntax(
                        normalizedName.Substring(delimiterIndex + delimiter.Length))
                    .Trim()
                    .Trim('"', '\'', '`', '[', ']', '(', ')', '{', '}', '<', '>');
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }

        private static bool IsMobAttackHitEffectAllowedFieldName(string fieldName, string[] allowedNames)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            for (int i = 0; i < allowedNames.Length; i++)
            {
                if (string.Equals(fieldName, allowedNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeMobAttackHitEffectEncodedPathSyntax(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            string normalized = token;
            for (int pass = 0; pass < 3; pass++)
            {
                string decoded = DecodeMobAttackHitEffectOpaqueEncodedPathSyntax(
                    NormalizeMobAttackHitEffectEntityEncodedPathSyntax(normalized));
                decoded = NormalizeMobAttackHitEffectEncodedPathSyntaxOnce(decoded);
                if (string.Equals(decoded, normalized, StringComparison.Ordinal))
                {
                    return decoded;
                }

                normalized = decoded;
            }

            return normalized;
        }

        private static string NormalizeMobAttackHitEffectEncodedPathSyntaxForRecordSplitting(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            string normalized = token;
            for (int pass = 0; pass < 3; pass++)
            {
                string decoded = DecodeMobAttackHitEffectOpaqueEncodedPathSyntax(
                    NormalizeMobAttackHitEffectEntityEncodedPathSyntax(normalized));
                decoded = NormalizeMobAttackHitEffectEncodedPathSyntaxOnce(
                    decoded,
                    preserveEscapedQuotes: true);
                if (string.Equals(decoded, normalized, StringComparison.Ordinal))
                {
                    return decoded;
                }

                normalized = decoded;
            }

            return normalized;
        }

        private static string DecodeMobAttackHitEffectOpaqueEncodedPathSyntax(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            string trimmed = token.Trim();
            string decoded = null;
            if (trimmed.StartsWith("hex:", StringComparison.OrdinalIgnoreCase))
            {
                decoded = DecodeMobAttackHitEffectHexString(trimmed.Substring(4));
            }
            else if (LooksLikeMobAttackHitEffectBase64Token(trimmed))
            {
                decoded = DecodeMobAttackHitEffectBase64String(trimmed);
            }

            if (string.IsNullOrWhiteSpace(decoded))
            {
                return token;
            }

            decoded = decoded.Trim();
            return LooksLikeMobAttackHitEffectDecodedOpaqueValue(decoded) ? decoded : token;
        }

        private static string DecodeMobAttackHitEffectHexString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string normalizedValue = value.Trim();
            if (normalizedValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalizedValue = normalizedValue.Substring(2);
            }

            normalizedValue = normalizedValue.Replace(" ", string.Empty).Replace("_", string.Empty);
            if (normalizedValue.Length == 0 || normalizedValue.Length % 2 != 0)
            {
                return null;
            }

            var bytes = new byte[normalizedValue.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(
                        normalizedValue.Substring(i * 2, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out bytes[i]))
                {
                    return null;
                }
            }

            return DecodeMobAttackHitEffectByteText(bytes);
        }

        private static bool LooksLikeMobAttackHitEffectBase64Token(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 12 || value.Length % 4 != 0)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if ((current >= 'A' && current <= 'Z')
                    || (current >= 'a' && current <= 'z')
                    || (current >= '0' && current <= '9')
                    || current == '+'
                    || current == '/'
                    || current == '=')
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static string DecodeMobAttackHitEffectBase64String(string value)
        {
            try
            {
                return DecodeMobAttackHitEffectByteText(Convert.FromBase64String(value));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static string DecodeMobAttackHitEffectByteText(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > 2048)
            {
                return null;
            }

            foreach (System.Text.Encoding encoding in EnumerateMobAttackHitEffectByteTextEncodings(bytes))
            {
                string decodedValue = encoding.GetString(bytes)
                    .Trim('\uFEFF', '\0')
                    .Trim();
                if (decodedValue.IndexOf('\0') < 0
                    && LooksLikeMobAttackHitEffectDecodedOpaqueValue(decodedValue))
                {
                    return decodedValue;
                }
            }

            return null;
        }

        private static IEnumerable<System.Text.Encoding> EnumerateMobAttackHitEffectByteTextEncodings(byte[] bytes)
        {
            yield return System.Text.Encoding.UTF8;

            if (bytes == null || bytes.Length < 4 || bytes.Length % 2 != 0)
            {
                yield break;
            }

            int evenNulls = 0;
            int oddNulls = 0;
            for (int i = 0; i < bytes.Length; i += 2)
            {
                if (bytes[i] == 0)
                {
                    evenNulls++;
                }

                if (bytes[i + 1] == 0)
                {
                    oddNulls++;
                }
            }

            int pairCount = bytes.Length / 2;
            if (oddNulls * 2 >= pairCount)
            {
                yield return System.Text.Encoding.Unicode;
            }

            if (evenNulls * 2 >= pairCount)
            {
                yield return System.Text.Encoding.BigEndianUnicode;
            }
        }

        private static bool LooksLikeMobAttackHitEffectDecodedOpaqueValue(string value)
        {
            return LooksLikeMobAttackHitEffectPathValue(value)
                   || value.IndexOf('=', StringComparison.Ordinal) > 0
                   || value.IndexOf(':', StringComparison.Ordinal) > 0
                   || value.IndexOf('|') >= 0
                   || value.IndexOf(';') >= 0
                   || value.IndexOf(',') >= 0;
        }

        private static string NormalizeMobAttackHitEffectEntityEncodedPathSyntax(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.IndexOf('&') < 0)
            {
                return token ?? string.Empty;
            }

            var builder = new System.Text.StringBuilder(token.Length);
            for (int i = 0; i < token.Length; i++)
            {
                char current = token[i];
                if (current == '&'
                    && TryParseMobAttackHitEffectEntityEncodedChar(token, i, out char decoded, out int consumedLength)
                    && IsMobAttackHitEffectDecodedPathCharacter(decoded))
                {
                    builder.Append(decoded == '\\' ? '/' : decoded);
                    i += consumedLength - 1;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool TryParseMobAttackHitEffectEntityEncodedChar(
            string token,
            int startIndex,
            out char decoded,
            out int consumedLength)
        {
            decoded = '\0';
            consumedLength = 0;
            if (string.IsNullOrEmpty(token)
                || startIndex < 0
                || startIndex >= token.Length
                || token[startIndex] != '&')
            {
                return false;
            }

            int semicolonIndex = token.IndexOf(';', startIndex + 1);
            if (semicolonIndex < 0 || semicolonIndex - startIndex > 10)
            {
                return false;
            }

            string entity = token.Substring(startIndex + 1, semicolonIndex - startIndex - 1);
            if (string.IsNullOrWhiteSpace(entity))
            {
                return false;
            }

            consumedLength = semicolonIndex - startIndex + 1;
            switch (entity.ToLowerInvariant())
            {
                case "quot":
                    decoded = '"';
                    return true;
                case "apos":
                    decoded = '\'';
                    return true;
                case "amp":
                    decoded = '&';
                    return true;
                case "lt":
                    decoded = '<';
                    return true;
                case "gt":
                    decoded = '>';
                    return true;
            }

            if (entity[0] != '#')
            {
                return false;
            }

            NumberStyles style = NumberStyles.Integer;
            string numericToken = entity.Substring(1);
            if (numericToken.Length > 1 && (numericToken[0] == 'x' || numericToken[0] == 'X'))
            {
                style = NumberStyles.HexNumber;
                numericToken = numericToken.Substring(1);
            }

            if (!int.TryParse(numericToken, style, CultureInfo.InvariantCulture, out int codePoint)
                || codePoint < 0
                || codePoint > char.MaxValue)
            {
                return false;
            }

            decoded = (char)codePoint;
            return true;
        }

        private static string NormalizeMobAttackHitEffectEncodedPathSyntaxOnce(
            string token,
            bool preserveEscapedQuotes = false)
        {
            var builder = new System.Text.StringBuilder(token.Length);
            for (int i = 0; i < token.Length; i++)
            {
                char current = token[i];
                if (current == '\\' && i < token.Length - 1)
                {
                    if (i < token.Length - 5
                        && (token[i + 1] == 'u' || token[i + 1] == 'U')
                        && TryParseMobAttackHitEffectUnicodeEscapedChar(token, i + 2, out char unicodeDecoded)
                        && IsMobAttackHitEffectDecodedPathCharacter(unicodeDecoded))
                    {
                        builder.Append(unicodeDecoded == '\\' ? '/' : unicodeDecoded);
                        i += 5;
                        continue;
                    }

                    if (i < token.Length - 3
                        && (token[i + 1] == 'x' || token[i + 1] == 'X')
                        && TryParseMobAttackHitEffectHexEncodedChar(token[i + 2], token[i + 3], out char hexDecoded)
                        && IsMobAttackHitEffectDecodedPathCharacter(hexDecoded))
                    {
                        builder.Append(hexDecoded == '\\' ? '/' : hexDecoded);
                        i += 3;
                        continue;
                    }

                    char escaped = token[i + 1];
                    if (preserveEscapedQuotes && (escaped == '"' || escaped == '\''))
                    {
                        builder.Append(current);
                        builder.Append(escaped);
                        i++;
                        continue;
                    }

                    if (IsMobAttackHitEffectEscapedSyntaxCharacter(escaped))
                    {
                        builder.Append(escaped == '\\' ? '/' : escaped);
                        i++;
                        continue;
                    }
                }

                if (current == '%'
                    && i < token.Length - 2
                    && TryParseMobAttackHitEffectHexEncodedChar(token[i + 1], token[i + 2], out char decoded)
                    && IsMobAttackHitEffectDecodedPathCharacter(decoded))
                {
                    builder.Append(decoded == '\\' ? '/' : decoded);
                    i += 2;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool IsMobAttackHitEffectDecodedPathCharacter(char character)
        {
            return (character >= 0x20 && character <= 0x7E)
                   || char.IsWhiteSpace(character);
        }

        private static bool IsMobAttackHitEffectEscapedSyntaxCharacter(char character)
        {
            return character == '/'
                   || character == '\\'
                   || character == ':'
                   || character == '='
                   || character == '+'
                   || character == '-'
                   || character == '.'
                   || character == '_'
                   || character == '"'
                   || character == '\''
                   || character == '('
                   || character == ')'
                   || character == '['
                   || character == ']'
                   || character == '{'
                   || character == '}'
                   || character == '<'
                   || character == '>';
        }

        private static bool TryParseMobAttackHitEffectUnicodeEscapedChar(
            string token,
            int firstHexIndex,
            out char decoded)
        {
            decoded = '\0';
            if (string.IsNullOrEmpty(token)
                || firstHexIndex < 0
                || firstHexIndex + 3 >= token.Length)
            {
                return false;
            }

            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!TryParseMobAttackHitEffectHexDigit(token[firstHexIndex + i], out int digit))
                {
                    return false;
                }

                value = (value << 4) | digit;
            }

            decoded = (char)value;
            return true;
        }

        private static bool TryParseMobAttackHitEffectHexEncodedChar(
            char firstHexChar,
            char secondHexChar,
            out char decoded)
        {
            decoded = '\0';
            if (!TryParseMobAttackHitEffectHexDigit(firstHexChar, out int high)
                || !TryParseMobAttackHitEffectHexDigit(secondHexChar, out int low))
            {
                return false;
            }

            decoded = (char)((high << 4) | low);
            return true;
        }

        private static bool TryParseMobAttackHitEffectHexDigit(char character, out int value)
        {
            value = 0;
            if (character >= '0' && character <= '9')
            {
                value = character - '0';
                return true;
            }

            char normalized = char.ToUpperInvariant(character);
            if (normalized >= 'A' && normalized <= 'F')
            {
                value = normalized - 'A' + 10;
                return true;
            }

            return false;
        }
        #endregion
    }
}
