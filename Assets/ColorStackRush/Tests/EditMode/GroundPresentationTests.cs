using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ColorStackRush.Tests
{
    public class GroundPresentationTests
    {
        static void AssertColor(Color actual, Color expected)
        {
            // Material colors round-trip through Unity's native representation.
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.00001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.00001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.00001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(.00001f));
        }

        static GameObject MakeTemplate(Transform parent)
        {
            var tile = new GameObject("ground-template");
            tile.SetActive(false);
            tile.transform.SetParent(parent, false);
            foreach (string name in new[] { "Surface", "ShoulderL", "ShoulderR" })
                new GameObject(name, typeof(MeshRenderer)).transform.SetParent(tile.transform, false);
            foreach (string name in new[] { "Decor_tree-high", "Decor_plant", "Decor_rocks-low", "Decor_stones" })
                new GameObject(name).transform.SetParent(tile.transform, false);
            var cache = tile.AddComponent<GroundPresentationCache>();
            cache.Warmup();
            cache.ApplyTheme(0);
            return tile;
        }

        [Test]
        public void InactivePoolClonesRemapCachedChildrenAndKeepTemplateUnchanged()
        {
            var root = new GameObject("ground-cache-clones");
            try
            {
                var template = MakeTemplate(root.transform);
                var pool = new ObjectPool(template, root.transform, 2);
                var first = pool.Get(Vector3.zero, Quaternion.identity);
                var second = pool.Get(Vector3.forward, Quaternion.identity);
                // Names no longer identify the children after warmup: cached references still apply.
                first.transform.Find("Surface").name = "RenamedSurface";
                first.GetComponent<GroundPresentationCache>().ApplyTheme(1);
                second.GetComponent<GroundPresentationCache>().ApplyTheme(2);
                AssertColor(first.transform.Find("RenamedSurface").GetComponent<MeshRenderer>().sharedMaterial.color, ThemePresentation.Ground(1));
                AssertColor(second.transform.Find("Surface").GetComponent<MeshRenderer>().sharedMaterial.color, ThemePresentation.Ground(2));
                AssertColor(template.transform.Find("Surface").GetComponent<MeshRenderer>().sharedMaterial.color, ThemePresentation.Ground(0));
                AssertColor(first.transform.Find("ShoulderL").GetComponent<MeshRenderer>().sharedMaterial.color, ThemePresentation.Shoulder(1));
                Assert.That(first.transform.Find("Decor_plant").gameObject.activeSelf, Is.False);
                Assert.That(second.transform.Find("Decor_tree-high").gameObject.activeSelf, Is.True);
                Assert.That(template.transform.Find("Decor_plant").gameObject.activeSelf, Is.True);
                pool.Release(first);
                Assert.That(pool.Get(Vector3.zero, Quaternion.identity), Is.SameAs(first));
                first.GetComponent<GroundPresentationCache>().ApplyTheme(0);
                Assert.That(first.transform.Find("Decor_plant").gameObject.activeSelf, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void TenThousandStreamedThemeApplicationsAllocateNothingAfterWarmup()
        {
            var root = new GameObject("ground-cache-allocation");
            try
            {
                var tile = MakeTemplate(root.transform);
                tile.SetActive(true);
                for (int i = 0; i < 100; i++) ThemePresentation.TintGround(tile, i % 3);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++) ThemePresentation.TintGround(tile, i % 3);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void LastLevelPageShowsOnlyValidIdsWithoutOverflow()
        {
            string folder = Path.Combine(Path.GetTempPath(), "csr-level-page-" + Guid.NewGuid().ToString("N"));
            SaveManager.SetStorageDirectoryForTests(folder);
            var root = new GameObject("level-page", typeof(RectTransform));
            try
            {
                SaveManager.Data.highestUnlockedLevel = long.MaxValue;
                var panel = root.AddComponent<LevelSelectPanel>();
                panel.Build();
                panel.Refresh();
                var card = root.transform.Find("Card");
                long first = (long.MaxValue - 1) / 12 * 12 + 1;
                Assert.That(card.Find("Page").GetComponent<Text>().text, Is.EqualTo(first + " – " + long.MaxValue));
                int visible = 0;
                for (int i = 0; i < 12; i++)
                {
                    var button = card.Find("Level" + i).GetComponent<Button>();
                    bool valid = i <= long.MaxValue - first;
                    Assert.That(button.gameObject.activeSelf, Is.EqualTo(valid));
                    if (valid)
                    {
                        visible++;
                        Assert.That(button.interactable, Is.True);
                        Assert.That(button.GetComponentInChildren<Text>().text, Does.StartWith("Bölüm " + (first + i) + "\n"));
                    }
                    else Assert.That(button.interactable, Is.False);
                }
                Assert.That(visible, Is.EqualTo(7));
                Assert.That(card.Find("Next").GetComponent<Button>().interactable, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                SaveManager.SetStorageDirectoryForTests(null);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
