using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Community;

public class MechFactoryIntegrationTests
{   
    private const string MechDirectory = "Resources/Mechs";
    
    [Fact]
    public void CreateFromMtfFiles_IntegrationTest()
    {
        // Arrange
        var parser = new MtfDataProvider();
        var structureValueProvider = new ClassicBattletechRulesProvider(); // Use actual provider
        var mechFactory = new MechFactory(structureValueProvider, Substitute.For<ILocalizationService>());

        // Get all MTF files from Resources/Mechs
        var mtfFiles = Directory.GetFiles(MechDirectory, "*.mtf");

        foreach (var mtfFile in mtfFiles)
        {
            Mech? createdMech = null;
            // Act & Assert
            Should.NotThrow(() =>
            {
                var mtfData = File.ReadAllLines(mtfFile);
                var mechData = parser.LoadMechFromTextData(mtfData);
                createdMech = mechFactory.Create(mechData);
            });
            createdMech.ShouldNotBeNull();
        }
    }

    public static UnitData LoadMechFromMtfFile(string mtfFile)
    {
        var parser = new MtfDataProvider();
        var mtfData = File.ReadAllLines(mtfFile);
        return parser.LoadMechFromTextData(mtfData); 
    }
}
