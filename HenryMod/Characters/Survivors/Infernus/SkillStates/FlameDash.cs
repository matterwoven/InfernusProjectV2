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
        //Dash duration (seconds)
        public static float duration = 3f;

        //Start speed fast
        public static float initialSpeedCoefficient = 3.8f;
        //Coast to a slower speed towards end
        public static float finalSpeedCoefficient = 4.2f;
        //W key multiplier
        public static float forwardMultiplier = 1.2f;

        //Flame dash only moves forward in Deadlock, but slows with WASD movement except for acceleration with W interestingly
        //Turn radius makes it so that while turning, you move slower to bank turns
        public static float speedLostPer45Degrees = 0.5f;

        //Turn rate towards aim direction in degrees
        public static float turnRateDegPerSec = 250f;

        //Flame Dash zone damage
        public static float damageCoefficient = 1f * InfernusStaticValues.dashDamageCoefficient;
        //Flame Dash spawn distance (how far apart) 
        public static float dashZoneInterval = 3f;
        //Flame Dash zone duration (seconds)
        public static float zoneLifetime = 5f;
        //Flame Dash damage tick timing (seconds)
        public static float zoneDamageInterval = 0.5f;
        //Zone size
        public static Vector3 zoneHalfExtents = new Vector3(1.5f, 1.5f, 1.5f);

        //Audio and visual effects
        public static string dodgeSoundString = "InfernusRoll";
        public static float dodgeFOV = global::EntityStates.Commando.DodgeState.dodgeFOV;

        //In-function definitions
        private bool accelerationPressed;
        private bool brakesPressed;
        private Vector3 dashDirection;
        private Animator animator;
        private Vector3 previousPosition;
        private float dashSpeed;
        private float speedThisFrame;
        private float distanceSinceLastZone;

        public override void OnEnter()
        {
            //Enter skill
            base.OnEnter();
            //Nab animator, enables anims
            animator = GetModelAnimator();

            //Is usable/other actions not inhibiting
            if (isAuthority)
            {
                //If there are is an input stream
                if (base.inputBank)
                {
                    this.dashDirection = base.inputBank.aimDirection;
                    this.dashDirection.y = 0f;
                    this.dashDirection.Normalize();
                }
            }

            //Model tracks to the floor like MulT
            if (base.modelLocator) base.modelLocator.normalizeToFloor = true;


            //Set velocity to dash calculated velocity
            if (base.characterDirection) base.characterDirection.forward = this.dashDirection;

            //Setting previous position for dash tracking, value setup
            previousPosition = transform.position;
            distanceSinceLastZone = 0f;

            //This should be the infernus dash SFX and voiceline with animation
            PlayAnimation("FullBody, Override", "Roll", "Roll.playbackRate", duration);
            Util.PlaySound(dodgeSoundString, gameObject);
        }

        private void inputMovementManager()
        {
            if (inputBank.rawMoveUp.justPressed)    accelerationPressed = true;
            if (inputBank.rawMoveUp.justReleased)   accelerationPressed = false;
            if (inputBank.rawMoveDown.justPressed)  brakesPressed = true;
            if (inputBank.rawMoveDown.justReleased) brakesPressed = false;
        }

        private float applyAccelerationMultiplier(float speed)
        {
            //Check for forward/back press
            inputMovementManager();
            if (isAuthority && base.inputBank)
            {
                Vector2 move = Util.Vector3XZToVector2XY(base.inputBank.moveVector);
                if (move.sqrMagnitude > 0.01f)
                {
                    Vector3 moveDir = new Vector3(move.x, 0f, move.y).normalized;
                    float alignment = Vector3.Dot(moveDir, dashDirection);
                    if (alignment > 0.7f && accelerationPressed)
                        speed *= forwardMultiplier;
                }
            }
            return speed;
        }

        private float applyTurnPenalty(float speed)
        {
            //Turns are more difficult the more the difference between your movement and the last direction, making arcs
            if (isAuthority && base.inputBank)
            {
                Vector3 aimFlat = base.inputBank.aimDirection;
                aimFlat.y = 0f;
                if (aimFlat.sqrMagnitude > 0.01f)
                {
                    aimFlat.Normalize();
                    float angleDeg = Vector3.Angle(dashDirection, aimFlat);
                    float penaltyUnits = (angleDeg / 45f) * speedLostPer45Degrees;
                    speed = Mathf.Max(0f, speed - moveSpeedStat * penaltyUnits);
                }
            }
            return speed;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            float transition = fixedAge / duration;

            //Update Direction
            if (isAuthority && base.inputBank)
            {
                Vector3 targetDir = base.inputBank.aimDirection;
                targetDir.y = 0f;
                if (targetDir.sqrMagnitude > 0.01f)
                {
                    targetDir.Normalize();
                    float maxRadThisFrame = turnRateDegPerSec * Mathf.Deg2Rad * Time.fixedDeltaTime;
                    dashDirection = Vector3.RotateTowards(dashDirection, targetDir, maxRadThisFrame, 0f);
                }
            }
            
            //Base speed
            speedThisFrame = moveSpeedStat * Mathf.Lerp(initialSpeedCoefficient, finalSpeedCoefficient, transition);

            //Cancel button
            if (inputBank.jump.justPressed)
            {
                outer.SetNextStateToMain();
            }

            applyAccelerationMultiplier(speedThisFrame);
            if (brakesPressed == true)  speedThisFrame *= 0.6f; 
            applyTurnPenalty(speedThisFrame);


            //Adjusts directional speed based on current speed over time against movement direction
            if (characterDirection)     characterDirection.forward = dashDirection; 

            //Camera FOV changes with speed now
            if (cameraTargetParams)     cameraTargetParams.fovOverride = Mathf.Lerp(dodgeFOV, 60f, transition);

            //With gravity, get speed and velocity
            if(characterMotor)
            {
                Vector3 velocity = dashDirection * speedThisFrame;
                velocity.y = characterMotor.velocity.y;
                characterMotor.velocity = velocity;
            }
            
            //Place Flame Dash zones
            if(isAuthority && NetworkServer.active)
            {
                float distThisFrame = Vector3.Distance(transform.position, previousPosition);
                distanceSinceLastZone += distThisFrame;

                while (distanceSinceLastZone >= dashZoneInterval)
                {
                    distanceSinceLastZone -= dashZoneInterval;
                    SpawnFlameZone(previousPosition);
                }
            }

            //Note last position in frame
            previousPosition = transform.position;

            //End
            if (isAuthority && (fixedAge >= duration || transition >= 0.9))
            {
                outer.SetNextStateToMain();
                return;
            }
        }

        public override void OnExit()
        {
            //Model stops tracking to the floor like MulT
            if (base.modelLocator) base.modelLocator.normalizeToFloor = false;
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