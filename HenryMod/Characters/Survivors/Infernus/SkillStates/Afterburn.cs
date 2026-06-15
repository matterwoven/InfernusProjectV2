using EntityStates;
using InfernusMod.Survivors.Infernus;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace InfernusMod.Characters.Survivors.Infernus.SkillStates
{
    public class Afterburn : BaseSkillState
    {
        public float procCoefficientAfterburn = 1.0f;
        public float procCoefficientDash = 1.0f;
        private float maxDuration = 8.0f;
        private bool windowPassed;
        private static Dictionary<RoR2.HealthComponent, float> afterburnTimers = new Dictionary<RoR2.HealthComponent, float>();
        private static LinkedList<RoR2.HealthComponent> flameDashVictims = new LinkedList<RoR2.HealthComponent>();

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            windowPassed = false;

            if (fixedAge >= 0.5f) fixedAge -= 0.5f; windowPassed = true;
            if (windowPassed != false)
            {
                foreach(HealthComponent a in afterburnTimers.Keys)
                {
                    //Update timers
                    afterburnTimers.Add(a, afterburnTimers.GetValueOrDefault(a, 0f) - fixedAge);
                }
                dealDamageBurn();
                dealDamageDash();
            }

            afterburnUpdate();
            flameDashVictims.Clear();
        }

        // ════════════════════════════════════════════════════════════════════
        /// Runs a Physics.OverlapBox at the proxy position every fixed frame
        /// during the attack window. Already-hit targets are skipped so each
        /// enemy takes damage at most once per cast.
        /// 
        private void afterburnUpdate()
        {
            foreach(RoR2.HealthComponent hc in afterburnTimers.Keys)
            {
                if (flameDashVictims.Contains(hc))
                {
                    afterburnTimers.Add(hc, maxDuration);
                }
                if(afterburnTimers.GetValueOrDefault(hc, 0.0f) < 0f)
                {
                    afterburnTimers.Remove(hc);
                }
            }
        }

        public void addDashTarget(RoR2.HurtBox hurtBox)
        {
            if (!flameDashVictims.Contains(hurtBox.healthComponent))
            {
                flameDashVictims.AddFirst(hurtBox.healthComponent);
            }
        }

        public void addDashTarget(RoR2.HealthComponent hc)
        {
            if (!flameDashVictims.Contains(hc))
            {
                flameDashVictims.AddFirst(hc);
            }
        }

        private void dealDamageBurn()
        {
            foreach (HealthComponent a in afterburnTimers.Keys)
            {
                // Deal damage once
                DamageInfo info = new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = InfernusStaticValues.afterburnDamageCoefficient * damageStat,
                    procCoefficient = procCoefficientAfterburn,
                    position = a.transform.position,
                    crit = false,
                    damageType = DamageType.Generic,
                    damageColorIndex = DamageColorIndex.Default,
                };

                a.TakeDamage(info);
                GlobalEventManager.instance.OnHitEnemy(info, a.gameObject);
                GlobalEventManager.instance.OnHitAll(info, a.gameObject);
            }
        }

        public void dealDamageBurn(HealthComponent a)
        {
            //Single target version by healthComponent value
            //For outside use
            DamageInfo info = new DamageInfo
            {
                attacker = gameObject,
                inflictor = gameObject,
                damage = InfernusStaticValues.afterburnDamageCoefficient * damageStat,
                procCoefficient = procCoefficientAfterburn,
                position = a.transform.position,
                crit = false,
                damageType = DamageType.Generic,
                damageColorIndex = DamageColorIndex.Default,
            };

            a.TakeDamage(info);
            GlobalEventManager.instance.OnHitEnemy(info, a.gameObject);
            GlobalEventManager.instance.OnHitAll(info, a.gameObject);
        }



        private void dealDamageDash()
        {
            foreach (HealthComponent a in flameDashVictims)
            {
                // Deal damage once
                DamageInfo info = new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = InfernusStaticValues.dashDamageCoefficient * damageStat,
                    procCoefficient = procCoefficientDash,
                    position = a.transform.position,
                    crit = false,
                    damageType = DamageType.Generic,
                    damageColorIndex = DamageColorIndex.Default,
                };

                a.TakeDamage(info);
                GlobalEventManager.instance.OnHitEnemy(info, a.gameObject);
                GlobalEventManager.instance.OnHitAll(info, a.gameObject);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            //No entrance logic for this passive
            //Once you have anims PlayAnimation();

            //Once you have the audio Util.PlaySound("InfernusNapalm", gameObject);
        }
    }
}
