using Microsoft.Extensions.Configuration;

namespace LibMatrix.Tests;

public class Config {
    public Config(IConfiguration? config) {
        config.GetSection("Configuration").Bind(this);
    }

    public string? TestHomeserver { get; set; } = Environment.GetEnvironmentVariable("LIBMATRIX_TEST_HOMESERVER") ?? null;
    public string? TestUsername { get; set; } = Environment.GetEnvironmentVariable("LIBMATRIX_TEST_USERNAME") ?? Guid.NewGuid().ToString();

    public string? TestPassword { get; set; } = Environment.GetEnvironmentVariable("LIBMATRIX_TEST_PASSWORD") ?? Guid.NewGuid().ToString();
    // public string? TestRoomId { get; set; } = Environment.GetEnvironmentVariable("LIBMATRIX_TEST_ROOM_ID") ?? null;
    // public string? TestRoomAlias { get; set; } = Environment.GetEnvironmentVariable("LIBMATRIX_TEST_ROOM_ALIAS") ?? null;

    public Dictionary<string, string> ExpectedHomeserverClientMappings { get; set; } = new() {
        { "matrix.org", "https://matrix-client.matrix.org" },
        { "rory.gay", "https://matrix.rory.gay" },
        { "feline.support", "https://matrix.feline.support" },
        { "transfem.dev", "https://matrix.transfem.dev/" },
        { "the-apothecary.club", "https://the-apothecary.club" },
        { "nixos.org", "https://matrix.nixos.org" },
        { "fedora.im", "https://fedora.ems.host" }
    };

    public Dictionary<string, string> ExpectedHomeserverFederationMappings { get; set; } = new() {
        { "rory.gay", "https://matrix.rory.gay:443" },
        { "matrix.org", "https://matrix-federation.matrix.org:443" },
        { "feline.support", "https://matrix.feline.support:8448" },
        { "transfem.dev", "https://matrix.transfem.dev:443" },
        { "the-apothecary.club", "https://the-apothecary.club:443" },
        { "nixos.org", "https://matrix.nixos.org:443" },
        { "fedora.im", "https://fedora.ems.host:443" }
    };

    public Dictionary<string, string> ExpectedAliasMappings { get; set; } = new() {
        { "#libmatrix:rory.gay", "!tuiLEoMqNOQezxILzt:rory.gay" },
        { "#matrix:matrix.org", "!OGEhHVWSdvArJzumhm:matrix.org" }
    };
}