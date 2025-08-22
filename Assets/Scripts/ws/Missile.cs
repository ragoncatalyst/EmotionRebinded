using System.Collections.Generic;
using System.Data;
using UnityEngine;
using Unity.Mathematics;

public class Missile : MonoBehaviour
{
    public MissileData data;
    public Vector2 direction;
    public SpriteRenderer spr;
    public Rigidbody2D rig;

    public float Angle => SMath.Angle(direction);

    private void Start()
    {
        spr = GetComponent<SpriteRenderer>();
        rig = GetComponent<Rigidbody2D>();

        transform.rotation = Quaternion.Euler(0, 0, Angle-90);

        StartCoroutine(LiveTime());
    }
    private void FixedUpdate()
    {
        rig.velocity = direction * data.speed;
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        foreach (var l in data.collisionMethods)
        {
            methods[l].Invoke(collision.gameObject, this);
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        foreach (var l in data.triggerMethods)
        {
            methods[l].Invoke(collision.gameObject, this);
        }
    }
    public System.Collections.IEnumerator LiveTime()
    {
        yield return new WaitForSeconds(data.liveTime);
        Destroy(gameObject);
    }



    public static Dictionary<string, MissileMethod> methods = new();
    public static Dictionary<string, MissileData> datas = new();

    /// <summary>
    /// 加入methods
    /// </summary>
    /// <param name="methName"></param>
    /// <param name="method"></param>
    public static void AddMethod(string methName, MissileMethod method)
    {
        methods.Add(methName, method);
    }
    public static void Load(string name, Vector2 position, Vector2 direction)
    {
        var d = datas[name];
        GameObject go = Resources.Load<GameObject>(d.objectPath);
        GameObject o = Instantiate(go);
        o.transform.position = position;
        var ms = o.GetComponent<Missile>();
        ms.data = d;
        ms.direction = direction.normalized;
    }
}
/// <summary>
/// 发射物信息
/// </summary>
public class MissileData//须在其内添加技能必要的信息
{
    public string name;
    /// <summary>
    ///发射物在resources的路径
    /// </summary>
    public string objectPath;
    public int damage;
    public float speed;

    /// <summary>
    /// 所有技能会调用的方法名称
    /// </summary>
    public List<string> collisionMethods;
    public List<string> triggerMethods;

    public float liveTime = 30f;
}
public delegate void MissileMethod(GameObject collider, Missile missile);

public static class SMath
{
    public static float Angle(Vector3 dir)
    {
        return Vector3.SignedAngle(Vector3.right, dir, Vector3.down);
    }
    public static float Angle(Vector2 dir)
    {
        Vector3 v = dir;
        float a = Angle(v);
        return dir.y < 0 ? -a : a;
    }
    public static float AngleStandardization(float angle)
    {
        angle %= 360;
        if (angle < 0)
            angle += 360;
        return angle;
    }
    public static float Smooth(float x)
    {
        x *= degRad;
        return math.sin(x);
    }
    public static float Smooth(float timeMax, float time)
    {
        float t = time / timeMax * 90 * degRad;
        return Sin(t);
    }
    public static float Parabola(float x, float p)
        => math.pow(x, p);
    public static float Abs(float v)
        => Mathf.Abs(v);
    public static int Abs(int v) => Mathf.Abs(v);

    public static float degRad = Mathf.Deg2Rad;

