using UnityEngine;
using UnityEngine.Rendering;

// Puts a URP Lit material into one of three surface modes, setting the properties, keywords, tags and
// render queue together so the result matches what the material inspector would produce.
// Used by the material set-up tools, which run in batch builds where no inspector is open.
public static class UrpSurface
{
    const string LitShader = "Universal Render Pipeline/Lit";

    public static bool UseLit(Material material)
    {
        Shader lit = Shader.Find(LitShader);
        if (lit == null)
        {
            Debug.LogError("UrpSurface: " + LitShader + " shader not found");
            return false;
        }
        if (material.shader != lit) material.shader = lit;
        return true;
    }

    // solid surface: skin, metal, polymer
    public static void Opaque(Material material, bool doubleSided = false)
    {
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_Blend", 0f);
        SetBlend(material, BlendMode.One, BlendMode.Zero, zWrite: true);
        SetAlphaClip(material, false, 0.5f);
        SetCull(material, doubleSided);
        material.SetOverrideTag("RenderType", "Opaque");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Geometry;
    }

    // opaque with holes cut where the texture's alpha falls below the cutoff: hair cards, decals, foliage
    public static void Cutout(Material material, float cutoff = 0.5f, bool doubleSided = true)
    {
        Opaque(material, doubleSided);
        SetAlphaClip(material, true, cutoff);
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
    }

    // alpha-blended surface: glass, lenses
    public static void Transparent(Material material, bool doubleSided = false)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        SetBlend(material, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, zWrite: false);
        SetAlphaClip(material, false, 0.5f);
        SetCull(material, doubleSided);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    static void SetBlend(Material material, BlendMode source, BlendMode destination, bool zWrite)
    {
        material.SetFloat("_SrcBlend", (float)source);
        material.SetFloat("_DstBlend", (float)destination);
        material.SetFloat("_SrcBlendAlpha", (float)source);
        material.SetFloat("_DstBlendAlpha", (float)destination);
        material.SetFloat("_ZWrite", zWrite ? 1f : 0f);
    }

    static void SetAlphaClip(Material material, bool on, float cutoff)
    {
        material.SetFloat("_AlphaClip", on ? 1f : 0f);
        material.SetFloat("_Cutoff", cutoff);
        if (on) material.EnableKeyword("_ALPHATEST_ON");
        else material.DisableKeyword("_ALPHATEST_ON");
    }

    static void SetCull(Material material, bool doubleSided)
    {
        material.SetFloat("_Cull", (float)(doubleSided ? CullMode.Off : CullMode.Back));
    }
}
