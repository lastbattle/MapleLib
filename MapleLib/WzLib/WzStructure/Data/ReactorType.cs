using System;

namespace MapleLib.WzLib.WzStructure.Data {

    [Serializable]
    public enum ReactorType {
        ActivatedByAnyHit = 0,
        ActivatedLeftHit = 1,
        ActivatedRightHit = 2,
        ActivatedBySkill = 5,
        ActivatedByHarvesting = 8,
        ActivatedByTouch = 9,
        ActivatedbyItem = 100,
        UNKNOWN = -1,
        AnimationOnly = 999 // Sits there and does nothing
    }
}
