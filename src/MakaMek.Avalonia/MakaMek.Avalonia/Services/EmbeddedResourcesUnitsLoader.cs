using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Services;

public class EmbeddedResourcesUnitsLoader : IUnitsLoader
{
    private readonly IUnitDataProvider _mtfDataProvider;

    public EmbeddedResourcesUnitsLoader(IUnitDataProvider unitDataProvider)
    {
        _mtfDataProvider = unitDataProvider;
    }

    public async Task<List<UnitData>> LoadUnits()
    {
        var assembly = typeof(App).Assembly;
        var resources = assembly.GetManifestResourceNames();

        var units = new List<UnitData>();
        foreach (var resourceName in resources)
        {
            if (!resourceName.EndsWith(".mtf", StringComparison.OrdinalIgnoreCase)) continue;
            await using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;
            using var reader = new StreamReader(stream);
            var mtfData = await reader.ReadToEndAsync();
            var lines = mtfData.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            var mechData = _mtfDataProvider.LoadMechFromTextData(lines);
                
            units.Add(mechData);
        }

        return units;
    }
}
