using InfernusMod.Characters.Survivors.Infernus.SkillStates;
using RoR2;
using System.Collections.Generic;
using UnityEngine;

namespace InfernusMod.Characters.Survivors.Infernus.SkillStates
{
    /// <summary>
    /// Container class that keeps FlameDashZone out of the global namespace
    /// while staying accessible to FlameDash via a simple using directive.
    /// </summary>
    internal class FlameDashController
    {
        public class FlameDashZone : MonoBehaviour
        {
            private GameObject attacker;
            private TeamIndex teamIndex;
            private float damage;
            private float procCoefficient;
            private float lifetime;
            private float tickInterval;
            private Vector3 halfExtents;

            private float age;
            private float timeSinceTick;

            // Dedup within one tick
            private readonly HashSet<HealthComponent> hitThisTick = new HashSet<HealthComponent>();

            private BoxCollider triggerCollider;

            // Cached once on Initialize – avoids GetComponent every tick
            private Afterburn afterBurnController;

            // ════════════════════════════════════════════════════════════════
            // Init
            // ════════════════════════════════════════════════════════════════

            public void Initialize(
                GameObject attacker,
                TeamIndex teamIndex,
                float damage,
                float procCoefficient,
                Vector3 halfExtents,
                float lifetime,
                float tickInterval)
            {
                this.attacker = attacker;
                this.teamIndex = teamIndex;
                this.damage = damage;
                this.procCoefficient = procCoefficient;
                this.halfExtents = halfExtents;
                this.lifetime = lifetime;
                this.tickInterval = tickInterval;

                // Trigger collider – no physics collision
                triggerCollider = gameObject.AddComponent<BoxCollider>();
                triggerCollider.isTrigger = true;
                triggerCollider.size = halfExtents * 2f;

                // Cache the passive controller from the attacker
                if (attacker != null)
                    afterBurnController = attacker.GetComponent<Afterburn>();

                CreateVisual();

                // First tick fires after a full interval
                timeSinceTick = 0f;
            }

            // ════════════════════════════════════════════════════════════════
            // Visual
            // ════════════════════════════════════════════════════════════════
            #region ShaderVisuals
            private void CreateVisual()
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(visual.GetComponent<Collider>());
                visual.transform.SetParent(transform, false);
                visual.transform.localScale = halfExtents * 2f;

                Renderer rend = visual.GetComponent<Renderer>();
                if (!rend) return;
                // TODO: assign flame dash shader/material here
            }
            #endregion

            // ════════════════════════════════════════════════════════════════
            // Lifecycle
            // ════════════════════════════════════════════════════════════════
            private void FixedUpdate()
            {
                age += Time.fixedDeltaTime;

                Collider[] cols = Physics.OverlapBox(
                    transform.position,
                    halfExtents,
                    Quaternion.identity,
                    LayerIndex.entityPrecise.mask
                );

                foreach (Collider col in cols)
                {
                    HurtBox hurtBox = col.GetComponent<HurtBox>();
                    if (hurtBox == null) continue;

                    HealthComponent hc = hurtBox.healthComponent;
                    if (hc == null || !hc.alive) continue;
                    if (hc.gameObject == attacker) continue;

                    TeamComponent tc = hc.GetComponent<TeamComponent>();
                    if (tc != null && tc.teamIndex == teamIndex) continue;

                    afterBurnController?.notifyStanding(hc);
                }

                if (age >= lifetime)
                    Destroy(gameObject);
            }
            // ════════════════════════════════════════════════════════════════
            // Damage tick
            // ════════════════════════════════════════════════════════════════

            private void TickDamage()
            {
                Collider[] cols = Physics.OverlapBox(
                    transform.position,
                    halfExtents,
                    Quaternion.identity,
                    LayerIndex.entityPrecise.mask
                );

                foreach (Collider col in cols)
                {
                    HurtBox hurtBox = col.GetComponent<HurtBox>();
                    if (hurtBox == null) continue;

                    HealthComponent hc = hurtBox.healthComponent;
                    if (hc == null || !hc.alive) continue;
                    //if (hitThisTick.Contains(hc)) continue;
                    if (hc.gameObject == attacker) continue; // no self-damage

                    // Skip allies
                    TeamComponent tc = hc.GetComponent<TeamComponent>();
                    if (tc != null && tc.teamIndex == teamIndex) continue;

                    //hitThisTick.Add(hc);

                    // ── Bridge: tell AfterBurnController this enemy was hit ──
                    // This refreshes their burn timer and registers them for
                    // the dash damage tick in AfterBurnController.DealDamageDash()
                    afterBurnController?.notifyStanding(hc);

                    // ── Direct zone damage (immediate, handled here) ──
                    //DamageInfo info = new DamageInfo
                    //{
                        //attacker = attacker,
                        //inflictor = gameObject,
                        //damage = damage,
                        //procCoefficient = procCoefficient,
                        //position = hc.transform.position,
                        //force = Vector3.zero,
                        //crit = false,
                        //damageType = DamageType.IgniteOnHit,
                        //damageColorIndex = DamageColorIndex.Item,
                    //};

                    //hc.TakeDamage(info);
                    //GlobalEventManager.instance.OnHitEnemy(info, hc.gameObject);
                    //GlobalEventManager.instance.OnHitAll(info, hc.gameObject);
                }
            }
        }
    }
}