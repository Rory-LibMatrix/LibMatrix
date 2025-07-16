using System.Net.Http.Json;
using LibMatrix.Abstractions;

namespace LibMatrix.Federation.Extensions;

public static class XMatrixAuthorizationSchemeExtensions {
    public static HttpRequestMessage ToSignedHttpRequestMessage(this XMatrixAuthorizationScheme.XMatrixRequestSignature requestSignature, VersionedHomeserverPrivateKey privateKey) {
        var signature = requestSignature.Sign(privateKey);
        var requestMessage = new HttpRequestMessage {
            Method = new HttpMethod(requestSignature.Method),
            RequestUri = new Uri(requestSignature.Uri, UriKind.Relative)
        };

        if (requestSignature.Content != null) {
            requestMessage.Content = JsonContent.Create(requestSignature.Content);
        }

        return requestMessage;
    }
}