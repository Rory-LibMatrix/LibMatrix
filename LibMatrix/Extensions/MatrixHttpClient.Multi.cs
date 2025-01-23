#define SINGLE_HTTPCLIENT // Use a single HttpClient instance for all MatrixHttpClient instances
// #define SYNC_HTTPCLIENT // Only allow one request as a time, for debugging
namespace LibMatrix.Extensions;

public static class HttpClientExtensions {
    public static async Task<bool> CheckSuccessStatus(this HttpClient hc, string url) {
        //cors causes failure, try to catch
        try {
            var resp = await hc.GetAsync(url);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception e) {
            Console.WriteLine($"Failed to check success status: {e.Message}");
            return false;
        }
    }
}

#region Per-instance HTTP client code

#if !SINGLE_HTTPCLIENT
public class MatrixHttpClient() : HttpClient(handler) {
    private static readonly SocketsHttpHandler handler = new() {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        MaxConnectionsPerServer = 256,
        EnableMultipleHttp2Connections = true
    };
    
    public Dictionary<string, string> AdditionalQueryParameters { get; set; } = new();
    internal string? AssertedUserId { get; set; }

    internal SemaphoreSlim _rateLimitSemaphore { get; } = new(1, 1);
    
    internal const bool debug = false;

    private JsonSerializerOptions GetJsonSerializerOptions(JsonSerializerOptions? options = null) {
        options ??= new JsonSerializerOptions();
        options.Converters.Add(new JsonFloatStringConverter());
        options.Converters.Add(new JsonDoubleStringConverter());
        options.Converters.Add(new JsonDecimalStringConverter());
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        return options;
    }

    public async Task<HttpResponseMessage> SendUnhandledAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if(debug) await _rateLimitSemaphore.WaitAsync(cancellationToken);
        Console.WriteLine($"Sending {request.Method} {BaseAddress}{request.RequestUri} ({Util.BytesToString(request.Content?.Headers.ContentLength ?? 0)})");
        if (request.RequestUri is null) throw new NullReferenceException("RequestUri is null");
        if (!request.RequestUri.IsAbsoluteUri) request.RequestUri = new Uri(BaseAddress, request.RequestUri);
        // if (AssertedUserId is not null) request.RequestUri = request.RequestUri.AddQuery("user_id", AssertedUserId);
        foreach (var (key, value) in AdditionalQueryParameters) request.RequestUri = request.RequestUri.AddQuery(key, value);

        // Console.WriteLine($"Sending request to {request.RequestUri}");

        try {
            var webAssemblyEnableStreamingResponseKey =
                new HttpRequestOptionsKey<bool>("WebAssemblyEnableStreamingResponse");
            request.Options.Set(webAssemblyEnableStreamingResponseKey, true);
        }
        catch (Exception e) {
            Console.WriteLine("Failed to set browser response streaming:");
            Console.WriteLine(e);
        }

        HttpResponseMessage? responseMessage;
        try {
            responseMessage = await base.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception e) {
            Console.WriteLine($"Failed to send request {request.Method} {BaseAddress}{request.RequestUri} ({Util.BytesToString(request.Content?.Headers.ContentLength ?? 0)}):\n{e}");
            throw;
        }
        finally {
            if(debug) _rateLimitSemaphore.Release();
        }
        
        Console.WriteLine($"Sending {request.Method} {request.RequestUri} ({Util.BytesToString(request.Content?.Headers.ContentLength ?? 0)}) -> {(int)responseMessage.StatusCode} {responseMessage.StatusCode} ({Util.BytesToString(responseMessage.Content.Headers.ContentLength ?? 0)})");

        return responseMessage;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var responseMessage = await SendUnhandledAsync(request, cancellationToken);
        if (responseMessage.IsSuccessStatusCode) return responseMessage;

        //error handling
        var content = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
        if (content.Length == 0)
            throw new MatrixException() {
                ErrorCode = "M_UNKNOWN",
                Error = "Unknown error, server returned no content"
            };
        if (!content.StartsWith('{')) throw new InvalidDataException("Encountered invalid data:\n" + content);
        //we have a matrix error

