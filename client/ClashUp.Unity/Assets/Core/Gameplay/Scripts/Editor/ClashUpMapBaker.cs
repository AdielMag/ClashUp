using System.Collections.Generic;
using System.IO;
using System.Linq;
using AetherNet;
using ClashUp.Client.Gameplay;
using ClashUp.Shared.Maps;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace ClashUp.Client.Gameplay.Editor
{
    public static class ClashUpMapBaker
    {
        [MenuItem("ClashUp/Bake Map to JSON")]
        public static void BakeMap()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string savePath = EditorUtility.SaveFilePanel(
                "Save Baked Map", "Assets", sceneName, "json");
            if (string.IsNullOrEmpty(savePath)) return;

            var map = BuildMapData(sceneName);
            string json = JsonConvert.SerializeObject(map, Formatting.Indented);
            File.WriteAllText(savePath, json);

            if (savePath.StartsWith(Application.dataPath))
                AssetDatabase.ImportAsset("Assets" + savePath[Application.dataPath.Length..]);

            Debug.Log($"[ClashUp] Baked {map.Entities.Length} entities, {map.SpawnAreas.Length} spawn areas to: {savePath}");
            EditorUtility.DisplayDialog("ClashUp Map Baker",
                $"Baked {map.Entities.Length} entities, {map.SpawnAreas.Length} spawn areas.\n\nSaved to:\n{savePath}", "OK");
        }

        private static MapData BuildMapData(string sceneName)
        {
            var rigidbodies = Object.FindObjectsByType<AetherRigidbody>(FindObjectsSortMode.None);
            var entities = new List<BakedEntityDef>(rigidbodies.Length);

            float ppm = AetherNet.SimulationConstants.PixelsPerMeter;

            for (int i = 0; i < rigidbodies.Length; i++)
            {
                var rb = rigidbodies[i];
                var tf = rb.transform;
                entities.Add(new BakedEntityDef
                {
                    EntityId = i,
                    BodyType = (int)rb.BodyType,
                    PositionX = tf.position.x / ppm,
                    PositionY = tf.position.y / ppm,
                    Angle = AetherNet.MathExtensions.ToSimAngle(tf.eulerAngles.z),
                    LinearDamping = rb.LinearDamping,
                    AngularDamping = rb.AngularDamping,
                    GravityScale = rb.GravityScale,
                    FixedRotation = rb.FixedRotation,
                    Constraints = (int)rb.Constraints,
                    Fixtures = BuildFixtures(rb, ppm),
                });
            }

            var spawnAreas = BuildSpawnAreas();

            return new MapData
            {
                MapName = sceneName,
                Entities = entities.ToArray(),
                SpawnAreas = spawnAreas,
            };
        }

        private static BakedFixtureDef[] BuildFixtures(AetherRigidbody rb, float ppm)
        {
            var result = new List<BakedFixtureDef>();

            foreach (var box in rb.GetComponents<AetherBoxCollider>())
            {
                var so = new SerializedObject(box);
                result.Add(new BakedFixtureDef
                {
                    Shape = BakedFixtureShape.Box,
                    Width = so.FindProperty("_size").vector2Value.x / ppm,
                    Height = so.FindProperty("_size").vector2Value.y / ppm,
                    OffsetX = so.FindProperty("_offset").vector2Value.x / ppm,
                    OffsetY = so.FindProperty("_offset").vector2Value.y / ppm,
                    IsSensor = so.FindProperty("_isTrigger").boolValue,
                    Layer = so.FindProperty("_layer").intValue,
                    Density = ReadMaterialField(so, m => m.Density, 1f),
                    Friction = ReadMaterialField(so, m => m.Friction, 0.2f),
                    Restitution = ReadMaterialField(so, m => m.Restitution, 0f),
                });
            }

            foreach (var circle in rb.GetComponents<AetherCircleCollider>())
            {
                var so = new SerializedObject(circle);
                result.Add(new BakedFixtureDef
                {
                    Shape = BakedFixtureShape.Circle,
                    Radius = so.FindProperty("_radius").floatValue / ppm,
                    OffsetX = so.FindProperty("_offset").vector2Value.x / ppm,
                    OffsetY = so.FindProperty("_offset").vector2Value.y / ppm,
                    IsSensor = so.FindProperty("_isTrigger").boolValue,
                    Layer = so.FindProperty("_layer").intValue,
                    Density = ReadMaterialField(so, m => m.Density, 1f),
                    Friction = ReadMaterialField(so, m => m.Friction, 0.2f),
                    Restitution = ReadMaterialField(so, m => m.Restitution, 0f),
                });
            }

            foreach (var poly in rb.GetComponents<AetherPolygonCollider>())
            {
                var so = new SerializedObject(poly);
                var verts = so.FindProperty("_vertices");
                var xs = new float[verts.arraySize];
                var ys = new float[verts.arraySize];
                for (int v = 0; v < verts.arraySize; v++)
                {
                    Vector2 pt = verts.GetArrayElementAtIndex(v).vector2Value;
                    xs[v] = pt.x / ppm;
                    ys[v] = pt.y / ppm;
                }
                result.Add(new BakedFixtureDef
                {
                    Shape = BakedFixtureShape.Polygon,
                    VerticesX = xs,
                    VerticesY = ys,
                    IsSensor = so.FindProperty("_isTrigger").boolValue,
                    Layer = so.FindProperty("_layer").intValue,
                    Density = ReadMaterialField(so, m => m.Density, 1f),
                    Friction = ReadMaterialField(so, m => m.Friction, 0.2f),
                    Restitution = ReadMaterialField(so, m => m.Restitution, 0f),
                });
            }

            return result.ToArray();
        }

        private static SpawnArea[] BuildSpawnAreas()
        {
            var markers = Object.FindObjectsByType<SpawnPointMarker>(FindObjectsSortMode.None);
            if (markers.Length == 0) return System.Array.Empty<SpawnArea>();

            float ppm = AetherNet.SimulationConstants.PixelsPerMeter;

            var grouped = markers
                .GroupBy(m => m.TeamIndex)
                .OrderBy(g => g.Key);

            var areas = new List<SpawnArea>();
            foreach (var group in grouped)
            {
                var sorted = group.OrderBy(m => m.SlotIndex).ToArray();
                var posX = new float[sorted.Length];
                var posZ = new float[sorted.Length];
                for (int i = 0; i < sorted.Length; i++)
                {
                    // Bake scene uses XY plane; X→game X, Y→game Z (Aether Y)
                    posX[i] = sorted[i].transform.position.x / ppm;
                    posZ[i] = sorted[i].transform.position.y / ppm;
                }
                areas.Add(new SpawnArea
                {
                    TeamIndex = group.Key,
                    PositionsX = posX,
                    PositionsZ = posZ,
                });
            }
            return areas.ToArray();
        }

        private static float ReadMaterialField(
            SerializedObject so,
            System.Func<AetherPhysicsMaterial, float> getter,
            float fallback)
        {
            var mat = so.FindProperty("_material").objectReferenceValue as AetherPhysicsMaterial;
            return mat != null ? getter(mat) : fallback;
        }
    }
}
