using System;
using System.Collections.Generic;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Ленточный конвейер вдоль оси X. Лента анимируется прокруткой текстуры,
    /// детали движутся по ленте и накапливаются перед упором (accumulate = true)
    /// либо уезжают до конца (accumulate = false).
    /// </summary>
    public class Conveyor : MonoBehaviour
    {
        public float length = 4.8f;
        public float speed = 0.6f;
        public float spacing = 0.7f;
        public bool accumulate = true;
        public bool Running = true;
        public Func<bool> canAccept;          // для выходного конвейера: есть ли место в таре
        public Action<Transform> onArrived;   // деталь доехала до конца (выходной конвейер)

        readonly List<Transform> items = new List<Transform>();
        Material belt;
        float offset;
        const float Stripe = 0.4f;

        public int Count { get { return items.Count; } }
        public Vector3 StartPoint { get { return transform.position + Vector3.up * (Parts.Size * 0.5f); } }
        public Vector3 EndPoint { get { return StartPoint + Vector3.right * length; } }

        public bool FrontReady
        {
            get
            {
                if (items.Count == 0 || items[0] == null) return false;
                return items[0].position.x - transform.position.x >= length - 0.03f;
            }
        }

        public static Conveyor Create(Transform parent, string name, Vector3 surfaceStart,
            float length, bool accumulate)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = surfaceStart;
            var c = go.AddComponent<Conveyor>();
            c.length = length;
            c.accumulate = accumulate;
            c.BuildVisual();
            return c;
        }

        void BuildVisual()
        {
            float L = length;
            float y = transform.position.y;
            var frame = Mats.Make(new Color(0.25f, 0.3f, 0.35f), 0.6f, 0.4f);
            var rail = Mats.Make(new Color(0.85f, 0.7f, 0.1f), 0.3f, 0.4f);
            var roller = Mats.Make(new Color(0.6f, 0.62f, 0.65f), 0.9f, 0.5f);

            belt = Mats.Make(Color.white, 0f, 0.1f);
            belt.mainTexture = Mats.StripeTex(new Color(0.12f, 0.12f, 0.13f), new Color(0.22f, 0.22f, 0.24f));
            belt.mainTextureScale = new Vector2(L / Stripe, 1f);

            Mats.Box("корпус", transform, new Vector3(L * 0.5f, -0.11f, 0f), new Vector3(L, 0.2f, 0.7f), frame);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "лента";
            var col = q.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            q.transform.SetParent(transform, false);
            q.transform.localPosition = new Vector3(L * 0.5f, 0.003f, 0f);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = new Vector3(L, 0.6f, 1f);
            q.GetComponent<Renderer>().sharedMaterial = belt;

            Mats.Box("борт1", transform, new Vector3(L * 0.5f, 0.04f, 0.33f), new Vector3(L, 0.08f, 0.05f), rail);
            Mats.Box("борт2", transform, new Vector3(L * 0.5f, 0.04f, -0.33f), new Vector3(L, 0.08f, 0.05f), rail);

            for (int i = 0; i < 2; i++)
            {
                var r = Mats.Cyl("валик", transform, new Vector3(i == 0 ? 0f : L, -0.1f, 0f), 0.22f, 0.7f, roller);
                r.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            int legs = Mathf.Max(2, Mathf.CeilToInt(L / 1.6f) + 1);
            float legH = y - 0.2f;
            for (int i = 0; i < legs; i++)
            {
                float x = L * i / (legs - 1);
                for (int s = -1; s <= 1; s += 2)
                    Mats.Box("опора", transform, new Vector3(x, -0.2f - legH * 0.5f, s * 0.3f),
                        new Vector3(0.08f, legH, 0.08f), frame);
            }
        }

        public void Put(Transform t)
        {
            t.SetParent(null, true);
            t.position = StartPoint;
            t.rotation = Quaternion.identity;
            items.Add(t);
        }

        /// <summary>Положить деталь на ленту на расстоянии s от начала (для начального заполнения).</summary>
        public void PutAt(Transform t, float s)
        {
            t.SetParent(null, true);
            t.position = StartPoint + Vector3.right * s;
            t.rotation = Quaternion.identity;
            items.Add(t);
        }

        public Transform TakeFront()
        {
            if (items.Count == 0) return null;
            var t = items[0];
            items.RemoveAt(0);
            return t;
        }

        void Update()
        {
            if (!Running) return;
            float dt = Time.deltaTime;

            offset -= speed * dt / Stripe;
            if (belt != null) belt.mainTextureOffset = new Vector2(offset, 0f);

            float x0 = transform.position.x;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null) { items.RemoveAt(i); i--; continue; }
                float s = it.position.x - x0;
                float target = accumulate ? length - i * spacing : length;
                float ns = Mathf.MoveTowards(s, target, speed * dt);
                var p = it.position;
                p.x = x0 + ns;
                it.position = p;
            }

            if (!accumulate && items.Count > 0)
            {
                var first = items[0];
                if (first.position.x - x0 >= length - 0.001f && (canAccept == null || canAccept()))
                {
                    items.RemoveAt(0);
                    if (onArrived != null) onArrived(first);
                }
            }
        }
    }
}
