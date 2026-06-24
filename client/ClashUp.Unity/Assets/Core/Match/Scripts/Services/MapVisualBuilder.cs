using ClashUp.Shared.Maps;
using UnityEngine;

namespace ClashUp.Client.Match
{
    /// <summary>
    /// Builds a simple, readable arena visual straight from the baked <see cref="MapData"/> when a map
    /// has no authored visual prefab. Renders a tiled grid floor (so player movement is easy to read) plus
    /// a block for every static map entity, guaranteeing the visuals always match the physics geometry.
    ///
    /// No Unity colliders are added — collision is AetherNet-only (see scene-ownership rules). Breakable
    /// boxes are NOT drawn here; they're spawned/streamed and rendered by the box view system.
    /// </summary>
    public static class MapVisualBuilder
    {
        private const float WallHeight = 2f;
        private const float GroundY = 0f;

        private static readonly Color GroundFill = new(0.24f, 0.27f, 0.24f);
        private static readonly Color GroundLine = new(0.38f, 0.44f, 0.38f);
        private static readonly Color WallColor = new(0.50f, 0.52f, 0.58f);
        private static readonly Color Team0Pad = new(0.25f, 0.5f, 0.95f, 1f);
        private static readonly Color Team1Pad = new(0.95f, 0.35f, 0.3f, 1f);

        public static GameObject Build(MapData map)
        {
            var root = new GameObject($"MapVisual_{map.MapName}");

            ComputeBounds(map, out var center, out var size);
            BuildGround(root.transform, center, size);

            var wallMat = MakeMaterial(WallColor);
            foreach (var entity in map.Entities)
            {
                if (entity.Fixtures == null) continue;
                foreach (var fix in entity.Fixtures)
                {
                    if (fix.Shape != BakedFixtureShape.Box) continue; // arenas are box geometry
                    BuildBlock(root.transform, entity, fix, wallMat);
                }
            }

            BuildSpawnPads(root.transform, map);
            return root;
        }

        private static void BuildGround(Transform parent, Vector3 center, Vector3 size)
        {
            // Unity's Plane is 10x10 units at scale 1 and already lies in the XZ plane.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(center.x, GroundY, center.z);
            ground.transform.localScale = new Vector3(size.x / 10f, 1f, size.z / 10f);
            StripCollider(ground);

            var mat = MakeMaterial(Color.white);
            var grid = MakeGridTexture();
            mat.mainTexture = grid;
            mat.mainTextureScale = new Vector2(size.x, size.z); // 1 grid cell per world unit
            ground.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void BuildBlock(Transform parent, BakedEntityDef entity, BakedFixtureDef fix, Material mat)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = $"Wall_{entity.EntityId}";
            block.transform.SetParent(parent, false);
            block.transform.position = new Vector3(
                entity.PositionX + fix.OffsetX, WallHeight * 0.5f, entity.PositionY + fix.OffsetY);
            block.transform.rotation = Quaternion.Euler(0f, -entity.Angle * Mathf.Rad2Deg, 0f);
            block.transform.localScale = new Vector3(fix.Width, WallHeight, fix.Height);
            StripCollider(block);
            block.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void BuildSpawnPads(Transform parent, MapData map)
        {
            if (map.SpawnAreas == null) return;
            foreach (var area in map.SpawnAreas)
            {
                var mat = MakeMaterial(area.TeamIndex == 0 ? Team0Pad : Team1Pad);
                int count = Mathf.Min(area.PositionsX.Length, area.PositionsZ.Length);
                for (int i = 0; i < count; i++)
                {
                    var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pad.name = $"SpawnPad_{area.TeamIndex}_{i}";
                    pad.transform.SetParent(parent, false);
                    pad.transform.position = new Vector3(area.PositionsX[i], GroundY + 0.04f, area.PositionsZ[i]);
                    pad.transform.localScale = new Vector3(2.2f, 0.04f, 2.2f); // flat disc
                    StripCollider(pad);
                    pad.GetComponent<Renderer>().sharedMaterial = mat;
                }
            }
        }

        // Axis-aligned bounds over all box fixtures, with a small margin.
        private static void ComputeBounds(MapData map, out Vector3 center, out Vector3 size)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            bool any = false;

            if (map.Entities != null)
            {
                foreach (var e in map.Entities)
                {
                    if (e.Fixtures == null) continue;
                    foreach (var f in e.Fixtures)
                    {
                        if (f.Shape != BakedFixtureShape.Box) continue;
                        float cx = e.PositionX + f.OffsetX;
                        float cz = e.PositionY + f.OffsetY;
                        minX = Mathf.Min(minX, cx - f.Width * 0.5f);
                        maxX = Mathf.Max(maxX, cx + f.Width * 0.5f);
                        minZ = Mathf.Min(minZ, cz - f.Height * 0.5f);
                        maxZ = Mathf.Max(maxZ, cz + f.Height * 0.5f);
                        any = true;
                    }
                }
            }

            if (!any) { center = Vector3.zero; size = new Vector3(40f, 1f, 40f); return; }

            center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            size = new Vector3(maxX - minX, 1f, maxZ - minZ);
        }

        private static Material MakeMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Standard")) { color = color };
            mat.SetFloat("_Glossiness", 0f); // flat, no specular sheen
            return mat;
        }

        // A 1-cell grid tile (lines on two edges) that tiles seamlessly with TextureWrapMode.Repeat.
        private static Texture2D MakeGridTexture()
        {
            const int s = 128;
            const int lineW = 5;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[s * s];
            Color32 fill = GroundFill;
            Color32 line = GroundLine;
            for (int y = 0; y < s; y++)
            {
                bool yLine = y < lineW;
                for (int x = 0; x < s; x++)
                {
                    bool isLine = yLine || x < lineW;
                    pixels[y * s + x] = isLine ? line : fill;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) return;
            // Destroy at runtime; DestroyImmediate only when running in-editor (e.g. a tool preview).
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
        }
    }
}
