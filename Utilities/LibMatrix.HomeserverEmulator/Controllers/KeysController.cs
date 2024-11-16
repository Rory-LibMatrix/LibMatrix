// using System.Security.Cryptography;
// using System.Text.Json.Nodes;
// using System.Text.Json.Serialization;
// using LibMatrix.HomeserverEmulator.Services;
// using LibMatrix.Responses;
// using LibMatrix.Services;
// using Microsoft.AspNetCore.Mvc;
//
// namespace LibMatrix.HomeserverEmulator.Controllers;
//
// [ApiController]
// [Route("/_matrix/client/{version}/")]
// public class KeysController(ILogger<KeysController> logger, TokenService tokenService, UserStore userStore) : ControllerBase {
//     [HttpPost("keys/upload")]
//     public async Task<object> UploadKeys(DeviceKeysUploadRequest request) {
//         var token = tokenService.GetAccessToken(HttpContext);
//         if (token == null)
//             throw new MatrixException() {
//                 ErrorCode = "M_MISSING_TOKEN",
//                 Error = "Missing token"
//             };
//
//         var user = await userStore.GetUserByToken(token);
//         if (user == null)
//             throw new MatrixException() {
//                 ErrorCode = "M_UNKNOWN_TOKEN",
//                 Error = "No such user"
//             };
//
//         return new { };
//     }
//     
//     [HttpGet("room_keys/version")]
//     public async Task<RoomKeysResponse> GetRoomKeys() {
//         var token = tokenService.GetAccessToken(HttpContext);
//         if (token == null)
//             throw new MatrixException() {
//                 ErrorCode = "M_MISSING_TOKEN",
//                 Error = "Missing token"
//             };
//
//         var user = await userStore.GetUserByToken(token);
//         if (user == null)
//             throw new MatrixException() {
//                 ErrorCode = "M_UNKNOWN_TOKEN",
//                 Error = "No such user"
//             };
//
//         if (user.RoomKeys is not { Count: > 0 })
//             throw new MatrixException() {
//                 ErrorCode = "M_NOT_FOUND",
//                 Error = "No keys found"
//             };
//
//         return user.RoomKeys.Values.Last();
//     }
//
//     [HttpPost("room_keys/version")]
//     public async Task<RoomKeysResponse> UploadRoomKeys(RoomKeysRequest request) {
//         var token = tokenService.GetAccessToken(HttpContext);
//         if (token == null)
//             throw new MatrixException() {
//                 ErrorCode = "M_MISSING_TOKEN",
//                 Error = "Missing token"
//             };
//
//         var user = await userStore.GetUserByToken(token);
//         if (user == null)
//             throw new MatrixException() {
//                 ErrorCode = "M_UNKNOWN_TOKEN",
//                 Error = "No such user"
//             };
//
//         var roomKeys = new RoomKeysResponse {
//             Version = Guid.NewGuid().ToString(),
//             Etag = Guid.NewGuid().ToString(),
//             Algorithm = request.Algorithm,
//             AuthData = request.AuthData
//         };
//         user.RoomKeys.Add(roomKeys.Version, roomKeys);
//         return roomKeys;
//     }
//     
//     [HttpPost("keys/device_signing/upload")]
//     public async Task<object> UploadDeviceSigning(JsonObject request) {
//         var token = tokenService.GetAccessToken(HttpContext);
//         if (token == null)
//             throw new MatrixException() {
//                 ErrorCode = "M_MISSING_TOKEN",
//                 Error = "Missing token"
//             };
//
//         var user = await userStore.GetUserByToken(token);
//         if (user == null)
//             throw new MatrixException() {
//                 ErrorCode = "M_UNKNOWN_TOKEN",
//                 Error = "No such user"
//             };
//
//         return new { };
//     }
// }
//
// public class DeviceSigningRequest {
//     public CrossSigningKey? MasterKey { get; set; }
//     public CrossSigningKey? SelfSigningKey { get; set; }
//     public CrossSigningKey? UserSigningKey { get; set; }
//     
//     public class CrossSigningKey {
//         [JsonPropertyName("keys")]
//         public Dictionary<string, string> Keys { get; set; }
//         
//         [JsonPropertyName("signatures")]
//         public Dictionary<string, Dictionary<string, string>> Signatures { get; set; }
//         
//         [JsonPropertyName("usage")]
//         public List<string> Usage { get; set; }
//         
//         [JsonPropertyName("user_id")]
//         public string UserId { get; set; }
//     }
// }