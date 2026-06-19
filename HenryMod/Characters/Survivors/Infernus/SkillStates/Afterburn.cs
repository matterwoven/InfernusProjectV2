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
        public float remaining;
        public float tickAccumulator;

        public DashStandData(float nextStandCheck)
        {
            remaining = nextStandCheck;
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
        private int currentFixedFrame;
        private float dashTickAccumulator;
        private float fixedAge;
        private float bodyDmgStat;
        private float damagePreCoefficient;
        private CharacterBody ownerBody;
        private GameObject ownerObject;
        private bool isCrit;

        private readonly List<RoR2.HealthComponent> standersToRemove = new List<RoR2.HealthComponent>();
        private readonly List<RoR2.HealthComponent> standersToDamageThisPoll = new List<RoR2.HealthComponent>();

        private Dictionary<RoR2.HealthComponent, AfterburnData> afterburnTimers = new Dictionary<RoR2.HealthComponent, AfterburnData>();
        private Dictionary<RoR2.HealthComponent, DashStandData> dashStandTimers = new Dictionary<RoR2.HealthComponent, DashStandData>();

        private LinkedList<RoR2.HealthComponent> flameDashVictims = new LinkedList<RoR2.HealthComponent>();

        private readonly List<RoR2.HealthComponent> toRemove = new List<RoR2.HealthComponent>();
        private readonly List<RoR2.HealthComponent> toDamageThisPoll = new List<RoR2.HealthComponent>();

        public void Init(CharacterBody body)
        {
            ownerBody = body;
            ownerObject = body.gameObject;
        }

        public void FixedUpdate()
        {
            currentFixedFrame = Time.frameCount;
            if (ownerBody == null) return;

            float dt = Time.fixedDeltaTime;

            refreshDashContacts();

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

            flameDashVictims.Clear();
        }

        private void refreshDashContacts()
        {
            foreach (RoR2.HealthComponent hc in flameDashVictims)
            {
                if (afterburnTimers.TryGetValue(hc, out AfterburnData data))
                {
                    data.remaining = maxDuration;
                }
            }
        }
        private void dashStandUpdate(float dt)
        {
            standersToRemove.Clear();
            standersToDamageThisPoll.Clear();

            foreach (RoR2.HealthComponent hc in dashStandTimers.Keys)
            {
                //check remaining, subtract time from remaining, if 0 deal damage
                DashStandData data = dashStandTimers[hc];

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
                if (data.tickAccumulator >= data.remaining)
                {
                    data.tickAccumulator -= data.remaining;
                    toDamageThisPoll.Add(hc);
                }
            }

            foreach (RoR2.HealthComponent hc in standersToRemove)
                dashStandTimers.Remove(hc);

            if (standersToDamageThisPoll.Count > 0)
            {
                bodyDmgStat = ownerBody.damageFromRecalculateStats;
                damagePreCoefficient = bodyDmgStat * tankMult * 0.5f;
                isCrit = ownerBody.RollCrit();
                foreach (RoR2.HealthComponent hc in standersToDamageThisPoll)
                {
                    dealDamageDash(hc);
                }
            }
        }

        private void afterburnUpdate(float dt)
        {
            toRemove.Clear();
            toDamageThisPoll.Clear();

            foreach(RoR2.HealthComponent hc in afterburnTimers.Keys)
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

        public void addDashTarget(RoR2.HurtBox hurtBox)
        {
            addDashTarget(hurtBox.healthComponent);
        }

        public void addDashTarget(RoR2.HealthComponent hc)
        {
            if (!flameDashVictims.Contains(hc))
            {
                flameDashVictims.AddFirst(hc);
                dealDamageDash(hc);
            }

            if (!afterburnTimers.ContainsKey(hc))
            {
                afterburnTimers[hc] = new AfterburnData(5.0f);
            }
        }
        public void addBurnTarget(RoR2.HurtBox hurtBox)
        {
            addBurnTarget(hurtBox.healthComponent);
        }

        public void notifyStanding(RoR2.HurtBox hurtBox)
        {
            notifyStanding(hurtBox.healthComponent);
        }

        public void notifyStanding(RoR2.HealthComponent hc)
        {
            if (!dashStandTimers.ContainsKey(hc))
            {
                dealDamageDash(hc);
                dashStandTimers[hc] = new DashStandData(0.5f);
            }
            //Adds duration if called on target
            else
            {
                dashStandTimers[hc].remaining = Mathf.Min(dashStandTimers[hc].remaining + 0.5f, 0.5f);
            }
        }

        public void addBurnTarget(RoR2.HealthComponent hc)
        {
            if (!afterburnTimers.ContainsKey(hc))
            {
                afterburnTimers[hc] = new AfterburnData(5.0f);
            }
            //Adds duration if called on target
            else
            {
                afterburnTimers[hc].remaining = Mathf.Min(afterburnTimers[hc].remaining + 0.5f, maxDuration);
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
            //Adds duration if called on target
            else
            {
                AfterburnData record = afterburnTimers[hc];
                record.remaining = Mathf.Min(record.remaining + 1.0f, maxDuration);
            }
        }

        private void dealDamageBurn()
        {
            foreach (HealthComponent a in afterburnTimers.Keys)
            {
                dealDamageBurn(a);
            }
        }

        public void dealDamageBurn(HurtBox hurt)
        {
            dealDamageBurn(hurt.healthComponent);
        }

        public void dealDamageBurn(HealthComponent a)
        {
            //Single target version by healthComponent value
            //For outside use

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

        private void updateTankCount()
        {
            tankMult = 1f + ownerBody.inventory.GetItemCountEffective(DLC1Content.Items.StrengthenBurn);
        }

        private void dealDamageDash()
        {
            isCrit = ownerBody.RollCrit();
            foreach (HealthComponent a in flameDashVictims)
            {
                dealDamageDash(a);
            }
        }

        public void dealDamageDash(HurtBox damagedHurtBox)
        {
            dealDamageDash(damagedHurtBox.healthComponent);
        }

        public void dealDamageDash(HealthComponent a)
        {
            if (a == null || !a.alive) return;
            // Deal damage once
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

    }
}
