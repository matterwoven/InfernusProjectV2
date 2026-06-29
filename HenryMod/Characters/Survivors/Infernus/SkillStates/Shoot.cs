using EntityStates;
using InfernusMod.Characters.Survivors.Infernus.SkillStates;
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
        public static Afterburn afterburnController;
        public static GameObject tracerEffectPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/Tracers/TracerGoldGat");

        public static bool startFlag = true;
        public static int buildupThreshold = 10;

        private bool prevIsCrit;
        private float duration;
        private float fireTime;
        private bool hasFired;
        private string muzzleString;

        public override void OnEnter()
        {
            base.OnEnter();
            if(afterburnController == null)
            {
                afterburnController = GetComponent<Afterburn>();
                startFlag = true;
            }
            if (startFlag == true)
            {
                afterburnController.Init(this.characterBody);
                startFlag = false;
            }
            duration = baseDuration / attackSpeedStat;
            fireTime = firePercentTime * duration;
            characterBody.SetAimTimer(2f);
            muzzleString = "Muzzle";

            PlayAnimation("RightArm, Override", "Shoot", "ShootGun.playbackRate", duration);
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
                prevIsCrit = RollCrit();

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
                        isCrit = prevIsCrit,
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
                        hitCallback = OnBulletHit(),
                    }.Fire();
                }
            }
        }
        #region callback
        private BulletAttack.HitCallback OnBulletHit()
        {
            return (BulletAttack bulletAttack, ref BulletAttack.BulletHit hitInfo) =>
            {
                bool returnValue = BulletAttack.DefaultHitCallbackImplementation(bulletAttack, ref hitInfo);
                bool isCrit = bulletAttack.isCrit;

                // Let the base damage through unconditionally
                if (hitInfo.hitHurtBox == null) return returnValue;
                HurtBox victimHurtBox = hitInfo.hitHurtBox;
                //Victim health component
                HealthComponent hc = hitInfo.hitHurtBox.healthComponent;
                if (hc == null || !hc.alive) return returnValue;
                GameObject victimGameObject = hc.gameObject;

                CharacterBody victim = hc.body;
                if (victim == null) return returnValue;

                ApplyDebuffLogic(victim, victimHurtBox);

                return returnValue; // returning returnValue keeps normal hit processing (damage, effects, etc.) DONT CHANGE TO TRUE/FALSE
            };
        }

        private void ApplyDebuffLogic(CharacterBody victim, HurtBox hitHurtBox)
        {
            bool isAlreadyBurning = victim.HasBuff(InfernusDebuffs.afterburnDebuff);

            TeamComponent tc = hitHurtBox.healthComponent.GetComponent<TeamComponent>();
            if (tc != null && tc.teamIndex == GetTeam()) return;

            if (isAlreadyBurning)
            {
                // Target is already burning — refresh to full duration.
                // Made MonoBehavior handle the tick-timing itself.
                if (prevIsCrit)
                    afterburnController.addBurnTargetCrit(hitHurtBox.healthComponent);
                else
                    afterburnController.addBurnTarget(hitHurtBox.healthComponent);
            }
            else
            {
                // Add one buildup stack
                victim.AddBuff(InfernusDebuffs.afterburnBuildup);
                if (prevIsCrit)
                    victim.AddBuff(InfernusDebuffs.afterburnBuildup);

                int currentStacks = victim.GetBuffCount(InfernusDebuffs.afterburnBuildup);

                if (currentStacks >= buildupThreshold)
                {
                    // Clear ALL buildup stacks at once
                    for (int i = 0; i < currentStacks; i++)
                        victim.RemoveBuff(InfernusDebuffs.afterburnBuildup);

                    //Deal on-proc damage
                    afterburnController.dealDamageBurn(hitHurtBox.healthComponent);

                    // Apply afterburn dot fresh
                    afterburnController.addBurnTarget(hitHurtBox.healthComponent);

                    victim.AddBuff(InfernusDebuffs.afterburnDebuff);
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
        #endregion
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }
}