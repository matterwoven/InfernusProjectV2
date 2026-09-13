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
                bundle.LoadAsset<Sprite>("texFireDebuffFR"),
                Color.yellow,
                false,
                true
            );

            afterburnBuildup = Modules.Content.CreateAndAddBuff(
                "InfernusBuildup",
                bundle.LoadAsset<Sprite>("texFireDebuffFR"),
                Color.white,
                true,
                false
            );

            napalmDebuff = Modules.Content.CreateAndAddBuff(
                "NapalmDebuff",
                bundle.LoadAsset<Sprite>("texNapalmDebuffFR"),
                Color.white,
                false,
                true
            );

            //DotController.DotDef afterburnDot = new DotController.DotDef
            //{
                //associatedBuff = afterburnDebuff,
                //damageCoefficient = InfernusStaticValues.afterburnDamageCoefficient * 0.5f,
                //interval = 0.5f,
                //terminalTimedBuffDuration = 8.0f,
                //damageColorIndex = DamageColorIndex.Void,
                //resetTimerOnAdd = true
            //};

            //afterburnDebuffIndex = DotAPI.RegisterDotDef(afterburnDot);
        }
    }
}
