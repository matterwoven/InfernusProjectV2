using EntityStates;
using InfernusMod.Characters.Survivors.Infernus.SkillStates;
using InfernusMod.Survivors.Infernus;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace InfernusMod.Survivors.Infernus.SkillStates
{
    public class FlameDash : BaseSkillState
    {
        public static float duration = 3f;

        public static float initialSpeedCoefficient = 5f;
        public static float finalSpeedCoefficient = 2.5f;
        public static float forwardMultiplier = 1.4f;

        public static float damageCoefficient = 1f * InfernusStaticValues.dashDamageCoefficient;

        public static float dashZoneInterval = 3f;
        public static float zoneLifetime = 5f;
        public static float zoneDamageInterval = 0.5f;
        public static Vector3 zoneHalfExtents = new Vector3(1.5f, 1.5f, 1.5f);

        public static string dodgeSoundString = "InfernusRoll";
        public static float dodgeFOV = global::EntityStates.Commando.DodgeState.dodgeFOV;

        private float distanceSinceLastZone;
        private Vector3 idealDirection;
        private float rollSpeed;
        private Vector3 dashDirection;
        private Animator animator;
        private Vector3 previousPosition;

        public override void OnEnter()
        {
            base.OnEnter();
            animator = GetModelAnimator();


            if (isAuthority)
            {
                if (base.inputBank)
                {
                    this.dashDirection = base.inputBank.aimDirection;
                    this.dashDirection.y = 0f;
                    this.dashDirection.Normalize();
                    this.UpdateDirection();
                }
            }

            if (base.modelLocator)
            {
                base.modelLocator.normalizeToFloor = true;
            }

            if (base.characterDirection)
            {
                base.characterDirection.forward = this.dashDirection;
            }

            previousPosition = transform.position;
            distanceSinceLastZone = 0f;

            PlayAnimation("FullBody, Override", "Roll", "Roll.playbackRate", duration);
            Util.PlaySound(dodgeSoundString, gameObject);
        }
        private void UpdateDirection()
        {
            if (base.inputBank)
            {
                Vector2 move = Util.Vector3XZToVector2XY(base.inputBank.moveVector);
                if (move.sqrMagnitude > 0.01f)
                {
                    move.Normalize();
                    dashDirection = new Vector3(move.x, 0f, move.y).normalized;
                }

            }
        }
        private void RecalculateRollSpeed()
        {
            rollSpeed = moveSpeedStat * Mathf.Lerp(initialSpeedCoefficient, finalSpeedCoefficient, fixedAge / duration);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            RecalculateRollSpeed();

            float speedThisFrame = rollSpeed;
            if (isAuthority && base.inputBank)
            {
                Vector2 move = Util.Vector3XZToVector2XY(base.inputBank.moveVector);
                if (move.sqrMagnitude > 0.01f)
                {
                    Vector3 moveDir = new Vector3(move.x, 0f, move.y).normalized;
                    float alignment = Vector3.Dot(moveDir, dashDirection); // -1..1
                    if (alignment > 0.7f)
                        speedThisFrame *= forwardMultiplier;
                }
            }

            //Adjusting directional speed based on current speed over time against movement direction
            //Considering forward to be adjusted direction
            if (characterDirection) { 
                characterDirection.forward = dashDirection; 
            }
            //Camera FOV changes with speed now
            if (cameraTargetParams)
            {
                cameraTargetParams.fovOverride = Mathf.Lerp(dodgeFOV, 60f, fixedAge / duration);
            }
            //With gravity, get speed and velocity
            if(characterMotor)
            {
                Vector3 velocity = dashDirection * finalSpeedCoefficient;
                velocity.y = characterMotor.velocity.y;
                characterMotor.velocity = velocity;
            }
            
            if(isAuthority && NetworkServer.active)
            {
                float distFromLastFrame = Vector3.Distance(transform.position, previousPosition);
                distanceSinceLastZone += distFromLastFrame;

                if(distanceSinceLastZone >= dashZoneInterval)
                {
                    distanceSinceLastZone -= dashZoneInterval;
                    SpawnFlameZone(previousPosition);
                }
            }

            previousPosition = transform.position;

            //End
            if (isAuthority && fixedAge >= duration)
            {
                outer.SetNextStateToMain();
                return;
            }
        }

        public override void OnExit()
        {
            if (cameraTargetParams) cameraTargetParams.fovOverride = -1f;
            if (characterMotor) characterMotor.disableAirControlUntilCollision = false;
            base.OnExit();
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(dashDirection);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            dashDirection = reader.ReadVector3();
        }

        private void SpawnFlameZone(Vector3 position)
        {
            // Centre the zone at chest height
            Vector3 spawnPos = position + Vector3.up * zoneHalfExtents.y;

            GameObject zoneObj = new GameObject("FlameDashZone");
            zoneObj.transform.position = spawnPos;

            FlameDashController.FlameDashZone zone =
                zoneObj.AddComponent<FlameDashController.FlameDashZone>();

            zone.Initialize(
                attacker: gameObject,
                teamIndex: GetTeam(),
                damage: damageStat * damageCoefficient,
                procCoefficient: damageCoefficient,
                halfExtents: zoneHalfExtents,
                lifetime: zoneLifetime,
                tickInterval: zoneDamageInterval
            );

            NetworkServer.Spawn(zoneObj);
        }
    }
}