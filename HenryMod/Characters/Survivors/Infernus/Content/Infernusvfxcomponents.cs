using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace InfernusMod.Survivors.Infernus.Components
{
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

    public class ConcussiveSphereIndicator : MonoBehaviour
    {
        private Transform outerShell, innerShell;
        private Material outerMat, innerMat;
        private string tintProperty;
        private Color outerBase = Color.white, innerBase = Color.white;

        private float radius, growTime, age, endTimer, endDuration;
        private float heightFactor = 1f, footOffsetY;
        private bool ending;

        public float Radius { get { return radius; } }

        public void Build(Mesh mesh, Material outer, Material inner, float radius, float growTime, float footOffsetY)
        {
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
        public void SetProgress(float p)
        {
            float eased = Mathf.Pow(Mathf.Clamp01(p), 4f);
            heightFactor = Mathf.Lerp(0.05f, 1f, eased);
        }
        public void Detonate()
        {
            if (ending) return;
            ending = true;
            endDuration = 0.25f;

            Vector3 center = transform.position;
            RaycastHit hit;
            if (Physics.Raycast(center + Vector3.up * 0.5f, Vector3.down, out hit, radius + 3f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)
                && hit.normal.y > 0.4f)
            {
                InfernusVFX.StampScorch(hit.point, hit.normal, radius * 2f, true);
            }
            InfernusVFX.PlayDetonation(center, radius);

            transform.SetParent(null, true); // don't vanish with the body mid fade
        }
        public void Cancel()
        {
            if (ending) return;
            ending = true;
            endDuration = 0.15f;
            transform.SetParent(null, true);
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
            float parentScale = transform.parent ? Mathf.Max(0.0001f, transform.parent.lossyScale.x) : 1f;
            float s = 2f * r / parentScale;
            transform.localScale = new Vector3(s, s * heightFactor, s);
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