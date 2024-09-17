// using System.Diagnostics;
// using LibMatrix.Helpers;
// using LibMatrix.Services;
// using LibMatrix.Tests.Abstractions;
// using LibMatrix.Tests.Fixtures;
// using Microsoft.Extensions.Logging;
// using Xunit.Abstractions;
// using Xunit.Microsoft.DependencyInjection.Abstracts;
//
// namespace LibMatrix.Tests.Tests;
//
// public class TestCleanup : TestBed<TestFixture> {
//     // private readonly TestFixture _fixture;
//     private readonly HomeserverResolverService _resolver;
//     private readonly Config _config;
//     private readonly HomeserverProviderService _provider;
//     private readonly ILogger<TestCleanup> _logger;
//
//     public TestCleanup(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
//         // _fixture = fixture;
//         _resolver = _fixture.GetService<HomeserverResolverService>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverResolverService)}");
//         _config = _fixture.GetService<Config>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(Config)}");
//         _provider = _fixture.GetService<HomeserverProviderService>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverProviderService)}");
//         _logger = _fixture.GetService<ILogger<TestCleanup>>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(ILogger<TestCleanup>)}");
//     }
//
//     [Fact]
//     public async Task Cleanup() {
//         Assert.False(string.IsNullOrWhiteSpace(_config.TestHomeserver), $"{nameof(_config.TestHomeserver)} must be set in appsettings!");
//         Assert.False(string.IsNullOrWhiteSpace(_config.TestUsername), $"{nameof(_config.TestUsername)} must be set in appsettings!");
//         Assert.False(string.IsNullOrWhiteSpace(_config.TestPassword), $"{nameof(_config.TestPassword)} must be set in appsettings!");
//
//         var hs = await HomeserverAbstraction.GetHomeserver();
//         Assert.NotNull(hs);
//
//         var syncHelper = new SyncHelper(hs, _logger) {
//             Timeout = 3000
//         };
//         _testOutputHelper.WriteLine("Starting sync loop");
//         var cancellationTokenSource = new CancellationTokenSource();
//         var sw = Stopwatch.StartNew();
//         syncHelper.SyncReceivedHandlers.Add(async response => {
//             if (sw.ElapsedMilliseconds >= 3000) {
//                 _testOutputHelper.WriteLine("Cancelling sync loop");
//
//                 var tasks = (await hs.GetJoinedRooms()).Select(async room => {
//                     _logger.LogInformation("Leaving room: {}", room.RoomId);
//                     await room.LeaveAsync();
//                     await room.ForgetAsync();
//                     return room;
//                 }).ToList();
//                 await Task.WhenAll(tasks);
//
//                 cancellationTokenSource.Cancel();
//             }
//
//             sw.Restart();
//             if (response.Rooms?.Leave is { Count: > 0 }) {
//                 // foreach (var room in response.Rooms.Leave) {
//                 // await hs.GetRoom(room.Key).ForgetAsync();
//                 // }
//                 var tasks = response.Rooms.Leave.Select(async room => {
//                     await hs.GetRoom(room.Key).ForgetAsync();
//                     return room;
//                 }).ToList();
//                 await Task.WhenAll(tasks);
//             }
//         });
//         await syncHelper.RunSyncLoopAsync(cancellationToken: cancellationTokenSource.Token);
//
//         Assert.NotNull(hs);
//         await hs.Logout();
//     }
// }