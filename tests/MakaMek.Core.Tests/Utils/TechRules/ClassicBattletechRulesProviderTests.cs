using Sanet.MakaMek.Core.Models.Game.Combat;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;

namespace Sanet.MakaMek.Core.Tests.Utils.TechRules
{
    public class ClassicBattletechRulesProviderTests
    {
        private readonly IRulesProvider _sut = new ClassicBattletechRulesProvider();

        [Theory]
        [InlineData(20, 3, 6, 5, 5, 3, 3, 4, 4)]
        [InlineData(25, 3, 8, 6, 6, 4, 4, 6, 6)]
        [InlineData(30, 3, 10, 7, 7, 5, 5, 7, 7)]
        [InlineData(35, 3, 11, 8, 8, 6, 6, 8, 8)]
        [InlineData(40, 3, 12, 10, 10, 6, 6, 10, 10)]
        [InlineData(45, 3, 14, 11, 11, 7, 7, 11, 11)]
        [InlineData(50, 3, 16, 12, 12, 8, 8, 12, 12)]
        [InlineData(55, 3, 18, 13, 13, 9, 9, 13, 13)]
        [InlineData(60, 3, 20, 14, 14, 10, 10, 14, 14)]
        [InlineData(65, 3, 21, 15, 15, 10, 10, 15, 15)]
        [InlineData(70, 3, 22, 15, 15, 11, 11, 15, 15)]
        [InlineData(75, 3, 23, 16, 16, 12, 12, 16, 16)]
        [InlineData(80, 3, 25, 17, 17, 13, 13, 17, 17)]
        [InlineData(85, 3, 27, 18, 18, 14, 14, 18, 18)]
        [InlineData(90, 3, 29, 19, 19, 15, 15, 19, 19)]
        [InlineData(95, 3, 30, 20, 20, 16, 16, 20, 20)]
        [InlineData(100, 3, 31, 21, 21, 17, 17, 21, 21)]
        public void GetStructureValues_ValidTonnage_ReturnsExpectedValues(int tonnage, int head, int centerTorso, int leftTorso, int rightTorso, int leftArm, int rightArm, int leftLeg, int rightLeg)
        {
            // Act
            var result = _sut.GetStructureValues(tonnage);

            // Assert
            result[PartLocation.Head].ShouldBe(head);
            result[PartLocation.CenterTorso].ShouldBe(centerTorso);
            result[PartLocation.LeftTorso].ShouldBe(leftTorso);
            result[PartLocation.RightTorso].ShouldBe(rightTorso);
            result[PartLocation.LeftArm].ShouldBe(leftArm);
            result[PartLocation.RightArm].ShouldBe(rightArm);
            result[PartLocation.LeftLeg].ShouldBe(leftLeg);
            result[PartLocation.RightLeg].ShouldBe(rightLeg);
        }

        [Fact]
        public void GetStructureValues_InvalidTonnage_ThrowsException()
        {
            // Act & Assert
            Should.Throw<ArgumentOutOfRangeException>(()=>_sut.GetStructureValues(150));
        }

        [Theory]
        [InlineData(MovementType.StandingStill, 0)]
        [InlineData(MovementType.Walk, 1)]
        [InlineData(MovementType.Run, 2)]
        [InlineData(MovementType.Jump, 3)]
        [InlineData(MovementType.Prone, 2)]
        public void GetAttackerMovementModifier_ReturnsExpectedValues(MovementType movementType, int expectedModifier)
        {
            _sut.GetAttackerMovementModifier(movementType).ShouldBe(expectedModifier);
        }

