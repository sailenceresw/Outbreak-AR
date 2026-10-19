using UnityEngine;

// The 3D model held for each gun. The pistol is the scene's own PT-9M; the others are low-poly CC0 models
// (Quaternius Ultimate Gun Pack) in Resources/Weapons, re-materialled as metal, polymer and wood.
// Shared by the game and the editor's first-person preview, so the preview shows exactly what plays.
public static class WeaponModels
{
    // the pack's models point their muzzle along -X with +Z up
    static readonly Vector3 ModelForward = Vector3.left;
    static readonly Vector3 ModelUp = Vector3.forward;

    struct HeldPose
    {
        public string resource;
        // overall length in the hand, in metres
        public float length;
        // where along the gun (0 = stock end, 1 = muzzle) the hand holds it; this point sits at the pistol's spot
        public float grip;
        // nudged from the pistol's spot so long guns sit lower and further right
        public Vector3 offset;
    }

    static HeldPose PoseFor(WeaponId id)
    {
        switch (id)
        {
            case WeaponId.Revolver:
                return new HeldPose { resource = "Weapons/Revolver", length = 0.28f, grip = 0.22f, offset = new Vector3(0f, -0.005f, 0f) };
            case WeaponId.Shotgun:
                return new HeldPose { resource = "Weapons/Shotgun", length = 0.75f, grip = 0.62f, offset = new Vector3(-0.01f, -0.035f, 0.24f) };
            case WeaponId.SMG:
                return new HeldPose { resource = "Weapons/SMG", length = 0.45f, grip = 0.45f, offset = new Vector3(0.02f, -0.02f, 0.05f) };
            case WeaponId.Rifle:
                return new HeldPose { resource = "Weapons/Rifle", length = 0.7f, grip = 0.6f, offset = new Vector3(0f, -0.045f, 0.24f) };
            default:
                return default;
        }
    }

    // true for guns that replace the scene's pistol model
    public static bool HasOwnModel(WeaponId id) => !string.IsNullOrEmpty(PoseFor(id).resource);

    // the gun's model, parented to the gun transform (so recoil and sway move it); null for the pistol.
    // muzzleLocal is where shots and flashes start, in the gun transform's space.
    public static GameObject Create(WeaponId id, Transform gun, out Vector3 muzzleLocal)
    {
        muzzleLocal = new Vector3(0f, 0f, 0.25f);
        HeldPose pose = PoseFor(id);
        if (string.IsNullOrEmpty(pose.resource)) return null;
        GameObject prefab = Resources.Load<GameObject>(pose.resource);
        if (prefab == null) return null;

        GameObject holder = new GameObject(id + " Model");
        holder.transform.SetParent(gun, false);
        holder.layer = gun.gameObject.layer;

        GameObject model = Object.Instantiate(prefab, holder.transform, false);
        model.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(ModelForward, ModelUp));
        model.transform.localPosition = Vector3.zero;
        model.transform.localScale = Vector3.one;
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gun.gameObject.layer;
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // measure in the holder's space: +Z is now the muzzle direction
        Bounds bounds = LocalBounds(model, holder.transform);
        float scale = pose.length / Mathf.Max(bounds.size.z, 0.0001f);
        model.transform.localScale = Vector3.one * scale;
        bounds = LocalBounds(model, holder.transform);

        // put the grip point (and the middle of the gun's height) at the holder's origin
        Vector3 gripPoint = new Vector3(bounds.center.x, bounds.center.y, Mathf.Lerp(bounds.min.z, bounds.max.z, pose.grip));
        model.transform.localPosition = -gripPoint;
        holder.transform.localPosition = pose.offset;

        muzzleLocal = pose.offset + new Vector3(0f, 0f, bounds.max.z - gripPoint.z);
        return holder;
    }

    static Bounds LocalBounds(GameObject model, Transform space)
    {
        bool any = false;
        Bounds bounds = new Bounds();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds mesh = filter.sharedMesh.bounds;
            Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = toSpace.MultiplyPoint3x4(corner);
                if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
}
