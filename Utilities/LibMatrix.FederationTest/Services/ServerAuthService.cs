using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using LibMatrix.Extensions;
using LibMatrix.Federation;
using LibMatrix.FederationTest.Utilities;
using LibMatrix.Responses.Federation;
using LibMatrix.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace LibMatrix.FederationTest.Services;

public class ServerAuthService(HomeserverProviderService hsProvider, IHttpContextAccessor httpContextAccessor) {
    private static Dictionary<string, SignedObject<ServerKeysResponse>> _serverKeysCache = new();

    public async Task AssertValidAuthentication(XMatrixAuthorizationScheme.XMatrixAuthorizationHeader authHeader) {
        var httpContext = httpContextAccessor.HttpContext!;
        var hs = await hsProvider.GetFederationClient(authHeader.Origin, "");
        var serverKeys = (_serverKeysCache.TryGetValue(authHeader.Origin, out var sk) && sk.TypedContent.ValidUntil > DateTimeOffset.UtcNow)
            ? sk
            : _serverKeysCache[authHeader.Origin] = await hs.GetServerKeysAsync();
        var publicKeyBase64 = serverKeys.TypedContent.VerifyKeys[authHeader.Key].Key;
        var publicKey = Ed25519Utils.LoadPublicKeyFromEncoded(publicKeyBase64);
        var requestAuthenticationData = new XMatrixAuthorizationScheme.XMatrixRequestSignature() {
            Method = httpContext.Request.Method,
            Uri = httpContext.Features.Get<IHttpRequestFeature>()!.RawTarget,
            OriginServerName = authHeader.Origin,
            DestinationServerName = authHeader.Destination,
            Content = httpContext.Request.HasJsonContentType() ? await httpContext.Request.ReadFromJsonAsync<JsonObject?>() : null
        };
        var contentBytes = CanonicalJsonSerializer.SerializeToUtf8Bytes(requestAuthenticationData);
        var signatureBytes = UnpaddedBase64.Decode(authHeader.Signature);

        Console.WriteLine($"Validating X-Matrix authorized request\n" +
                          $" - From: {requestAuthenticationData.OriginServerName}, To: {requestAuthenticationData.DestinationServerName}\n" +
                          $" - Key: {authHeader.Key} ({publicKeyBase64})\n" +
                          $" - Signature: {authHeader.Signature}\n" +
                          $" - Request: {requestAuthenticationData.Method} {requestAuthenticationData.Uri}\n" +
                          $" - Has request body: {requestAuthenticationData.Content is not null}\n" +
                          // $" - Canonicalized request body (or null if missing): {(requestAuthenticationData.Content is null ? "(null)" : CanonicalJsonSerializer.Serialize(requestAuthenticationData.Content))}\n" +
                          $" - Canonicalized message to verify: {System.Text.Encoding.UTF8.GetString(contentBytes)}");

        if (!publicKey.Verify(Ed25519.Algorithm.Ed25519, null, contentBytes, 0, contentBytes.Length, signatureBytes, 0)) {
            throw new UnauthorizedAccessException("Invalid signature in X-Matrix authorization header.");
        }

        Console.WriteLine("INFO | Valid X-Matrix authorization header.");
    }

    public async Task AssertValidAuthentication() {
        await AssertValidAuthentication(
            XMatrixAuthorizationScheme.XMatrixAuthorizationHeader.FromHeaderValue(
                httpContextAccessor.HttpContext!.Request.GetTypedHeaders().Get<AuthenticationHeaderValue>("Authorization")!
            )
        );
    }
}