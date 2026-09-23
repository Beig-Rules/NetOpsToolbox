using NetOps.Core.Playbooks;
using Xunit;

namespace NetOps.Core.Tests;

public class PlaybookTests
{
    [Fact]
    public void Catalog_has_core_and_extended_playbooks()
    {
        Assert.True(PlaybookEngine.All.Count >= 10);
        Assert.Contains(PlaybookEngine.All, p => p.Id == "wan_outage");
        Assert.Contains(PlaybookEngine.All, p => p.Id == "captive_portal");
        Assert.Contains(PlaybookEngine.All, p => p.Id == "tls_break");
        Assert.Contains(PlaybookEngine.All, p => p.Id == "mikrotik_backup");
    }

    [Fact]
    public void Render_includes_numbered_steps()
    {
        var p = PlaybookEngine.All.First(x => x.Id == "dns_poison_suspect");
        var text = PlaybookEngine.Render(p);
        Assert.Contains("1.", text);
        Assert.Contains(p.Title, text);
    }
}