        [Theory]
        [InlineData(0, 0)]  // 0-2 hexes: no modifier
        [InlineData(2, 0)]
        [InlineData(3, 1)]  // 3-4 hexes: +1
        [InlineData(4, 1)]
        [InlineData(5, 2)]  // 5-6 hexes: +2
        [InlineData(6, 2)]
        [InlineData(7, 3)]  // 7-9 hexes: +3
        [InlineData(9, 3)]
        [InlineData(10, 4)] // 10-17 hexes: +4
        [InlineData(17, 4)]
        [InlineData(18, 5)] // 18-24 hexes: +5
        [InlineData(24, 5)]
        [InlineData(25, 6)] // 25+ hexes: +6
        [InlineData(30, 6)]
        public void GetTargetMovementModifier_ReturnsExpectedValues(int hexesMoved, int expectedModifier)
        {
            _sut.GetTargetMovementModifier(hexesMoved).ShouldBe(expectedModifier);
        }

        [Theory]
        [InlineData(WeaponRange.Minimum,6,6, 1)]
        [InlineData(WeaponRange.Minimum,6,5, 2)]
        [InlineData(WeaponRange.Short,1,1,0)]
        [InlineData(WeaponRange.Medium,1,1, 2)]
        [InlineData(WeaponRange.Long,1,1, 4)]
        [InlineData(WeaponRange.OutOfRange,1,1, ToHitBreakdown.ImpossibleRoll)]
        public void GetRangeModifier_ReturnsExpectedValues(WeaponRange range, int rangeValue, int distance, int expectedModifier)
        {
            _sut.GetRangeModifier(range,rangeValue,distance).ShouldBe(expectedModifier);
        }

        [Theory]
        [InlineData(MakaMekTerrains.LightWoods, 1)]
        [InlineData(MakaMekTerrains.HeavyWoods, 2)]
        [InlineData(MakaMekTerrains.Clear, 0)]
        public void GetTerrainToHitModifier_ReturnsExpectedValues(MakaMekTerrains terrainId, int expectedModifier)
        {
            _sut.GetTerrainToHitModifier(terrainId).ShouldBe(expectedModifier);
        }

        [Fact]
        public void GetAttackerMovementModifier_InvalidMovementType_ThrowsArgumentException()
        {
            var invalidType = (MovementType)999;
            Should.Throw<ArgumentException>(() => _sut.GetAttackerMovementModifier(invalidType));
        }

        [Fact]
        public void GetRangeModifier_InvalidRange_ThrowsArgumentException()
        {
            var invalidRange = (WeaponRange)999;
            Should.Throw<ArgumentException>(() => _sut.GetRangeModifier(invalidRange,999,999));
        }

        [Theory]
        [InlineData(true, 1)]   // Front arc: +1 modifier
        [InlineData(false, 2)]  // Other arc: +2 modifier
        public void GetSecondaryTargetModifier_ReturnsExpectedValues(bool isFrontArc, int expectedModifier)
        {
            _sut.GetSecondaryTargetModifier(isFrontArc).ShouldBe(expectedModifier);
        }

        #region Hit Location Tests

        [Theory]
        [InlineData(2, PartLocation.CenterTorso)]  // Critical hit
        [InlineData(3, PartLocation.RightArm)]
        [InlineData(4, PartLocation.RightArm)]
        [InlineData(5, PartLocation.RightLeg)]
        [InlineData(6, PartLocation.RightTorso)]
        [InlineData(7, PartLocation.CenterTorso)]
        [InlineData(8, PartLocation.LeftTorso)]
        [InlineData(9, PartLocation.LeftLeg)]
        [InlineData(10, PartLocation.LeftArm)]
        [InlineData(11, PartLocation.LeftArm)]
        [InlineData(12, PartLocation.Head)]
        public void GetHitLocation_FrontAttack_ReturnsCorrectLocation(int diceResult, PartLocation expectedLocation)
        {
            // Act
            var result = _sut.GetHitLocation(diceResult, FiringArc.Forward);

            // Assert
            result.ShouldBe(expectedLocation);
        }