        MatrixException? ex = null;
        try {
            ex = JsonSerializer.Deserialize<MatrixException>(content);
        }
        catch (JsonException e) {
            throw new LibMatrixException() {
                ErrorCode = "M_INVALID_JSON",
                Error = e.Message + "\nBody:\n" + await responseMessage.Content.ReadAsStringAsync(cancellationToken)
            };
        }

        Debug.Assert(ex != null, nameof(ex) + " != null");
        ex.RawContent = content;
        // Console.WriteLine($"Failed to send request: {ex}");
        if (ex?.RetryAfterMs is null) throw ex!;
        //we have a ratelimit error
        await Task.Delay(ex.RetryAfterMs.Value, cancellationToken);
        typeof(HttpRequestMessage).GetField("_sendStatus", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(request, 0);
        return await SendAsync(request, cancellationToken);
    }

    // GetAsync
    public Task<HttpResponseMessage> GetAsync([StringSyntax("Uri")] string? requestUri, CancellationToken? cancellationToken = null) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUri), cancellationToken ?? CancellationToken.None);

    // GetFromJsonAsync
    public async Task<T?> TryGetFromJsonAsync<T>(string requestUri, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default) {
        try {
            return await GetFromJsonAsync<T>(requestUri, options, cancellationToken);
        }
        catch (HttpRequestException e) {
            Console.WriteLine($"Failed to get {requestUri}: {e.Message}");
            return default;
        }
    }

    public async Task<T> GetFromJsonAsync<T>(string requestUri, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default) {
        options = GetJsonSerializerOptions(options);
        // Console.WriteLine($"GetFromJsonAsync called for {requestUri} with json options {options?.ToJson(ignoreNull:true)} and cancellation token {cancellationToken}");
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
#if DEBUG && false // This is only used for testing, so it's disabled by default
        try {
            await PostAsync("http://localhost:5116/validate/" + typeof(T).AssemblyQualifiedName, new StreamContent(responseStream), cancellationToken);
        }
        catch (Exception e) {
            Console.WriteLine("[!!] Checking sync response failed: " + e);
        }
#endif
        return await JsonSerializer.DeserializeAsync<T>(responseStream, options, cancellationToken) ??
               throw new InvalidOperationException("Failed to deserialize response");
    }

    // GetStreamAsync
    public new async Task<Stream> GetStreamAsync(string requestUri, CancellationToken cancellationToken = default) {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task<HttpResponseMessage> PutAsJsonAsync<T>([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, T value, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default) where T : notnull {
        options = GetJsonSerializerOptions(options);
        var request = new HttpRequestMessage(HttpMethod.Put, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Console.WriteLine($"Sending PUT {requestUri}");
        // Console.WriteLine($"Content: {JsonSerializer.Serialize(value, value.GetType(), options)}");
        // Console.WriteLine($"Type: {value.GetType().FullName}");
        request.Content = new StringContent(JsonSerializer.Serialize(value, value.GetType(), options),
            Encoding.UTF8, "application/json");
        return await SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsJsonAsync<T>([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, T value, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default) where T : notnull {
        options ??= new JsonSerializerOptions();
        options.Converters.Add(new JsonFloatStringConverter());
        options.Converters.Add(new JsonDoubleStringConverter());
        options.Converters.Add(new JsonDecimalStringConverter());
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(value, value.GetType(), options),
            Encoding.UTF8, "application/json");
        return await SendAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<T?> GetAsyncEnumerableFromJsonAsync<T>([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, JsonSerializerOptions? options = null) {
        options = GetJsonSerializerOptions(options);
        var res = await GetAsync(requestUri);
        var result = JsonSerializer.DeserializeAsyncEnumerable<T>(await res.Content.ReadAsStreamAsync(), options);
        await foreach (var resp in result) yield return resp;
    }
}
#endif

#endregion