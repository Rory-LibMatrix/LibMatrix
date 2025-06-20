namespace LibMatrix.FederationTest.Services;

public class FederationTestConfiguration {
    public FederationTestConfiguration(IConfiguration configurationSection) {
        configurationSection.GetRequiredSection("FederationTest").Bind(this);
    }
    
    public string ServerName { get; set; } = "localhost";
    public string KeyStorePath { get; set; } = "./.keys";
}