        [Theory]
        [InlineData(2, PartLocation.CenterTorso)]  // Critical hit
        [InlineData(3, PartLocation.RightArm)]
        [InlineData(4, PartLocation.RightArm)]
        [InlineData(5, PartLocation.RightLeg)]
        [InlineData(6, PartLocation.RightTorso)]
        [InlineData(7, PartLocation.CenterTorso)]
        [InlineData(8, PartLocation.LeftTorso)]
        [InlineData(9, PartLocation.LeftLeg)]
        [InlineData(10, PartLocation.LeftArm)]
        [InlineData(11, PartLocation.LeftArm)]
        [InlineData(12, PartLocation.Head)]
        public void GetHitLocation_RearAttack_ReturnsCorrectLocation(int diceResult, PartLocation expectedLocation)
        {
            // Act
            var result = _sut.GetHitLocation(diceResult, FiringArc.Rear);

            // Assert
            result.ShouldBe(expectedLocation);
        }

        [Theory]
        [InlineData(2, PartLocation.LeftTorso)]  // Critical hit
        [InlineData(3, PartLocation.LeftLeg)]
        [InlineData(4, PartLocation.LeftArm)]
        [InlineData(5, PartLocation.LeftArm)]
        [InlineData(6, PartLocation.LeftLeg)]
        [InlineData(7, PartLocation.LeftTorso)]
        [InlineData(8, PartLocation.CenterTorso)]
        [InlineData(9, PartLocation.RightTorso)]
        [InlineData(10, PartLocation.RightArm)]
        [InlineData(11, PartLocation.RightLeg)]
        [InlineData(12, PartLocation.Head)]
        public void GetHitLocation_LeftAttack_ReturnsCorrectLocation(int diceResult, PartLocation expectedLocation)
        {
            // Act
            var result = _sut.GetHitLocation(diceResult, FiringArc.Left);

            // Assert
            result.ShouldBe(expectedLocation);
        }

        [Theory]
        [InlineData(2, PartLocation.RightTorso)]  // Critical hit
        [InlineData(3, PartLocation.RightLeg)]
        [InlineData(4, PartLocation.RightArm)]
        [InlineData(5, PartLocation.RightArm)]
        [InlineData(6, PartLocation.RightLeg)]
        [InlineData(7, PartLocation.RightTorso)]
        [InlineData(8, PartLocation.CenterTorso)]
        [InlineData(9, PartLocation.LeftTorso)]
        [InlineData(10, PartLocation.LeftArm)]
        [InlineData(11, PartLocation.LeftLeg)]
        [InlineData(12, PartLocation.Head)]
        public void GetHitLocation_RightAttack_ReturnsCorrectLocation(int diceResult, PartLocation expectedLocation)
        {
            // Act
            var result = _sut.GetHitLocation(diceResult, FiringArc.Right);

            // Assert
            result.ShouldBe(expectedLocation);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(13)]
        [InlineData(20)]
        public void GetHitLocation_InvalidDiceResult_ThrowsArgumentOutOfRangeException(int invalidDiceResult)
        {
            // Act & Assert
            Should.Throw<ArgumentOutOfRangeException>(() => _sut.GetHitLocation(invalidDiceResult, FiringArc.Forward));
            Should.Throw<ArgumentOutOfRangeException>(() => _sut.GetHitLocation(invalidDiceResult, FiringArc.Left));
            Should.Throw<ArgumentOutOfRangeException>(() => _sut.GetHitLocation(invalidDiceResult, FiringArc.Right));
            Should.Throw<ArgumentOutOfRangeException>(() => _sut.GetHitLocation(invalidDiceResult, FiringArc.Rear));
        }

        [Fact]
        public void GetHitLocation_InvalidAttackDirection_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Should.Throw<ArgumentOutOfRangeException>(() => _sut.GetHitLocation(7, (FiringArc)999));
        }

        #endregion

        #region Cluster Hits Tests

