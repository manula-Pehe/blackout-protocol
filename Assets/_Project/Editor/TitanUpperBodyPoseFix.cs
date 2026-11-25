using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BlackoutProtocol.EditorTools
{
    public static class TitanUpperBodyPoseFix
    {
        [MenuItem("Blackout Protocol/Animation/Fix upper-body idle pose")]
        public static void Fix()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string path = "Assets/_Project/Animations/AgentBody/Titan/Completed/TitanCompleted.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) { Debug.LogError("Build TitanCompleted.controller first."); return; }
            var layers = controller.layers;
            int index = System.Array.FindIndex(layers, l => l.name == "Upper Body");
            if (index < 0) { Debug.LogError("Upper Body layer not found."); return; }
            Undo.RecordObject(controller, "Fix Titan upper-body idle pose");
            layers[index].defaultWeight = 0f;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Titan upper-body idle pose fix saved. Attack/Alert temporarily enable the layer at runtime.");
        }
    }
}
