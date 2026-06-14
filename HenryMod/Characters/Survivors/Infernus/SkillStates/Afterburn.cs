using EntityStates;
using IL.RoR2;
using InfernusMod.Survivors.Infernus;
using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace InfernusMod.Characters.Survivors.Infernus.SkillStates
{
    public class Afterburn : BaseSkillState
    {
        private float maxDuration = 8.0f;
        private bool windowPassed;
        private static Dictionary<RoR2.HealthComponent, float> afterburnTimers = new Dictionary<RoR2.HealthComponent, float>();
        private static LinkedList<RoR2.HealthComponent> flameDashVictims = new LinkedList<RoR2.HealthComponent>();

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            afterburnUpdate();

            windowPassed = false;

            if (fixedAge >= 0.5f) fixedAge -= 0.5f; windowPassed = true;
            if (windowPassed != false)
            {
                populateDashAttacks();
                dealDamageBurn();
                dealDamageDash();

                if (isAuthority && inWindow)
                {
                    // Lerp the proxy outward along the locked aim ray
                    float t = Mathf.InverseLerp(attackStart, attackEnd, fixedAge);
                    float offset = Mathf.Lerp(splashStartOffset, splashEndOffset, t);
                    splashProxy.position = lockedOrigin + lockedAimDirection * offset;

                    FireSplashTick();
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        /// Runs a Physics.OverlapBox at the proxy position every fixed frame
        /// during the attack window. Already-hit targets are skipped so each
        /// enemy takes damage at most once per cast.
        /// 
        public void afterburnUpdate()
        {
            foreach(Dictionary<RoR2.HealthComponent, float> e in afterburnTimers)
            {

            }
        }

        public void dealDamageBurn()
        {

        }

        public void dealDamageDash()
        {

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
                if (hc == null || !hc.alive) continue;
                if (hurtBox.teamIndex == GetTeam()) continue; // no friendly fire
                if (!hitTargets.Contains(hc))
                {
                    // Apply napalm debuff
                    CharacterBody body = hc.body;
                    if (body != null) body.AddTimedBuff(InfernusDebuffs.napalmDebuff, napalmDebuffDuration);
                    dealDamageConstructed(hc);
                    hitTargets.Add(hc);
                }
            }
        }

        public void dealDamageConstructed(HealthComponent healthComponentDmg)
        {
            // Deal damage once
            DamageInfo info = new DamageInfo
            {
                attacker = gameObject,
                inflictor = gameObject,
                damage = InfernusStaticValues.napalmDamageCoefficient * damageStat,
                procCoefficient = procCoefficient,
                position = healthComponentDmg.transform.position,
                force = lockedAimDirection * pushForce,
                crit = overlapAttack.isCrit,
                damageType = DamageType.Generic,
                damageColorIndex = DamageColorIndex.Default,
            };

            healthComponentDmg.TakeDamage(info);
            GlobalEventManager.instance.OnHitEnemy(info, healthComponentDmg.gameObject);
            GlobalEventManager.instance.OnHitAll(info, healthComponentDmg.gameObject);
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
    }
}
