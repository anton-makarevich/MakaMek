---
name: generate-unit-tests
description: Generate xUnit tests following established patterns for a given C# source file, covering untested public methods using NSubstitute for mocks and Shouldly for assertions. Use this skill whenever the user asks to add tests, generate unit tests, write test coverage, or create a test file for a C# class — even if they just say "add tests for this" or "what needs test coverage".
---

# Generate Unit Tests

Generate xUnit tests following established patterns for a given file, covering untested public methods using NSubstitute for mocks and Shouldly for assertions.

## Context Validation Checkpoints

- Is the source file path provided and accessible?
- Does the target class have public methods that need testing?
- Are there existing tests for this class to avoid duplication?
- Are dependencies identifiable for mocking with NSubstitute?
- Is the test project location known for placing the new test file?

## Implementation Steps

### Step 1: Analyze Source File
Read the source file to identify the class name, namespace, public methods, and dependencies. Determine which methods need test coverage based on the optional method name parameter.

### Step 2: Check Existing Tests
Search for existing test files matching the pattern `*Tests.cs` for the target class. Identify which public methods are already tested to avoid duplication.

### Step 3: Determine Test File Location
Locate the appropriate test project (in `tests/` directory). Determine if a new test file is needed or if tests should be added to an existing one.

### Step 4: Generate Test Class Structure
Create the test class with proper naming (`<ClassName>Tests`), namespace matching the test project structure, and `_sut` field for the system under test.

```csharp
public class GameManagerTests
{
    private readonly GameManager _sut;
    private readonly IRulesProvider _rulesProvider = Substitute.For<IRulesProvider>();
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
}
```

### Step 5: Generate Test Methods
For each untested public method, generate test methods using the arrange-act-assert pattern with descriptive underscore-separated names.

```csharp
[Fact]
public void MethodName_Condition_ExpectedResult()
{
    // Arrange
    _rulesProvider.GetRules().Returns(new Rules());
    
    // Act
    var result = _sut.MethodName();
    
    // Assert
    result.ShouldBeTrue();
}
```

### Step 6: Add Parameterized Tests Where Applicable
For methods with multiple input scenarios, use `[Theory]` and `[InlineData]` for parameterized testing.

```csharp
[Theory]
[InlineData(2, 100.0)]
[InlineData(12, 0.0)]
public void CalculateHitProbability_WithRoll_ReturnsExpectedValue(int roll, double expected)
{
    var result = _sut.CalculateHitProbability(roll);
    result.ShouldBe(expected);
}
```

### Step 7: Add Required Usings and Constructor
Include all necessary using statements (xUnit, NSubstitute, Shouldly) and implement the constructor with dependency initialization.

```csharp
using Xunit;
using NSubstitute;
using Shouldly;
```

### Step 8: Write or Update Test File
Create the new test file or append tests to the existing one, ensuring proper formatting and organization.
