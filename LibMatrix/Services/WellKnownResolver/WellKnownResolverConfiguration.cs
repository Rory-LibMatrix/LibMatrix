using System.Text.Json.Serialization;

namespace LibMatrix.Services.WellKnownResolver;

public class WellKnownResolverConfiguration {
    /// <summary>
    /// Allow transparent downgrades to plaintext HTTP if HTTPS fails
    /// Enabling this is unsafe!
    /// </summary>
    [JsonPropertyName("allow_http")]
    public bool AllowHttp { get; set; } = false;
    
    /// <summary>
    /// Use DNS resolution if available, for resolving SRV records
    /// </summary>
    [JsonPropertyName("allow_dns")]
    public bool AllowDns { get; set; } = true;
    
    /// <summary>
    /// Use system resolver(s) if empty
    /// </summary>
    [JsonPropertyName("dns_servers")]
    public List<string> DnsServers { get; set; } = new();
    
    /// <summary>
    /// Same as AllowDns, but for DNS over HTTPS - useful in browser contexts
    /// </summary>
    [JsonPropertyName("allow_doh")]
    public bool AllowDoh { get; set; } = true;
    
    /// <summary>
    /// Use DNS over HTTPS - useful in browser contexts
    /// Disabled if empty
    /// </summary>
    [JsonPropertyName("doh_servers")]
    public List<string> DohServers { get; set; } = new();
    
    /// <summary>
    /// Whether to allow fallback subdomain lookups
    /// </summary>
    [JsonPropertyName("allow_fallback_subdomains")]
    public bool AllowFallbackSubdomains { get; set; } = true;
    
    /// <summary>
    /// Fallback subdomains to try if the homeserver is not found
    /// </summary>
    [JsonPropertyName("fallback_subdomains")]
    public List<string> FallbackSubdomains { get; set; } = ["matrix", "chat", "im"];
}