using R2API;
using RoR2;
using UnityEngine;

namespace InfernusMod.Survivors.Infernus
{
    public static class InfernusDebuffs
    {
        public static BuffDef afterburnDebuff;
        public static BuffDef afterburnBuildup;
        public static BuffDef napalmDebuff;
        public static DotController.DotIndex afterburnDebuffIndex;

        public static void Init(AssetBundle bundle)
        {
            afterburnDebuff = Modules.Content.CreateAndAddBuff(
                "InfernusAfterburn",
                LegacyResourcesAPI.Load<BuffDef>("BuffDefs/Onfire").iconSprite,
                Color.red,
                false,
                true
            );

            afterburnBuildup = Modules.Content.CreateAndAddBuff(
                "InfernusBuildup",
                LegacyResourcesAPI.Load<BuffDef>("BuffDefs/OnFire").iconSprite,
                Color.white,
                true,
                false
            );

            napalmDebuff = Modules.Content.CreateAndAddBuff(
                "NapalmDebuff",
                LegacyResourcesAPI.Load<BuffDef>("BuffDefs/OnFire").iconSprite,
                Color.black,
                false,
                true
            );

            DotController.DotDef afterburnDot = new DotController.DotDef
            {
                associatedBuff = afterburnDebuff,
                damageCoefficient = InfernusStaticValues.afterburnDamageCoefficient * 0.5f,
                interval = 0.5f,
                damageColorIndex = DamageColorIndex.Void,
                resetTimerOnAdd = true
            };

            afterburnDebuffIndex = DotAPI.RegisterDotDef(afterburnDot);
        }
    }
}
