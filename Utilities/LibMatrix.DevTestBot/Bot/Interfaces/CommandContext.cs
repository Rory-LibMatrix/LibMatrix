using LibMatrix.EventTypes.Spec;
using LibMatrix.RoomTypes;

namespace LibMatrix.ExampleBot.Bot.Interfaces;

public class CommandContext {
    public required GenericRoom Room { get; init; }
    public required StateEventResponse MessageEvent { get; init; }
    public string CommandName => MessageContent.Body.Split(' ')[0][1..];
    public string[] Args => MessageContent.Body.Split(' ')[1..];
    private RoomMessageEventContent MessageContent => MessageEvent.TypedContent as RoomMessageEventContent ?? throw new Exception("Message content is not a RoomMessageEventContent");
}