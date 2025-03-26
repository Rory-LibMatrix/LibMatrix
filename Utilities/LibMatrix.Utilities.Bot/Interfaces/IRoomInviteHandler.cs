namespace LibMatrix.Utilities.Bot.Interfaces;

public interface IRoomInviteHandler {
    public Task HandleInviteAsync(RoomInviteContext invite);
}