        [Theory]
        // Test for 2-missile weapons (SRM-2)
        [InlineData(2, 2, 1)]   // Roll 2 -> 1 hit
        [InlineData(3, 2, 1)]   // Roll 3 -> 1 hit
        [InlineData(4, 2, 1)]   // Roll 4 -> 1 hit
        [InlineData(5, 2, 1)]   // Roll 5 -> 1 hit
        [InlineData(6, 2, 1)]   // Roll 6 -> 1 hit
        [InlineData(7, 2, 1)]   // Roll 7 -> 1 hit
        [InlineData(8, 2, 2)]   // Roll 8 -> 2 hits
        [InlineData(9, 2, 2)]   // Roll 9 -> 2 hits
        [InlineData(10, 2, 2)]  // Roll 10 -> 2 hits
        [InlineData(11, 2, 2)]  // Roll 11 -> 2 hits
        [InlineData(12, 2, 2)]  // Roll 12 -> 2 hits
        public void GetClusterHits_SRM2_ReturnsCorrectHits(int diceResult, int weaponSize, int expectedHits)
        {
            // Act
            var result = _sut.GetClusterHits(diceResult, weaponSize);

            // Assert
            result.ShouldBe(expectedHits);
        }

        [Theory]
        // Test for 4-missile weapons (SRM-4)
        [InlineData(2, 4, 1)]   // Roll 2 -> 1 hit
        [InlineData(3, 4, 2)]   // Roll 3 -> 2 hits
        [InlineData(4, 4, 2)]   // Roll 4 -> 2 hits
        [InlineData(5, 4, 2)]   // Roll 5 -> 2 hits
        [InlineData(6, 4, 2)]   // Roll 6 -> 2 hits
        [InlineData(7, 4, 3)]   // Roll 7 -> 3 hits
        [InlineData(8, 4, 3)]   // Roll 8 -> 3 hits
        [InlineData(9, 4, 3)]   // Roll 9 -> 3 hits
        [InlineData(10, 4, 3)]  // Roll 10 -> 3 hits
        [InlineData(11, 4, 4)]  // Roll 11 -> 4 hits
        [InlineData(12, 4, 4)]  // Roll 12 -> 4 hits
        public void GetClusterHits_SRM4_ReturnsCorrectHits(int diceResult, int weaponSize, int expectedHits)
        {
            // Act
            var result = _sut.GetClusterHits(diceResult, weaponSize);

            // Assert
            result.ShouldBe(expectedHits);
        }

        [Theory]
        // Test for 5-missile weapons (LRM-5)
        [InlineData(2, 5, 1)]   // Roll 2 -> 1 hit
        [InlineData(3, 5, 2)]   // Roll 3 -> 2 hits
        [InlineData(4, 5, 2)]   // Roll 4 -> 2 hits
        [InlineData(5, 5, 3)]   // Roll 5 -> 3 hits
        [InlineData(6, 5, 3)]   // Roll 6 -> 3 hits
        [InlineData(7, 5, 3)]   // Roll 7 -> 3 hits
        [InlineData(8, 5, 3)]   // Roll 8 -> 3 hits
        [InlineData(9, 5, 4)]   // Roll 9 -> 4 hits
        [InlineData(10, 5, 4)]  // Roll 10 -> 4 hits
        [InlineData(11, 5, 5)]  // Roll 11 -> 5 hits
        [InlineData(12, 5, 5)]  // Roll 12 -> 5 hits
        public void GetClusterHits_LRM5_ReturnsCorrectHits(int diceResult, int weaponSize, int expectedHits)
        {
            // Act
            var result = _sut.GetClusterHits(diceResult, weaponSize);

            // Assert
            result.ShouldBe(expectedHits);
        }

