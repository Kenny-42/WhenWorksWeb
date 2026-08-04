using WhenWorksWeb.Common;
using WhenWorksWeb.Services;

namespace WhenWorksWeb.Tests.Services;

/// <summary>
/// Tier 1 unit tests for <see cref="UniqueCodeGenerator"/>. Exercises the internal
/// <c>GenerateUniqueCodeAsync(Func&lt;string, Task&lt;bool&gt;&gt;)</c> retry loop directly via a stub
/// existence check, so the retry/max-attempts behavior is deterministic and doesn't depend on the
/// non-seedable cryptographic RNG behind code generation to actually force a collision. No database
/// or HTTP context involved.
/// </summary>
public class UniqueCodeGeneratorTests
{
    [Fact]
    public async Task GenerateUniqueCodeAsync_WhenNoCollision_ReturnsCodeOfConfiguredLength()
    {
        var code = await UniqueCodeGenerator.GenerateUniqueCodeAsync(_ => Task.FromResult(false));

        Assert.Equal(ModelConstants.UniqueCodeLength, code.Length);
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_WhenNoCollision_ReturnsCodeUsingOnlyAlphabetCharacters()
    {
        var code = await UniqueCodeGenerator.GenerateUniqueCodeAsync(_ => Task.FromResult(false));

        Assert.All(code, c => Assert.Contains(c, ModelConstants.UniqueCodeAlphabet));
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_WhenFirstCandidatesCollide_RetriesUntilExistsAsyncReturnsFalse()
    {
        var attempts = 0;

        var code = await UniqueCodeGenerator.GenerateUniqueCodeAsync(_ =>
        {
            attempts++;
            return Task.FromResult(attempts < 3);
        });

        Assert.Equal(3, attempts);
        Assert.Equal(ModelConstants.UniqueCodeLength, code.Length);
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_WhenExistsAsyncAlwaysReturnsTrue_ThrowsAfterExactlyMaxAttempts()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UniqueCodeGenerator.GenerateUniqueCodeAsync(_ =>
            {
                attempts++;
                return Task.FromResult(true);
            }));

        Assert.Equal(UniqueCodeGenerator.MaxAttempts, attempts);
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_WhenExistsAsyncSucceedsOnFinalAllowedAttempt_ReturnsThatCode()
    {
        var attempts = 0;

        var code = await UniqueCodeGenerator.GenerateUniqueCodeAsync(_ =>
        {
            attempts++;
            return Task.FromResult(attempts < UniqueCodeGenerator.MaxAttempts);
        });

        Assert.Equal(UniqueCodeGenerator.MaxAttempts, attempts);
        Assert.Equal(ModelConstants.UniqueCodeLength, code.Length);
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_WhenExistsAsyncThrows_PropagatesExceptionWithoutRetrying()
    {
        var attempts = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UniqueCodeGenerator.GenerateUniqueCodeAsync(_ =>
            {
                attempts++;
                throw new InvalidOperationException("Simulated database failure.");
            }));

        Assert.Equal("Simulated database failure.", ex.Message);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_AcrossManyCalls_ProducesMoreThanOneDistinctCode()
    {
        var codes = new HashSet<string>();

        for (var i = 0; i < 25; i++)
        {
            codes.Add(await UniqueCodeGenerator.GenerateUniqueCodeAsync(_ => Task.FromResult(false)));
        }

        Assert.True(codes.Count > 1, "Expected more than one distinct code across 25 independent generations.");
    }
}
