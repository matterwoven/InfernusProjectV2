using EntityStates;
using RoR2;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InfernusMod.Survivors.Infernus.SkillStates
{
    public class Napalm : BaseSkillState
    {
        //WAVE LOGIC NOT HANDLED BY A HUMAN, Napalms wave logic was handled by Claude code and not by me, only as a learning element comparing the two did I make changes for readability
        // ── Timing ────────────────────────────────────────────────────────────
        /// Total state length in seconds (scaled by attack speed).
        public static float baseDuration          = 0.5f;
        /// Duration in seconds at which the hitbox starts sweeping.
        public static float attackStartPercent    = 0.15f;
        /// Duration in seconds at which the hitbox stops.
        public static float attackEndPercent      = 0.5f;

        // ── Damage ────────────────────────────────────────────────────────────
        public static float procCoefficient       = 1f;
        public static float pushForce             = 10f;

        // ── Hitbox geometry ───────────────────────────────────────────────────
        /// How far along the aim ray the centre of the box sits at the
        /// START of the sweep (world units).
        public static float splashStartOffset     = 1.0f;

        /// How far along the aim ray the box travels by the END of the sweep.
        /// Increase this for a more "thrown" feel.
        public static float splashEndOffset       = 20f;
        /// Half-extents of the overlap box (width, height, depth).
        public static Vector3 splashHalfExtents   = new Vector3(1.2f, 1.2f, 1.2f);

        // ── Debuff ────────────────────────────────────────────────────────────
        public static float napalmDebuffDuration  = 8f;

        // ── Private runtime ───────────────────────────────────────────────────
        private float     duration;
        private float     attackStart;
        private float     attackEnd;

        /// Aim direction locked at cast time so the splash goes where you aimed.
        private Vector3   lockedAimDirection;
        private Vector3   lockedOrigin;

        /// World-space proxy Transform — detached from the model so we can
        /// position and orient it freely along the aim ray.
        private Transform splashProxy;

        private OverlapAttack overlapAttack;
        private bool          attackActive;

        /// Enemies already hit this cast — prevents double-hits during the sweep.
        private readonly List<HurtBox> hitTargets = new List<HurtBox>();
        private readonly List<OverlapAttack.OverlapInfo> overlapResults = new List<OverlapAttack.OverlapInfo>();

        // ════════════════════════════════════════════════════════════════════
        public override void OnEnter()
        {
            base.OnEnter();

            duration    = baseDuration / attackSpeedStat;
            attackStart = attackStartPercent * duration;
            attackEnd   = attackEndPercent   * duration;

            PlayAnimation(duration);

            // Lock aim at the moment the skill is activated
            Ray aimRay          = GetAimRay();
            lockedAimDirection  = aimRay.direction;          // already normalized
            lockedOrigin        = aimRay.origin;

            // Build a world-space proxy Transform for the OverlapAttack.
            // It is NOT parented to the model, so rotating it never fights
            // the character's facing direction.
            GameObject proxyGO  = new GameObject("NapalmSplashProxy");
            splashProxy         = proxyGO.transform;
            splashProxy.rotation = Quaternion.LookRotation(lockedAimDirection, Vector3.up);

            characterBody.SetAimTimer(2f);

            // Animation & sound placeholders
            // PlayCrossfade("Gesture, Override", "Napalm", "Slash.playbackRate", duration, 0.05f);
            // Util.PlaySound("InfernusNapalm", gameObject);

            if (isAuthority)
                BuildOverlapAttack();
        }

        // ════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Creates the OverlapAttack once. The proxy Transform is repositioned
        /// every frame during the attack window so the sweep moves outward.
        /// </summary>
        private void BuildOverlapAttack()
        {
            overlapAttack = new OverlapAttack
            {
                attacker         = gameObject,
                inflictor        = gameObject,
                teamIndex        = characterBody.teamComponent.teamIndex,
                damage           = InfernusStaticValues.napalmDamageCoefficient * damageStat,
                procCoefficient  = procCoefficient,
                forceVector      = lockedAimDirection * pushForce,
                isCrit           = RollCrit(),
                damageType       = DamageType.Generic,

                // Point the OverlapAttack at our proxy; we drive its position manually.
                // hitBoxGroup is left null — we'll use Fire(overlapResults) with a
                // Physics.OverlapBox call instead so we control the shape exactly.
            };
        }

        // ════════════════════════════════════════════════════════════════════
        public override void FixedUpdate()
        {
            base.FixedUpdate();

            bool inWindow = fixedAge >= attackStart && fixedAge <= attackEnd;

            if (isAuthority && inWindow)
            {
                // Lerp the proxy outward along the locked aim ray
                float t           = Mathf.InverseLerp(attackStart, attackEnd, fixedAge);
                float offset      = Mathf.Lerp(splashStartOffset, splashEndOffset, t);
                splashProxy.position = lockedOrigin + lockedAimDirection * offset;

                FireSplashTick();
            }

            if (isAuthority && fixedAge >= duration)
            {
                outer.SetNextStateToMain();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        /// Runs a Physics.OverlapBox at the proxy position every fixed frame
        /// during the attack window. Already-hit targets are skipped so each
        /// enemy takes damage at most once per cast.
        /// 
        public void PlayAnimation(float duration)
        {

            if (GetModelAnimator())
            {
                PlayAnimation("Gesture, Override", "Napalm", "Slash.playbackRate", duration);
            }
        }

        private void FireSplashTick()
        {
            Collider[] cols = Physics.OverlapBox(
                splashProxy.position,
                splashHalfExtents,
                splashProxy.rotation,
                LayerIndex.entityPrecise.mask
            );

            foreach (Collider col in cols)
            {
                HurtBox hurtBox = col.GetComponent<HurtBox>();
                if (hurtBox == null) continue;

                HealthComponent hc = hurtBox.healthComponent;
                if (hc == null || !hc.alive)       continue;
                if (hurtBox.teamIndex == GetTeam()) continue; // no friendly fire
                if (hitTargets.Contains(hurtBox))  continue; // already hit this cast

                hitTargets.Add(hurtBox);

                // Apply napalm debuff
                CharacterBody body = hc.body;
                if (body != null)
                    body.AddTimedBuff(InfernusDebuffs.napalmDebuff, napalmDebuffDuration);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        public override void OnExit()
        {
            foreach (HurtBox hurtBox in hitTargets.Distinct())
            {
                HealthComponent hc = hurtBox.healthComponent;
                // Deal damage once
                DamageInfo info = new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = InfernusStaticValues.napalmDamageCoefficient * damageStat,
                    procCoefficient = procCoefficient,
                    position = hc.transform.position,
                    force = lockedAimDirection * pushForce,
                    crit = overlapAttack.isCrit,
                    damageType = DamageType.Generic,
                    damageColorIndex = DamageColorIndex.Default,
                };

                hc.TakeDamage(info);
                GlobalEventManager.instance.OnHitEnemy(info, hc.gameObject);
                GlobalEventManager.instance.OnHitAll(info, hc.gameObject);
            }
            // Always clean up the proxy — it is not parented so Unity won't
            // destroy it automatically when this state exits.
            if (splashProxy != null)
                UnityEngine.Object.Destroy(splashProxy.gameObject);

            base.OnExit();
        }

        // ════════════════════════════════════════════════════════════════════
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }
}
