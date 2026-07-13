using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Utils;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Map.Factories;

namespace Sanet.MakaMek.Core.Models.Game.Factories;

/// <summary>
/// Concrete implementation for creating game instances.
/// </summary>
public class GameFactory : IGameFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IWeaponAttackResolver _weaponAttackResolver;
    private readonly IRulesProvider _rulesProvider;
    private readonly IMechFactory _mechFactory;
    private readonly IDiceRoller _diceRoller;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly IDamageTransferCalculator _damageTransferCalculator;
    private readonly ICriticalHitsCalculator _criticalHitsCalculator;
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator;
    private readonly IConsciousnessCalculator _consciousnessCalculator;
    private readonly IHeatEffectsCalculator _heatEffectsCalculator;
    private readonly IFallProcessor _fallProcessor;
    private readonly IBattleMapFactory _battleMapFactory;
    private readonly IHashService _hashService;

    public GameFactory(
        ILoggerFactory loggerFactory,
        IWeaponAttackResolver weaponAttackResolver,
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        IDiceRoller diceRoller,
        IToHitCalculator toHitCalculator,
        IDamageTransferCalculator damageTransferCalculator,
        ICriticalHitsCalculator criticalHitsCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IFallProcessor fallProcessor,
        IBattleMapFactory battleMapFactory,
        IHashService hashService)
    {
        _loggerFactory = loggerFactory;
        _weaponAttackResolver = weaponAttackResolver;
        _rulesProvider = rulesProvider;
        _mechFactory = mechFactory;
        _diceRoller = diceRoller;
        _toHitCalculator = toHitCalculator;
        _damageTransferCalculator = damageTransferCalculator;
        _criticalHitsCalculator = criticalHitsCalculator;
        _pilotingSkillCalculator = pilotingSkillCalculator;
        _consciousnessCalculator = consciousnessCalculator;
        _heatEffectsCalculator = heatEffectsCalculator;
        _fallProcessor = fallProcessor;
        _battleMapFactory = battleMapFactory;
        _hashService = hashService;
    }

    public ServerGame CreateServerGame(ICommandPublisher commandPublisher)
    {
        var logger = _loggerFactory.CreateLogger<ServerGame>();

        var hullBreachCalculator = new HullBreachCalculator(_diceRoller);

        return new ServerGame(_rulesProvider,
            _mechFactory,
            commandPublisher,
            _diceRoller,
            _toHitCalculator,
            _damageTransferCalculator,
            _criticalHitsCalculator,
            hullBreachCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _fallProcessor,
            _weaponAttackResolver,
            logger);
    }

    public ClientGame CreateClientGame(ICommandPublisher commandPublisher)
    {
        var logger = _loggerFactory.CreateLogger<ClientGame>();

        return new ClientGame(_rulesProvider,
            _mechFactory,
            commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _battleMapFactory,
            _hashService,
            logger);
    }
}
