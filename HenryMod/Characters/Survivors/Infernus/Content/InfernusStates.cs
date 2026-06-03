using InfernusMod.Survivors.Infernus.SkillStates;

namespace InfernusMod.Survivors.Infernus
{
    public static class InfernusStates
    {
        public static void Init()
        {
            Modules.Content.AddEntityState(typeof(Shoot));

            Modules.Content.AddEntityState(typeof(Napalm));

            Modules.Content.AddEntityState(typeof(FlameDash));

            Modules.Content.AddEntityState(typeof(ConcussiveCombustion));
        }
    }
}
