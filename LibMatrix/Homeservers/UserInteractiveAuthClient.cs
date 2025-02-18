using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibMatrix.Responses;

namespace LibMatrix.Homeservers;

public class UserInteractiveAuthClient {
    public UserInteractiveAuthClient(RemoteHomeserver hs) {
        Homeserver = hs;
    }

    [JsonIgnore]
    public RemoteHomeserver Homeserver { get; }
    private LoginResponse? _guestLogin;

    public async Task<UIAStage1Client> GetAvailableFlowsAsync(bool enableRegister = false, bool enableGuest = false) {
        // var resp = await Homeserver.ClientHttpClient.GetAsync("/_matrix/client/v3/login");
        // var data = await resp.Content.ReadFromJsonAsync<LoginFlowsResponse>();
        // if (!resp.IsSuccessStatusCode) Console.WriteLine("LoginFlows: " + await resp.Content.ReadAsStringAsync());
        // var loginFlows = data;
        //
        // try {
        //     var req = new HttpRequestMessage(HttpMethod.Post, "/_matrix/client/v3/register") {
        //         Content = new StringContent("{}")
        //     };
        //     var resp2 = await Homeserver.ClientHttpClient.SendUnhandledAsync(req, CancellationToken.None);
        //     var data2 = await resp2.Content.ReadFromJsonAsync<RegisterFlowsResponse>();
        //     if (!resp.IsSuccessStatusCode) Console.WriteLine("RegisterFlows: " + data2.ToJson());
        //     // return data;
        // }
        // catch (MatrixException e) {
        //     if (e is { ErrorCode: "M_FORBIDDEN" }) return null;
        //     throw;
        // }
        // catch (Exception e) {
        //     Console.WriteLine(e);
        //     throw;
        // }
        //
        //
        return new UIAStage1Client() {
            
        };
    }

    private async Task<RegisterFlowsResponse?> GetRegisterFlowsAsync() {
        return null;
    }

    internal class RegisterFlowsResponse {
        [JsonPropertyName("session")]
        public string Session { get; set; }

        [JsonPropertyName("flows")]
        public List<RegisterFlow> Flows { get; set; }

        [JsonPropertyName("params")]
        public JsonObject Params { get; set; }

        public class RegisterFlow {
            [JsonPropertyName("stages")]
            public List<string> Stages { get; set; }
        }
    }

    internal class LoginFlowsResponse {
        [JsonPropertyName("flows")]
        public List<LoginFlow> Flows { get; set; }

        public class LoginFlow {
            [JsonPropertyName("type")]
            public string Type { get; set; }
        }
    }

    public interface IUIAStage {
        public IUIAStage? PreviousStage { get; }
    }
    public class UIAStage1Client : IUIAStage {
        public IUIAStage? PreviousStage { get; }
        // public LoginFlowsResponse LoginFlows { get; set; }
    }
}