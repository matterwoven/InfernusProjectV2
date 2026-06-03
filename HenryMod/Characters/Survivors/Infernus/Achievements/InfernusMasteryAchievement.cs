using RoR2;
using InfernusMod.Modules.Achievements;

namespace InfernusMod.Survivors.Infernus.Achievements
{
    //automatically creates language tokens "ACHIEVMENT_{identifier.ToUpper()}_NAME" and "ACHIEVMENT_{identifier.ToUpper()}_DESCRIPTION" 
    [RegisterAchievement(identifier, unlockableIdentifier, null, 10, null)]
    public class InfernusMasteryAchievement : BaseMasteryAchievement
    {
        public const string identifier = InfernusSurvivor.INFERNUS_PREFIX + "masteryAchievement";
        public const string unlockableIdentifier = InfernusSurvivor.INFERNUS_PREFIX + "masteryUnlockable";

        public override string RequiredCharacterBody => InfernusSurvivor.instance.bodyName;

        //difficulty coeff 3 is monsoon. 3.5 is typhoon for grandmastery skins
        public override float RequiredDifficultyCoefficient => 3;
    }
}