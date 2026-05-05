using Moq;
using WhenWorksWeb.Services;
using Xunit;

namespace WhenWorksWeb.Tests.Services;

public sealed class EventCodeGeneratorTests
{
    [Fact]
    public async Task GenerateUniqueCodeAsync_ReturnsFirstUniqueCode()
    {
        var codeSource = new Mock<IEventCodeSource>(MockBehavior.Strict);
        codeSource.Setup(x => x.GenerateCode()).Returns("BRG7K2");

        var lookup = new Mock<IEventCodeLookup>(MockBehavior.Strict);
        lookup.Setup(x => x.ExistsAsync("BRG7K2", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new EventCodeGenerator(codeSource.Object, lookup.Object);

        var result = await sut.GenerateUniqueCodeAsync();

        Assert.Equal("BRG7K2", result);
        codeSource.Verify(x => x.GenerateCode(), Times.Once);
        lookup.Verify(x => x.ExistsAsync("BRG7K2", It.IsAny<CancellationToken>()), Times.Once);
        codeSource.VerifyNoOtherCalls();
        lookup.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_RetriesAfterCollision_AndReturnsUniqueCode()
    {
        var codeSource = new Mock<IEventCodeSource>(MockBehavior.Strict);
        codeSource.SetupSequence(x => x.GenerateCode())
            .Returns("AAAAAA")
            .Returns("BBBBBB");

        var lookup = new Mock<IEventCodeLookup>(MockBehavior.Strict);
        lookup.Setup(x => x.ExistsAsync("AAAAAA", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        lookup.Setup(x => x.ExistsAsync("BBBBBB", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new EventCodeGenerator(codeSource.Object, lookup.Object);

        var result = await sut.GenerateUniqueCodeAsync();

        Assert.Equal("BBBBBB", result);
        codeSource.Verify(x => x.GenerateCode(), Times.Exactly(2));
        lookup.Verify(x => x.ExistsAsync("AAAAAA", It.IsAny<CancellationToken>()), Times.Once);
        lookup.Verify(x => x.ExistsAsync("BBBBBB", It.IsAny<CancellationToken>()), Times.Once);
        codeSource.VerifyNoOtherCalls();
        lookup.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_ThrowsWhenEveryAttemptCollides()
    {
        var codeSource = new Mock<IEventCodeSource>(MockBehavior.Strict);
        codeSource.SetupSequence(x => x.GenerateCode())
            .Returns("AAAAAA")
            .Returns("BBBBBB")
            .Returns("CCCCCC");

        var lookup = new Mock<IEventCodeLookup>(MockBehavior.Strict);
        lookup.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = new EventCodeGenerator(codeSource.Object, lookup.Object, maxAttempts: 3);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GenerateUniqueCodeAsync());

        Assert.Equal("Unable to generate a unique event code.", ex.Message);
        codeSource.Verify(x => x.GenerateCode(), Times.Exactly(3));
        lookup.Verify(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        codeSource.VerifyNoOtherCalls();
        lookup.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_PassesCancellationTokenToLookup()
    {
        var codeSource = new Mock<IEventCodeSource>(MockBehavior.Strict);
        codeSource.Setup(x => x.GenerateCode()).Returns("ABC123");

        var lookup = new Mock<IEventCodeLookup>(MockBehavior.Strict);
        CancellationToken observedToken = default;
        lookup.Setup(x => x.ExistsAsync("ABC123", It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, token) => observedToken = token)
            .ReturnsAsync(false);

        using var cts = new CancellationTokenSource();
        var sut = new EventCodeGenerator(codeSource.Object, lookup.Object);

        var result = await sut.GenerateUniqueCodeAsync(cts.Token);

        Assert.Equal("ABC123", result);
        Assert.Equal(cts.Token, observedToken);
        codeSource.Verify(x => x.GenerateCode(), Times.Once);
        lookup.Verify(x => x.ExistsAsync("ABC123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_PropagatesSourceExceptions()
    {
        var codeSource = new Mock<IEventCodeSource>(MockBehavior.Strict);
        codeSource.Setup(x => x.GenerateCode()).Throws(new InvalidOperationException("source failed"));

        var lookup = new Mock<IEventCodeLookup>(MockBehavior.Strict);
        var sut = new EventCodeGenerator(codeSource.Object, lookup.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GenerateUniqueCodeAsync());

        Assert.Equal("source failed", ex.Message);
        codeSource.Verify(x => x.GenerateCode(), Times.Once);
        lookup.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_PropagatesLookupExceptions()
    {
        var codeSource = new Mock<IEventCodeSource>(MockBehavior.Strict);
        codeSource.Setup(x => x.GenerateCode()).Returns("ABC123");

        var lookup = new Mock<IEventCodeLookup>(MockBehavior.Strict);
        lookup.Setup(x => x.ExistsAsync("ABC123", It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("lookup failed"));

        var sut = new EventCodeGenerator(codeSource.Object, lookup.Object);

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => sut.GenerateUniqueCodeAsync());

        Assert.Equal("lookup failed", ex.Message);
        codeSource.Verify(x => x.GenerateCode(), Times.Once);
        lookup.Verify(x => x.ExistsAsync("ABC123", It.IsAny<CancellationToken>()), Times.Once);
        codeSource.VerifyNoOtherCalls();
        lookup.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ThrowsForInvalidMaxAttempts(int maxAttempts)
    {
        var codeSource = new Mock<IEventCodeSource>();
        var lookup = new Mock<IEventCodeLookup>();

        Assert.Throws<ArgumentOutOfRangeException>(() => new EventCodeGenerator(codeSource.Object, lookup.Object, maxAttempts));
    }

    [Fact]
    public void Constructor_ThrowsForNullDependencies()
    {
        var codeSource = new Mock<IEventCodeSource>();
        var lookup = new Mock<IEventCodeLookup>();

        Assert.Throws<ArgumentNullException>(() => new EventCodeGenerator(null!, lookup.Object));
        Assert.Throws<ArgumentNullException>(() => new EventCodeGenerator(codeSource.Object, null!));
    }
}
