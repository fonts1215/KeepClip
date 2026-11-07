using KeepClip.Application.Selection;
namespace KeepClip.Tests;
public sealed class SelectionReadSafetyTests
{
    [Fact] public void UnsupportedAttributeCannotAuthorizeAutomaticCopy()
        => Assert.Null(SelectionReadSafety.ProbeReadOnly(() => new object()));

    [Fact] public void ProviderFailureCannotAuthorizeAutomaticCopy()
    {
        var failure = new InvalidOperationException("Provider unavailable"); Exception? reported = null;
        Assert.Null(SelectionReadSafety.ProbeReadOnly(() => throw failure, ex => reported = ex));
        Assert.Same(failure, reported);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void ExplicitAttributeKeepsItsMeaning(bool readOnly)
        => Assert.Equal(readOnly, SelectionReadSafety.ProbeReadOnly(() => readOnly));
}
