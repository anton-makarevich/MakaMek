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

        /// <summary>
        /// Computes the effective standing level and submersion flag for a LOS endpoint.
        ///
        /// Occupied case (surface supplied): uses that surface directly for standing level.
        ///   Submerged only when on Ground surface with water depth meeting/exceeding unit height.
        ///
        /// Empty case (surface null): if the hex has a bridge, resolves to Bridge surface
        ///   (never submerged); otherwise resolves to Ground with the normal water-depth
        ///   submersion rule.
        /// The empty-hex optimistic bridge inference is intentionally forward-looking,
        /// keeping bridge hexes visible/targetable and supporting future bridge-as-target functionality.
        /// </summary>
        public (int EffectiveStandingLevel, bool IsSubmerged) GetLosEndpointInfo(HexSurface? surface, int unitHeight)
        {
            var effectiveSurface = surface ?? (hex.GetBridgeHeight() != null ? HexSurface.Bridge : HexSurface.Ground);
            var effectiveStandingLevel = hex.GetStandingLevel(effectiveSurface);

            var waterDepth = hex.GetWaterDepth();
            var isSubmerged = effectiveSurface == HexSurface.Ground
                              && waterDepth is > 0
                              && waterDepth >= unitHeight;

            return (effectiveStandingLevel, isSubmerged);
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

        public int? GetRoadSurfaceLevel()
        {
            var roadTerrain = hex.GetRoadOrPavedTerrain();
            if (roadTerrain == null) return null;
            return roadTerrain.Id == MakaMekTerrains.Bridge
                ? hex.GetStandingLevel(HexSurface.Bridge)
                : hex.GetStandingLevel(HexSurface.Ground);
        }

        public bool CanRoadConnectTo(Hex neighbor)
        {
            var myLevel = hex.GetRoadSurfaceLevel();
            var neighborLevel = neighbor.GetRoadSurfaceLevel();
            if (myLevel == null || neighborLevel == null) return false;
            return Math.Abs(myLevel.Value - neighborLevel.Value) < 2;
        }

        public bool IsOnRoadOrBridge(Hex fromHex, HexSurface fromSurface, HexSurface toSurface)
        {
            var toTerrain = hex.GetRoadOrPavedTerrain();
            var fromTerrain = fromHex.GetRoadOrPavedTerrain();

            return IsAppropriate(fromTerrain, fromSurface) && IsAppropriate(toTerrain, toSurface);

            bool IsAppropriate(Terrain? terrain, HexSurface surface) => (terrain?.Id, surface) switch
            {
                (MakaMekTerrains.Road, HexSurface.Ground) => true,
                (MakaMekTerrains.Bridge, HexSurface.Bridge) => true,
                _ => false
            };
        }
    }
}
