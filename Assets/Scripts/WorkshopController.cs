using System.Collections;
using UnityEngine;

namespace Workshop
{
    /// <summary>Тара для готовых деталей: 8 мест (2 слоя по 4).</summary>
    public class Crate
    {
        public Vector3 center;
        readonly Transform[] parts = new Transform[8];
        readonly bool[] reserved = new bool[8];

        public int Count
        {
            get { int n = 0; for (int i = 0; i < parts.Length; i++) if (parts[i] != null) n++; return n; }
        }
        public bool HasRoom
        {
            get { for (int i = 0; i < reserved.Length; i++) if (!reserved[i]) return true; return false; }
        }
        public Vector3 Slot(int i)
        {
            int layer = i / 4, k = i % 4;
            return center + new Vector3(k % 2 == 0 ? -0.22f : 0.22f, 0.27f + layer * 0.32f, k / 2 == 0 ? -0.22f : 0.22f);
        }
        public int Reserve()
        {
            for (int i = 0; i < reserved.Length; i++) if (!reserved[i]) { reserved[i] = true; return i; }
            return -1;
        }
        public void Land(int i, Transform t) { parts[i] = t; }
        public Transform Take()
        {
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) { var t = parts[i]; parts[i] = null; reserved[i] = false; return t; }
            return null;
        }
    }

    /// <summary>
    /// Логика цеха: четыре параллельных процесса (корутины)
    /// 1) R1 берёт заготовку с конвейера 1 и кладёт в станок;
    /// 2) станок обрабатывает деталь;
    /// 3) R2 достаёт деталь и кладёт на конвейер 2, который везёт её в тару;
    /// 4) тележка AGV привозит заготовки со склада и вывозит готовые детали.
    /// </summary>
    public class WorkshopController : MonoBehaviour
    {
        public Conveyor belt1, belt2;
        public RobotArm robot1, robot2;
        public CncMachine cnc;
        public AgvCart cart;
        public Crate crate = new Crate();
        public Vector3 rackPoint;
        public Vector3 storageStop, feedStop, shipStop;
        public float laneZ = -4.5f;

        public float machiningTime = 8f;
        public int batch = 4;
        public int produced;
        public float simTime, busyTime;

        const float TransitR = 1.6f, TransitY = 2.1f;
        static readonly Vector3 Lift = new Vector3(0f, 0.45f, 0f);

        void Start()
        {
            // начальный запас: 3 заготовки на конвейере 1, чтобы линия сразу заработала
            for (int i = 0; i < 3; i++)
            {
                var p = Parts.Create(belt1.StartPoint, null);
                belt1.PutAt(p, belt1.length - i * belt1.spacing);
            }
            belt2.canAccept = () => crate.HasRoom;
            belt2.onArrived = t => StartCoroutine(ToCrate(t));

            StartCoroutine(Robot1Loop());
            StartCoroutine(CncLoop());
            StartCoroutine(Robot2Loop());
            StartCoroutine(CartLoop());
        }

        void Update()
        {
            simTime += Time.deltaTime;
            if (cnc.state == CncMachine.State.Busy) busyTime += Time.deltaTime;
        }

        public float Utilization { get { return simTime > 0.1f ? busyTime / simTime : 0f; } }

        public static IEnumerator Hop(Transform t, Vector3 to, float dur, float height)
        {
            Vector3 from = t.position;
            float k = 0f;
            while (k < 1f)
            {
                k += Time.deltaTime / dur;
                float s = Mathf.Clamp01(k);
                Vector3 p = Vector3.Lerp(from, to, s);
                p.y += 4f * height * s * (1f - s);
                t.position = p;
                yield return null;
            }
            t.position = to;
        }

        // ---------- робот R1: конвейер 1 -> станок ----------
        IEnumerator Robot1Loop()
        {
            var r = robot1;
            while (true)
            {
                r.Status = "Ожидает заготовку";
                yield return r.MovePolar(270f, TransitR, TransitY);
                while (!(belt1.FrontReady && cnc.CanLoad)) yield return null;
                cnc.SetStateText(CncMachine.State.Loading, "Загрузка");

                r.Status = "Берёт заготовку";
                Vector3 pick = belt1.EndPoint;
                yield return r.MoveTip(pick + Lift);
                yield return r.MoveTip(pick);
                var part = belt1.TakeFront();
                yield return r.Grip(part);
                yield return r.MoveTip(pick + Lift);

                r.Status = "Несёт к станку";
                yield return r.MovePolar(270f, TransitR, TransitY);
                Coroutine door = StartCoroutine(cnc.SetDoor(true, true));
                yield return r.MovePolar(90f, TransitR, TransitY);
                yield return door;

                r.Status = "Загружает станок";
                Vector3 place = cnc.PartPoint;
                yield return r.MoveTip(place + Lift);
                yield return r.MoveTip(place);
                yield return r.Release();
                cnc.Part = part;
                yield return r.MoveTip(place + Lift);
                yield return r.MovePolar(90f, TransitR, TransitY);

                yield return cnc.SetDoor(true, false);
                cnc.SetStateText(CncMachine.State.Loaded, "Загружен");
            }
        }

        // ---------- станок ----------
        IEnumerator CncLoop()
        {
            while (true)
            {
                if (cnc.state == CncMachine.State.Empty && !cnc.ParkedPlus)
                    yield return cnc.Park(true);
                if (cnc.state == CncMachine.State.Loaded)
                    yield return cnc.Process(machiningTime);
                yield return null;
            }
        }

        // ---------- робот R2: станок -> конвейер 2 ----------
        IEnumerator Robot2Loop()
        {
            var r = robot2;
            while (true)
            {
                r.Status = "Ожидает деталь";
                yield return r.MovePolar(270f, TransitR, TransitY);
                while (cnc.state != CncMachine.State.Done) yield return null;
                cnc.SetStateText(CncMachine.State.Unloading, "Выгрузка");

                yield return cnc.SetDoor(false, true);
                r.Status = "Достаёт деталь";
                Vector3 pick = cnc.PartPoint;
                yield return r.MoveTip(pick + Lift);
                yield return r.MoveTip(pick);
                var part = cnc.Part;
                yield return r.Grip(part);
                yield return r.MoveTip(pick + Lift);
                yield return r.MovePolar(270f, TransitR, TransitY);
                cnc.Part = null;
                StartCoroutine(CloseAndFree());

                r.Status = "Несёт на конвейер";
                yield return r.MovePolar(90f, TransitR, TransitY);
                Vector3 place = belt2.StartPoint;
                yield return r.MoveTip(place + Lift);
                yield return r.MoveTip(place);
                yield return r.Release();
                belt2.Put(part);
                yield return r.MoveTip(place + Lift);
                yield return r.MovePolar(90f, TransitR, TransitY);
            }
        }

        IEnumerator CloseAndFree()
        {
            yield return cnc.SetDoor(false, false);
            cnc.SetStateText(CncMachine.State.Empty, "Свободен");
        }

        IEnumerator ToCrate(Transform t)
        {
            int idx = crate.Reserve();
            if (idx < 0) { Destroy(t.gameObject); yield break; }
            yield return Hop(t, crate.Slot(idx), 0.5f, 0.3f);
            crate.Land(idx, t);
            produced++;
        }

        // ---------- тележка AGV ----------
        IEnumerator CartLoop()
        {
            var c = cart;
            Vector3 storageLane = new Vector3(storageStop.x, 0f, laneZ);
            Vector3 feedLane = new Vector3(feedStop.x, 0f, laneZ);
            Vector3 shipLane = new Vector3(shipStop.x, 0f, laneZ);
            while (true)
            {
                // --- склад ---
                c.Status = "Едет на склад";
                yield return c.DriveTo(storageLane);
                yield return c.DriveTo(storageStop);
                yield return c.TurnTo(270f);
                c.Status = "Склад: разгрузка/погрузка";
                for (int i = c.cargo.Count - 1; i >= 0; i--)
                {
                    var t = c.cargo[i];
                    t.SetParent(null, true);
                    yield return Hop(t, rackPoint, 0.5f, 0.3f);
                    Destroy(t.gameObject);
                    c.cargo.RemoveAt(i);
                }
                for (int i = 0; i < batch; i++)
                {
                    var t = Parts.Create(rackPoint, null);
                    yield return Hop(t, c.SlotWorld(i), 0.5f, 0.3f);
                    t.SetParent(c.transform, true);
                    c.cargo.Add(t);
                }

                // --- подача на конвейер 1 ---
                c.Status = "Везёт заготовки";
                yield return c.DriveTo(storageLane);
                yield return c.DriveTo(feedLane);
                yield return c.DriveTo(feedStop);
                yield return c.TurnTo(90f);
                c.Status = "Выгружает на конвейер 1";
                for (int i = 0; i < c.cargo.Count; i++)
                {
                    var t = c.cargo[i];
                    t.SetParent(null, true);
                    yield return Hop(t, belt1.StartPoint, 0.7f, 0.5f);
                    belt1.Put(t);
                    yield return new WaitForSeconds(0.6f);
                }
                c.cargo.Clear();

                // --- забор готовых деталей ---
                c.Status = "Едет за готовыми";
                yield return c.DriveTo(feedLane);
                yield return c.DriveTo(shipLane);
                yield return c.DriveTo(shipStop);
                yield return c.TurnTo(90f);
                while (crate.Count < batch)
                {
                    c.Status = "Ждёт готовые детали (" + crate.Count + "/" + batch + ")";
                    yield return null;
                }
                c.Status = "Загружает готовые";
                for (int i = 0; i < batch; i++)
                {
                    var t = crate.Take();
                    yield return Hop(t, c.SlotWorld(i), 0.6f, 0.4f);
                    t.SetParent(c.transform, true);
                    c.cargo.Add(t);
                }
                c.Status = "Возвращается на склад";
                yield return c.DriveTo(shipLane);
            }
        }
    }
}
