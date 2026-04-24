using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Islands.EditorTools.Tests
{
    public sealed class IslandDefaultMaterialTests
    {
        private const string DefaultMaterialPath = "Assets/IslandShape/IslandShapeDefault.mat";

        [Test]
        public void DefaultIslandMaterial_UsesTriplanarCheckerShader()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);

            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("IslandShape/Triplanar Checker"));
        }

        [Test]
        public void DefaultIslandMaterial_ExposesCheckerColorsAndWorldTileSize()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);

            Assert.That(material, Is.Not.Null);
            Assert.That(material.HasProperty("_CheckerColorA"), Is.True);
            Assert.That(material.HasProperty("_CheckerColorB"), Is.True);
            Assert.That(material.HasProperty("_TileSize"), Is.True);
            Assert.That(material.GetTexture("_CheckerTex"), Is.Not.Null);
            AssertColor(material.GetColor("_CheckerColorA"), new Color(0.47f, 0.66f, 0.34f, 1f));
            AssertColor(material.GetColor("_CheckerColorB"), new Color(0.70f, 0.84f, 0.48f, 1f));
            Assert.That(material.GetFloat("_TileSize"), Is.EqualTo(3f).Within(0.0001f));
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }
    }
}
