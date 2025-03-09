using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminDestinationListResult : SynapseNextTokenTotalCollectionResult {
    [JsonPropertyName("destinations")]
    public List<SynapseAdminDestinationListResultDestination> Destinations { get; set; } = new();

    public class SynapseAdminDestinationListResultDestination {
        [JsonPropertyName("destination")]
        public string Destination { get; set; }

        [JsonPropertyName("retry_last_ts")]
        public long RetryLastTs { get; set; }

        [JsonPropertyName("retry_interval")]
        public long RetryInterval { get; set; }

        [JsonPropertyName("failure_ts")]
        public long? FailureTs { get; set; }

        [JsonPropertyName("last_successful_stream_ordering")]
        public long? LastSuccessfulStreamOrdering { get; set; }

        [JsonIgnore]
        public DateTime? FailureTsDateTime {
            get => FailureTs.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(FailureTs.Value).DateTime : null;
            set => FailureTs = value.HasValue ? new DateTimeOffset(value.Value).ToUnixTimeMilliseconds() : null;
        }

        [JsonIgnore]
        public DateTime? RetryLastTsDateTime {
            get => DateTimeOffset.FromUnixTimeMilliseconds(RetryLastTs).DateTime;
            set => RetryLastTs = new DateTimeOffset(value.Value).ToUnixTimeMilliseconds();
        }

        [JsonIgnore]
        public TimeSpan RetryIntervalTimeSpan {
            get => TimeSpan.FromMilliseconds(RetryInterval);
            set => RetryInterval = (long)value.TotalMilliseconds;
        }
    }
}

public class SynapseAdminDestinationRoomListResult : SynapseNextTokenTotalCollectionResult {
    [JsonPropertyName("rooms")]
    public List<SynapseAdminDestinationRoomListResultRoom> Rooms { get; set; } = new();

    public class SynapseAdminDestinationRoomListResultRoom {
        [JsonPropertyName("room_id")]
        public string RoomId { get; set; }

        [JsonPropertyName("stream_ordering")]
        public int StreamOrdering { get; set; }
    }
}