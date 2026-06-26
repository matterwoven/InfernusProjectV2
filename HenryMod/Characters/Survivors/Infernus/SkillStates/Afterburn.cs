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
    public class AfterburnData
    {
        public float remaining;
        public float tickAccumulator;

        public AfterburnData(float initialDuration)
        {
            remaining = initialDuration;
            tickAccumulator = 0f;
        }
    }

    public class DashStandData
    {
        // True if a zone reported this target as "still standing" THIS frame.
        // Afterburn.FixedUpdate reads it, then clears it before the zones run again.
        public bool presentThisFrame;
        public float tickAccumulator;

        public DashStandData()
        {
            presentThisFrame = true;
            tickAccumulator = 0f;
        }
    }

    public class Afterburn : MonoBehaviour
    {
        public float procCoefficientAfterburn = 0.0f;
        public float procCoefficientDash = 0.0f;
        public float maxDuration = 8.0f;

        private const float PollInterval = 0.1f;
        private const float TickThreshold = 0.5f;

        private float tankMult = 1f;
        private float dashTickAccumulator;
        private float bodyDmgStat;
        private float damagePreCoefficient;
        private CharacterBody ownerBody;
        private GameObject ownerObject;
        private bool isCrit;

        private Dictionary<RoR2.HealthComponent, AfterburnData> afterburnTimers = new Dictionary<RoR2.HealthComponent, AfterburnData>();
        private Dictionary<RoR2.HealthComponent, DashStandData> dashStandTimers = new Dictionary<RoR2.HealthComponent, DashStandData>();

        private readonly List<RoR2.HealthComponent> toRemove = new List<RoR2.HealthComponent>();
        private readonly List<RoR2.HealthComponent> toDamageThisPoll = new List<RoR2.HealthComponent>();

        private readonly List<RoR2.HealthComponent> standersToRemove = new List<RoR2.HealthComponent>();
        private readonly List<RoR2.HealthComponent> standersToDamageThisPoll = new List<RoR2.HealthComponent>();

        public void Init(CharacterBody body)
        {
            ownerBody = body;
            ownerObject = body.gameObject;
        }

        public void FixedUpdate()
        {
            if (ownerBody == null) return;

            float dt = Time.fixedDeltaTime;

            dashTickAccumulator += PollInterval;
            if (dashTickAccumulator >= TickThreshold)
            {
                updateTankCount();
                dashTickAccumulator -= TickThreshold;
                bodyDmgStat = ownerBody.damageFromRecalculateStats;
                damagePreCoefficient = bodyDmgStat * tankMult * 0.5f;
            }

            afterburnUpdate(dt);
            dashStandUpdate(dt);
        }

        private void afterburnUpdate(float dt)
        {
            toRemove.Clear();
            toDamageThisPoll.Clear();

            foreach (RoR2.HealthComponent hc in afterburnTimers.Keys)
            {
                AfterburnData data = afterburnTimers[hc];

                if (!hc.alive)
                {
                    toRemove.Add(hc);
                    continue;
                }

                data.remaining -= dt;
                if (data.remaining <= 0f)
                {
                    toRemove.Add(hc);
                    continue;
                }

                data.tickAccumulator += dt;
                if (data.tickAccumulator >= TickThreshold)
                {
                    data.tickAccumulator -= TickThreshold;
                    toDamageThisPoll.Add(hc);
                }
            }

            foreach (RoR2.HealthComponent hc in toRemove)
                RemoveBurn(hc);

            if (toDamageThisPoll.Count > 0)
            {
                foreach (RoR2.HealthComponent hc in toDamageThisPoll)
                {
                    dealDamageBurn(hc);
                }
            }
        }

        public void addBurnTarget(RoR2.HurtBox hurtBox)
        {
            addBurnTarget(hurtBox.healthComponent);
        }

        public void addBurnTarget(RoR2.HealthComponent hc)
        {
            if (!afterburnTimers.ContainsKey(hc))
            {
                afterburnTimers[hc] = new AfterburnData(5.0f);
            }
            else
            {
                afterburnTimers[hc].remaining = Mathf.Min(afterburnTimers[hc].remaining + 0.5f, maxDuration);
            }
        }

        public void refreshBurnTarget(RoR2.HealthComponent hc)
        {
            if (afterburnTimers.ContainsKey(hc))
            {
                afterburnTimers[hc].remaining = 5.0f;
            }
        }

        public void addBurnTargetCrit(RoR2.HurtBox hurtBox)
        {
            addBurnTargetCrit(hurtBox.healthComponent);
        }

        public void addBurnTargetCrit(RoR2.HealthComponent hc)
        {
            if (!afterburnTimers.ContainsKey(hc))
            {
                afterburnTimers.Add(hc, new AfterburnData(maxDuration));
            }
            else
            {
                AfterburnData record = afterburnTimers[hc];
                record.remaining = Mathf.Min(record.remaining + 1.0f, maxDuration);
            }
        }

        public void dealDamageBurn(HurtBox hurt)
        {
            dealDamageBurn(hurt.healthComponent);
        }

        public void dealDamageBurn(HealthComponent a)
        {
            if (a == null || !a.alive) return;

            isCrit = ownerBody.RollCrit();
            DamageInfo info = new DamageInfo
            {
                attacker = ownerObject,
                inflictor = ownerObject,
                damage = InfernusStaticValues.afterburnDamageCoefficient * damagePreCoefficient,
                procCoefficient = procCoefficientAfterburn,
                position = a.transform.position,
                crit = false,
                damageType = DamageType.DoT,
                damageColorIndex = DamageColorIndex.Default,
            };

            a.TakeDamage(info);
            GlobalEventManager.instance.OnHitEnemy(info, a.gameObject);
            GlobalEventManager.instance.OnHitAll(info, a.gameObject);
        }

        private void RemoveBurn(RoR2.HealthComponent hc)
        {
            if (hc == null || !hc.alive) return;

            afterburnTimers.Remove(hc);

            CharacterBody victimBody = hc.body;
            if (victimBody.HasBuff(InfernusDebuffs.afterburnDebuff))
            {
                victimBody.RemoveBuff(InfernusDebuffs.afterburnDebuff);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Dash standing damage — presence-based, ticks every 0.5s for as
        // long as a zone keeps reporting the target as still standing.
        // Mirrors afterburnUpdate's shape; "presentThisFrame" replaces
        // "remaining" as the expiry condition.
        // ════════════════════════════════════════════════════════════════

        private void dashStandUpdate(float dt)
        {
            standersToRemove.Clear();
            standersToDamageThisPoll.Clear();

            foreach (RoR2.HealthComponent hc in dashStandTimers.Keys)
            {
                DashStandData data = dashStandTimers[hc];

                if (!hc.alive || !data.presentThisFrame)
                {
                    standersToRemove.Add(hc);
                    continue;
                }

                data.tickAccumulator += dt;
                if (data.tickAccumulator >= TickThreshold)
                {
                    data.tickAccumulator -= TickThreshold;
                    standersToDamageThisPoll.Add(hc);
                }

                // Reset for next frame; a zone must re-flag it via notifyStanding
                // or it's considered "left" by the time this runs again.
                data.presentThisFrame = false;
            }

            foreach (RoR2.HealthComponent hc in standersToRemove)
                dashStandTimers.Remove(hc);

            if (standersToDamageThisPoll.Count > 0)
            {
                isCrit = ownerBody.RollCrit();
                foreach (RoR2.HealthComponent hc in standersToDamageThisPoll)
                {
                    dealDamageDash(hc);
                }
            }
        }

        public void notifyStanding(RoR2.HurtBox hurtBox)
        {
            notifyStanding(hurtBox.healthComponent);
        }

        public void notifyStanding(RoR2.HealthComponent hc)
        {
            if (hc == null) return;

            if (!dashStandTimers.TryGetValue(hc, out DashStandData data))
            {
                dashStandTimers[hc] = new DashStandData();
            }
            else
            {
                data.presentThisFrame = true;
            }
        }

        public void dealDamageDash(HurtBox damagedHurtBox)
        {
            dealDamageDash(damagedHurtBox.healthComponent);
        }

        public void dealDamageDash(HealthComponent a)
        {
            if (a == null || !a.alive) return;

            DamageInfo info = new DamageInfo
            {
                attacker = ownerObject,
                inflictor = ownerObject,
                damage = InfernusStaticValues.dashDamageCoefficient * damagePreCoefficient,
                procCoefficient = procCoefficientDash,
                position = a.transform.position,
                crit = false,
                damageType = DamageType.AOE,
                damageColorIndex = DamageColorIndex.Default,
            };

            a.TakeDamage(info);
            GlobalEventManager.instance.OnHitEnemy(info, a.gameObject);
            GlobalEventManager.instance.OnHitAll(info, a.gameObject);
        }

        private void updateTankCount()
        {
            tankMult = 1f + ownerBody.inventory.GetItemCountEffective(DLC1Content.Items.StrengthenBurn);
        }
    }
}