using UnityEngine;

namespace Workshop
{
    /// <summary>Заготовки (детали): создание, обработка (карман), смена цвета.</summary>
    public static class Parts
    {
        public const float Size = 0.3f;
        static Material raw;
        static Material pocketMat;

        public static Transform Create(Vector3 worldPos, Transform parent)
        {
            if (raw == null) raw = Mats.Make(new Color(0.58f, 0.6f, 0.63f), 0.85f, 0.45f);
            var g = Mats.Box("Деталь", parent, Vector3.zero, Vector3.one * Size, raw);
            g.transform.position = worldPos;
            return g.transform;
        }

        public static Transform EnsurePocket(Transform part)
        {
            var p = part.Find("карман");
            if (p != null) return p;
            if (pocketMat == null) pocketMat = Mats.Make(new Color(0.08f, 0.08f, 0.1f), 0.5f, 0.3f);
            // локальные координаты куба 1x1x1: верхняя грань y = 0.5
            var g = Mats.Cyl("карман", part, new Vector3(0f, 0.5f, 0f), 0.5f, 0.2f, pocketMat);
            g.transform.localScale = new Vector3(0.5f, 0.001f, 0.5f);
            return g.transform;
        }

        public static void SetPocketDepth(Transform part, float k)
        {
            var p = EnsurePocket(part);
            p.localScale = new Vector3(0.5f, Mathf.Lerp(0.001f, 0.1f, Mathf.Clamp01(k)), 0.5f);
        }

        public static void MarkFinished(Transform part)
        {
            var r = part.GetComponent<Renderer>();
            if (r == null) return;
            var m = r.material; // экземпляр
            Color c = new Color(0.25f, 0.6f, 0.95f);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.9f);
            SetPocketDepth(part, 1f);
        }
    }
}
