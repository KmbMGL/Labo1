using UnityEngine;
using UnityEngine.Rendering;

namespace Workshop
{
    /// <summary>
    /// Вспомогательные функции: материалы (работают и в Built-in, и в URP) и создание примитивов.
    /// </summary>
    public static class Mats
    {
        static Shader _lit;

        public static bool IsUrp { get { return GraphicsSettings.currentRenderPipeline != null; } }

        static Shader Lit
        {
            get
            {
                if (_lit != null) return _lit;
                if (IsUrp) _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null) _lit = Shader.Find("Standard");
                if (_lit == null) _lit = Shader.Find("Sprites/Default");
                return _lit;
            }
        }

        public static Material Make(Color c, float metallic = 0.1f, float smooth = 0.4f)
        {
            var m = new Material(Lit);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        public static Material Emissive(Color c, float intensity = 2f)
        {
            var m = Make(c, 0f, 0.2f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * intensity);
            return m;
        }

        public static void SetEmission(Material m, Color c, float intensity)
        {
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * intensity);
        }

        public static Material Glass(Color c)
        {
            var m = Make(c, 0f, 0.9f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            if (IsUrp)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                m.SetFloat("_Mode", 3f);
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            m.renderQueue = 3000;
            return m;
        }

        public static Texture2D StripeTex(Color a, Color b)
        {
            var t = new Texture2D(16, 2, TextureFormat.RGBA32, false);
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 2; y++)
                    t.SetPixel(x, y, x < 8 ? a : b);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Point;
            t.Apply();
            return t;
        }

        static void Kill(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        /// <summary>Создать примитив (без коллайдера) с материалом.</summary>
        public static GameObject Prim(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (col != null) Kill(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            return Prim(PrimitiveType.Cube, name, parent, pos, size, mat);
        }

        /// <summary>Цилиндр: d - диаметр, h - полная высота (ось Y).</summary>
        public static GameObject Cyl(string name, Transform parent, Vector3 pos, float d, float h, Material mat)
        {
            return Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(d, h * 0.5f, d), mat);
        }

        public static GameObject Ball(string name, Transform parent, Vector3 pos, float d, Material mat)
        {
            return Prim(PrimitiveType.Sphere, name, parent, pos, new Vector3(d, d, d), mat);
        }

        public static GameObject Empty(string name, Transform parent, Vector3 localPos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            return g;
        }
    }
}
