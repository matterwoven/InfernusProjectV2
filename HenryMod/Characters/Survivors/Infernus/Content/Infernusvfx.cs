using System.Collections.Generic;
using InfernusMod.Survivors.Infernus.Components;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace InfernusMod.Survivors.Infernus
{
    /// <summary>
    /// All cosmetic effects for Infernus. Everything here is LOCAL (not networked): call it from code that runs
    /// on every machine (skill state OnEnter/FixedUpdate WITHOUT an isAuthority / NetworkServer.active check).
    ///
    /// RoR2 resources used (verify paths with your addressables catalog / Unity Explorer if a warning is logged):
    ///   - HGCloudRemap shader        -> flame sphere
    ///   - OmniExplosionVFX prefab    -> special detonation burst
    ///   - LayerIndex.world           -> ground raycasts + particle collision
    /// Everything else (oil, footprints, scorch, cracks, ember sprites, flame noise, fire ramp) is generated
    /// procedurally at Init so there are no asset bundle dependencies.
    /// </summary>
    public static class InfernusVFX
    {
        #region RoR2 resources
        public const string CloudRemapShaderPath = "RoR2/Base/Shaders/HGCloudRemap.shader";
        public const string DetonationEffectPath = "RoR2/Base/Common/VFX/OmniExplosionVFX.prefab";

        public static Shader cloudRemapShader;
        public static GameObject detonationEffectPrefab;

        /// <summary>Optional: assign a material from your asset bundle (via ShaderSwapper) to replace the generated flame sphere look.</summary>
        public static Material flameSphereMaterial;
        #endregion

        #region tuning
        private const int MaxOilDecals = 90;
        private const int MaxFootprints = 48;
        private const int MaxScorches = 40;

        // HGCloudRemap knobs, tweak in game if the sphere is too faint / blown out
        private const float FlameBoost = 1.5f;
        private const float FlameAlphaBoost = 2f;
        #endregion

        private static bool initialized;

        private static Sprite softDotSprite, oilBlobSprite, oilSplatSprite, footprintSprite, scorchSprite, crackSprite, ringSprite;
        private static Texture2D flameGrayTexture, flameColorTexture, fireRampTexture;
        private static Material oilParticleMaterial, emberParticleMaterial;
        private static Mesh sphereMesh;

        private static readonly Queue<GameObject> oilDecals = new Queue<GameObject>();
        private static readonly Queue<GameObject> footprints = new Queue<GameObject>();
        private static readonly Queue<GameObject> scorches = new Queue<GameObject>();

        private struct OilStamp { public Vector3 position; public float time; }
        private static readonly List<OilStamp> recentOil = new List<OilStamp>();

        #region init
        public static void Init()
        {
            if (initialized) return;

            try
            {
                cloudRemapShader = TryLoad<Shader>(CloudRemapShaderPath);
                detonationEffectPrefab = TryLoad<GameObject>(DetonationEffectPath);

                BuildTextures();
                BuildParticleMaterials();

                GameObject prim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereMesh = prim.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.Destroy(prim);

                initialized = true;
            }
            catch (System.Exception e)
            {
                // cosmetics are optional: every public method early-outs while initialized == false
                Log.Error("InfernusVFX failed to initialize, visual effects disabled: " + e);
            }
        }

        public static T TryLoad<T>(string key) where T : UnityEngine.Object
        {
            try
            {
                T asset = Addressables.LoadAssetAsync<T>(key).WaitForCompletion();
                if (asset == null) Log.Warning("InfernusVFX: addressable returned null for " + key);
                return asset;
            }
            catch (System.Exception e)
            {
                Log.Warning("InfernusVFX: could not load '" + key + "' (" + e.Message + "). Falling back.");
                return null;
            }
        }
        #endregion

        #region public API
        /// <summary>Utility: one yellow glowing boot print. Call once per step.</summary>
        public static void SpawnFootprint(Vector3 footPosition, Vector3 moveDirection, bool leftFoot)
        {
            if (!initialized) return;

            Vector3 dir = Vector3.ProjectOnPlane(moveDirection, Vector3.up);
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            Vector3 side = Vector3.Cross(Vector3.up, dir) * (leftFoot ? -0.22f : 0.22f);
            RaycastHit hit;
            if (!GroundCast(footPosition + side + Vector3.up * 0.8f, 3f, out hit)) return;

            GroundDecal decal = NewDecal("InfernusFootprint", hit.point, hit.normal, dir, 0.32f, 0.025f);
            decal.Configure(0.08f);
            // soft yellow glow underneath, then the print on top
            decal.AddLayer(softDotSprite, 0, new Color(1f, 0.75f, 0.1f, 0.35f), new Color(1f, 0.4f, 0.05f, 0.35f), 1.2f, 2.8f, false, 2.2f);
            decal.AddLayer(footprintSprite, 1, Color.white, new Color(1f, 0.55f, 0.25f, 1f), 1.4f, 2.6f, !leftFoot, 1f);
            Track(footprints, decal.gameObject, MaxFootprints);
        }

        /// <summary>Utility: charred ground patch with glowing cracks that cool off, plus a few embers.</summary>
        public static void SpawnBurnSpot(Vector3 footPosition, float diameter = 2.6f)
        {
            if (!initialized) return;
            RaycastHit hit;
            if (!GroundCast(footPosition + Vector3.up * 0.8f, 3f, out hit)) return;

            StampScorch(hit.point, hit.normal, diameter, false);
            SpawnEmbers(hit.point + hit.normal * 0.15f, 8, diameter * 0.25f, 3f);
        }

        /// <summary>Secondary: start spraying oil. Keep the handle and call SetAim each frame; Stop() when the state ends.</summary>
        public static OilSprayEmitter SpawnOilSpray(Vector3 origin, Vector3 direction, float range, float duration, float particleLife = 0.55f, float gravity = 0.5f)
        {
            if (!initialized || oilParticleMaterial == null) return null;

            GameObject go = new GameObject("InfernusOilSpray");
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction : Vector3.forward));
            ParticleSystem ps = NewParticleSystem(go);

            float life = particleLife;
            float speed = range / life;   // no drag: max reach ~= range

            ParticleSystem.MainModule main = ps.main;
            main.duration = Mathf.Max(0.05f, duration);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.8f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.75f, speed * 1.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.0f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.05f, 0.03f, 0.07f, 0.95f), new Color(0.16f, 0.10f, 0.20f, 0.90f));
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = Mathf.Max(260f, 100f / main.duration); // short bursts still get ~100 droplets

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.08f;

            // the mist spreads out and thins as it travels = dissipates
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1.2f), new Keyframe(1f, 2.0f)));

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);

            // droplets that reach the ground die and stamp oil there (OilSprayEmitter.OnParticleCollision)
            ParticleSystem.CollisionModule collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.collidesWith = LayerIndex.world.mask;
            collision.dampen = 0f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;
            collision.radiusScale = 0.5f;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.sendCollisionMessages = true;

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = oilParticleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            OilSprayEmitter emitter = go.AddComponent<OilSprayEmitter>();
            emitter.Init(ps);
            ps.Play();
            return emitter;
        }

        /// <summary>Special: spherical flame indicator parented to the body. Call Detonate() when the blast resolves.</summary>
        public static ConcussiveSphereIndicator CreateFlameSphere(Transform parent, float radius, float growDuration = 0.25f, float footOffsetY = 0f)
        {
            if (!initialized || sphereMesh == null) return null;

            Material outer = BuildFlameMaterial(false);
            Material inner = BuildFlameMaterial(true);
            if (outer == null || inner == null) return null;

            GameObject go = new GameObject("InfernusFlameSphere");
            go.transform.SetParent(parent, false);
            ConcussiveSphereIndicator indicator = go.AddComponent<ConcussiveSphereIndicator>();
            indicator.Build(sphereMesh, outer, inner, radius, growDuration, footOffsetY);
            return indicator;
        }

        public static void StampOil(Vector3 point, Vector3 normal, float size)
        {
            if (!initialized || normal.y < 0.5f) return;

            // don't pile ten decals on the same spot within a single spray
            float now = Time.time;
            for (int i = recentOil.Count - 1; i >= 0; i--)
            {
                if (now - recentOil[i].time > 1f) { recentOil.RemoveAt(i); continue; }
                if ((recentOil[i].position - point).sqrMagnitude < 0.09f) return;
            }
            recentOil.Add(new OilStamp { position = point, time = now });

            GroundDecal decal = NewDecal("InfernusOil", point, normal, RandomTangent(normal), size, 0.02f);
            decal.Configure(0.12f);
            Color c = new Color(1f, 1f, 1f, 0.95f);
            decal.AddLayer(oilSplatSprite, 0, c, c, 9f, 5f, false, 1f);
            Track(oilDecals, decal.gameObject, MaxOilDecals);
        }

        /// <summary>Puddle on the ground below a point, for sprays that travel roughly level. Ignored if the ground is further than maxDrop.</summary>
        public static void StampOilBelow(Vector3 point, float maxDrop, float size)
        {
            if (!initialized) return;
            RaycastHit hit;
            if (GroundCast(point + Vector3.up * 0.5f, maxDrop + 0.5f, out hit))
                StampOil(hit.point, hit.normal, size);
        }

        public static void StampScorch(Vector3 point, Vector3 normal, float diameter, bool blast)
        {
            if (!initialized) return;

            GroundDecal decal = NewDecal(blast ? "InfernusBlastScorch" : "InfernusBurnSpot", point, normal, RandomTangent(normal), diameter, blast ? 0.03f : 0.02f);
            decal.Configure(blast ? 0.2f : 0.15f);

            Color soot = new Color(1f, 1f, 1f, 0.9f);
            decal.AddLayer(scorchSprite, 1, soot, soot, blast ? 6f : 7f, blast ? 6f : 4f, false, 1f);

            if (blast)
                decal.AddLayer(ringSprite, 2, new Color(1f, 0.7f, 0.2f, 1f), new Color(0.6f, 0.1f, 0.02f, 1f), 0.8f, 5f, false, 1f);
            else
                decal.AddLayer(crackSprite, 2, new Color(1f, 0.8f, 0.25f, 1f), new Color(0.7f, 0.1f, 0.02f, 1f), 1.2f, 4.5f, false, 1f);

            Track(scorches, decal.gameObject, MaxScorches);
        }

        public static void PlayDetonation(Vector3 position, float radius)
        {
            if (detonationEffectPrefab != null)
                EffectManager.SpawnEffect(detonationEffectPrefab, new EffectData { origin = position, scale = radius }, true); // false = local only
            SpawnEmbers(position, 40, radius * 0.5f, 9f);
        }

        public static void SpawnEmbers(Vector3 position, int count, float radius, float speed)
        {
            if (!initialized || emberParticleMaterial == null) return;

            GameObject go = new GameObject("InfernusEmbers");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.up)); // hemisphere pole = local +Z
            ParticleSystem ps = NewParticleSystem(go);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.05f, 1f), new Color(1f, 0.85f, 0.3f, 1f));
            main.gravityModifier = 0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(count, 8);
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = Mathf.Max(0.05f, radius);

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = emberParticleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
        }
        #endregion

        #region decal / particle helpers
        private static bool GroundCast(Vector3 origin, float distance, out RaycastHit hit)
        {
            if (Physics.Raycast(origin, Vector3.down, out hit, distance, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                return hit.normal.y > 0.4f;
            return false;
        }

        private static GroundDecal NewDecal(string name, Vector3 point, Vector3 normal, Vector3 upHint, float size, float lift)
        {
            Vector3 n = normal.normalized;
            Vector3 hint = Vector3.ProjectOnPlane(upHint, n);
            if (hint.sqrMagnitude < 0.0001f) hint = RandomTangent(n);

            GameObject go = new GameObject(name);
            // Sprites face local -Z, so look "into" the surface to make the sprite face out of it
            go.transform.SetPositionAndRotation(point + n * lift, Quaternion.LookRotation(-n, hint));
            go.transform.localScale = Vector3.one * size;
            return go.AddComponent<GroundDecal>();
        }

        private static Vector3 RandomTangent(Vector3 n)
        {
            Vector3 t = Vector3.Cross(n, Mathf.Abs(n.x) < 0.9f ? Vector3.right : Vector3.forward).normalized;
            return Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), n) * t;
        }

        private static void Track(Queue<GameObject> queue, GameObject go, int max)
        {
            queue.Enqueue(go);
            while (queue.Count > max)
            {
                GameObject old = queue.Dequeue();
                if (old) UnityEngine.Object.Destroy(old);
            }
        }

        private static ParticleSystem NewParticleSystem(GameObject go)
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // must be stopped before touching duration etc.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static void BuildParticleMaterials()
        {
            oilParticleMaterial = MakeSpriteShaderMaterial(oilBlobSprite.texture);
            emberParticleMaterial = MakeSpriteShaderMaterial(softDotSprite.texture);
        }

        private static Shader spriteShader;

        // RoR2BepInExPack hooks Shader.Find and throws for names it has no GUID for (e.g. "Sprites/Default"),
        // so borrow the shader from the material Unity gives every SpriteRenderer instead.
        private static Shader GetSpriteShader()
        {
            if (spriteShader != null) return spriteShader;
            GameObject temp = new GameObject("InfernusShaderProbe");
            try
            {
                SpriteRenderer sr = temp.AddComponent<SpriteRenderer>();
                if (sr.sharedMaterial != null) spriteShader = sr.sharedMaterial.shader;
            }
            finally
            {
                UnityEngine.Object.Destroy(temp);
            }
            return spriteShader;
        }

        private static Material MakeSpriteShaderMaterial(Texture tex)
        {
            Shader s = GetSpriteShader();
            if (s == null) { Log.Warning("InfernusVFX: default sprite shader not available."); return null; }
            Material m = new Material(s);
            m.mainTexture = tex;
            m.hideFlags = HideFlags.HideAndDontSave;
            return m;
        }
        #endregion

        #region flame sphere material
        private static Material BuildFlameMaterial(bool inner)
        {
            Material m;

            if (flameSphereMaterial != null)
            {
                return new Material(flameSphereMaterial);
            }
            else if (cloudRemapShader != null)
            {
                m = new Material(cloudRemapShader);
                SetTexture(m, "_MainTex", flameGrayTexture);
                SetTexture(m, "_RemapTex", fireRampTexture);
                SetFloat(m, "_Boost", FlameBoost);
                SetFloat(m, "_AlphaBoost", FlameAlphaBoost);
                SetFloat(m, "_AlphaBias", 0f);
                SetFloat(m, "_Cull", 0f); // double sided
                SetFloat(m, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                SetFloat(m, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); // additive glow
                if (m.HasProperty("_TintColor"))
                    m.SetColor("_TintColor", inner ? new Color(1f, 0.9f, 0.55f, 0.35f) : new Color(1f, 0.6f, 0.2f, 0.5f));
            }
            else
            {
                Shader s = GetSpriteShader();
                if (s == null) return null;
                m = new Material(s);
                m.mainTexture = flameColorTexture;
                m.color = inner ? new Color(1f, 0.9f, 0.6f, 0.3f) : new Color(1f, 0.65f, 0.3f, 0.4f);
            }

            m.mainTextureScale = inner ? new Vector2(9f, 5f) : new Vector2(6f, 3f);
            return m;
        }

        private static void SetTexture(Material m, string prop, Texture t) { if (m.HasProperty(prop)) m.SetTexture(prop, t); }
        private static void SetFloat(Material m, string prop, float v) { if (m.HasProperty(prop)) m.SetFloat(prop, v); }
        #endregion

        #region procedural textures
        private static float Sstep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float Radial(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            return Mathf.Sqrt(dx * dx + dy * dy) * 2f;
        }

        // >0 inside the ellipse, 0 on the edge
        private static float Ell(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx, dy = (v - cy) / ry;
            return 1f - Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq + 37.1f, y * freq + 91.7f);
                norm += amp;
                freq *= 2f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        // Perlin that tiles seamlessly over u,v in 0..1
        private static float TileNoise(float u, float v, float su, float sv)
        {
            float a = Mathf.PerlinNoise(u * su + 50f, v * sv + 50f);
            float b = Mathf.PerlinNoise((u - 1f) * su + 50f, v * sv + 50f);
            float c = Mathf.PerlinNoise(u * su + 50f, (v - 1f) * sv + 50f);
            float d = Mathf.PerlinNoise((u - 1f) * su + 50f, (v - 1f) * sv + 50f);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static float FlameIntensity(float u, float v)
        {
            float n = TileNoise(u, v, 5f, 2f) * 0.5f + TileNoise(u, v, 10f, 5f) * 0.3f + TileNoise(u, v, 20f, 10f) * 0.2f;
            return Sstep(0.38f, 0.78f, n);
        }

        private static Color FireRamp(float t)
        {
            if (t < 0.35f) return Color.Lerp(new Color(0.15f, 0.01f, 0f), new Color(0.85f, 0.15f, 0.01f), t / 0.35f);
            if (t < 0.7f) return Color.Lerp(new Color(0.85f, 0.15f, 0.01f), new Color(1f, 0.55f, 0.06f), (t - 0.35f) / 0.35f);
            return Color.Lerp(new Color(1f, 0.55f, 0.06f), new Color(1f, 0.95f, 0.55f), (t - 0.7f) / 0.3f);
        }

        private static Texture2D MakeTexture(int w, int h, TextureWrapMode wrap, System.Func<float, float, Color> f)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels32(px);
            tex.wrapMode = wrap;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(true);
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private static Sprite MakeSprite(int w, int h, System.Func<float, float, Color> f)
        {
            Texture2D tex = MakeTexture(w, h, TextureWrapMode.Clamp, f);
            // pixelsPerUnit == width -> sprite is exactly 1 unit wide; scale the transform to size it
            Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        private static void BuildTextures()
        {
            // soft round dot (embers, footprint glow)
            softDotSprite = MakeSprite(64, 64, (u, v) => new Color(1f, 1f, 1f, Sstep(1f, 0.1f, Radial(u, v))));

            // ragged blob for the spray droplets (tinted by particle start color)
            oilBlobSprite = MakeSprite(64, 64, (u, v) =>
            {
                float d = Radial(u, v) + (Fbm(u * 6f, v * 6f, 3) - 0.5f) * 0.5f;
                return new Color(1f, 1f, 1f, Sstep(0.95f, 0.5f, d));
            });

            // oil puddle on the ground: dark, with purple sheen
            oilSplatSprite = MakeSprite(128, 128, (u, v) =>
            {
                float d = Radial(u, v);
                float n = Fbm(u * 5f, v * 5f, 4);
                float shape = 1f - d + (n - 0.5f) * 0.9f;
                float a = Sstep(0.12f, 0.3f, shape) * Sstep(1f, 0.88f, d);
                float sheen = Sstep(0.55f, 0.8f, Fbm(u * 9f + 4f, v * 9f + 8f, 3));
                Color c = Color.Lerp(new Color(0.05f, 0.035f, 0.07f), new Color(0.22f, 0.16f, 0.30f), sheen * 0.8f);
                c.a = a * 0.92f;
                return c;
            });

            // boot print, toe toward +V. Arch sits on the +U side, flip for the other foot.
            footprintSprite = MakeSprite(64, 128, (u, v) =>
            {
                float ball = Ell(u, v, 0.50f, 0.70f, 0.30f, 0.24f);
                float heel = Ell(u, v, 0.50f, 0.17f, 0.20f, 0.15f);
                float arch = Ell(u, v, 0.60f, 0.44f, 0.12f, 0.17f);
                float m = Mathf.Max(ball, Mathf.Max(heel, arch));
                float a = Sstep(0f, 0.15f, m);
                float tread = 0.88f + 0.12f * Mathf.Sin(v * 70f);
                Color c = Color.Lerp(new Color(1f, 0.55f, 0.05f), new Color(1f, 0.92f, 0.25f), Sstep(0.1f, 0.6f, m));
                c.a = a * tread;
                return c;
            });

            // burn spot: sooty irregular patch
            scorchSprite = MakeSprite(128, 128, (u, v) =>
            {
                float d = Radial(u, v);
                float n = Fbm(u * 4f, v * 4f, 4);
                float shape = 1f - d + (n - 0.5f) * 0.8f;
                float a = Sstep(0f, 0.35f, shape) * Sstep(1f, 0.9f, d);
                Color c = Color.Lerp(new Color(0.02f, 0.018f, 0.016f), new Color(0.10f, 0.07f, 0.05f), n);
                c.a = a * 0.9f;
                return c;
            });

            // glowing cracks inside the scorch
            crackSprite = MakeSprite(128, 128, (u, v) =>
            {
                float d = Radial(u, v);
                float n = Fbm(u * 5f + 20f, v * 5f + 7f, 3);
                float ridge = 1f - Mathf.Abs(n * 2f - 1f);
                float crack = Sstep(0.94f, 0.985f, ridge);
                float inside = Sstep(0.9f, 0.45f, d);
                float a = Mathf.Max(crack * inside, 0.25f * inside * Sstep(0.8f, 0.94f, ridge));
                Color c = Color.Lerp(new Color(1f, 0.35f, 0.03f), new Color(1f, 0.85f, 0.25f), crack);
                c.a = a;
                return c;
            });

            // blast ring for the special's ground decal
            ringSprite = MakeSprite(256, 256, (u, v) =>
            {
                float n = Fbm(u * 6f, v * 6f, 3);
                float d = Radial(u, v) + (n - 0.5f) * 0.15f;
                float ring = Sstep(0.70f, 0.9f, d) * Sstep(1f, 0.93f, d);
                float fill = 0.12f * Sstep(0.9f, 0.3f, d);
                float a = Mathf.Clamp01(ring + fill) * Mathf.Lerp(0.7f, 1f, n);
                Color c = Color.Lerp(new Color(1f, 0.4f, 0.05f), new Color(1f, 0.85f, 0.3f), ring * n);
                c.a = a;
                return c;
            });

            // scrolling flame noise for the sphere shells (tileable)
            flameGrayTexture = MakeTexture(256, 256, TextureWrapMode.Repeat, (u, v) =>
            {
                float i = FlameIntensity(u, v);
                return new Color(i, i, i, i);
            });
            flameColorTexture = MakeTexture(256, 256, TextureWrapMode.Repeat, (u, v) =>
            {
                float i = FlameIntensity(u, v);
                Color c = FireRamp(i);
                c.a = i * 0.85f;
                return c;
            });
            fireRampTexture = MakeTexture(256, 1, TextureWrapMode.Clamp, (u, v) =>
            {
                Color c = FireRamp(u);
                c.a = 1f;
                return c;
            });
        }
        #endregion
    }
}