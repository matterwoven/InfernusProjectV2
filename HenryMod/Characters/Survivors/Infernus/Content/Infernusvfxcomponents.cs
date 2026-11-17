using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace InfernusMod.Survivors.Infernus.Components
{
    /// <summary>
    /// A sprite decal lying on a surface, made of one or more layers that hold, then fade (optionally shifting color).
    /// Destroys itself when every layer is gone.
    /// </summary>
    public class GroundDecal : MonoBehaviour
    {
        private struct Layer
        {
            public SpriteRenderer renderer;
            public Color color;
            public Color endColor;
            public float hold;
            public float fade;
        }

        private readonly List<Layer> layers = new List<Layer>();
        private float age;
        private float life;
        private float growTime;
        private Vector3 targetScale;

        /// <summary>Pop-in: scales from 50% to full over growTime. Call after the scale has been set.</summary>
        public GroundDecal Configure(float growTime)
        {
            targetScale = transform.localScale;
            this.growTime = growTime;
            if (growTime > 0f) transform.localScale = targetScale * 0.5f;
            return this;
        }

        public SpriteRenderer AddLayer(Sprite sprite, int order, Color color, Color endColor, float hold, float fade, bool flipX, float scale)
        {
            GameObject child = new GameObject("Layer");
            child.transform.SetParent(transform, false);
            child.transform.localScale = Vector3.one * scale;

            SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.sortingOrder = order;
            sr.color = color;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;

            layers.Add(new Layer { renderer = sr, color = color, endColor = endColor, hold = hold, fade = fade });
            life = Mathf.Max(life, hold + fade);
            return sr;
        }

        private void Update()
        {
            age += Time.deltaTime;

            if (growTime > 0f)
            {
                if (age < growTime)
                {
                    float t = age / growTime;
                    transform.localScale = Vector3.Lerp(targetScale * 0.5f, targetScale, 1f - (1f - t) * (1f - t));
                }
                else
                {
                    transform.localScale = targetScale;
                    growTime = 0f;
                }
            }

            for (int i = 0; i < layers.Count; i++)
            {
                Layer l = layers[i];
                if (!l.renderer) continue;
                float fadeT = age <= l.hold ? 0f : Mathf.Clamp01((age - l.hold) / Mathf.Max(0.01f, l.fade));
                Color c = Color.Lerp(l.color, l.endColor, fadeT);
                c.a = l.color.a * (1f - fadeT);
                l.renderer.color = c;
            }

            if (age >= life) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Lives on the oil spray particle system. When a droplet hits the ground it stamps an oil puddle decal.
    /// </summary>
    public class OilSprayEmitter : MonoBehaviour
    {
        private ParticleSystem ps;
        private readonly List<ParticleCollisionEvent> events = new List<ParticleCollisionEvent>();

        public void Init(ParticleSystem system) { ps = system; }

        public void SetAim(Vector3 origin, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
        }

        /// <summary>Stop emitting; live droplets finish and the object cleans itself up.</summary>
        public void Stop()
        {
            if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnParticleCollision(GameObject other)
        {
            if (!ps) return;
            int count = ps.GetCollisionEvents(other, events);
            for (int i = 0; i < count; i++)
            {
                ParticleCollisionEvent e = events[i];
                InfernusVFX.StampOil(e.intersection, e.normal, UnityEngine.Random.Range(1.0f, 1.9f));
            }
        }
    }

    /// <summary>
    /// A decal built by draping a mesh over the real ground (polar grid of downward raycasts), so it follows
    /// slopes and dips instead of floating as a flat sprite. Layers share the mesh and fade independently.
    /// </summary>
    public class ConformingDecal : MonoBehaviour
    {
        private struct Layer
        {
            public Material material;
            public Color color, endColor;
            public float hold, fade;
        }

        private const float GrowTime = 0.25f;
        private const float MaxStep = 1.5f; // skip triangles spanning a bigger height jump (cliffs, ledges)

        private readonly List<Layer> layers = new List<Layer>();
        private Mesh mesh;
        private float age, life;

        /// <summary>center should be a point on the ground. Returns false if no usable ground was found.</summary>
        public bool BuildMesh(Vector3 center, float radius, int rings, int segments, float lift)
        {
            transform.position = center;
            transform.rotation = Quaternion.identity;

            int count = 1 + rings * segments;
            Vector3[] verts = new Vector3[count];
            Vector2[] uvs = new Vector2[count];
            bool[] valid = new bool[count];
            float spin = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < count; i++)
            {
                float r = 0f, theta = 0f;
                if (i > 0)
                {
                    int k = (i - 1) / segments + 1;
                    int j = (i - 1) % segments;
                    r = radius * k / rings;
                    theta = j / (float)segments * Mathf.PI * 2f;
                }
                float dx = r * Mathf.Cos(theta), dz = r * Mathf.Sin(theta);

                RaycastHit hit;
                Vector3 origin = new Vector3(center.x + dx, center.y + 4f, center.z + dz);
                if (Physics.Raycast(origin, Vector3.down, out hit, 16f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)
                    && hit.normal.y > 0.3f)
                {
                    verts[i] = new Vector3(dx, hit.point.y - center.y + lift, dz);
                    valid[i] = true;
                }
                else
                {
                    verts[i] = new Vector3(dx, lift, dz);
                }

                float t = r / radius;
                uvs[i] = new Vector2(0.5f + 0.5f * t * Mathf.Cos(theta + spin), 0.5f + 0.5f * t * Mathf.Sin(theta + spin));
            }

            List<int> tris = new List<int>();
            for (int k = 0; k < rings; k++)
            {
                for (int j = 0; j < segments; j++)
                {
                    if (k == 0)
                    {
                        AddTri(tris, verts, valid, 0, Idx(1, j + 1, segments), Idx(1, j, segments));
                    }
                    else
                    {
                        int a = Idx(k, j, segments), b = Idx(k, j + 1, segments);
                        int c = Idx(k + 1, j, segments), d = Idx(k + 1, j + 1, segments);
                        AddTri(tris, verts, valid, a, d, c);
                        AddTri(tris, verts, valid, a, b, d);
                    }
                }
            }
            if (tris.Count == 0) return false;

            mesh = new Mesh();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            return true;
        }

        private static int Idx(int ring, int seg, int segments)
        {
            return ring == 0 ? 0 : 1 + (ring - 1) * segments + (seg % segments);
        }

        private static void AddTri(List<int> tris, Vector3[] v, bool[] valid, int a, int b, int c)
        {
            if (!valid[a] || !valid[b] || !valid[c]) return;
            float minY = Mathf.Min(v[a].y, Mathf.Min(v[b].y, v[c].y));
            float maxY = Mathf.Max(v[a].y, Mathf.Max(v[b].y, v[c].y));
            if (maxY - minY > MaxStep) return;
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        /// <summary>queueOffset orders layers that overlap exactly (higher draws on top).</summary>
        public void AddLayer(Material material, int queueOffset, Color color, Color endColor, float hold, float fade)
        {
            GameObject child = new GameObject("Layer");
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = child.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            material.renderQueue = 3000 + queueOffset;
            material.color = color;

            layers.Add(new Layer { material = material, color = color, endColor = endColor, hold = hold, fade = fade });
            life = Mathf.Max(life, hold + fade);
        }

        private void Update()
        {
            age += Time.deltaTime;

            // grow outward only; heights are baked into the mesh so Y stays 1
            float g = Mathf.Clamp01(age / GrowTime);
            g = 1f - (1f - g) * (1f - g);
            float s = Mathf.Lerp(0.7f, 1f, g);
            transform.localScale = new Vector3(s, 1f, s);

            for (int i = 0; i < layers.Count; i++)
            {
                Layer l = layers[i];
                if (!l.material) continue;
                float fadeT = age <= l.hold ? 0f : Mathf.Clamp01((age - l.hold) / Mathf.Max(0.01f, l.fade));
                Color c = Color.Lerp(l.color, l.endColor, fadeT);
                c.a = l.color.a * (1f - fadeT);
                l.material.color = c;
            }

            if (age >= life) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < layers.Count; i++)
                if (layers[i].material) Destroy(layers[i].material);
            if (mesh) Destroy(mesh);
        }
    }

    /// <summary>
    /// Flame-textured sphere bound to the character. Two counter-scrolling, counter-rotating shells.
    /// Detonate() leaves a fading scorch decal on the ground under the sphere; Cancel() just fades it away.
    /// </summary>
    public class ConcussiveSphereIndicator : MonoBehaviour
    {
        private Transform outerShell, innerShell;
        private Material outerMat, innerMat;
        private string tintProperty;
        private Color outerBase = Color.white, innerBase = Color.white;

        private float radius, growTime, age, endTimer, endDuration;
        private float heightFactor = 1f, footOffsetY;
        public float maxLifetime = 15f;
        private float autoDetonateAfter; // seconds; 0 = wait for the state to call Detonate()
        private bool ending;
        private bool pinned;
        private Vector3 pinnedPosition;

        public float Radius { get { return radius; } }

        public void Build(Mesh mesh, Material outer, Material inner, float radius, float growTime, float footOffsetY, float autoDetonateAfter)
        {
            this.autoDetonateAfter = autoDetonateAfter;
            this.radius = radius;
            this.growTime = growTime;
            this.footOffsetY = footOffsetY;
            outerMat = outer;
            innerMat = inner;

            transform.localPosition = Vector3.zero;
            outerShell = MakeShell("OuterShell", mesh, outerMat, 1f);
            innerShell = MakeShell("InnerShell", mesh, innerMat, 0.94f);

            tintProperty = outer.HasProperty("_TintColor") ? "_TintColor" : (outer.HasProperty("_Color") ? "_Color" : null);
            if (tintProperty != null)
            {
                outerBase = outer.GetColor(tintProperty);
                innerBase = inner.GetColor(tintProperty);
            }

            ApplyRadius(0.01f);
            ApplyAlpha(0f);
        }

        /// <summary>
        /// Windup progress 0..1. Stays a thin band near the ground for most of the windup,
        /// then rises into a full sphere at the end. Skip calling this for a constant full sphere.
        /// </summary>
        public void SetProgress(float p)
        {
            float eased = Mathf.Pow(Mathf.Clamp01(p), 4f);
            heightFactor = Mathf.Lerp(0.05f, 1f, eased);
        }

        /// <summary>The blast resolved: flash, leave the ground decal, fade the sphere out.</summary>
        public void Detonate(float fadeDuration = -1f)
        {
            if (ending) return;
            ending = true;
            if (fadeDuration < 0f) fadeDuration = InfernusVFX.sphereFadeSeconds;
            endDuration = Mathf.Max(0.05f, fadeDuration);

            Vector3 center = transform.position;
            Pin(center);
            Log.Debug("Infernus flame sphere detonated at " + center);
            RaycastHit hit;
            if (Physics.Raycast(center + Vector3.up * 0.5f, Vector3.down, out hit, radius + 3f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)
                && hit.normal.y > 0.4f)
            {
                InfernusVFX.StampBlastDecal(hit.point, hit.normal, radius);
            }
            InfernusVFX.PlayDetonation(center, radius);

            Destroy(gameObject, endDuration + 1f); // failsafe
        }

        /// <summary>State was interrupted: fade out, no decal, no blast effect.</summary>
        public void Cancel()
        {
            if (ending) return;
            ending = true;
            endDuration = 0.15f;
            Pin(transform.position);
            Destroy(gameObject, endDuration + 1f); // failsafe
        }

        // Detach from the body and lock to a world position so the fade can never follow the character.
        private void Pin(Vector3 worldPosition)
        {
            pinned = true;
            pinnedPosition = worldPosition;
            transform.SetParent(null, true);
            transform.position = pinnedPosition;
        }

        private Transform MakeShell(string name, Mesh mesh, Material mat, float scale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        private void ApplyRadius(float r)
        {
            // built-in sphere has diameter 1
            float parentScale = transform.parent ? Mathf.Max(0.0001f, transform.parent.lossyScale.x) : 1f;
            float s = 2f * r / parentScale;
            transform.localScale = new Vector3(s, s * heightFactor, s);
            // while squashed, sit near the feet; slide up to body center as it becomes a full sphere
            if (transform.parent)
                transform.localPosition = new Vector3(0f, Mathf.Lerp(footOffsetY, 0f, heightFactor), 0f);
        }

        private void ApplyAlpha(float a)
        {
            if (tintProperty == null) return;
            Color o = outerBase; o.a *= a; outerMat.SetColor(tintProperty, o);
            Color i = innerBase; i.a *= a; innerMat.SetColor(tintProperty, i);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            // watchdog: if the owning state threw before calling Detonate/Cancel, don't hang around forever
            if (!ending && autoDetonateAfter > 0f && age >= autoDetonateAfter) Detonate();
            if (!ending && age > maxLifetime) Cancel();

            float grow = growTime > 0f ? Mathf.Clamp01(age / growTime) : 1f;
            grow = 1f - (1f - grow) * (1f - grow);

            float flash = 1f, alpha = 1f;
            if (ending)
            {
                endTimer += dt;
                float t = Mathf.Clamp01(endTimer / endDuration);
                flash = 1f + 0.12f * t;
                alpha = 1f - t;
                if (t >= 1f) { Destroy(gameObject); return; }
            }

            float pulse = 1f + 0.025f * Mathf.Sin(age * 14f);
            ApplyRadius(radius * grow * pulse * flash);
            ApplyAlpha(grow * alpha);

            // once detonated/cancelled, stay exactly where the explosion happened
            if (pinned)
            {
                if (transform.parent) transform.SetParent(null, true);
                transform.position = pinnedPosition;
            }

            // flames lick upward (offset.y falls), shells counter-rotate for depth
            outerMat.mainTextureOffset += new Vector2(0.03f, -0.55f) * dt;
            innerMat.mainTextureOffset += new Vector2(-0.05f, -0.90f) * dt;
            outerShell.Rotate(0f, 30f * dt, 0f, Space.Self);
            innerShell.Rotate(0f, -45f * dt, 0f, Space.Self);
        }

        private void OnDestroy()
        {
            if (outerMat) Destroy(outerMat);
            if (innerMat) Destroy(innerMat);
        }
    }
}