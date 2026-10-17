using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Автономная тележка (AGV): едет по точкам, плавно разгоняется/тормозит, поворачивает на месте,
    /// крутит колёса, мигает маячком и возит ящик с деталями (4 места на платформе).
    /// </summary>
    public class AgvCart : MonoBehaviour
    {
        public float maxSpeed = 2.2f;
        public float turnSpeed = 110f;
        public string Status = "Ожидание";
        public readonly List<Transform> cargo = new List<Transform>();

        readonly Transform[] wheels = new Transform[4];
        Material beaconMat;
        float v, wheelAngle, blink;

        public static readonly Vector3[] Slots =
        {
            new Vector3(-0.27f, 0.6f, -0.37f), new Vector3(0.27f, 0.6f, -0.37f),
            new Vector3(-0.27f, 0.6f,  0.37f), new Vector3(0.27f, 0.6f,  0.37f)
        };

        public Vector3 SlotWorld(int i) { return transform.TransformPoint(Slots[i]); }

        public static AgvCart Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Тележка AGV");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var c = go.AddComponent<AgvCart>();
            c.BuildVisual();
            return c;
        }

        void BuildVisual()
        {
            var body = Mats.Make(new Color(0.95f, 0.75f, 0.1f), 0.3f, 0.5f);
            var dark = Mats.Make(new Color(0.1f, 0.1f, 0.12f), 0.5f, 0.3f);
            var tyre = Mats.Make(new Color(0.05f, 0.05f, 0.05f), 0f, 0.2f);
            var blue = Mats.Emissive(new Color(0.2f, 0.6f, 1f), 2f);
            beaconMat = Mats.Emissive(new Color(1f, 0.55f, 0f), 3f);

            Mats.Box("платформа", transform, new Vector3(0f, 0.35f, 0f), new Vector3(1.2f, 0.2f, 1.6f), body);
            Mats.Box("нижняя_часть", transform, new Vector3(0f, 0.22f, 0f), new Vector3(1.0f, 0.1f, 1.4f), dark);
            Mats.Box("бампер", transform, new Vector3(0f, 0.3f, 0.82f), new Vector3(1.0f, 0.12f, 0.05f), blue);
            Mats.Cyl("маяк", transform, new Vector3(0.45f, 0.55f, -0.7f), 0.1f, 0.2f, beaconMat);
            Mats.Box("мачта", transform, new Vector3(0.45f, 0.5f, -0.7f), new Vector3(0.03f, 0.1f, 0.03f), dark);

            int k = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var pivot = Mats.Empty("колесо", transform, new Vector3(sx * 0.58f, 0.16f, sz * 0.55f)).transform;
                    var w = Mats.Cyl("шина", pivot, Vector3.zero, 0.32f, 0.12f, tyre);
                    w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Mats.Box("метка", pivot, new Vector3(0f, 0.1f, 0f), new Vector3(0.14f, 0.05f, 0.05f), dark);
                    wheels[k++] = pivot;
                }
        }

        void Update()
        {
            blink += Time.deltaTime;
            bool on = ((int)(blink * 3f)) % 2 == 0;
            Mats.SetEmission(beaconMat, on ? new Color(1f, 0.55f, 0f) : new Color(0.25f, 0.12f, 0f), on ? 3f : 0.2f);
            wheelAngle += v * Time.deltaTime / 0.16f * Mathf.Rad2Deg;
            for (int i = 0; i < 4; i++)
                wheels[i].localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
        }

        public IEnumerator TurnTo(float yawDeg)
        {
            v = 0f;
            while (true)
            {
                float y = transform.eulerAngles.y;
                float ny = Mathf.MoveTowardsAngle(y, yawDeg, turnSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, ny, 0f);
                if (Mathf.Abs(Mathf.DeltaAngle(ny, yawDeg)) < 0.3f) break;
                yield return null;
            }
            transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        }

        public IEnumerator DriveTo(Vector3 target)
        {
            target.y = transform.position.y;
            Vector3 d = target - transform.position;
            if (d.magnitude < 0.01f) yield break;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            yield return TurnTo(yaw);

            while (true)
            {
                Vector3 to = target - transform.position;
                float dist = to.magnitude;
                if (dist < 0.02f) break;
                float vTarget = Mathf.Min(maxSpeed, Mathf.Sqrt(2f * 1.5f * dist) + 0.15f);
                v = Mathf.MoveTowards(v, vTarget, 1.6f * Time.deltaTime);
                float step = Mathf.Min(v * Time.deltaTime, dist);
                transform.position += to / dist * step;
                yield return null;
            }
            transform.position = target;
            v = 0f;
        }
    }
}
