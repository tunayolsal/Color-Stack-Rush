using UnityEngine;

namespace ColorStackRush
{
    // Both wall colliders resolve to the same root Obstacle and pool lease.
    // Reconfiguration changes actual geometry rather than scaling the root.
    [RequireComponent(typeof(Obstacle))]
    public sealed class RouteGate : MonoBehaviour
    {
        Transform left, right;
        BoxCollider leftCollider, rightCollider;
        Transform leftBumper, rightBumper;
        public float GapCentre { get; private set; }
        public float GapWidth { get; private set; }
        void Awake() => BindParts();
        void BindParts()
        {
            left = transform.Find("Left"); right = transform.Find("Right");
            leftCollider = left.GetComponent<BoxCollider>(); rightCollider = right.GetComponent<BoxCollider>();
            leftBumper = transform.Find("LeftBumper"); rightBumper = transform.Find("RightBumper");
        }
        public void Setup(float gapCentre, float gapWidth, float roadWidth = 7)
        {
            // Inactive pooled clones and EditMode construction can be configured
            // before Unity invokes Awake. Binding also handles such first use.
            if (left == null || right == null) BindParts();
            GapCentre = gapCentre; GapWidth = gapWidth;
            float half = roadWidth * .5f, gapLeft = gapCentre - gapWidth * .5f, gapRight = gapCentre + gapWidth * .5f;
            Configure(left, leftCollider, leftBumper, (-half + gapLeft) * .5f, gapLeft + half);
            Configure(right, rightCollider, rightBumper, (gapRight + half) * .5f, half - gapRight);
        }
        static void Configure(Transform wall, BoxCollider collider, Transform bumper, float centreX, float width)
        {
            wall.localPosition = new Vector3(centreX, .6f, 0);
            Vector3 meshSize = wall.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            wall.localScale = new Vector3(width / meshSize.x, 1.2f / meshSize.y, .85f / meshSize.z);
            // Collider inherits the exact normalized visible cuboid dimensions.
            collider.center = Vector3.zero; collider.size = meshSize;
            bumper.localPosition = new Vector3(centreX, .72f, -.48f);
            Vector3 bumperSize = bumper.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            bumper.localScale = new Vector3(width / bumperSize.x, .25f / bumperSize.y, .12f / bumperSize.z);
        }
    }
}
