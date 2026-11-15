using System.Net;
using System.Text;

namespace Aggregator.UnitTests.Support;

internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static FakeHttpHandler Returning(string json, HttpStatusCode status = HttpStatusCode.OK) => new(_ => Json(json, status));

    public HttpClient CreateClient(string baseUrl) => new(this) { BaseAddress = new Uri(baseUrl) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }
}
