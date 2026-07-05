using System.Text.Json.Serialization;
using System.Web;

namespace LibMatrix.EventTypes.Interop.Draupnir;

[MatrixEvent(EventName = EventId)]
public class DraupnirWatchedListsData : EventContent {
    public const string EventId = "org.matrix.mjolnir.watched_lists";

    [JsonPropertyName("references")]
    public List<string> References { get; set; }

    public List<(string RoomId, List<string>? Vias)> GetReferenceRooms() {
        List<(string RoomId, List<string>? Vias)> results = [];
        foreach (var reference in References) {
            var id = HttpUtility.UrlDecode(reference.Split("/#/")[1].Split("?via")[0]);
            var vias =
                reference.Contains('?')
                    ? reference.Split('?')[1].Split('&').Select(x => HttpUtility.UrlDecode(x.Replace("via=", "")))
                    : id.Contains(':')
                        ? [id.Split(':')[1]]
                        : null;
            results.Add((id, vias?.ToList()));
        }

        return results;
    }
}