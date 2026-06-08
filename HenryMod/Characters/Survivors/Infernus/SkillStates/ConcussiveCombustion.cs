using EntityStates;
using InfernusMod.Survivors.Infernus;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace InfernusMod.Survivors.Infernus.SkillStates
{
    public class ConcussiveCombustion : BaseSkillState
    {
        //Stun duration on special, not needed because of how stun is implemented in damage
        //public static float StunDuration = 1.05f;

        //Wind up values
        public OverlapAttack concussiveAttack;
        public static float baseDuration = 3.0f;
        public static float damageCoefficient = InfernusStaticValues.bombDamageCoefficient;
        public static float procCoefficient = 2f;
        public static float napalmDuration = 15f;
        public static float firePercentTime = 1f;
        public static float pushForce = 10f;
        public static float windupTime = 3f; //3 secs
        public static float hitboxScale = 1f;

        private static float duration;

        private float attackDelay;
        private float fireTime;
        private bool hasFired;

        //Synopsis for rework

        //OnEnter, load the area of the hit relative to the character
        //Wait 3 seconds, then exit
        //OnExit, play animation of character, play attached sound, then in defined area hit characters with stun hit for damage coefficient


        //Returns a float ex: Begins 0.1 -> Ends 1.0
        protected float PercentageDone()
        {
            return Mathf.Clamp01(base.fixedAge / duration);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            hasFired = false;
            duration = baseDuration / attackSpeedStat;
            characterBody.SetAimTimer(duration);

            //Once you have anims PlayAnimation();

            //Once you have the audio Util.PlaySound("InfernusNapalm", gameObject);
        }

        public void InitializeAttack()
        {
            HitBoxGroup concussiveCombustion = FindHitBoxGroup("ConcussiveGroup");

            HitBox hitbox = concussiveCombustion.hitBoxes[0];

            hitbox.gameObject.transform.localScale = new Vector3(hitboxScale, hitboxScale, hitboxScale);

            concussiveCombustion.hitBoxes[0] = hitbox;

            concussiveAttack = new OverlapAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = characterBody.teamComponent.teamIndex,
                damage = InfernusStaticValues.bombDamageCoefficient * damageStat,
                procCoefficient = procCoefficient,
                //hitEffectPrefab = hitEffectPrefab,
                isCrit = RollCrit(),
                damageType = DamageType.Stun1s | DamageType.AOE,
                hitBoxGroup = concussiveCombustion,
            };

        }

        public override void FixedUpdate()
        {
            //Implement windup here through a tickdown
            base.FixedUpdate();


            float readyState = PercentageDone();


            // Fire only during active hit window
            if (isAuthority && (readyState >= 1f))
            {
                outer.SetNextStateToMain();
            }

        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }

        public void PlayAnimation(float duration)
        {

            if (GetModelAnimator())
            {
                PlayAnimation("FullBody, Override", "Combustion", "ThrowBomb.playbackRate", duration * 2f);
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            InitializeAttack();
            concussiveAttack.Fire();
            PlayAnimation(duration);
        }
    }
}