using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

// GUI/Text Shader рисует TextMesh поверх всего, поэтому надписи просвечивают сквозь стены
// и читаются зеркально с обратной стороны. Здесь TextMesh сцены получают материал
// RogueDrive/WorldText с проверкой глубины и отсечением обратной стороны.
public static class WorldTextMaterials
{
    const string Dir = "Assets/Art/WorldText/";

    public static int Apply(Scene scene)
    {
        var shader = Shader.Find("RogueDrive/WorldText");
        if(!AssetDatabase.IsValidFolder("Assets/Art/WorldText")) AssetDatabase.CreateFolder("Assets/Art", "WorldText");
        int count = 0;
        foreach(var root in scene.GetRootGameObjects())
        foreach(var text in root.GetComponentsInChildren<TextMesh>(true))
        {
            if(text.font == null) continue;
            string path = Dir + "WorldText_" + text.font.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.mainTexture = text.font.material.mainTexture;
            var renderer = text.GetComponent<MeshRenderer>();
            if(renderer.sharedMaterial == mat) continue;
            renderer.sharedMaterial = mat;
            EditorUtility.SetDirty(renderer);
            count++;
        }
        return count;
    }
}
