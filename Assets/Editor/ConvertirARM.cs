using UnityEngine;
using UnityEditor;
using System.IO;

public class ConvertirARM
{
    [MenuItem("Assets/Convertir ARM a Unity")]
    static void Convertir()
    {
        var arm = Selection.activeObject as Texture2D;
        if (arm == null) { Debug.LogError("Selecciona la textura ARM"); return; }

        string ruta = AssetDatabase.GetAssetPath(arm);
        Color[] px = arm.GetPixels();

        Color[] metal = new Color[px.Length];  // R = metallic, A = smoothness
        Color[] ao = new Color[px.Length];     // AO en gris

        for (int i = 0; i < px.Length; i++)
        {
            float oclusion = px[i].r;
            float rugosidad = px[i].g;
            float metalico = px[i].b;

            metal[i] = new Color(metalico, 0f, 0f, 1f - rugosidad);
            ao[i] = new Color(oclusion, oclusion, oclusion, 1f);
        }

        Guardar(metal, arm.width, arm.height, Path.ChangeExtension(ruta, null) + "_metallic.png");
        Guardar(ao, arm.width, arm.height, Path.ChangeExtension(ruta, null) + "_ao.png");
        AssetDatabase.Refresh();
        Debug.Log("Listo: _metallic.png y _ao.png creados");
    }

    static void Guardar(Color[] px, int w, int h, string ruta)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        t.Apply();
        File.WriteAllBytes(ruta, t.EncodeToPNG());
    }
}