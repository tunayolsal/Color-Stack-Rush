using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ColorStackRush.Tests
{
    public class VisualFramingTests
    {
        Scene scene;
        string folder;
        [UnitySetUp] public IEnumerator Setup()
        {
            folder = Path.Combine(Path.GetTempPath(), "csr-frame-" + Guid.NewGuid());
            SaveManager.SetStorageDirectoryForTests(folder);
            SaveManager.Data.reducedMotion = true;
            SaveManager.Save();
            scene = SceneManager.CreateScene("CSR-Framing");
            SceneManager.SetActiveScene(scene);
            new GameObject("Bootstrap").AddComponent<GameBootstrapper>();
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(ps.gameObject);
            SaveManager.SetStorageDirectoryForTests(null);
            Time.timeScale = 1; Time.fixedDeltaTime = .02f;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        [UnityTest] public IEnumerator NormalSpeedPortraitKeepsPlayerAndRoadReadable()
        {
            GameManager.Instance.StartRun(RunConfig.Level(1));
            yield return new WaitForSecondsRealtime(.6f);
            var camera = Camera.main;
            var ball = PlayerController.Instance.transform.Find("Ball").GetComponent<MeshRenderer>();
            Assert.That(camera.fieldOfView, Is.EqualTo(45).Within(.01f));
            Assert.That(ball.bounds.size.x, Is.InRange(.94f, .96f));
            float screenY = camera.WorldToViewportPoint(ball.bounds.center).y;
            Assert.That(screenY, Is.InRange(.35f, .46f), "Player should sit near 58% from the top.");

            float originalAspect = camera.aspect;
            camera.aspect = 540f / 960;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            long triangles = 0;
            int visibleMeshes = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                visibleMeshes++;
                var mesh = filter.sharedMesh;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles) triangles += mesh.GetIndexCount(i) / 3;
            }
            camera.aspect = originalAspect;
            Debug.Log($"[CSR Framing] portrait visibleTriangles={triangles}, visibleMeshRenderers={visibleMeshes}, playerViewportY={screenY:F3}, playerDiameter={ball.bounds.size.x:F3}");
            Assert.That(triangles, Is.LessThanOrEqualTo(100000));
        }
    }
}
