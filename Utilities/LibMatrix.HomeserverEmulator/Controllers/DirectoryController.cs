using System.Text.Json.Serialization;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.HomeserverEmulator.Services;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/_matrix/")]
public class DirectoryController(ILogger<DirectoryController> logger, RoomStore roomStore) : ControllerBase {
#region Room directory

    [HttpGet("client/v3/directory/room/{alias}")]
    public async Task<AliasResult> GetRoomAliasV3(string alias) {
        var match = roomStore._rooms.FirstOrDefault(x =>
            x.State.Any(y => y.Type == RoomCanonicalAliasEventContent.EventId && y.StateKey == "" && y.RawContent?["alias"]?.ToString() == alias));

        if (match == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        var servers = match.State.Where(x => x.Type == RoomMemberEventContent.EventId && x.RawContent?["membership"]?.ToString() == "join")
            .Select(x => x.StateKey!.Split(':', 2)[1]).ToList();

        return new() {
            RoomId = match.RoomId,
            Servers = servers
        };
    }

    [HttpGet("client/v3/publicRooms")]
    public async Task<PublicRoomDirectoryResult> GetPublicRooms(int limit = 100, string? server = null, string? since = null) {
        var rooms = roomStore._rooms.OrderByDescending(x => x.JoinedMembers.Count).AsEnumerable();

        if (since != null) {
            rooms = rooms.SkipWhile(x => x.RoomId != since).Skip(1);
        }

        if (server != null) {
            rooms = rooms.Where(x => x.State.Any(y => y.Type == RoomMemberEventContent.EventId && y.StateKey!.EndsWith(server)));
        }

        var count = rooms.Count();
        rooms = rooms.Take(limit);

        return new PublicRoomDirectoryResult() {
            Chunk = rooms.Select(x => new PublicRoomDirectoryResult.PublicRoomListItem() {
                RoomId = x.RoomId,
                Name = x.State.FirstOrDefault(y => y.Type == RoomNameEventContent.EventId)?.RawContent?["name"]?.ToString(),
                Topic = x.State.FirstOrDefault(y => y.Type == RoomTopicEventContent.EventId)?.RawContent?["topic"]?.ToString(),
                AvatarUrl = x.State.FirstOrDefault(y => y.Type == RoomAvatarEventContent.EventId)?.RawContent?["url"]?.ToString(),
                GuestCanJoin = x.State.Any(y => y.Type == RoomGuestAccessEventContent.EventId && y.RawContent?["guest_access"]?.ToString() == "can_join"),
                NumJoinedMembers = x.JoinedMembers.Count,
                WorldReadable = x.State.Any(y => y.Type == RoomHistoryVisibilityEventContent.EventId && y.RawContent?["history_visibility"]?.ToString() == "world_readable"),
                JoinRule = x.State.FirstOrDefault(y => y.Type == RoomJoinRulesEventContent.EventId)?.RawContent?["join_rule"]?.ToString(),
                CanonicalAlias = x.State.FirstOrDefault(y => y.Type == RoomCanonicalAliasEventContent.EventId)?.RawContent?["alias"]?.ToString()
            }).ToList(),
            NextBatch = count > limit ? rooms.Last().RoomId : null,
            TotalRoomCountEstimate = count
        };
    }

    [HttpPost("client/v3/publicRooms")]
    public async Task<PublicRoomDirectoryResult> GetFilteredPublicRooms([FromBody] PublicRoomDirectoryRequest request, [FromQuery] string? server = null) {
        var rooms = roomStore._rooms.OrderByDescending(x => x.JoinedMembers.Count).AsEnumerable();

        if (request.Since != null) {
            rooms = rooms.SkipWhile(x => x.RoomId != request.Since).Skip(1);
        }

        if (server != null) {
            rooms = rooms.Where(x => x.State.Any(y => y.Type == RoomMemberEventContent.EventId && y.StateKey!.EndsWith(server)));
        }

        var count = rooms.Count();
        rooms = rooms.Take(request.Limit ?? 100);

        return new PublicRoomDirectoryResult() {
            Chunk = rooms.Select(x => new PublicRoomDirectoryResult.PublicRoomListItem() {
                RoomId = x.RoomId,
                Name = x.State.FirstOrDefault(y => y.Type == RoomNameEventContent.EventId)?.RawContent?["name"]?.ToString(),
                Topic = x.State.FirstOrDefault(y => y.Type == RoomTopicEventContent.EventId)?.RawContent?["topic"]?.ToString(),
                AvatarUrl = x.State.FirstOrDefault(y => y.Type == RoomAvatarEventContent.EventId)?.RawContent?["url"]?.ToString(),
                GuestCanJoin = x.State.Any(y => y.Type == RoomGuestAccessEventContent.EventId && y.RawContent?["guest_access"]?.ToString() == "can_join"),
                NumJoinedMembers = x.JoinedMembers.Count,
                WorldReadable = x.State.Any(y => y.Type == RoomHistoryVisibilityEventContent.EventId && y.RawContent?["history_visibility"]?.ToString() == "world_readable"),
                JoinRule = x.State.FirstOrDefault(y => y.Type == RoomJoinRulesEventContent.EventId)?.RawContent?["join_rule"]?.ToString(),
                CanonicalAlias = x.State.FirstOrDefault(y => y.Type == RoomCanonicalAliasEventContent.EventId)?.RawContent?["alias"]?.ToString()
            }).ToList(),
            NextBatch = count > request.Limit ? rooms.Last().RoomId : null,
            TotalRoomCountEstimate = count
        };
    }

#endregion

#region User directory

    [HttpPost("client/v3/user_directory/search")]
    public async Task<UserDirectoryResponse> SearchUserDirectory([FromBody] UserDirectoryRequest request) {
        var users = roomStore._rooms
            .SelectMany(x => x.State.Where(y =>
                    y.Type == RoomMemberEventContent.EventId
                    && y.RawContent?["membership"]?.ToString() == "join"
                    && (y.StateKey!.ContainsAnyCase(request.SearchTerm) || y.RawContent?["displayname"]?.ToString()?.ContainsAnyCase(request.SearchTerm) == true)
                )
            )
            .DistinctBy(x => x.StateKey)
            .ToList();

        request.Limit ??= 10;

        return new() {
            Results = users.Select(x => new UserDirectoryResponse.UserDirectoryResult {
                UserId = x.StateKey!,
                DisplayName = x.RawContent?["displayname"]?.ToString(),
                AvatarUrl = x.RawContent?["avatar_url"]?.ToString()
            }).ToList(),
            Limited = users.Count > request.Limit
        };
    }

#endregion
}

public class PublicRoomDirectoryRequest {
    [JsonPropertyName("filter")]
    public PublicRoomDirectoryFilter Filter { get; set; }

    [JsonPropertyName("include_all_networks")]
    public bool IncludeAllNetworks { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("since")]
    public string? Since { get; set; }

    [JsonPropertyName("third_party_instance_id")]
    public string? ThirdPartyInstanceId { get; set; }

    public class PublicRoomDirectoryFilter {
        [JsonPropertyName("generic_search_term")]
        public string? GenericSearchTerm { get; set; }

        [JsonPropertyName("room_types")]
        public List<string>? RoomTypes { get; set; }
    }
}