    public static float pi = math.PI;
    public static float Cos(float x)
        => math.cos(x);
    public static float CosA(float angle)
    {
        angle *= degRad;
        return math.cos(angle);
    }
    public static float Sin(float x)
        => math.sin(x);
    public static float SinA(float angle)
    {
        angle *= degRad;
        return math.sin(angle);
    }
    public static int Random(int seed, int max, int min)
    {
        UnityEngine.Random.InitState(seed);
        return UnityEngine.Random.Range(min, max);
    }
    public static float Random(int seed, float max, float min)
    {
        UnityEngine.Random.InitState(seed);
        return UnityEngine.Random.Range(min, max);
    }
    public static float Random(float max, float min)
    {
        return UnityEngine.Random.Range(min, max);
    }
    public static int Random(int max, int min)
    {
        return UnityEngine.Random.Range(min, max);
    }
    public static bool Random()
    {
        return Random(1, 0) == 0;
    }
    public static int RandomInt()
    {
        return Random(int.MaxValue, int.MinValue);
    }
    public static int Floor(float var)
    {
        return (int)math.floor(var);
    }
    /// <summary>
    /// get vec2 from angle
    /// </summary>
    /// <param name="angle"></param>
    /// <returns></returns>
    public static Vector2 GetVector(float angle) => new(CosA(angle), SinA(angle));
    public static class V3
    {
        /// <summary>
        /// around parallele by plane xz
        /// </summary>
        public static Vector3 ParaAround(Vector3 center, float angle, float radius)
        {
            angle *= degRad;
            Vector3 rela = new Vector3(Cos(angle), 0, Sin(angle)) * radius;

            return center + rela;
        }
        public static float Length(Vector3 to, Vector3 from)
        {
            Vector3 r = to - from;
            return r.magnitude;
        }
        public static Vector3 GetVector(float x = 0, float y = 0, float z = 0)
            => new(x, y, z);
        /// <summary>
        /// get a plan position
        /// </summary>
        /// <param name="vec"></param>
        /// <returns></returns>
        public static Vector3 GetVector(Vector2 vec, float height = 0) => new(vec.x, height, vec.y);
        public static Vector3 Parse(string p)
        {
            try
            {
                p = p.TrimStart('{');
                p = p.TrimEnd('}');
                string[] s = p.Split(',');
                return new(float.Parse(s[0]), float.Parse(s[1]), float.Parse(s[2]));
            }
            catch
            {
                return Vector3.zero;
            }
        }

        public static Vector3 DirectionAdjustment(Vector3 dir, float angle)
        {
            float b = Angle(dir);
            float r = b - 90 + angle;

            return GetVector(SMath.GetVector(r)) * dir.magnitude;
        }
    }
    public static class V2
    {
        public static Vector2Int Floor(Vector2 position)
        {
            return new(SMath.Floor(position.x), SMath.Floor(position.y));
        }
        public static float Length(Vector2 from, Vector2 to)
        {
            Vector2 v = from - to;
            return v.magnitude;
        }
        public static Vector2Int Random(Vector2Int max, Vector2Int min)
        {
            return new(SMath.Random(max.x, min.x), SMath.Random(max.y, min.y));
        }
        public static Vector2 Random(float max, float min)
        {
            return new(SMath.Random(max, min), SMath.Random(max, min));
        }
        public static Vector2 RandomByDirection(float dirangle, float angleArea)
        {
            float a = angleArea / 2;
            float b = SMath.Random(a, -a);
            float c = dirangle + b;
            return GetVector(c);
        }
        public static Vector2 RandomByDirection(Vector2 dir, float dirangle)
        {
            float a = Angle(dir);
            return RandomByDirection(a, dirangle);
        }
    }
    public static class Spr
    {
        public static int pxPerUnit = 32;
        public static Vector2Int GetDistance(Sprite sprite)
        {
            Texture2D tex = sprite.texture;
            Color co = new();
            Vector2Int v = new();
            for (int x = 0; x < 32; x++)
            {
                bool found = false;
                for (int i = 0; i < 32; i++)
                {
                    if (tex.GetPixel(x, i) != co)
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    v.x = x + 1;
                    break;
                }
            }
            for (int y = 0; y < 32; y++)
            {
                bool found = false;
                for (int i = 0; i < 32; i++)
                {
                    if (tex.GetPixel(i, y) != co)
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    v.y = y + 1;
                    break;
                }
            }
            return v;
        }
        /// <summary>
        /// Get area of opaque pixels
        /// </summary>
        /// <param name="sprite"></param>
        /// <returns></returns>
        public static Rect GetValidPixels(Sprite sprite)
        {
            return GetValidPixels(sprite.texture, sprite.rect);
        }
        public static Rect GetValidPixels(Texture2D texture, Rect spriteRect)
        {
            //get sprite area
            int startX = (int)spriteRect.x;
            int startY = (int)spriteRect.y;
            int width = (int)spriteRect.width;
            int height = (int)spriteRect.height;

            int minX = width, maxX = 0, minY = height, maxY = 0;
            bool hasOpaquePixel = false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = texture.GetPixel(startX + x, startY + y);

                    if (pixel.a > 0) //check just opaque pixel
                    {
                        hasOpaquePixel = true;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (hasOpaquePixel)
            {
                Debug.Log($"[SMath.Spr]Area of opaque px: minX={minX}, maxX={maxX}, minY={minY}, maxY={maxY}");
            }
            else
            {
                Debug.Log("[SMath.Spr]Has not opaque area!!");
            }

            return new(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

    }
    public static class Px
    {
        public static Texture2D GetSubTexture(Texture2D tex, int startX, int startY, int width, int height)
        {
            int texWidth = tex.width;
            int texHeight = tex.height;

            Color32[] allPixels = tex.GetPixels32();
            Color32[] subPixels = new Color32[width * height];

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int srcX = startX + col;
                    int srcY = startY + row;
                    int dstIndex = row * width + col;

                    if (srcX >= 0 && srcX < texWidth && srcY >= 0 && srcY < texHeight)
                    {
                        int srcIndex = srcY * texWidth + srcX;
                        subPixels[dstIndex] = allPixels[srcIndex];
                    }
                    else
                    {
                        // 超出边界部分设为透明
                        subPixels[dstIndex] = new Color32(0, 0, 0, 0);
                    }
                }
            }

            Texture2D subTex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            subTex.SetPixels32(subPixels);
            subTex.filterMode = FilterMode.Point;
            subTex.Apply();

            return subTex;
        }
        public static Texture2D Fill(Texture2D tex, TRect trect, Color color = new(), bool autoExpand = false, bool alphaBlend = true)
        {
            int rectRight = trect.x + trect.width;
            int rectTop = trect.y + trect.height;

            bool needsExpand = rectRight > tex.width || rectTop > tex.height || trect.x < 0 || trect.y < 0;

            if (!needsExpand || !autoExpand)
            {
                int startX = Mathf.Clamp(trect.x, 0, tex.width);
                int startY = Mathf.Clamp(trect.y, 0, tex.height);
                int width = Mathf.Clamp(trect.width, 0, tex.width - startX);
                int height = Mathf.Clamp(trect.height, 0, tex.height - startY);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int px = startX + x;
                        int py = startY + y;

                        if (alphaBlend)
                        {
                            Color dst = tex.GetPixel(px, py);
                            tex.SetPixel(px, py, AlphaBlend(dst, color));
                        }
                        else
                        {
                            tex.SetPixel(px, py, color);
                        }
                    }
                }

                tex.Apply();
                return tex;
            }

