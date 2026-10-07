using ClipShelf.Core;
namespace ClipShelf.Tests;
public class StartupCommandTests
{
    [Fact] public void QuotesExecutablePathWithSpacesAndAddsBackgroundArgument()
    { Assert.Equal("\"C:\\Users\\Someone\\My Apps\\ClipShelf.App.exe\" --autostart", StartupCommand.Build(@"C:\Users\Someone\My Apps\ClipShelf.App.exe")); }
    [Fact] public void RegistrationForAnotherExecutableIsNotReportedEnabled()
    { Assert.False(StartupCommand.Matches("\"C:\\Other\\ClipShelf.App.exe\" --autostart", @"C:\Apps\ClipShelf.App.exe")); Assert.True(StartupCommand.Matches("\"c:\\apps\\clipshelf.app.exe\" --autostart", @"C:\Apps\ClipShelf.App.exe")); }
    [Fact] public void OnlyStartupLaunchesRemainHidden()
    { Assert.True(StartupCommand.ShouldStartHidden(["app", "--autostart"], false)); Assert.True(StartupCommand.ShouldStartHidden(["app"], true)); Assert.False(StartupCommand.ShouldStartHidden(["app"], false)); }
}
