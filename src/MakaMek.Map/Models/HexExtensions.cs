using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Models;

public static class HexExtensions
{
    extension(Hex hex)
    {
        public Hex WithTerrain(Terrain terrain)
        {
            hex.AddTerrain(terrain);
            return hex;
        }

        public Hex WithLevel(int level)
        {
            hex.Level = level;
            return hex;
        }

        /// <summary>
        /// Gets the water depth of the hex as a positive integer.
        /// Returns null if the hex has no water terrain.
        /// Returns 0 for shallow water (Height 0), 1 for depth -1, 2 for depth -2, etc.
        /// </summary>
        public int? GetWaterDepth()
        {
            var waterTerrain = hex.GetTerrain(MakaMekTerrains.Water);
            return -waterTerrain?.Height;
        }

        public int GetBottomLevel()
        {
            return hex.Level - (hex.GetWaterDepth() ?? 0);
        }

        public int GetElevationChangeTo(Hex? toHex)
        {
            if (toHex == null) return 0;
            var effectiveFromLevel = hex.GetBottomLevel();
            var effectiveToLevel = toHex.GetBottomLevel();
            return effectiveToLevel - effectiveFromLevel;
        }

        public int? GetBridgeHeight()
        {
            return hex.GetTerrain(MakaMekTerrains.Bridge)?.Height;
        }

        public int? GetBridgeClearance()
        {
            if (hex.GetBridgeHeight() is not { } bridgeHeight) return null;
            return (hex.Level + bridgeHeight) - hex.GetBottomLevel();
        }

        public bool HasHardPavement()
        {
            return hex.HasTerrain(MakaMekTerrains.Road) 
                   || hex.HasTerrain(MakaMekTerrains.Pavement) 
                   || hex.HasTerrain(MakaMekTerrains.Bridge);
        }
        
        public MakaMekTerrains? GetRoadOrPavedTerrainId()
        {
            if (hex.HasTerrain(MakaMekTerrains.Road)) return MakaMekTerrains.Road;
            if (hex.HasTerrain(MakaMekTerrains.Pavement)) return MakaMekTerrains.Pavement;
            if (hex.HasTerrain(MakaMekTerrains.Bridge)) return MakaMekTerrains.Bridge;
            return null;
        }

        public bool IsOnRoadOrBridge(Hex fromHex)
        {
            var toTerrain = hex.GetRoadOrPavedTerrainId();
            var fromTerrain = fromHex.GetRoadOrPavedTerrainId();
            return toTerrain is MakaMekTerrains.Bridge or MakaMekTerrains.Road
                && fromTerrain is MakaMekTerrains.Bridge or MakaMekTerrains.Road;
        }
    }
}
