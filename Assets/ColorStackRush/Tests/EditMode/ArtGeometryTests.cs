using NUnit.Framework;
using UnityEngine;
namespace ColorStackRush.Tests
{
    public class ArtGeometryTests
    {
        [TestCase(PrimitiveType.Sphere)]
        [TestCase(PrimitiveType.Cube)]
        [TestCase(PrimitiveType.Cylinder)]
        [TestCase(PrimitiveType.Capsule)]
        [TestCase(PrimitiveType.Plane)]
        [TestCase(PrimitiveType.Quad)]
        public void CodeBuiltVisualsMatchUnityPrimitiveDimensions(PrimitiveType type)
        {
            var expected = GameObject.CreatePrimitive(type);
            var scale = new Vector3(.95f, .7f, 1.3f);
            expected.transform.localScale = scale;
            var actual = Primitives.Create(type, null, Vector3.zero, scale, null);
            try
            {
                Vector3 expectedSize = expected.GetComponent<MeshRenderer>().bounds.size;
                Vector3 actualSize = actual.GetComponent<MeshRenderer>().bounds.size;
                Assert.That(Vector3.Distance(actualSize, expectedSize), Is.LessThan(.001f),
                    type + ": visual " + actualSize + " versus Unity " + expectedSize);
                Assert.That(actual.GetComponent<Collider>(), Is.Null);
            }
            finally { Object.DestroyImmediate(actual); Object.DestroyImmediate(expected); }
        }
    }
}
