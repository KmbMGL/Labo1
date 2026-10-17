using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Точка входа. Собирает весь цех программно при запуске (Play): помещение, конвейеры, станок ЧПУ,
    /// двух роботов, тележку AGV, свет, камеру и интерфейс. Работает в любой пустой сцене.
    /// </summary>
    public class WorkshopBootstrap : MonoBehaviour
    {
        public static WorkshopBootstrap Instance;

        GameObject root;
        int rebuildStep;
        bool first = true;

        // Ключевые координаты (метры)
        const float BeltY = 0.8f, BeltLen = 4.8f, LaneZ = -4.5f;
        static readonly Vector3 Belt1Start = new Vector3(-11f, BeltY, 0f);
        static readonly Vector3 Belt2Start = new Vector3(6.2f, BeltY, 0f);
        static readonly Vector3 R1Pos = new Vector3(-3.3f, 0f, 0f);
        static readonly Vector3 R2Pos = new Vector3(3.3f, 0f, 0f);
        static readonly Vector3 CratePos = new Vector3(11.45f, 0f, 0f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Auto()
        {
            if (FindFirstObjectByType<WorkshopBootstrap>() == null)
                new GameObject("WorkshopBootstrap").AddComponent<WorkshopBootstrap>();
        }

        void Awake()
        {
            Instance = this;
            Build();
        }

        public void RequestRebuild() { if (rebuildStep == 0) rebuildStep = 1; }

        void Update()
        {
            if (rebuildStep == 1) { Time.timeScale = 1f; Destroy(root); rebuildStep = 2; }
            else if (rebuildStep == 2) { rebuildStep = 0; Build(); }
        }

        // =================================================================
        void Build()
        {
            if (first) DisableDefaults();
            first = false;

            root = new GameObject("Цех");
            Transform rt = root.transform;

            SetupEnvironment(rt);
            BuildHall(rt);
            BuildStorage(rt);
            BuildCrate(rt);

            var belt1 = Conveyor.Create(rt, "Конвейер 1", Belt1Start, BeltLen, true);
            var belt2 = Conveyor.Create(rt, "Конвейер 2", Belt2Start, BeltLen, false);
            var cnc = CncMachine.Create(rt, Vector3.zero);
            var r1 = RobotArm.Create(rt, "Робот R1", R1Pos, new Color(1f, 0.78f, 0.05f));
            var r2 = RobotArm.Create(rt, "Робот R2", R2Pos, new Color(1f, 0.45f, 0.05f));
            var cart = AgvCart.Create(rt, new Vector3(-13.8f, 0f, LaneZ), 270f);

            // камера
            var camGo = new GameObject("Камера");
            camGo.transform.SetParent(rt, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.66f, 0.72f, 0.8f);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<CameraRig>();
            rig.followTarget = cart.transform;

            // логика
            var ctlGo = new GameObject("Контроллер");
            ctlGo.transform.SetParent(rt, false);
            var ctl = ctlGo.AddComponent<WorkshopController>();
            ctl.belt1 = belt1; ctl.belt2 = belt2;
            ctl.robot1 = r1; ctl.robot2 = r2;
            ctl.cnc = cnc; ctl.cart = cart;
            ctl.crate.center = CratePos;
            ctl.rackPoint = new Vector3(-15.5f, 1.0f, LaneZ);
            ctl.laneZ = LaneZ;
            ctl.storageStop = new Vector3(-13.8f, 0f, LaneZ);
            ctl.feedStop = new Vector3(-10f, 0f, -1.7f);
            ctl.shipStop = new Vector3(CratePos.x, 0f, -1.7f);

            // интерфейс
            var hudGo = new GameObject("Интерфейс");
            hudGo.transform.SetParent(rt, false);
            var hud = hudGo.AddComponent<Hud>();
            hud.ctl = ctl; hud.rig = rig;
            hud.AddLabel(belt1.transform, new Vector3(2.4f, 0.9f, 0f), () => "Конвейер 1 (подача)");
            hud.AddLabel(belt2.transform, new Vector3(2.4f, 0.9f, 0f), () => "Конвейер 2 (выдача)");
            hud.AddLabel(cnc.transform, new Vector3(0f, 3.9f, 0f), () => "Станок ЧПУ: " + cnc.Status);
            hud.AddLabel(r1.transform, new Vector3(0f, 3.0f, 0f), () => "R1: " + r1.Status);
            hud.AddLabel(r2.transform, new Vector3(0f, 3.0f, 0f), () => "R2: " + r2.Status);
            hud.AddLabel(cart.transform, new Vector3(0f, 1.3f, 0f), () => "AGV: " + cart.Status);
            hud.AddLabel(root.transform, new Vector3(-15.5f, 2.2f, LaneZ), () => "Склад");
            hud.AddLabel(root.transform, new Vector3(CratePos.x, 1.4f, 0f), () => "Тара: " + ctl.crate.Count + " шт.");
        }

        void DisableDefaults()
        {
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);
        }

        void SetupEnvironment(Transform rt)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.54f, 0.58f);
            RenderSettings.fog = false;
            QualitySettings.shadowDistance = 70f;

            var go = new GameObject("Солнце");
            go.transform.SetParent(rt, false);
            go.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.96f, 0.9f);
            l.intensity = 1.15f;
            l.shadows = LightShadows.Soft;
        }

        // =================================================================
        void BuildHall(Transform rt)
        {
            var wall = Mats.Make(new Color(0.84f, 0.85f, 0.87f), 0f, 0.2f);
            var stripe = Mats.Make(new Color(0.15f, 0.35f, 0.65f), 0f, 0.2f);
            var steel = Mats.Make(new Color(0.35f, 0.38f, 0.42f), 0.7f, 0.4f);
            var yellow = Mats.Make(new Color(0.95f, 0.8f, 0.05f), 0f, 0.3f);
            var lamp = Mats.Emissive(new Color(1f, 0.97f, 0.88f), 2.2f);
            var win = Mats.Emissive(new Color(0.65f, 0.82f, 0.98f), 1.2f);

            // пол: квадратные плиты 2x2 м
            var floorM = Mats.Make(Color.white, 0f, 0.15f);
            floorM.mainTexture = FloorTex();
            floorM.mainTextureScale = new Vector2(19f, 12f);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Пол";
            var qc = q.GetComponent<Collider>();
            if (qc != null) Destroy(qc);
            q.transform.SetParent(rt, false);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = new Vector3(38f, 24f, 1f);
            q.GetComponent<Renderer>().sharedMaterial = floorM;

            // стены (спереди открыто для камеры)
            Mats.Box("Стена задняя", rt, new Vector3(0f, 3.5f, 11.9f), new Vector3(38f, 7f, 0.3f), wall);
            Mats.Box("Стена левая", rt, new Vector3(-18.9f, 3.5f, 0f), new Vector3(0.3f, 7f, 24f), wall);
            Mats.Box("Стена правая", rt, new Vector3(18.9f, 3.5f, 0f), new Vector3(0.3f, 7f, 24f), wall);
            Mats.Box("Полоса задняя", rt, new Vector3(0f, 1.0f, 11.73f), new Vector3(38f, 1.0f, 0.05f), stripe);
            Mats.Box("Полоса левая", rt, new Vector3(-18.73f, 1.0f, 0f), new Vector3(0.05f, 1.0f, 24f), stripe);
            Mats.Box("Полоса правая", rt, new Vector3(18.73f, 1.0f, 0f), new Vector3(0.05f, 1.0f, 24f), stripe);
            for (int i = -3; i <= 3; i++)
            {
                float x = i * 5.5f;
                Mats.Box("Окно", rt, new Vector3(x, 4.4f, 11.72f), new Vector3(3f, 1.8f, 0.05f), win);
                Mats.Box("Колонна", rt, new Vector3(x + 2.75f, 3.5f, 11.55f), new Vector3(0.5f, 7f, 0.5f), steel);
            }

            // фермы и светильники
            for (int i = -3; i <= 3; i++)
            {
                float x = i * 5.5f;
                Mats.Box("Балка", rt, new Vector3(x, 7.0f, 0f), new Vector3(0.3f, 0.45f, 24f), steel);
                for (int s = -1; s <= 1; s += 2)
                    Mats.Box("Светильник", rt, new Vector3(x, 6.5f, s * 5f), new Vector3(0.7f, 0.08f, 3f), lamp);
            }

            // разметка пола
            Mats.Box("Линия AGV 1", rt, new Vector3(-1f, 0.006f, LaneZ + 0.65f), new Vector3(28f, 0.012f, 0.12f), yellow);
            Mats.Box("Линия AGV 2", rt, new Vector3(-1f, 0.006f, LaneZ - 0.65f), new Vector3(28f, 0.012f, 0.12f), yellow);
            float zc = 0.0f;
            Mats.Box("Зона ячейки 1", rt, new Vector3(0f, 0.006f, zc + 3.2f), new Vector3(10.8f, 0.012f, 0.12f), yellow);
            Mats.Box("Зона ячейки 2", rt, new Vector3(0f, 0.006f, zc - 3.2f), new Vector3(10.8f, 0.012f, 0.12f), yellow);
            Mats.Box("Зона ячейки 3", rt, new Vector3(-5.4f, 0.006f, zc), new Vector3(0.12f, 0.012f, 6.4f), yellow);
            Mats.Box("Зона ячейки 4", rt, new Vector3(5.4f, 0.006f, zc), new Vector3(0.12f, 0.012f, 6.4f), yellow);

            // ограждение за станком
            var mesh = Mats.Glass(new Color(1f, 0.55f, 0.1f, 0.22f));
            Mats.Box("Ограждение", rt, new Vector3(0f, 0.85f, 3.4f), new Vector3(10.8f, 1.2f, 0.03f), mesh);
            for (int i = -3; i <= 3; i++)
                Mats.Box("Стойка ограждения", rt, new Vector3(i * 1.8f, 0.75f, 3.4f), new Vector3(0.07f, 1.5f, 0.07f), yellow);
            for (int s = -1; s <= 1; s += 2)
            {
                Mats.Box("Ограждение бок", rt, new Vector3(s * 5.4f, 0.85f, 2.3f), new Vector3(0.03f, 1.2f, 2.2f), mesh);
                Mats.Box("Стойка", rt, new Vector3(s * 5.4f, 0.75f, 1.2f), new Vector3(0.07f, 1.5f, 0.07f), yellow);
            }

            // шкафы управления
            var cab = Mats.Make(new Color(0.55f, 0.58f, 0.62f), 0.4f, 0.4f);
            var scr = Mats.Emissive(new Color(0.2f, 0.9f, 0.5f), 1.5f);
            for (int i = 0; i < 3; i++)
            {
                float x = -1.5f + i * 1.3f;
                Mats.Box("Шкаф", rt, new Vector3(x, 1.0f, 2.7f), new Vector3(1.2f, 2.0f, 0.6f), cab);
                Mats.Box("Экран", rt, new Vector3(x, 1.5f, 2.39f), new Vector3(0.5f, 0.3f, 0.02f), scr);
            }
        }

        static Texture2D FloorTex()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
            Color a = new Color(0.50f, 0.51f, 0.53f), b = new Color(0.36f, 0.37f, 0.39f);
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                    t.SetPixel(x, y, (x < 2 || y < 2) ? b : a);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            t.Apply();
            return t;
        }

        void BuildStorage(Transform rt)
        {
            var post = Mats.Make(new Color(0.15f, 0.3f, 0.6f), 0.5f, 0.4f);
            var shelf = Mats.Make(new Color(0.9f, 0.5f, 0.1f), 0.3f, 0.4f);
            var blank = Mats.Make(new Color(0.58f, 0.6f, 0.63f), 0.85f, 0.45f);
            Vector3 c = new Vector3(-15.9f, 0f, LaneZ);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Mats.Box("Стойка стеллажа", rt, c + new Vector3(sx * 0.45f, 1.0f, sz * 1.2f), new Vector3(0.08f, 2.0f, 0.08f), post);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.4f + i * 0.45f;
                Mats.Box("Полка", rt, c + new Vector3(0f, y, 0f), new Vector3(1.0f, 0.05f, 2.5f), shelf);
                if (i < 2)
                    for (int k = -2; k <= 2; k++)
                        Mats.Box("Заготовка на полке", rt, c + new Vector3(0f, y + 0.175f, k * 0.45f), Vector3.one * Parts.Size, blank);
            }
            // табличка
            Mats.Box("Табличка склада", rt, c + new Vector3(0f, 2.2f, 0f), new Vector3(0.1f, 0.4f, 1.4f),
                Mats.Emissive(new Color(0.2f, 0.6f, 1f), 1.0f));
        }

        void BuildCrate(Transform rt)
        {
            var m = Mats.Make(new Color(0.2f, 0.45f, 0.3f), 0.2f, 0.4f);
            Vector3 c = CratePos;
            Mats.Box("Дно тары", rt, c + new Vector3(0f, 0.05f, 0f), new Vector3(1.1f, 0.1f, 1.1f), m);
            Mats.Box("Борт тары", rt, c + new Vector3(0f, 0.35f, -0.525f), new Vector3(1.1f, 0.5f, 0.05f), m);
            Mats.Box("Борт тары", rt, c + new Vector3(0f, 0.35f, 0.525f), new Vector3(1.1f, 0.5f, 0.05f), m);
            Mats.Box("Борт тары", rt, c + new Vector3(0.525f, 0.35f, 0f), new Vector3(0.05f, 0.5f, 1.1f), m);
            Mats.Box("Борт тары", rt, c + new Vector3(-0.525f, 0.35f, 0f), new Vector3(0.05f, 0.5f, 1.1f), m);
        }
    }
}
