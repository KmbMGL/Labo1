using System.Collections;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Станок с ЧПУ: кожух со стеклом, две раздвижные двери (слева для R1, справа для R2),
    /// шпиндель, который ездит по осям X/Y/Z, искры, подсветка и сигнальная колонна.
    /// </summary>
    public class CncMachine : MonoBehaviour
    {
        public enum State { Empty, Loading, Loaded, Busy, Done, Unloading }

        public State state = State.Empty;
        public Transform Part;
        public string Status = "Свободен";

        const float TableTop = 1.0f;
        const float ParkY = 2.15f;
        const float ParkX = 0.7f;
        const float ToolLen = 0.5f;

        Transform head, tool;
        Transform[] leftLeaf = new Transform[2], rightLeaf = new Transform[2];
        readonly float[] closedZ = { -0.4f, 0.4f };
        ParticleSystem sparks;
        Material lampRed, lampYel, lampGrn, lampOff;
        Renderer[] lamps = new Renderer[3];
        bool spinning;
        public bool ParkedPlus { get; private set; }

        public Vector3 PartPoint { get { return transform.position + new Vector3(0f, TableTop + Parts.Size * 0.5f, 0f); } }
        public bool CanLoad { get { return state == State.Empty && ParkedPlus; } }

        public static CncMachine Create(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Станок ЧПУ");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var c = go.AddComponent<CncMachine>();
            c.BuildVisual();
            return c;
        }

        void BuildVisual()
        {
            var baseM = Mats.Make(new Color(0.16f, 0.22f, 0.3f), 0.4f, 0.5f);
            var white = Mats.Make(new Color(0.88f, 0.9f, 0.92f), 0.2f, 0.5f);
            var steel = Mats.Make(new Color(0.72f, 0.74f, 0.77f), 0.9f, 0.6f);
            var dark = Mats.Make(new Color(0.12f, 0.12f, 0.14f), 0.6f, 0.4f);
            var glass = Mats.Glass(new Color(0.6f, 0.8f, 0.95f, 0.18f));
            var leafM = Mats.Make(new Color(0.25f, 0.4f, 0.6f), 0.4f, 0.5f);

            Mats.Box("основание", transform, new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.9f, 2.4f), baseM);
            Mats.Box("стол", transform, new Vector3(0f, 0.95f, 0f), new Vector3(1.1f, 0.1f, 0.9f), steel);
            Mats.Box("тиски_л", transform, new Vector3(-0.28f, 1.05f, 0f), new Vector3(0.1f, 0.1f, 0.5f), dark);
            Mats.Box("тиски_п", transform, new Vector3(0.28f, 1.05f, 0f), new Vector3(0.1f, 0.1f, 0.5f), dark);

            // каркас: крыша, задняя стенка, передняя стеклянная, боковые панели
            Mats.Box("крыша", transform, new Vector3(0f, 2.65f, 0f), new Vector3(2.5f, 0.1f, 2.5f), white);
            Mats.Box("задняя", transform, new Vector3(0f, 1.75f, 1.2f), new Vector3(2.5f, 1.7f, 0.1f), white);
            Mats.Box("стекло", transform, new Vector3(0f, 1.75f, -1.2f), new Vector3(2.4f, 1.7f, 0.04f), glass);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Mats.Box("стойка", transform, new Vector3(sx * 1.22f, 1.75f, -1.2f), new Vector3(0.1f, 1.7f, 0.1f), white);
                for (int sz = -1; sz <= 1; sz += 2)
                    Mats.Box("панель", transform, new Vector3(sx * 1.25f, 1.75f, sz * 1.0f), new Vector3(0.1f, 1.7f, 0.4f), white);
            }

            // двери (две створки на каждой стороне)
            for (int i = 0; i < 2; i++)
            {
                leftLeaf[i] = Mats.Box("дверь_л" + i, transform, new Vector3(-1.34f, 1.75f, closedZ[i]), new Vector3(0.06f, 1.68f, 0.8f), leafM).transform;
                rightLeaf[i] = Mats.Box("дверь_п" + i, transform, new Vector3(1.34f, 1.75f, closedZ[i]), new Vector3(0.06f, 1.68f, 0.8f), leafM).transform;
            }

            // шпиндельная головка
            head = Mats.Empty("головка", transform, new Vector3(ParkX, ParkY, 0f)).transform;
            Mats.Box("корпус_головки", head, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.5f), dark);
            Mats.Cyl("цанга", head, new Vector3(0f, -0.08f, 0f), 0.22f, 0.16f, steel);
            tool = Mats.Empty("инструмент", head, new Vector3(0f, -0.3f, 0f)).transform;
            Mats.Cyl("фреза", tool, Vector3.zero, 0.08f, 0.4f, steel);
            Mats.Box("зуб", tool, new Vector3(0.04f, -0.05f, 0f), new Vector3(0.025f, 0.2f, 0.012f), dark);
            // направляющая портала
            Mats.Box("портал", transform, new Vector3(0f, 2.5f, 0f), new Vector3(2.3f, 0.1f, 0.14f), steel);

            // искры
            var sp = Mats.Empty("искры", head, new Vector3(0f, -ToolLen, 0f));
            sp.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            sparks = sp.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 2.2f;
            main.startSize = 0.03f;
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1f, 0.65f, 0.15f);
            var sh = sparks.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 35f;
            sh.radius = 0.02f;
            var em = sparks.emission;
            em.rateOverTime = 0f;
            var psr = sp.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = Mats.Emissive(new Color(1f, 0.6f, 0.1f), 3f);

            // подсветка рабочей зоны
            var lg = Mats.Empty("свет", transform, new Vector3(0f, 2.3f, 0f));
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 4f;
            l.intensity = Mats.IsUrp ? 6f : 0.9f;
            l.color = new Color(1f, 0.96f, 0.88f);

            // сигнальная колонна
            lampRed = Mats.Emissive(new Color(1f, 0.15f, 0.1f), 2.5f);
            lampYel = Mats.Emissive(new Color(1f, 0.75f, 0.05f), 2.5f);
            lampGrn = Mats.Emissive(new Color(0.1f, 1f, 0.2f), 2.5f);
            lampOff = Mats.Make(new Color(0.15f, 0.15f, 0.15f), 0f, 0.3f);
            Mats.Cyl("колонна_огней", transform, new Vector3(1.0f, 2.95f, 1.0f), 0.05f, 0.5f, dark);
            for (int i = 0; i < 3; i++)
            {
                var g = Mats.Cyl("огонь" + i, transform, new Vector3(1.0f, 3.25f + i * 0.14f, 1.0f), 0.16f, 0.12f, lampOff);
                lamps[i] = g.GetComponent<Renderer>();
            }
            SetLamp(2);
            ParkedPlus = true;
        }

        void SetLamp(int on) // 0 красный, 1 жёлтый, 2 зелёный
        {
            lamps[0].sharedMaterial = on == 0 ? lampRed : lampOff;
            lamps[1].sharedMaterial = on == 1 ? lampYel : lampOff;
            lamps[2].sharedMaterial = on == 2 ? lampGrn : lampOff;
        }

        void Update()
        {
            if (spinning && tool != null) tool.Rotate(Vector3.up, 2400f * Time.deltaTime, Space.Self);
        }

        // ---------- двери ----------
        public IEnumerator SetDoor(bool left, bool open)
        {
            var leaves = left ? leftLeaf : rightLeaf;
            float[] from = new float[2];
            for (int i = 0; i < 2; i++) from[i] = leaves[i].localPosition.z;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 1.0f;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                for (int i = 0; i < 2; i++)
                {
                    float to = open ? closedZ[i] * 2f : closedZ[i];
                    var p = leaves[i].localPosition;
                    p.z = Mathf.Lerp(from[i], to, e);
                    leaves[i].localPosition = p;
                }
                yield return null;
            }
        }

        // ---------- головка ----------
        IEnumerator MoveHead(Vector3 target, float speed)
        {
            while (Vector3.Distance(head.localPosition, target) > 0.002f)
            {
                head.localPosition = Vector3.MoveTowards(head.localPosition, target, speed * Time.deltaTime);
                yield return null;
            }
            head.localPosition = target;
        }

        /// <summary>Отвести шпиндель к стороне +X (для загрузки роботом R1) или -X (для выгрузки роботом R2).</summary>
        public IEnumerator Park(bool plusSide)
        {
            yield return MoveHead(new Vector3(plusSide ? ParkX : -ParkX, ParkY, 0f), 1.2f);
            ParkedPlus = plusSide;
        }

        // ---------- обработка ----------
        public IEnumerator Process(float seconds)
        {
            state = State.Busy;
            Status = "Обработка";
            SetLamp(1);
            ParkedPlus = false;

            Transform part = Part;
            Parts.EnsurePocket(part);
            Parts.SetPocketDepth(part, 0f);

            float cutY = TableTop + Parts.Size - 0.05f + ToolLen; // кончик фрезы на 5 см ниже верха детали
            yield return MoveHead(new Vector3(0f, ParkY, 0f), 1.2f);
            spinning = true;
            yield return MoveHead(new Vector3(0f, cutY + 0.15f, 0f), 0.9f);

            var em = sparks.emission;
            em.rateOverTime = 120f;
            sparks.Play();

            float cutTime = Mathf.Max(1.5f, seconds - 4f);
            float t = 0f;
            while (t < cutTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / cutTime);
                float a = t * 3.5f;
                float y = Mathf.Lerp(cutY + 0.15f, cutY, Mathf.Clamp01(k * 3f));
                head.localPosition = new Vector3(0.035f * Mathf.Cos(a), y, 0.035f * Mathf.Sin(a));
                Parts.SetPocketDepth(part, k);
                yield return null;
            }

            em.rateOverTime = 0f;
            yield return MoveHead(new Vector3(0f, ParkY, 0f), 0.9f);
            spinning = false;
            Parts.MarkFinished(part);
            yield return MoveHead(new Vector3(-ParkX, ParkY, 0f), 1.2f);

            state = State.Done;
            Status = "Деталь готова";
            SetLamp(2);
        }

        public void SetStateText(State s, string text)
        {
            state = s;
            Status = text;
        }
    }
}
