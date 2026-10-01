using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>程序化引导图标生成器：SDF 超采样扁平风格，统一橙金色调。菜单 Tools > Generate Guidance Icons。</summary>
public static class GuidanceIconGenerator
{
    const int Size = 256;
    const int SS = 2; // 超采样
    const string OutDir = "Assets/UI/Icons/Guidance";

    // 色板
    static Color Orange = new Color(0.96f, 0.62f, 0.18f, 1f);
    static Color Amber = new Color(1f, 0.82f, 0.40f, 1f);
    static Color Deep = new Color(0.91f, 0.38f, 0.18f, 1f);
    static Color White = new Color(1f, 1f, 1f, 1f);
    static Color Green = new Color(0.30f, 0.85f, 0.45f, 1f);

    [MenuItem("Tools/Generate Guidance Icons")]
    public static void GenerateAll()
    {
        string[] names = { "icon_power", "icon_alarm", "icon_extinguisher", "icon_fire", "icon_towel", "icon_door", "icon_crouch", "icon_exit", "icon_stairs", "icon_check", "icon_arrow" };
        foreach (var name in names)
        {
            var tex = Render(name, Size, SS);
            var bytes = tex.EncodeToPNG();
            var path = OutDir + "/" + name + ".png";
            Directory.CreateDirectory(OutDir);
            File.WriteAllBytes(path, bytes);
            Object.DestroyImmediate(tex);
        }
        AssetDatabase.Refresh();
        Debug.Log("GuidanceIconGenerator: generated " + names.Length + " icons to " + OutDir);
    }

    static Texture2D Render(string name, int size, int ss)
    {
        int S = size * ss;
        var pixels = new Color[S * S];
        float scale = (float)size / S; // 归一化到 0..1

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // 归一化坐标（中心 0.5,0.5）
                float u = (float)x / S;
                float v = (float)y / S;
                var col = PaintIcon(name, u, v);
                pixels[y * S + x] = col;
            }

