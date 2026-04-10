using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Islands.EditorTools
{
    internal static class IslandShapeEditorUtility
    {
        private const string DefaultMaterialPath = "Assets/IslandShape/IslandShapeDefault.mat";
        private const string DefaultWaterMaterialPath = "Assets/IslandShape/IslandWaterDefault.mat";

        internal static void ActivateTool(IslandShape island)
        {
            if (island == null)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (island == null)
                {
                    return;
                }

                Selection.activeGameObject = island.gameObject;

                EditorApplication.delayCall += () =>
                {
                    if (island == null)
                    {
                        return;
                    }

                    Selection.activeGameObject = island.gameObject;
                    ToolManager.RefreshAvailableTools();
                    ToolManager.SetActiveTool<IslandShapeTool>();
                    SceneView.RepaintAll();
                };
            };
        }

        internal static void EnsureDefaultMaterial(IslandShape island)
        {
            if (island == null)
            {
                return;
            }

            island.EnsureSetup();
            var renderer = island.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial != null)
            {
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                             Shader.Find("Universal Render Pipeline/Simple Lit") ??
                             Shader.Find("Standard");
                if (shader == null)
                {
                    return;
                }

                material = new Material(shader)
                {
                    name = "Island Shape Default"
                };

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", new Color(0.57f, 0.73f, 0.43f));
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", 0.15f);
                }

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", 0f);
                }

                AssetDatabase.CreateAsset(material, DefaultMaterialPath);
                AssetDatabase.SaveAssets();
            }

            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        internal static void EnsureDefaultWaterMaterial(IslandWater water)
        {
            if (water == null)
            {
                return;
            }

            var renderer = water.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial != null)
            {
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(DefaultWaterMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("IslandShape/Water Vertex Color") ??
                             Shader.Find("Universal Render Pipeline/Unlit") ??
                             Shader.Find("Standard");
                if (shader == null)
                {
                    return;
                }

                material = new Material(shader)
                {
                    name = "Island Water Default"
                };

                if (material.HasProperty("_GlobalTint"))
                {
                    material.SetColor("_GlobalTint", Color.white);
                }

                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                }

                AssetDatabase.CreateAsset(material, DefaultWaterMaterialPath);
                AssetDatabase.SaveAssets();
            }

            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }
    }
}

