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

        public int GetGroundElevationChange(Hex fromHex)
        {
            return hex.GetBottomLevel() - fromHex.GetBottomLevel();
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

        public int GetStandingLevel(HexSurface surface)
        {
            return surface == HexSurface.Bridge
                ? hex.Level + (hex.GetBridgeHeight() ?? 0)
                : hex.GetBottomLevel();
        }

        public int GetElevationChange(Hex fromHex, HexSurface fromSurface, HexSurface toSurface)
        {
            return hex.GetStandingLevel(toSurface) - fromHex.GetStandingLevel(fromSurface);
        }

        public IReadOnlyList<HexSurface> GetHexSurfaces()
        {
            return hex.GetBridgeHeight() != null
                ? [HexSurface.Ground, HexSurface.Bridge]
                : [HexSurface.Ground];
        }

        public HexSurface GetHighestSurface()
        {
            var surfaces = hex.GetHexSurfaces();
            return surfaces.Count == 1
                ? surfaces[0]
                : surfaces.OrderByDescending(hex.GetStandingLevel)
                    .ThenByDescending(s => s)
                    .First();
        }

        public bool CanStandOnGround(int unitHeight)
        {
            return unitHeight <= (hex.GetBridgeClearance() ?? int.MaxValue);
        }

        public bool HasHardPavement()
        {
            return hex.HasTerrain(MakaMekTerrains.Road) 
                   || hex.HasTerrain(MakaMekTerrains.Pavement) 
                   || hex.HasTerrain(MakaMekTerrains.Bridge);
        }

        public Terrain? GetRoadOrPavedTerrain()
        {
            var roadTerrain = hex.GetTerrain(MakaMekTerrains.Road);
            if (roadTerrain != null) return roadTerrain;
            var pavedTerrain = hex.GetTerrain(MakaMekTerrains.Pavement);
            if (pavedTerrain != null) return pavedTerrain;
            return hex.GetTerrain(MakaMekTerrains.Bridge);
        }

        public bool IsOnRoadOrBridge(Hex fromHex)
        {
            var toTerrain = hex.GetRoadOrPavedTerrain();
            var fromTerrain = fromHex.GetRoadOrPavedTerrain();
            return toTerrain?.Id is MakaMekTerrains.Bridge or MakaMekTerrains.Road
                && fromTerrain?.Id is MakaMekTerrains.Bridge or MakaMekTerrains.Road;
        }
    }
}
