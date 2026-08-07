using System.Net;
using System.Text;
using Spiritual.Contracts;
using Spiritual.Implementation;
using Xunit;

namespace Spiritual.Tests;

public sealed class HttpBiblePassageProviderTests
{
    [Theory]
    [InlineData(BibleTranslation.Schlachter1951, "deu_sch", "deu1951")]
    [InlineData(BibleTranslation.Luther1912, "deu_l12", "deu1912")]
    [InlineData(BibleTranslation.ElberfelderUnrevised, "deu_elo", "deuelo")]
    [InlineData(BibleTranslation.Textbibel, "deu_tkw", "deutkw")]
    public async Task GermanReferenceUsesCuratedProviderMappingAndReturnsOnlyRequestedVerses(
        BibleTranslation translation,
        string providerId,
        string technicalId)
    {
        Uri? requestedUri = null;
        using var httpClient = new HttpClient(new DelegateHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(JsonResponse(
                """
                {
                  "chapter": {
                    "number": 3,
                    "content": [
                      { "type": "verse", "number": 15, "content": ["Vers fünfzehn."] },
                      { "type": "verse", "number": 16, "content": ["Denn Gott hat die Welt so geliebt."] },
                      { "type": "verse", "number": 17, "content": ["Vers siebzehn."] }
                    ]
                  }
                }
                """));
        }))
        {
            BaseAddress = new Uri("https://bible.example.test/")
        };
        var provider = CreateProvider(httpClient);

        var result = await provider.FetchAsync(
            new BiblePassageRequest(translation, "Johannes 3,16"),
            TestContext.Current.CancellationToken);

        Assert.Equal(BiblePassageFetchStatus.Found, result.Status);
        Assert.Equal($"https://bible.example.test/api/{providerId}/JHN/3.json", requestedUri?.AbsoluteUri);
        Assert.Equal("16 Denn Gott hat die Welt so geliebt.", result.Passage?.TextExcerpt);
        Assert.Equal(technicalId, result.Passage?.TechnicalTranslationId);
    }

    [Fact]
    public async Task InvalidReferenceDoesNotCallTheRemoteProvider()
    {
        using var httpClient = new HttpClient(new DelegateHandler((_, _) =>
            throw new InvalidOperationException("Der Handler darf nicht aufgerufen werden.")))
        {
            BaseAddress = new Uri("https://bible.example.test/")
        };
        var provider = CreateProvider(httpClient);

        var result = await provider.FetchAsync(
            new BiblePassageRequest(BibleTranslation.Schlachter1951, "keine Bibelstelle"),
            TestContext.Current.CancellationToken);

        Assert.Equal(BiblePassageFetchStatus.ReferenceNotFound, result.Status);
    }

    [Fact]
    public async Task TimeoutIsReportedWithoutMaskingCallerCancellation()
    {
        using var httpClient = new HttpClient(new DelegateHandler((_, _) =>
            throw new TaskCanceledException("Zeitüberschreitung")))
        {
            BaseAddress = new Uri("https://bible.example.test/")
        };
        var provider = CreateProvider(httpClient);

        var result = await provider.FetchAsync(
            new BiblePassageRequest(BibleTranslation.Schlachter1951, "Johannes 3,16"),
            TestContext.Current.CancellationToken);

        Assert.Equal(BiblePassageFetchStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task RequestTimeoutResponseIsReportedAsTimeout()
    {
        using var httpClient = new HttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestTimeout))))
        {
            BaseAddress = new Uri("https://bible.example.test/")
        };
        var provider = CreateProvider(httpClient);

        var result = await provider.FetchAsync(
            new BiblePassageRequest(BibleTranslation.Schlachter1951, "Johannes 3,16"),
            TestContext.Current.CancellationToken);

        Assert.Equal(BiblePassageFetchStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task NetworkFailureIsReportedAsUnavailable()
    {
        using var httpClient = new HttpClient(new DelegateHandler((_, _) =>
            throw new HttpRequestException("Netzwerk nicht verfügbar")))
        {
            BaseAddress = new Uri("https://bible.example.test/")
        };
        var provider = CreateProvider(httpClient);

        var result = await provider.FetchAsync(
            new BiblePassageRequest(BibleTranslation.Schlachter1951, "Johannes 3,16"),
            TestContext.Current.CancellationToken);

        Assert.Equal(BiblePassageFetchStatus.Unavailable, result.Status);
    }

#pragma warning disable CA1859 // Tests exercise the external provider interface.
    private static IBiblePassageProvider CreateProvider(HttpClient httpClient) =>
        new HttpBiblePassageProvider(
            httpClient,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero)));
#pragma warning restore CA1859

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
    }
}
