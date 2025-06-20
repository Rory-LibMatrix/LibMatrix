using LibMatrix.EventTypes.Spec;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using LibMatrix.RoomTypes;

namespace LibMatrix.Utilities.Bot.Interfaces;

public class CommandContext {
    public required GenericRoom Room { get; set; }
    public required StateEventResponse MessageEvent { get; set; }

    public string MessageContentWithoutReply =>
        (MessageEvent.TypedContent as RoomMessageEventContent)!
        .Body.Split('\n')
        .SkipWhile(x => x.StartsWith(">"))
        .Aggregate((x, y) => $"{x}\n{y}");

    public required string CommandName;
    public required string[] Args;
    public required AuthenticatedHomeserverGeneric Homeserver { get; set; }

    public async Task<EventIdResponse> Reply(RoomMessageEventContent content) => await Room.SendMessageEventAsync(content);
}

public class CommandResult {
    public required bool Success { get; set; }
    public Exception? Exception { get; set; }
    public required CommandResultType Result { get; set; }
    public required CommandContext Context { get; set; }

    public enum CommandResultType {
        Success,
        Failure_Exception,
        Failure_NoPermission,
        Failure_InvalidCommand
    }
}