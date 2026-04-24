using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Islands.EditorTools
{
    internal static class IslandShapeEditorUtility
    {
        private const string DefaultMaterialPath = "Assets/IslandShape/IslandShapeDefault.mat";
        private const string DefaultWaterMaterialPath = "Assets/IslandShape/IslandWaterDefault.mat";
        private const string DefaultIslandShaderName = "IslandShape/Triplanar Checker";
        private const string UnityDefaultCheckerTextureName = "PreTextureRGB";
        private const float DefaultCheckerTileSize = 3f;
        private static readonly Color DefaultCheckerColorA = new Color(0.47f, 0.66f, 0.34f);
        private static readonly Color DefaultCheckerColorB = new Color(0.70f, 0.84f, 0.48f);

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
                var shader = Shader.Find(DefaultIslandShaderName) ??
                             Shader.Find("Universal Render Pipeline/Lit") ??
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

                ConfigureDefaultIslandMaterial(material);

                AssetDatabase.CreateAsset(material, DefaultMaterialPath);
                AssetDatabase.SaveAssets();
            }

            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        private static void ConfigureDefaultIslandMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_CheckerTex"))
            {
                material.SetTexture("_CheckerTex", LoadDefaultCheckerTexture());
            }

            if (material.HasProperty("_CheckerColorA"))
            {
                material.SetColor("_CheckerColorA", DefaultCheckerColorA);
            }

            if (material.HasProperty("_CheckerColorB"))
            {
                material.SetColor("_CheckerColorB", DefaultCheckerColorB);
            }

            if (material.HasProperty("_TileSize"))
            {
                material.SetFloat("_TileSize", DefaultCheckerTileSize);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", DefaultCheckerColorA);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", DefaultCheckerColorA);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.15f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
        }

        private static Texture2D LoadDefaultCheckerTexture()
        {
            return EditorGUIUtility.Load(UnityDefaultCheckerTextureName) as Texture2D ??
                   EditorGUIUtility.FindTexture(UnityDefaultCheckerTextureName) ??
                   Texture2D.grayTexture;
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

