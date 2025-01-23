using ArcaneLibs.Extensions;
using ArcaneLibs.Extensions.Streams;
using LibMatrix.Services;
using LibMatrix.Tests.Abstractions;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests;

public class AuthMediaTests : TestBed<TestFixture> {
    private readonly TestFixture _fixture;
    private readonly HomeserverResolverService _resolver;
    private readonly Config _config;
    private readonly HomeserverProviderService _provider;
    private readonly HomeserverAbstraction _hsAbstraction;

    public AuthMediaTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
        _fixture = fixture;
        _resolver = _fixture.GetService<HomeserverResolverService>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverResolverService)}");
        _config = _fixture.GetService<Config>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(Config)}");
        _provider = _fixture.GetService<HomeserverProviderService>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverProviderService)}");
        _hsAbstraction = _fixture.GetService<HomeserverAbstraction>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverAbstraction)}");
    }

    [Fact]
    public async Task UploadFileAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();

        var mxcUri = await hs.UploadFile("test", "LibMatrix test file".AsBytes());
        Assert.NotNull(mxcUri);
    }
    
    [Fact]
    public async Task DownloadFileAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();

        var mxcUri = await hs.UploadFile("test", "LibMatrix test file".AsBytes());
        Assert.NotNull(mxcUri);
        
        var file = await hs.GetMediaStreamAsync(mxcUri);
        Assert.NotNull(file);
        
        var data = file!.ReadToEnd().AsString();
        Assert.Equal("LibMatrix test file", data);
    }
    
    [SkippableFact(typeof(LibMatrixException))] // This test will fail if the homeserver does not support URL previews
    public async Task GetUrlPreviewAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var preview = await hs.GetUrlPreviewAsync("https://matrix.org");
        
        Assert.NotNull(preview);
    }
}