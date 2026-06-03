using System.Collections.Generic;
using RoR2;
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
            private GameObject  attacker;
            private TeamIndex   teamIndex;
            private float       damage;
            private float       procCoefficient;
            private float       lifetime;
            private float       tickInterval;
            private Vector3     halfExtents;


            private float age;
            private float timeSinceTick;

            //Removing duplicate enemy marks
            private HashSet<HealthComponent> hitThisTick = new HashSet<HealthComponent>();

            private BoxCollider triggerCollider;

    
            public void Initialize(
                GameObject  attacker,
                TeamIndex   teamIndex,
                float       damage,
                float       procCoefficient,
                Vector3     halfExtents,
                float       lifetime,
                float       tickInterval)
            {
                this.attacker        = attacker;
                this.teamIndex       = teamIndex;
                this.damage          = damage;
                this.procCoefficient = procCoefficient;
                this.halfExtents     = halfExtents;
                this.lifetime        = lifetime;
                this.tickInterval    = tickInterval;

                //Trigger does not have collision like last projects early iterations
                triggerCollider           = gameObject.AddComponent<BoxCollider>();
                triggerCollider.isTrigger = true;
                triggerCollider.size      = halfExtents * 2f;

                CreateVisual();

                //Clock start on initialize, first tick fires after a full interval
                timeSinceTick = 0f;
            }
            #region ShaderVisuals
            //Shader stuff, new to me
            private void CreateVisual()
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(visual.GetComponent<Collider>()); //Removal of the collider
                visual.transform.SetParent(transform, false); //No parents
                visual.transform.localScale = halfExtents * 2f; //Scale management, feels better to me

                Renderer rend = visual.GetComponent<Renderer>();
                if (!rend) return;

                //Setting material settings with selection, set to transparent
                Material mat = new Material(Shader.Find("Standard"));


                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;

                mat.color = new Color(1f, 0.1f, 0f, 0.25f); // transparent red-orange

                rend.material             = mat;
                rend.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows       = false;
            }
            #endregion 
            private void FixedUpdate()
            {
                age           += Time.fixedDeltaTime;
                timeSinceTick += Time.fixedDeltaTime;

                if (timeSinceTick >= tickInterval)
                {
                    timeSinceTick -= tickInterval;
                    hitThisTick.Clear();
                    TickDamage();
                }

                if (age >= lifetime)
                    Destroy(gameObject);
            }
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
                    if (hc == null || !hc.alive)    continue;
                    if (hitThisTick.Contains(hc))   continue;
                    if (hc.gameObject == attacker)  continue; // no self-damage

                    // Skip allies
                    TeamComponent tc = hc.GetComponent<TeamComponent>();
                    if (tc != null && tc.teamIndex == teamIndex) continue;

                    hitThisTick.Add(hc);

                    DamageInfo info = new DamageInfo
                    {
                        attacker         = attacker,
                        inflictor        = gameObject,
                        damage           = damage,
                        procCoefficient  = procCoefficient,
                        position         = hc.transform.position,
                        force            = Vector3.zero,
                        crit             = false,
                        damageType       = DamageType.IgniteOnHit,
                        damageColorIndex = DamageColorIndex.Item,
                    };

                    hc.TakeDamage(info);
                    GlobalEventManager.instance.OnHitEnemy(info, hc.gameObject);
                    GlobalEventManager.instance.OnHitAll(info, hc.gameObject);
                }
            }
        }
    }
}
