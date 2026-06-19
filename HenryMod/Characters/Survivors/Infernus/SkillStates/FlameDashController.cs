using InfernusMod.Characters.Survivors.Infernus.SkillStates;
using RoR2;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace InfernusMod.Characters.Survivors.Infernus.SkillStates
{
    /// <summary>
    /// Heavily overhauled logic for single-target damage management
    /// </summary>
    internal class FlameDashController
    {
        public class FlameDashZone : MonoBehaviour
        {
            private GameObject attacker;
            private CharacterBody ownerBody;
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
            private Afterburn afterBurnController;

            public void Initialize(
                GameObject attacker,
                CharacterBody ownerBody,
                TeamIndex teamIndex,
                float damage,
                float procCoefficient,
                Vector3 halfExtents,
                float lifetime,
                float tickInterval)
            {
                this.attacker = attacker;
                this.ownerBody = ownerBody;
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

                // Cache the passive controller from the owner
                if (ownerBody != null)
                    afterBurnController = ownerBody.GetComponent<Afterburn>();
                if (ownerBody == null)
                    Chat.AddMessage("Hey that damn controller is null dangnabbit");

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

            private void FixedUpdate()
            {
                age += Time.fixedDeltaTime;

                Collider[] cols = Physics.OverlapBox(
                    transform.position,
                    halfExtents,
                    Quaternion.identity,
                    LayerIndex.entityPrecise.mask
                );
                foreach (Collider col in cols.Distinct())
                {
                    HurtBox hurtBox = col.GetComponent<HurtBox>();
                    if (hurtBox == null) continue;

                    HealthComponent hc = hurtBox.healthComponent;
                    if (hc == null || !hc.alive) continue;
                    if (hc.gameObject == attacker) continue;

                    TeamComponent tc = hc.GetComponent<TeamComponent>();
                    if (tc != null && tc.teamIndex == teamIndex) continue;
                    afterBurnController.notifyStanding(hc);
                    afterBurnController.refreshBurnTarget(hc);
                }

                if (age >= lifetime)
                    Destroy(gameObject);
            }
        }
    }
}