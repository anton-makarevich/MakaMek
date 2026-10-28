using Sanet.MakaMek.Assets.Models.Units;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Assets.Services.PackageReaders;

/// <summary>
/// Parsed contents of an MMUX unit package: the unit data, its static image and, for v2 packages,
/// the optional sprite atlas with its metadata.
/// </summary>
/// <param name="Data">Unit data from <c>unit.json</c>.</param>
/// <param name="Image">Static <c>unit.png</c>, always present and used as the reduced-motion/idle fallback.</param>
/// <param name="Animation">Optional v2 sprite atlas and metadata; <c>null</c> for v1 or broken sprite entries.</param>
/// <param name="SpriteError">Why an optional v2 sprite payload was skipped, so the caller can log it.</param>
public sealed record UnitPackage(
    UnitData Data,
    byte[] Image,
    UnitAnimationData? Animation = null,
    string? SpriteError = null);