        [Theory]
        // Test for 6-missile weapons (SRM-6)
        [InlineData(2, 6, 2)]   // Roll 2 -> 2 hits
        [InlineData(3, 6, 2)]   // Roll 3 -> 2 hits
        [InlineData(4, 6, 3)]   // Roll 4 -> 3 hits
        [InlineData(5, 6, 3)]   // Roll 5 -> 3 hits
        [InlineData(6, 6, 4)]   // Roll 6 -> 4 hits
        [InlineData(7, 6, 4)]   // Roll 7 -> 4 hits
        [InlineData(8, 6, 4)]   // Roll 8 -> 4 hits
        [InlineData(9, 6, 5)]   // Roll 9 -> 5 hits
        [InlineData(10, 6, 5)]  // Roll 10 -> 5 hits
        [InlineData(11, 6, 6)]  // Roll 11 -> 6 hits
        [InlineData(12, 6, 6)]  // Roll 12 -> 6 hits
        public void GetClusterHits_SRM6_ReturnsCorrectHits(int diceResult, int weaponSize, int expectedHits)
        {
            // Act
            var result = _sut.GetClusterHits(diceResult, weaponSize);

            // Assert
            result.ShouldBe(expectedHits);
        }

        [Theory]
        // Test for 10-missile weapons (LRM-10)
        [InlineData(2, 10, 3)]   // Roll 2 -> 3 hits
        [InlineData(3, 10, 3)]   // Roll 3 -> 3 hits
        [InlineData(4, 10, 4)]   // Roll 4 -> 4 hits
        [InlineData(5, 10, 6)]   // Roll 5 -> 6 hits
        [InlineData(6, 10, 6)]   // Roll 6 -> 6 hits
        [InlineData(7, 10, 6)]   // Roll 7 -> 6 hits
        [InlineData(8, 10, 6)]   // Roll 8 -> 6 hits
        [InlineData(9, 10, 8)]   // Roll 9 -> 8 hits
        [InlineData(10, 10, 8)]  // Roll 10 -> 8 hits
        [InlineData(11, 10, 10)] // Roll 11 -> 10 hits
        [InlineData(12, 10, 10)] // Roll 12 -> 10 hits
        public void GetClusterHits_LRM10_ReturnsCorrectHits(int diceResult, int weaponSize, int expectedHits)
        {
            // Act
            var result = _sut.GetClusterHits(diceResult, weaponSize);

            // Assert
            result.ShouldBe(expectedHits);
        }

        [Theory]
        // Test for 15-missile weapons (LRM-15)
        [InlineData(2, 15, 5)]   // Roll 2 -> 5 hits
        [InlineData(3, 15, 5)]   // Roll 3 -> 5 hits
        [InlineData(4, 15, 6)]   // Roll 4 -> 6 hits
        [InlineData(5, 15, 9)]   // Roll 5 -> 9 hits
        [InlineData(6, 15, 9)]   // Roll 6 -> 9 hits
        [InlineData(7, 15, 9)]   // Roll 7 -> 9 hits
        [InlineData(8, 15, 9)]   // Roll 8 -> 9 hits
        [InlineData(9, 15, 12)]  // Roll 9 -> 12 hits
        [InlineData(10, 15, 12)] // Roll 10 -> 12 hits
        [InlineData(11, 15, 15)] // Roll 11 -> 15 hits
        [InlineData(12, 15, 15)] // Roll 12 -> 15 hits
        public void GetClusterHits_LRM15_ReturnsCorrectHits(int diceResult, int weaponSize, int expectedHits)
        {
            // Act
            var result = _sut.GetClusterHits(diceResult, weaponSize);

            // Assert
            result.ShouldBe(expectedHits);
        }

        [Theory]
        // Test for 20-missile weapons (LRM-20)
        [InlineData(2, 20, 6)]   // Roll 2 -> 6 hits
        [InlineData(3, 20, 6)]   // Roll 3 -> 6 hits
        [InlineData(4, 20, 9)]   // Roll 4 -> 9 hits
        [InlineData(5, 20, 12)]  // Roll 5 -> 12 hits
        [InlineData(6, 20, 12)]  // Roll 6 -> 12 hits
        [InlineData(7, 20, 12)]  // Roll 7 -> 12 hits
        [InlineData(8, 20, 12)]  // Roll 8 -> 12 hits
        [InlineData(9, 20, 16)]  // Roll 9 -> 16 hits
        [InlineData(10, 20, 16)] // Roll 10 -> 16 hits
        [InlineData(11, 20, 20)] // Roll 11 -> 20 hits
        [InlineData(12, 20, 20)] // Roll 12 -> 20 hits
        public void GetClusterHits_LRM20_ReturnsCorrectHits(int diceResult, int weaponSize, int expectedHits)
        {
            // Act
            var result = _sut.GetClusterHits(diceResult, weaponSize);

            // Assert
            result.ShouldBe(expectedHits);
        }