            // 扩展逻辑
            int offsetX = Mathf.Min(trect.x, 0);
            int offsetY = Mathf.Min(trect.y, 0);
            int newWidth = Mathf.Max(tex.width, trect.x + trect.width) - offsetX;
            int newHeight = Mathf.Max(tex.height, trect.y + trect.height) - offsetY;

            Texture2D newTex = new Texture2D(newWidth, newHeight, tex.format, false);

            // 清空
            Color[] clear = new Color[newWidth * newHeight];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color(0, 0, 0, 0);
            newTex.SetPixels(clear);

            // 拷贝旧图
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    newTex.SetPixel(x - offsetX, y - offsetY, tex.GetPixel(x, y));
                }
            }

            // 填充颜色
            int fillStartX = trect.x - offsetX;
            int fillStartY = trect.y - offsetY;

            for (int y = 0; y < trect.height; y++)
            {
                for (int x = 0; x < trect.width; x++)
                {
                    int px = fillStartX + x;
                    int py = fillStartY + y;

                    if (px < 0 || py < 0 || px >= newTex.width || py >= newTex.height) continue;

                    if (alphaBlend)
                    {
                        Color dst = newTex.GetPixel(px, py);
                        newTex.SetPixel(px, py, AlphaBlend(dst, color));
                    }
                    else
                    {
                        newTex.SetPixel(px, py, color);
                    }
                }
            }

            newTex.Apply();
            return newTex;
        }
        private static Color AlphaBlend(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);

            if (a < 1e-6f)
                return new Color(0, 0, 0, 0); // 完全透明

            float r = (src.r * src.a + dst.r * dst.a * (1f - src.a)) / a;
            float g = (src.g * src.a + dst.g * dst.a * (1f - src.a)) / a;
            float b = (src.b * src.a + dst.b * dst.a * (1f - src.a)) / a;

            return new Color(r, g, b, a);
        }



    }
    public struct TRect
    {
        public int x, y;
        public int width, height;
        public Texture2D GetTexture(Texture2D origin, int power = 1)
        {
            return SMath.Px.GetSubTexture(origin, x, y, width, height);
        }
        public TRect(int x, int y, int width = 32, int height = 32)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }
}