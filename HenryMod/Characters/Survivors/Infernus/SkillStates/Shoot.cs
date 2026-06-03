using EntityStates;
using InfernusMod.Survivors.Infernus;
using RoR2;
using UnityEngine;

namespace InfernusMod.Survivors.Infernus.SkillStates
{
    public class Shoot : BaseSkillState
    {
        public static float damageCoefficient = InfernusStaticValues.gunDamageCoefficient;
        public static float procCoefficient = 0.6f;
        public static float baseDuration = 0.23f;
        //delay on firing is usually ass-feeling. only set this if you know what you're doing
        public static float firePercentTime = 0.0f;
        public static float force = 200f;
        public static float recoil = 0.5f;
        public static float range = 256f;
        public static GameObject tracerEffectPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/Tracers/TracerGoldGat");

        public static int buildupThreshold = 10;

        public static float afterburnDuration = 5f;

        private float duration;
        private float fireTime;
        private bool hasFired;
        private string muzzleString;

        public override void OnEnter()
        {
            base.OnEnter();
            duration = baseDuration / attackSpeedStat;
            fireTime = firePercentTime * duration;
            characterBody.SetAimTimer(2f);
            muzzleString = "Muzzle";

            PlayAnimation("LeftArm, Override", "ShootGun", "ShootGun.playbackRate", 1.8f);
        }

        public override void OnExit()
        {
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (fixedAge >= fireTime)
            {
                Fire();
            }

            if (fixedAge >= duration && isAuthority)
            {
                outer.SetNextStateToMain();
                return;
            }
        }

        private void Fire()
        {
            if (!hasFired)
            {
                hasFired = true;

                characterBody.AddSpreadBloom(1.5f);
                EffectManager.SimpleMuzzleFlash(EntityStates.Commando.CommandoWeapon.FirePistol2.muzzleEffectPrefab, gameObject, muzzleString, false);
                Util.PlaySound("InfernusShootPistol", gameObject);

                if (isAuthority)
                {
                    Ray aimRay = GetAimRay();
                    AddRecoil(-1f * recoil, -2f * recoil, -0.5f * recoil, 0.5f * recoil);

                    new BulletAttack
                    {
                        bulletCount = 1,
                        aimVector = aimRay.direction,
                        origin = aimRay.origin,
                        damage = damageCoefficient * damageStat,
                        damageColorIndex = DamageColorIndex.Default,
                        damageType = DamageTypeCombo.GenericSecondary,
                        falloffModel = BulletAttack.FalloffModel.None,
                        maxDistance = range,
                        force = force,
                        hitMask = LayerIndex.CommonMasks.bullet,
                        minSpread = 0f,
                        maxSpread = 0f,
                        isCrit = RollCrit(),
                        owner = gameObject,
                        muzzleName = muzzleString,
                        smartCollision = true,
                        procChainMask = default,
                        procCoefficient = procCoefficient,
                        radius = 0.75f,
                        sniper = false,
                        stopperMask = LayerIndex.CommonMasks.bullet,
                        weapon = null,
                        tracerEffectPrefab = tracerEffectPrefab,
                        spreadPitchScale = 1f,
                        spreadYawScale = 1f,
                        queryTriggerInteraction = QueryTriggerInteraction.UseGlobal,
                        hitEffectPrefab = EntityStates.Commando.CommandoWeapon.FirePistol2.hitEffectPrefab,
                        hitCallback = OnBulletHit,
                    }.Fire();
                }
            }
        }
        #region callback
        private bool OnBulletHit(BulletAttack attack, ref BulletAttack.BulletHit hitInfo)
        {
            // Let the base damage through unconditionally
            if (hitInfo.hitHurtBox == null) return true;
            HurtBox victimHurtBox = hitInfo.hitHurtBox;
            //Victim health component
            HealthComponent hc = hitInfo.hitHurtBox.healthComponent;
            if (hc == null || !hc.alive) return true;
            GameObject victimGameObject = hc.gameObject;

            CharacterBody victim = hc.body;
            if (victim == null) return true;

            ApplyDebuffLogic(victim, victimHurtBox);

            return true; // returning true keeps normal hit processing (damage, effects, etc.)
        }

        private void ApplyDebuffLogic(CharacterBody victim, HurtBox hitHurtBox)
        {
            bool isAlreadyBurning = victim.HasBuff(InfernusDebuffs.afterburnDebuff);

            if (isAlreadyBurning)
            {
                // Target is already burning — refresh the dot to full duration.
                // We manually clear all matching dot stacks from dotStackList
                // (no public RemoveDotStacksForType exists in this RoR2 version)
                // then immediately re-inflict so the timer resets cleanly.
                DotController dc = DotController.FindDotController(victim.gameObject);
                if (dc != null)
                    ClearDotStacks(dc, InfernusDebuffs.afterburnDebuffIndex);

                InflictAfterburn(victim, hitHurtBox);
            }
            else
            {
                // Add one buildup stack
                victim.AddBuff(InfernusDebuffs.afterburnBuildup);

                int currentStacks = victim.GetBuffCount(InfernusDebuffs.afterburnBuildup);

                if (currentStacks >= buildupThreshold)
                {
                    // Clear ALL buildup stacks at once
                    for (int i = 0; i < currentStacks; i++)
                        victim.RemoveBuff(InfernusDebuffs.afterburnBuildup);

                    // Apply afterburn dot fresh
                    InflictAfterburn(victim, hitHurtBox);
                }
            }
        }

        private static void ClearDotStacks(DotController dc, DotController.DotIndex targetIndex)
        {
            // dotStackList is a public List<DotController.DotStack> on DotController
            for (int i = dc.dotStackList.Count - 1; i >= 0; i--)
            {
                if (dc.dotStackList[i].dotIndex == targetIndex)
                    dc.dotStackList.RemoveAt(i);
            }
        }

        private void InflictAfterburn(CharacterBody victim, HurtBox hitHurtbox)
        {
            DotController.InflictDot(
                victim.gameObject,
                gameObject,                           // attacker
                hitHurtbox,
                InfernusDebuffs.afterburnDebuffIndex,
                afterburnDuration,
                1f                                    // damage multiplier on top of DotDef.damageCoefficient
            );
        }
        #endregion
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }
}