        // 超采样降采样
        var outTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var outPixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color acc = Color.clear;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                        acc += pixels[(y * ss + sy) * S + (x * ss + sx)];
                outPixels[y * size + x] = acc / (ss * ss);
            }
        outTex.SetPixels(outPixels);
        outTex.Apply();
        return outTex;
    }

    /// <summary>每图标绘制函数：输入归一化 uv (0-1)，返回颜色。</summary>
    static Color PaintIcon(string name, float u, float v)
    {
        // 通用 glow：距离中心越远越透明
        float dx = u - 0.5f, dy = v - 0.5f;
        float distC = Mathf.Sqrt(dx * dx + dy * dy);
        Color result = Color.clear;

        // 图标主体颜色（橙金渐变：上亮下深）
        Color grad = Color.Lerp(Amber, Deep, Mathf.Clamp01(0.5f - (v - 0.5f) + 0.5f));

        switch (name)
        {
            case "icon_power":
            {
                // 电源环
                float d = dist(u, v, 0.5f, 0.52f);
                if (d >= 0.14f && d <= 0.22f) result = Alpha(grad, d, 0.14f, 0.22f);
                // 竖线（电源符号的竖杠）
                else if (inRect(u, v, 0.5f, 0.56f, 0.03f, 0.14f, 0f)) result = Alpha(grad);
                // 把手（右上杠杆）
                else if (inRoundRect(u, v, 0.64f, 0.30f, 0.10f, 0.05f, 0.03f, -0.5f)) result = Alpha(grad);
                break;
            }
            case "icon_alarm":
            {
                // 铃铛：上半圆 + 下沿矩形 + 底部小圆
                if (inHalfCircle(u, v, 0.5f, 0.52f, 0.20f, true)) result = Alpha(grad);
                else if (inRect(u, v, 0.5f, 0.36f, 0.26f, 0.04f, 0f)) result = Alpha(grad);
                else if (inCircle(u, v, 0.5f, 0.28f, 0.05f)) result = Alpha(grad);
                // 声波弧线
                else if (inRingSegment(u, v, 0.80f, 0.52f, 0.24f, 0.27f, -0.5f, 0.5f)) result = Alpha(Amber, 0.7f);
                break;
            }
            case "icon_extinguisher":
            {
                // 灭火器瓶体
                if (inRoundRect(u, v, 0.48f, 0.45f, 0.11f, 0.22f, 0.06f, 0f)) result = Alpha(grad);
                // 瓶颈
                else if (inRect(u, v, 0.48f, 0.72f, 0.05f, 0.08f, 0f)) result = Alpha(grad);
                // 压力表
                else if (inCircle(u, v, 0.60f, 0.76f, 0.06f)) result = Alpha(White);
                // 喷管
                else if (inRect(u, v, 0.62f, 0.68f, 0.10f, 0.025f, 0.3f)) result = Alpha(grad);
                // 底座
                else if (inRect(u, v, 0.48f, 0.22f, 0.09f, 0.03f, 0f)) result = Alpha(grad);
                break;
            }
            case "icon_fire":
            {
                // 外焰（大水滴形）
                float d = dist(u, v, 0.5f, 0.42f);
                if (d <= 0.22f && v <= 0.58f) result = Alpha(Deep, Mathf.Clamp01((0.22f - d) / 0.06f));
                else if (d <= 0.22f && v > 0.58f && v < 0.72f && Mathf.Abs(u - 0.5f) < 0.10f) result = Alpha(Deep, Mathf.Clamp01((0.22f - d) / 0.06f));
                // 内焰（黄色小水滴）
                float d2 = dist(u, v, 0.5f, 0.38f);
                if (d2 <= 0.12f && v <= 0.50f) result = Alpha(Amber, Mathf.Clamp01((0.12f - d2) / 0.04f));
                break;
            }
            case "icon_towel":
            {
                // 毛巾主体
                if (inRoundRect(u, v, 0.50f, 0.48f, 0.22f, 0.16f, 0.04f, 0f)) result = Alpha(grad);
                // 折痕线
                else if (inRect(u, v, 0.50f, 0.48f, 0.20f, 0.015f, 0f)) result = Alpha(Deep, 0.8f);
                // 挂钩
                else if (inRing(u, v, 0.50f, 0.72f, 0.03f, 0.06f)) result = Alpha(grad);
                break;
            }
            case "icon_door":
            {
                // 门框
                if (inRect(u, v, 0.50f, 0.48f, 0.20f, 0.28f, 0f) && !inRect(u, v, 0.50f, 0.48f, 0.15f, 0.23f, 0f)) result = Alpha(grad);
                // 门板
                else if (inRect(u, v, 0.50f, 0.46f, 0.15f, 0.23f, 0f)) result = Alpha(grad, 0.85f);
                // 门把手
                else if (inCircle(u, v, 0.60f, 0.48f, 0.035f)) result = Alpha(White);
                break;
            }
            case "icon_crouch":
            {
                // 头部
                if (inCircle(u, v, 0.42f, 0.68f, 0.08f)) result = Alpha(grad);
                // 弯曲身体
                else if (inRoundRect(u, v, 0.52f, 0.50f, 0.14f, 0.07f, 0.05f, 0.4f)) result = Alpha(grad);
                // 腿
                else if (inRect(u, v, 0.62f, 0.32f, 0.05f, 0.12f, 0f)) result = Alpha(grad);
                break;
            }
            case "icon_exit":
            {
                // 门框
                if (inRect(u, v, 0.42f, 0.48f, 0.18f, 0.26f, 0f) && !inRect(u, v, 0.42f, 0.48f, 0.13f, 0.21f, 0f)) result = Alpha(grad);
                // 出口箭头（向右）
                else if (inRect(u, v, 0.60f, 0.48f, 0.10f, 0.04f, 0f)) result = Alpha(grad);
                else if (inTriangle(u, v, 0.70f, 0.48f, 0.62f, 0.56f, 0.62f, 0.40f)) result = Alpha(grad);
                break;
            }
            case "icon_stairs":
            {
                // 四级阶梯
                for (int i = 0; i < 4; i++)
                {
                    float sx = 0.28f + i * 0.13f;
                    float sy = 0.22f + i * 0.15f;
                    if (inRect(u, v, sx, sy, 0.14f, 0.07f, 0f)) { result = Alpha(grad); break; }
                }
                break;
            }
            case "icon_check":
            {
                // 绿色对勾（两段粗线）
                if (onSegment(u, v, 0.30f, 0.50f, 0.45f, 0.30f, 0.055f) || onSegment(u, v, 0.45f, 0.30f, 0.72f, 0.68f, 0.055f))
                    result = Alpha(Green);
                break;
            }
            case "icon_arrow":
            {
                // 下箭头：竖杆 + 三角头
                if (inRect(u, v, 0.50f, 0.58f, 0.05f, 0.20f, 0f)) result = Alpha(grad);
                else if (inTriangle(u, v, 0.50f, 0.22f, 0.30f, 0.50f, 0.70f, 0.50f)) result = Alpha(grad);
                break;
            }
        }

        // 外发光（柔和橙色辉光）
        if (result.a < 0.01f && distC < 0.42f)
        {
            float glow = Mathf.Clamp01(1f - distC / 0.42f) * 0.15f;
            result = new Color(Orange.r, Orange.g, Orange.b, glow);
        }
        return result;
    }

    // === 几何辅助 ===
    static float dist(float x1, float y1, float x2, float y2) { var dx = x1 - x2; var dy = y1 - y2; return Mathf.Sqrt(dx * dx + dy * dy); }

    static Color Alpha(Color c, float a = 1f) { return new Color(c.r, c.g, c.b, c.a * a); }
    static Color Alpha(Color c, float d, float inner, float outer) { return new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01((outer - d) / (outer - inner))); }

    static bool inCircle(float px, float py, float cx, float cy, float r) { var dx = px - cx; var dy = py - cy; return dx * dx + dy * dy <= r * r; }
    static bool inRing(float px, float py, float cx, float cy, float ri, float ro) { var dx = px - cx; var dy = py - cy; var d2 = dx * dx + dy * dy; return d2 >= ri * ri && d2 <= ro * ro; }
    static bool inRect(float px, float py, float cx, float cy, float hw, float hh, float rot)
    { var dx = px - cx; var dy = py - cy; var cr = Mathf.Cos(rot); var sr = Mathf.Sin(rot); return Mathf.Abs(dx * cr + dy * sr) <= hw && Mathf.Abs(-dx * sr + dy * cr) <= hh; }
    static bool inRoundRect(float px, float py, float cx, float cy, float hw, float hh, float rad, float rot)
    {
        var dx = px - cx; var dy = py - cy; var cr = Mathf.Cos(rot); var sr = Mathf.Sin(rot);
        var rx = dx * cr + dy * sr; var ry = -dx * sr + dy * cr;
        var qx = Mathf.Abs(rx) - hw + rad; var qy = Mathf.Abs(ry) - hh + rad;
        if (qx > 0 && qy > 0) return qx * qx + qy * qy <= rad * rad;
        return Mathf.Abs(rx) <= hw && Mathf.Abs(ry) <= hh;
    }
    static bool inHalfCircle(float px, float py, float cx, float cy, float r, bool top)
    { var dx = px - cx; var dy = py - cy; if (top && dy > 0) return false; if (!top && dy < 0) return false; return dx * dx + dy * dy <= r * r; }
    static bool inRingSegment(float px, float py, float cx, float cy, float ri, float ro, float a0, float a1)
    {
        var dx = px - cx; var dy = py - cy;
        float d2 = dx * dx + dy * dy; if (d2 < ri * ri || d2 > ro * ro) return false;
        float angle = Mathf.Atan2(dy, dx); if (angle < 0) angle += Mathf.PI * 2;
        return angle >= a0 && angle <= a1;
    }
    static bool inTriangle(float px, float py, float x1, float y1, float x2, float y2, float x3, float y3)
    {
        float d1 = (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
        float d2 = (px - x3) * (y2 - y3) - (x2 - x3) * (py - y3);
        float d3 = (px - x1) * (y3 - y1) - (x3 - x1) * (py - y1);
        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }
    static bool onSegment(float px, float py, float x1, float y1, float x2, float y2, float width)
    {
        float dx = x2 - x1, dy = y2 - y1;
        float lenSq = dx * dx + dy * dy;
        float t = Mathf.Clamp01(((px - x1) * dx + (py - y1) * dy) / lenSq);
        float cx = x1 + t * dx, cy = y1 + t * dy;
        float ex = px - cx, ey = py - cy;
        return ex * ex + ey * ey <= width * width;
    }
}
