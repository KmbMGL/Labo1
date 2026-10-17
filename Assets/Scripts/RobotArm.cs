using System.Collections;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Манипулятор: поворот основания (yaw) + двухзвенная рука (плечо, локоть) с обратной кинематикой
    /// и двухпальцевый захват. Движение задаётся в цилиндрических координатах (yaw, радиус, высота),
    /// поэтому рука разворачивается вокруг основания естественно, а не "едет" по прямой сквозь себя.
    /// </summary>
    public class RobotArm : MonoBehaviour
    {
        public float L1 = 1.9f, L2 = 1.9f;
        public float shoulderH = 1.5f;     // высота плеча над полом
        public float wristDrop = 0.45f;    // расстояние от запястья вниз до центра удерживаемой детали
        public float yawSpeed = 75f;       // град/с
        public float linSpeed = 1.3f;      // м/с
        public string Status = "Ожидание";

        public struct Cyl { public float yaw, r, y; }

        Transform yawPivot, shoulder, elbow, tip, fingerL, fingerR;
        Cyl cur;
        float fingerOpen = 0.27f, fingerClosed = 0.175f, finger = 0.27f;

        public Transform Held { get; private set; }
        public Vector3 BasePos { get { return transform.position; } }

        public static RobotArm Create(Transform parent, string name, Vector3 basePos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = basePos;
            var r = go.AddComponent<RobotArm>();
            r.BuildVisual(color);
            return r;
        }

        void BuildVisual(Color color)
        {
            var paint = Mats.Make(color, 0.3f, 0.6f);
            var dark = Mats.Make(new Color(0.15f, 0.15f, 0.17f), 0.7f, 0.4f);
            var steel = Mats.Make(new Color(0.7f, 0.72f, 0.75f), 0.9f, 0.6f);

            Mats.Cyl("плита", transform, new Vector3(0f, 0.06f, 0f), 1.3f, 0.12f, dark);
            Mats.Cyl("колонна", transform, new Vector3(0f, 0.66f, 0f), 0.8f, 1.08f, paint);

            yawPivot = Mats.Empty("поворот", transform, new Vector3(0f, 1.2f, 0f)).transform;
            Mats.Cyl("турель", yawPivot, Vector3.zero, 0.9f, 0.2f, dark);
            Mats.Box("корпус_плеча", yawPivot, new Vector3(0f, 0.3f, 0f), new Vector3(0.55f, 0.5f, 0.55f), paint);

            shoulder = Mats.Empty("плечо", yawPivot, new Vector3(0f, 0.3f, 0f)).transform;
            var j1 = Mats.Ball("сустав1", shoulder, Vector3.zero, 0.5f, dark);
            Mats.Box("звено1", shoulder, new Vector3(0f, 0f, L1 * 0.5f), new Vector3(0.28f, 0.28f, L1), paint);

            elbow = Mats.Empty("локоть", shoulder, new Vector3(0f, 0f, L1)).transform;
            Mats.Ball("сустав2", elbow, Vector3.zero, 0.38f, dark);
            Mats.Box("звено2", elbow, new Vector3(0f, 0f, L2 * 0.5f), new Vector3(0.21f, 0.21f, L2), paint);
            Mats.Ball("запястье", elbow, new Vector3(0f, 0f, L2), 0.28f, dark);

            // захват - отдельный объект, всегда направлен вниз
            tip = Mats.Empty("захват", yawPivot, Vector3.zero).transform;
            Mats.Box("вертикаль", tip, new Vector3(0f, wristDrop - 0.06f, 0f), new Vector3(0.1f, 0.12f, 0.1f), steel);
            Mats.Box("ладонь", tip, new Vector3(0f, 0.27f, 0f), new Vector3(0.52f, 0.12f, 0.14f), dark);
            fingerL = Mats.Box("палец_л", tip, new Vector3(-finger, 0.045f, 0f), new Vector3(0.05f, 0.33f, 0.12f), steel).transform;
            fingerR = Mats.Box("палец_п", tip, new Vector3(finger, 0.045f, 0f), new Vector3(0.05f, 0.33f, 0.12f), steel).transform;

            SetPose(270f, 1.6f, 2.1f);
        }

        // ---------- координаты ----------
        public Cyl FromWrist(Vector3 w)
        {
            Vector3 d = w - transform.position;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            if (yaw < 0f) yaw += 360f;
            return new Cyl { yaw = yaw, r = Mathf.Sqrt(d.x * d.x + d.z * d.z), y = w.y };
        }

        public void SetPose(float yaw, float r, float wristY)
        {
            cur = new Cyl { yaw = yaw, r = r, y = wristY };
            Apply();
        }

        void Apply()
        {
            Quaternion q = Quaternion.Euler(0f, cur.yaw, 0f);
            yawPivot.rotation = q;

            float h = cur.y - (transform.position.y + shoulderH);
            float rr = cur.r;
            float d = Mathf.Sqrt(rr * rr + h * h);
            d = Mathf.Clamp(d, Mathf.Abs(L1 - L2) + 0.05f, L1 + L2 - 0.02f);

            float cosA = Mathf.Clamp((L1 * L1 + d * d - L2 * L2) / (2f * L1 * d), -1f, 1f);
            float a1 = Mathf.Atan2(h, rr) + Mathf.Acos(cosA);                 // угол подъёма плеча
            float cosE = Mathf.Clamp((L1 * L1 + L2 * L2 - d * d) / (2f * L1 * L2), -1f, 1f);
            float bend = Mathf.PI - Mathf.Acos(cosE);                         // сгиб в локте

            shoulder.localRotation = Quaternion.Euler(-a1 * Mathf.Rad2Deg, 0f, 0f);
            elbow.localRotation = Quaternion.Euler(bend * Mathf.Rad2Deg, 0f, 0f);

            Vector3 dir = q * Vector3.forward;
            Vector3 b = transform.position;
            tip.position = new Vector3(b.x + dir.x * rr, cur.y - wristDrop, b.z + dir.z * rr);
            tip.rotation = q;
        }

        // ---------- движения ----------
        public IEnumerator MovePolar(float yaw, float r, float wristY)
        {
            yield return MoveCyl(new Cyl { yaw = yaw, r = r, y = wristY });
        }

        /// <summary>Переместить центр удерживаемой детали (точку захвата) в мировую точку.</summary>
        public IEnumerator MoveTip(Vector3 tipWorld)
        {
            yield return MoveCyl(FromWrist(tipWorld + Vector3.up * wristDrop));
        }

        IEnumerator MoveCyl(Cyl goal)
        {
            Cyl start = cur;
            float dur = Mathf.Max(0.08f,
                Mathf.Abs(goal.yaw - start.yaw) / yawSpeed,
                Mathf.Abs(goal.r - start.r) / linSpeed,
                Mathf.Abs(goal.y - start.y) / linSpeed);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cur.yaw = Mathf.Lerp(start.yaw, goal.yaw, e);
                cur.r = Mathf.Lerp(start.r, goal.r, e);
                cur.y = Mathf.Lerp(start.y, goal.y, e);
                Apply();
                yield return null;
            }
            cur = goal;
            Apply();
        }

        IEnumerator Fingers(float to)
        {
            float from = finger;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.3f;
                finger = Mathf.Lerp(from, to, Mathf.Clamp01(t));
                fingerL.localPosition = new Vector3(-finger, 0.045f, 0f);
                fingerR.localPosition = new Vector3(finger, 0.045f, 0f);
                yield return null;
            }
        }

        public IEnumerator Grip(Transform part)
        {
            yield return Fingers(fingerClosed);
            part.SetParent(tip, true);
            Held = part;
        }

        public IEnumerator Release()
        {
            if (Held != null) Held.SetParent(null, true);
            Held = null;
            yield return Fingers(fingerOpen);
        }
    }
}
