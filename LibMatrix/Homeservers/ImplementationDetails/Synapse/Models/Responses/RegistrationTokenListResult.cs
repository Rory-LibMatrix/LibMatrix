using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminRegistrationTokenListResult {
    [JsonPropertyName("registration_tokens")]
    public List<SynapseAdminRegistrationTokenListResultToken> RegistrationTokens { get; set; } = new();

    public class SynapseAdminRegistrationTokenListResultToken {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("uses_allowed")]
        public int? UsesAllowed { get; set; }

        [JsonPropertyName("pending")]
        public int Pending { get; set; }

        [JsonPropertyName("completed")]
        public int Completed { get; set; }

        [JsonPropertyName("expiry_time")]
        public long? ExpiryTime { get; set; }

        [JsonIgnore]
        public DateTime? ExpiryTimeDateTime {
            get => ExpiryTime.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(ExpiryTime.Value).DateTime : null;
            set => ExpiryTime = value.HasValue ? new DateTimeOffset(value.Value).ToUnixTimeMilliseconds() : null;
        }
    }
}