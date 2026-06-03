using InfernusMod.Survivors.Infernus.SkillStates;

namespace InfernusMod.Survivors.Infernus
{
    public static class InfernusStates
    {
        public static void Init()
        {
            Modules.Content.AddEntityState(typeof(SlashCombo));

            Modules.Content.AddEntityState(typeof(Shoot));

            Modules.Content.AddEntityState(typeof(Roll));

            Modules.Content.AddEntityState(typeof(ThrowBomb));
        }
    }
}
