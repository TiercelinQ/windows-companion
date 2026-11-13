using System.Net;
using System.Net.Http;
using System.Text;

namespace WindowsCompanion.Tests.Support;

/// <summary>HTTP handler driven by the test: no network, every request is answered by <paramref name="respond"/>.</summary>
internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int Calls { get; private set; }

    public HttpRequestMessage? LastRequest { get; private set; }

    public static FakeHttpMessageHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    public static FakeHttpMessageHandler Release(string tag) => Json($$"""{ "tag_name": "{{tag}}", "name": "ignored" }""");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastRequest = request;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(respond(request, cancellationToken));
    }
}
