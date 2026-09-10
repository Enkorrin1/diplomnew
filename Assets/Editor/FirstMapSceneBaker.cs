#if UNITY_EDITOR
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    public static class FirstMapSceneBaker
    {
        [MenuItem("RogueDrive/First Map/Bake Into Active Scene")]
        public static void BakeIntoActiveScene()
        {
            ProceduralTrackGenerator generator = Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            if (generator == null)
            {
                Debug.LogError("Нужен объект с ProceduralTrackGenerator в открытой сцене.");
                return;
            }

            if (generator.transform.Find("Campaign_Maps") != null)
            {
                Selection.activeGameObject = generator.transform.Find("Campaign_Maps").gameObject;
                Debug.Log("Кампания уже сохранена: редактируйте Campaign_Maps в Hierarchy.");
                return;
            }
            FirstMapSetup.Build();
            generator.BakeFirstMapIntoScene();
            EditorSceneManager.SaveScene(generator.gameObject.scene);
            Debug.Log("Первая карта сохранена в сцене как BakedFirstMap. Все объекты можно редактировать вручную.");
        }
    }
}
#endif
