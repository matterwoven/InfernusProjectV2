using System;
using InfernusMod.Modules;
using InfernusMod.Survivors.Infernus.Achievements;

namespace InfernusMod.Survivors.Infernus
{
    public static class InfernusTokens
    {
        public static void Init()
        {
            AddInfernusTokens();

            ////uncomment this to spit out a lanuage file with all the above tokens that people can translate
            ////make sure you set Language.usingLanguageFolder and printingEnabled to true
            //Language.PrintOutput("Infernus.txt");
            ////refer to guide on how to build and distribute your mod with the proper folders
        }

        public static void AddInfernusTokens()
        {
            string prefix = InfernusSurvivor.INFERNUS_PREFIX;

            string desc = "Infernus is a flame slinging bartender from New York who uses his mastery over flames to take down his foes.<color=#E32D2D>" + Environment.NewLine + Environment.NewLine
             + "< ! > Afterburns ixian flame deals 200% damage per second, its duration refreshing when your abilities hit a burning enemy." + Environment.NewLine + Environment.NewLine
             + "< ! > Incendiary Remarks applies stacks of afterburn buildup on hit." + Environment.NewLine + Environment.NewLine
             + "< ! > Napalm coats your enemies in a flammable cocktail that makes your attacks more effective." + Environment.NewLine + Environment.NewLine
             + "< ! > Flame Dash leaves behind a trail of flames that can damage crowds of enemies in an area." + Environment.NewLine + Environment.NewLine
             + "< ! > Concussive Combustion turns you into a living timebomb, stunning everyone around you after a windup." + Environment.NewLine + Environment.NewLine;

            string outro = "..and so he ran, dreaming of the new gifts he's obtained to save Jezebel's.";
            string outroFailure = "..and so his flame drowned, returned to the haunted streets of New York.";

            Language.Add(prefix + "NAME", "Infernus");
            Language.Add(prefix + "DESCRIPTION", desc);
            Language.Add(prefix + "SUBTITLE", "Loyal friend of Hank");
            Language.Add(prefix + "LORE", "sample lore");
            Language.Add(prefix + "OUTRO_FLAVOR", outro);
            Language.Add(prefix + "OUTRO_FAILURE", outroFailure);

            #region Skins
            Language.Add(prefix + "MASTERY_SKIN_NAME", "Alternate");
            #endregion

            #region Passive
            Language.Add(prefix + "PASSIVE_NAME", "Afterburn");
            Language.Add(prefix + "PASSIVE_DESCRIPTION", $"Weapon hits build up a burning effect, dealing <style=cIsDamage>{100f * InfernusStaticValues.afterburnDamageCoefficient}% damage over time</style>.");
            #endregion

            #region Primary
            Language.Add(prefix + "PRIMARY_REMARKS_NAME", "Incendiary Remarks");
            Language.Add(prefix + "PRIMARY_REMARKS_DESCRIPTION", Tokens.emberPrefix + $"Shoot flaming bullets dealing <style=cIsDamage>{100f * InfernusStaticValues.gunDamageCoefficient}% damage</style>.");
            #endregion

            #region Secondary
            Language.Add(prefix + "SECONDARY_NAPALM_NAME", "Napalm");
            Language.Add(prefix + "SECONDARY_NAPALM_DESCRIPTION", Tokens.emberPrefix + $"Eject napalm to coat enemies dealing <style=cIsDamage>{100f * InfernusStaticValues.napalmDamageCoefficient}% damage</style>.");
            #endregion

            #region Utility
            Language.Add(prefix + "UTILITY_DASH_NAME", "Flame Dash");
            Language.Add(prefix + "UTILITY_DASH_DESCRIPTION", Tokens.emberPrefix + $"Dash swiftly while leaving behind a trail of flames that deals <style=cIsDamage>{100f * InfernusStaticValues.dashDamageCoefficient}% damage over time</style>.");
            #endregion

            #region Special
            Language.Add(prefix + "SPECIAL_BOMB_NAME", "Concussive Combustion");
            Language.Add(prefix + "SPECIAL_BOMB_DESCRIPTION", $"Throw a bomb for <style=cIsDamage>{100f * InfernusStaticValues.bombDamageCoefficient}% damage</style>.");
            #endregion

            #region Achievements
            Language.Add(Tokens.GetAchievementNameToken(InfernusMasteryAchievement.identifier), "Infernus: Mastery");
            Language.Add(Tokens.GetAchievementDescriptionToken(InfernusMasteryAchievement.identifier), "As Infernus, beat the game or obliterate on Monsoon.");
            #endregion
        }
    }
}