        [Theory]
        [InlineData(1, 10)]  // Invalid dice roll (too low)
        [InlineData(13, 10)] // Invalid dice roll (too high)
        public void GetClusterHits_InvalidDiceResult_ThrowsArgumentOutOfRangeException(int invalidDiceResult, int weaponSize)
        {
            // Act & Assert
            Should.Throw<ArgumentOutOfRangeException>(() => _sut.GetClusterHits(invalidDiceResult, weaponSize));
        }

        [Theory]
        [InlineData(7, 3)]  // Unsupported weapon size
        [InlineData(7, 7)]  // Unsupported weapon size
        [InlineData(7, 8)]  // Unsupported weapon size
        [InlineData(7, 9)]  // Unsupported weapon size
        [InlineData(7, 11)] // Unsupported weapon size
        [InlineData(7, 12)] // Unsupported weapon size
        [InlineData(7, 13)] // Unsupported weapon size
        [InlineData(7, 14)] // Unsupported weapon size
        [InlineData(7, 16)] // Unsupported weapon size
        [InlineData(7, 17)] // Unsupported weapon size
        [InlineData(7, 18)] // Unsupported weapon size
        [InlineData(7, 19)] // Unsupported weapon size
        [InlineData(7, 21)] // Unsupported weapon size
        public void GetClusterHits_UnsupportedWeaponSize_ReturnsWeaponSize(int diceResult, int weaponSize)
        {
            // For unsupported weapon sizes, we should still get a valid result
            // The implementation should default to the weapon size itself
            
            // Act
            var result = _sut.GetClusterHits(diceResult, weaponSize);

            // Assert
            result.ShouldBe(weaponSize);
        }

        [Theory]
        [InlineData(0, 2)]  // Invalid roll (too low)
        [InlineData(13, 2)] // Invalid roll (too high)
        public void GetClusterHits_InvalidRoll_ThrowsArgumentOutOfRangeException(int diceResult, int invalidWeaponSize)
        {
            // Act & Assert
            Should.Throw<ArgumentOutOfRangeException>(() => _sut.GetClusterHits(diceResult, invalidWeaponSize));
        }

        [Fact]
        public void GetClusterHits_WeaponSizeOne_ReturnsOne()
        {
            // For a weapon with size 1 (non-cluster weapon), should always return 1
            
            // Act
            var result = _sut.GetClusterHits(7, 1);

            // Assert
            result.ShouldBe(1);
        }

        #endregion
        
        [Theory]
        [InlineData(MovementType.StandingStill, 0, 0)]
        [InlineData(MovementType.Walk, 5, 1)]
        [InlineData(MovementType.Run, 5, 2)]
        [InlineData(MovementType.Jump, 0, 3)]
        [InlineData(MovementType.Jump, 1, 3)]
        [InlineData(MovementType.Jump, 2, 3)]
        [InlineData(MovementType.Jump, 3, 3)]
        [InlineData(MovementType.Jump, 4, 4)]
        [InlineData(MovementType.Jump, 5, 5)]
        public void GetMovementHeatPoints_ReturnsExpectedHeatPoints(MovementType movementType, int movementPointsSpent, int expectedHeatPoints)
        {
            // Act
            var result = _sut.GetMovementHeatPoints(movementType, movementPointsSpent);

            // Assert
            result.ShouldBe(expectedHeatPoints);
        }
    }
}
