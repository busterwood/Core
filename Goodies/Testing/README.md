﻿# BusterWood.Testing

Small simple library for testing, based on Go's `Testing` package.  No need for test external test runners, just create a console application and put your tests in that.

## Comparison to NUnit

| NUnit         | BusterWood.Testing |
| ------------- | ------------------ |
| `[TestFixture]` attribute | No attribute or special naming required |
| `[Test]` attribute on tests methods | Test methods (static or instance) take a single `Test t` parameter |
| `[TestCase]` attribute on tests methods | Test methods include loops for many values |
| `[Setup]` attribute | Class constructor |
| `[TearDown]` attribute | Implement `IDisposable` |
| `[TestFixtureSetUp]` attribute | static constructor |
| `[SetUpFixture]` attribute | `Program.Main()` method |
| `[Ignore]` attribute | Set the call `Test.Skip(string message)` or `Test.SkipNow()` methods |
| `nunit-console.exe` with configuration for dotnet framework version | create your own console application that contains your tests |
| Lots of other attributes and complexity | Just write code in your test method |

## How to write tests

Tests are public methods that take a single parameter of type `BusterWood.Testing.Test`, for example:
```
public static one_does_not_equal_two(Test t)
{
	if (1 == 2) // faster than using Assert but more verbose
	    t.Error("1 should not equal 2");
}

public static one_does_not_equal_two_using_assert(Test t)
{
	t.Assert(() => 1 != 2); // slower more more compact
}
```

Static test methods are run in isolation.

Instance methods are run on a new object instance per test.

### How to set-up a test?

If all tests in a class need common setup code then put this code in the constructor and use non-static test methods.

### How to clean up a test after it has finished?

If the test class implements `IDisposable` then the `Dispose()` method is called after each non-static test method is run.

### How to run setup-code before any test is run?

One-off setup code can be placed in a static constructor, or in the `Main()` method of the program.

## How to run tests

Create a console application and add the following class, which will run all the tests found in the console application assembly:

```
using BusterWood.Testing;

namespace Sample
{
    public class Program
    {
        public static int Main()
        {
            return Tests.Run();
        }
    }
}
```

Note that `Tests.Run()` returns the number of tests that failed, so can be used as the return value of the executable, for easy integration with build tools and scripts.

### Command Line Options

The `Tests.Run()` method recognises the following parameter:
* `--verbose` to list all the test names and all test messages.  Normally only failed test names and messages are shown.
* `--short` which sets the `Test.Short` to TRUE, and can be used by your test code to slow tests.

# Test Class

The `Test` class have the following methods:
* `void Fail()` marks the test as having Failed but continues execution.
* `void FailNow()` marks the test as having Failed and stops its execution.
* `void Log(string message)` adds a message to the log of the current test.  Messages are shown for failing tests, or all tests when in `--verbose` mode.
* `void SkipNow()` marks the test as having been skipped (ignored) and stops execution of the test.

The following extension methods to the `Test` class make it easier to use:
* `void Error(string message)` is used to log a message then fail the test (although execution of the test continues)
* `void Fatal(string message)` calls `Log()` then `FailNow()`
* `void Skip(string message)` calls `Log()` then `SkipNow()`
* `void Assert(Expression<Func<bool>> expression)` Checks that the expression returns true, or reports the expression as an `Error()`
* `void AssertNot(Expression<Func<bool>> expression)` Checks that the expression returns false, or reports the expression as an `Error()`
* `void AssertThrows<TException>(Expression<Func<object>> expression)` Checks that the expression throws an exception of type `TException` or reports an `Error()`