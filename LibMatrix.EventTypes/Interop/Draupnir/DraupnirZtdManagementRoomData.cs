using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Web;

namespace LibMatrix.EventTypes.Interop.Draupnir;

[MatrixEvent(EventName = EventId)]
public class DraupnirZtdManagementRoomData : EventContent {
    public const string EventId = "space.draupnir.zero_touch_deploy_room";

    [JsonPropertyName("room")]
    public string? Room { get; set; }

    public string? GetPlainRoomId() {
        var val = Room?.Split("/#/")[1].Split("?via")[0];
        return HttpUtility.UrlDecode(val);
    }
}