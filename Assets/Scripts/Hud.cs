using System;
using System.Collections.Generic;
using UnityEngine;

namespace Workshop
{
    /// <summary>
    /// Экранный интерфейс (IMGUI): статус оборудования, управление временем, камеры, подписи над устройствами.
    /// Ввод обрабатывается через события OnGUI, поэтому работает при любом режиме Input (старый/новый).
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public WorkshopController ctl;
        public CameraRig rig;

        struct Lbl { public Transform t; public Vector3 off; public Func<string> text; }
        readonly List<Lbl> labels = new List<Lbl>();

        bool showLabels = true, paused, dragging;
        float speedMul = 1f, fps = 60f;
        GUIStyle label, title, small, tagStyle;

        public void AddLabel(Transform t, Vector3 off, Func<string> text)
        {
            labels.Add(new Lbl { t = t, off = off, text = text });
        }

        void OnDestroy() { Time.timeScale = 1f; }

        void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
        }

        void InitStyles()
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = false };
            title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            tagStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }, wordWrap = false
            };
        }

        void SetTime()
        {
            Time.timeScale = paused ? 0f : speedMul;
        }

        void OnGUI()
        {
            if (ctl == null) return;
            Event e = Event.current;
            if (label == null) InitStyles();
            float sc = Mathf.Clamp(Screen.height / 760f, 1f, 2.2f);

            if (showLabels && e.type == EventType.Repaint) DrawLabels(sc);

            GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
            GUILayout.BeginArea(new Rect(10, 10, 330, Screen.height / sc - 20));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Цифровая модель цеха", title);
            GUILayout.Label("Время моделирования: " + FormatTime(ctl.simTime), label);
            GUILayout.Label("Выпущено деталей: " + ctl.produced + "   (в таре: " + ctl.crate.Count + ")", label);
            GUILayout.Label("Загрузка станка: " + Mathf.RoundToInt(ctl.Utilization * 100f) + " %", label);
            GUILayout.Space(4);
            GUILayout.Label("Станок ЧПУ: " + ctl.cnc.Status, label);
            GUILayout.Label("Робот R1: " + ctl.robot1.Status, label);
            GUILayout.Label("Робот R2: " + ctl.robot2.Status, label);
            GUILayout.Label("Тележка AGV: " + ctl.cart.Status, label);
            GUILayout.Label("Конвейер 1: " + ctl.belt1.Count + " дет.   Конвейер 2: " + ctl.belt2.Count + " дет.", label);
            GUILayout.Label("FPS: " + Mathf.RoundToInt(fps), small);
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "▶ Старт" : "❚❚ Пауза")) { paused = !paused; SetTime(); }
            if (GUILayout.Button("x1")) { speedMul = 1f; paused = false; SetTime(); }
            if (GUILayout.Button("x2")) { speedMul = 2f; paused = false; SetTime(); }
            if (GUILayout.Button("x4")) { speedMul = 4f; paused = false; SetTime(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Сброс")) WorkshopBootstrap.Instance.RequestRebuild();
            showLabels = GUILayout.Toggle(showLabels, " Подписи (L)");
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Камера:", label);
            for (int i = 0; i < CameraRig.PresetNames.Length; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < i + 3 && j < CameraRig.PresetNames.Length; j++)
                    if (GUILayout.Button(CameraRig.PresetNames[j])) rig.Preset(j);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(4);
            GUILayout.Label("Мышь: вращение - перетаскивание, колесо - масштаб.\nWASD / стрелки - сдвиг, Пробел - пауза.", small);
            GUILayout.EndVertical();
            GUILayout.EndArea();

            HandleInput(e);
        }

        void HandleInput(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseDown: dragging = true; break;
                case EventType.MouseUp: dragging = false; break;
                case EventType.MouseDrag:
                    if (dragging) rig.Orbit(e.delta.x, e.delta.y);
                    break;
                case EventType.ScrollWheel: rig.Zoom(e.delta.y); break;
                case EventType.KeyDown:
                    float st = 0.5f;
                    switch (e.keyCode)
                    {
                        case KeyCode.Space: paused = !paused; SetTime(); break;
                        case KeyCode.L: showLabels = !showLabels; break;
                        case KeyCode.R: WorkshopBootstrap.Instance.RequestRebuild(); break;
                        case KeyCode.Alpha1: rig.Preset(0); break;
                        case KeyCode.Alpha2: rig.Preset(1); break;
                        case KeyCode.Alpha3: rig.Preset(2); break;
                        case KeyCode.Alpha4: rig.Preset(3); break;
                        case KeyCode.Alpha5: rig.Preset(4); break;
                        case KeyCode.Alpha6: rig.Preset(5); break;
                        case KeyCode.W: case KeyCode.UpArrow: rig.Pan(0f, st); break;
                        case KeyCode.S: case KeyCode.DownArrow: rig.Pan(0f, -st); break;
                        case KeyCode.A: case KeyCode.LeftArrow: rig.Pan(-st, 0f); break;
                        case KeyCode.D: case KeyCode.RightArrow: rig.Pan(st, 0f); break;
                    }
                    break;
            }
        }

        void DrawLabels(float sc)
        {
            var cam = Camera.main;
            if (cam == null) return;
            tagStyle.fontSize = Mathf.RoundToInt(12 * sc);
            foreach (var l in labels)
            {
                if (l.t == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(l.t.position + l.off);
                if (sp.z < 0f) continue;
                string s = l.text();
                Vector2 size = tagStyle.CalcSize(new GUIContent(s));
                var r = new Rect(sp.x - size.x * 0.5f - 6f, Screen.height - sp.y - size.y - 4f, size.x + 12f, size.y + 4f);
                GUI.Box(r, s, tagStyle);
            }
        }

        static string FormatTime(float t)
        {
            int m = (int)(t / 60f);
            int s = (int)(t % 60f);
            return m.ToString("00") + ":" + s.ToString("00");
        }
    }
}
