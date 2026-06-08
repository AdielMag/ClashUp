using System;
using ClashUp.Shared.Simulation;

namespace ClashUp.Shared.Maps
{
    public static class SpawnResolver
    {
        public static (float x, float z) GetSpawnPosition(MapData? mapData, int teamId, int slotIndex)
        {
            if (mapData?.SpawnAreas == null)
                return (slotIndex * MovementModel.SpawnSpacing, 0f);

            for (int i = 0; i < mapData.SpawnAreas.Length; i++)
            {
                var area = mapData.SpawnAreas[i];
                if (area.TeamIndex != teamId) continue;

                int count = Math.Min(area.PositionsX.Length, area.PositionsZ.Length);
                if (count == 0) break;

                int idx = slotIndex % count;
                return (area.PositionsX[idx], area.PositionsZ[idx]);
            }

            return (slotIndex * MovementModel.SpawnSpacing, 0f);
        }
    }
}
