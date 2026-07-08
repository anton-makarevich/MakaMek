using Avalonia.Media;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// Data for rendering a hex boundary outline on the HexMap canvas.
/// </summary>
public record BorderOutlineData(byte EdgeMask, Color Color, double Thickness);