using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BK.Kit.Editor
{
    public static class GardenPresentationBuilder
    {
        [MenuItem("BK/Integration/Apply Garden Presentation")]
        public static void Apply()
        {
            foreach(string suffix in new[]{"G","B","P","Y","5","6","7","8","9"})
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(RippleBoxSetup.Root+"/Ripple_"+suffix+".mat");
                m.SetColor("_SColor",new Color(.48f,.56f,.68f));
                m.SetColor("_HColor",Color.white);
                m.SetColor("_MatCapColor",new Color(.25f,.25f,.25f));
                m.SetFloat("_RampThreshold",.5f);m.SetFloat("_RampSmoothing",.55f);
                EditorUtility.SetDirty(m);
            }
            TintCannon("Blued iron",new Color(.055f,.29f,.47f),.58f,.25f);
            TintCannon("Brushed brass",new Color(.9f,.54f,.11f),.48f,.35f);
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Bounce/Scenes/VisualIngame.unity");
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.55f,.62f,.7f);
            var sun=Object.FindFirstObjectByType<Light>();
            sun.transform.rotation=Quaternion.Euler(50,-35,0);
            sun.color=new Color(1,.95f,.83f);sun.intensity=1.05f;
            sun.shadows=LightShadows.Soft;sun.shadowStrength=.55f;sun.shadowBias=.025f;sun.shadowNormalBias=.12f;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("GARDEN_PRESENTATION_APPLIED");
        }
        private static void TintCannon(string name,Color color,float smooth,float metallic)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Bounce/Content/Cannon/"+name+".mat");
            material.color=color;material.SetFloat("_Smoothness",smooth);material.SetFloat("_Metallic",metallic);
            EditorUtility.SetDirty(material);
        }
    }
}
