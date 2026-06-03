using InfernusMod.Survivors.Infernus.Achievements;
using RoR2;
using UnityEngine;

namespace InfernusMod.Survivors.Infernus
{
    public static class InfernusUnlockables
    {
        public static UnlockableDef characterUnlockableDef = null;
        public static UnlockableDef masterySkinUnlockableDef = null;

        public static void Init()
        {
            masterySkinUnlockableDef = Modules.Content.CreateAndAddUnlockbleDef(
                InfernusMasteryAchievement.unlockableIdentifier,
                Modules.Tokens.GetAchievementNameToken(InfernusMasteryAchievement.identifier),
                InfernusSurvivor.instance.assetBundle.LoadAsset<Sprite>("texMasteryAchievement"));
        }
    }
}
