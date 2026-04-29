using System.Net;

namespace Soverance.Auth.Tests.Services;

/// <summary>
/// Handwritten test helper for HttpClient. Tests use Enqueue() to script responses;
/// the handler matches outgoing requests against the queued predicates in FIFO order.
/// All received requests are captured in Received for assertions.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(Func<HttpRequestMessage, bool> Match, HttpResponseMessage Response)> _queue = new();
    public List<HttpRequestMessage> Received { get; } = new();

    public StubHttpMessageHandler EnqueueJson(
        Func<HttpRequestMessage, bool> match,
        HttpStatusCode status,
        string jsonBody)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
        };
        _queue.Enqueue((match, response));
        return this;
    }

    public StubHttpMessageHandler EnqueueRaw(
        Func<HttpRequestMessage, bool> match,
        HttpStatusCode status,
        string body,
        string contentType = "text/plain")
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, contentType)
        };
        _queue.Enqueue((match, response));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Materialize content so request.Content can be re-read in assertions.
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        Received.Add(request);

        if (_queue.Count == 0)
            throw new InvalidOperationException(
                $"StubHttpMessageHandler received an unscripted request: {request.Method} {request.RequestUri}");

        var (match, response) = _queue.Dequeue();
        if (!match(request))
            throw new InvalidOperationException(
                $"StubHttpMessageHandler request did not match queued predicate. Got: {request.Method} {request.RequestUri}");

        return response;
    }
}
