namespace ClashUp.Shared.Maps
{
    public enum BakedFixtureShape { Box, Circle, Polygon }

    public class BakedFixtureDef
    {
        public BakedFixtureShape Shape { get; set; }
        public float Width { get; set; } = 1f;
        public float Height { get; set; } = 1f;
        public float Radius { get; set; } = 0.5f;
        public float[] VerticesX { get; set; } = System.Array.Empty<float>();
        public float[] VerticesY { get; set; } = System.Array.Empty<float>();
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public float Density { get; set; } = 1f;
        public float Friction { get; set; } = 0.2f;
        public float Restitution { get; set; }
        public bool IsSensor { get; set; }
        public int Layer { get; set; }
    }

    public class BakedEntityDef
    {
        public int EntityId { get; set; }
        public int BodyType { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float Angle { get; set; }
        public float LinearDamping { get; set; }
        public float AngularDamping { get; set; }
        public float GravityScale { get; set; } = 1f;
        public bool FixedRotation { get; set; }
        public int Constraints { get; set; }
        public BakedFixtureDef[] Fixtures { get; set; } = System.Array.Empty<BakedFixtureDef>();
    }

    public class SpawnArea
    {
        public int TeamIndex { get; set; }
        public float[] PositionsX { get; set; } = System.Array.Empty<float>();
        public float[] PositionsZ { get; set; } = System.Array.Empty<float>();
    }

    public class MapData
    {
        public string MapName { get; set; } = string.Empty;
        public BakedEntityDef[] Entities { get; set; } = System.Array.Empty<BakedEntityDef>();
        public SpawnArea[] SpawnAreas { get; set; } = System.Array.Empty<SpawnArea>();
    }
}
