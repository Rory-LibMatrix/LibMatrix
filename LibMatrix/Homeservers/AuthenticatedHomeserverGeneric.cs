using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LibMatrix.Extensions;
using LibMatrix.Helpers;
using LibMatrix.Responses;
using LibMatrix.RoomTypes;
using LibMatrix.Services;

namespace LibMatrix.Homeservers;

public class AuthenticatedHomeserverGeneric : RemoteHomeServer {
    public AuthenticatedHomeserverGeneric(TieredStorageService storage, string canonicalHomeServerDomain, string accessToken) : base(canonicalHomeServerDomain) {
        Storage = storage;
        AccessToken = accessToken.Trim();
        HomeServerDomain = canonicalHomeServerDomain.Trim();
        SyncHelper = new SyncHelper(this, storage);
        _httpClient = new MatrixHttpClient();
    }

    public TieredStorageService Storage { get; set; }
    public SyncHelper SyncHelper { get; init; }
    public WhoAmIResponse WhoAmI { get; set; } = null!;
    public string UserId => WhoAmI.UserId;
    public string AccessToken { get; set; }


    public Task<GenericRoom> GetRoom(string roomId) => Task.FromResult<GenericRoom>(new(this, roomId));

    public async Task<List<GenericRoom>> GetJoinedRooms() {
        var roomQuery = await _httpClient.GetAsync("/_matrix/client/v3/joined_rooms");

        var roomsJson = await roomQuery.Content.ReadFromJsonAsync<JsonElement>();
        var rooms = roomsJson.GetProperty("joined_rooms").EnumerateArray().Select(room => new GenericRoom(this, room.GetString()!)).ToList();

        Console.WriteLine($"Fetched {rooms.Count} rooms");

        return rooms;
    }

    public async Task<string> UploadFile(string fileName, Stream fileStream, string contentType = "application/octet-stream") {
        var res = await _httpClient.PostAsync($"/_matrix/media/v3/upload?filename={fileName}", new StreamContent(fileStream));
        if (!res.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to upload file: {await res.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to upload file: {await res.Content.ReadAsStringAsync()}");
        }

        var resJson = await res.Content.ReadFromJsonAsync<JsonElement>();
        return resJson.GetProperty("content_uri").GetString()!;
    }

    public async Task<GenericRoom> CreateRoom(CreateRoomRequest creationEvent) {
        creationEvent.CreationContent["creator"] = UserId;
        var res = await _httpClient.PostAsJsonAsync("/_matrix/client/v3/createRoom", creationEvent, new JsonSerializerOptions {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        if (!res.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to create room: {await res.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to create room: {await res.Content.ReadAsStringAsync()}");
        }

        return await GetRoom((await res.Content.ReadFromJsonAsync<JsonObject>())!["room_id"]!.ToString());
    }

#region Account Data

    public async Task<T> GetAccountData<T>(string key) {
        // var res = await _httpClient.GetAsync($"/_matrix/client/v3/user/{UserId}/account_data/{key}");
        // if (!res.IsSuccessStatusCode) {
        //     Console.WriteLine($"Failed to get account data: {await res.Content.ReadAsStringAsync()}");
        //     throw new InvalidDataException($"Failed to get account data: {await res.Content.ReadAsStringAsync()}");
        // }
        //
        // return await res.Content.ReadFromJsonAsync<T>();
        return await _httpClient.GetFromJsonAsync<T>($"/_matrix/client/v3/user/{UserId}/account_data/{key}");
    }

    public async Task SetAccountData(string key, object data) {
        var res = await _httpClient.PutAsJsonAsync($"/_matrix/client/v3/user/{UserId}/account_data/{key}", data);
        if (!res.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to set account data: {await res.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to set account data: {await res.Content.ReadAsStringAsync()}");
        }
    }

#endregion
}
