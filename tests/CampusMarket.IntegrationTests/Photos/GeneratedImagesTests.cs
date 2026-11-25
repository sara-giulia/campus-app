using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Common.Storage;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos;
using CampusMarket.Api.Features.Photos.GetListingPhotos;
using CampusMarket.Api.Infrastructure.Images;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CampusMarket.IntegrationTests.Photos;

public sealed class GeneratedImagesTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private const string OllamaBaseUrl = "http://ollama.test/";
    private const string ServiceCategory = "CampusMarket.Api.Infrastructure.Background.ImageGenerationService";
    private const string OllamaCategory = "CampusMarket.Api.Infrastructure.Images.OllamaImageGenerator";
    private const string SvgContentType = "image/svg+xml";

    private static readonly TimeSpan Expected = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NotExpected = TimeSpan.FromSeconds(1);

    // A small real PNG (1x1), the same file the Postman collection uploads.
    private static readonly byte[] ListingPhoto = File.ReadAllBytes(FindRepoFile("postman", "files", "listing-photo.png"));

    // The signature of a PNG followed by zeros, for photos seeded as rows.
    private static readonly byte[] PngImage = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];

    private static readonly byte[] SeededSvg = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'/>");

    private readonly CapturingLoggerProvider _logs = new();

    // A copy of the API on the same database with image generation on and the capturing logger.
    private WebApplicationFactory<Program> WithGeneration(
        string provider = ImageGenerationOptions.SvgProvider, Action<IServiceCollection>? configureServices = null) =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ImageGeneration:Enabled", "true");
            builder.UseSetting("ImageGeneration:Provider", provider);
            builder.UseSetting("ImageGeneration:Ollama:BaseUrl", OllamaBaseUrl);
            builder.ConfigureLogging(logging => logging
                .AddProvider(_logs)
                .AddFilter<CapturingLoggerProvider>(ServiceCategory, LogLevel.Debug));
            if (configureServices is not null)
                builder.ConfigureTestServices(configureServices);
        });

    // Ollama as the provider, with its HttpClient answered by the fake handler (never a real Ollama).
    private WebApplicationFactory<Program> WithOllama(FakeOllamaHandler handler) =>
        WithGeneration(ImageGenerationOptions.OllamaProvider, services =>
            services.AddHttpClient<IImageGenerator, OllamaImageGenerator>()
                .ConfigurePrimaryHttpMessageHandler(() => handler));

    private static HttpClient LoggedInClient(WebApplicationFactory<Program> app, Guid memberId)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(memberId));
        return client;
    }

    // Polls every 50 ms until the condition holds or the time runs out; the only place with Task.Delay.
    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return true;

            await Task.Delay(50);
        }

        return await condition();
    }

    // Waits until the service has finished with the listing (generated, skipped or failed), by its log line.
    // The wait is based on the log line, not on database reads: the line is written when the service is done.
    private Task<bool> WaitForServiceAsync(Guid listingId, TimeSpan timeout) =>
        WaitUntilAsync(() => Task.FromResult(
            _logs.Entries.Any(e => e.Category == ServiceCategory && e.Message.Contains(listingId.ToString()))), timeout);

    // Only with generation off (no background service): polls db.Set<Photo>() for a generated photo.
    private Task<bool> WaitForPhotoAsync(Guid listingId, TimeSpan timeout) =>
        WaitUntilAsync(() => QueryDbAsync(db => db.Set<Photo>().AnyAsync(p => p.ListingId == listingId && p.IsGenerated)), timeout);

    private Task<List<Photo>> PhotosOfAsync(Guid listingId) =>
        QueryDbAsync(db => db.Set<Photo>().AsNoTracking().Where(p => p.ListingId == listingId).ToListAsync());

    private List<CapturingLoggerProvider.Entry> Logs(string category, LogLevel level) =>
        _logs.Entries.Where(e => e.Category == category && e.Level == level).ToList();

    private string PhotoPath(Guid listingId, string fileName) =>
        Path.Combine(Factory.PhotosPath, listingId.ToString(), fileName);

    private async Task<(Member Seller, Listing Listing)> SeedSellerAndListingAsync(
        string title = "Manual POO", bool published = false, Category category = Category.Textbook, string? description = null)
    {
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, title, category, Condition.Good, 50m, CreatedAt, description).Value;
        if (published)
            listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());

        await SeedAsync(seller, listing);
        return (seller, listing);
    }

    // A generated photo row with its file in the photos folder, as the background service leaves it.
    private async Task<Photo> SeedGeneratedPhotoAsync(Listing listing)
    {
        var generated = Photo.CreateGenerated(listing, 0, SeededSvg, PhotoFormat.Svg, CreatedAt).Value;
        await SeedAsync(generated);
        var storage = Factory.Services.GetRequiredService<IPhotoStorage>();
        await storage.SaveAsync(listing.Id, generated.FileName, SeededSvg);
        return generated;
    }

    private async Task SeedRealPhotosAsync(Member seller, Listing listing, int count)
    {
        for (var i = 0; i < count; i++)
            await SeedAsync(Photo.Create(seller.Id, listing, i, PngImage, CreatedAt.AddMinutes(i + 1)).Value);
    }

    // Publishes the listing with the copy's client and returns the SVG made for it.
    private async Task<string> PublishAndReadSvgAsync(WebApplicationFactory<Program> app, Member seller, Listing listing)
    {
        using var client = LoggedInClient(app, seller.Id);
        var response = await client.PostAsync($"/api/v1/listings/{listing.Id}/publish", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WaitForServiceAsync(listing.Id, Expected)).Should().BeTrue("the service must handle the listing within 5 seconds");
        var photo = (await PhotosOfAsync(listing.Id)).Single();
        return await File.ReadAllTextAsync(PhotoPath(listing.Id, photo.FileName));
    }

    // Every <text> of the SVG; the last one is the category name, the others are the title lines.
    private static List<string> TitleLines(string svg)
    {
        var texts = XDocument.Parse(svg).Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value).ToList();
        return texts[..^1];
    }

    private static MultipartFormDataContent Form(byte[] content)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "listing-photo.png" } };
    }

    private static string FindRepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine([dir.FullName, .. parts])))
            dir = dir.Parent;

        return dir is null
            ? throw new FileNotFoundException($"Could not find {Path.Combine(parts)} above {AppContext.BaseDirectory}")
            : Path.Combine([dir.FullName, .. parts]);
    }

    [Fact]
    public async Task Given_GenerationOn_When_PublishingDraft_Then_ListingGetsGeneratedSvg()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        await using var app = WithGeneration();
        using var client = LoggedInClient(app, seller.Id);

        // Act
        var response = await client.PostAsync($"/api/v1/listings/{listing.Id}/publish", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WaitForServiceAsync(listing.Id, Expected)).Should().BeTrue("the service must handle the listing within 5 seconds");

        var photo = (await PhotosOfAsync(listing.Id)).Single();
        photo.IsGenerated.Should().BeTrue();
        photo.ContentType.Should().Be(SvgContentType);

        var list = await Client.GetFromJsonAsync<GetListingPhotosResponse>($"/api/v1/listings/{listing.Id}/photos", Json);
        list!.Items.Should().ContainSingle().Which.IsGenerated.Should().BeTrue();

        var file = await Client.GetAsync($"/api/v1/photos/{photo.Id}");
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.Should().Be(SvgContentType);
        var svg = await file.Content.ReadAsStringAsync();
        svg.Should().StartWith("<svg").And.Contain("Manual POO").And.Contain("Manuale");
        file.Headers.GetValues("Content-Security-Policy").Should().ContainSingle()
            .Which.Should().Be("default-src 'none'");
        file.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
    }

    [Fact]
    public async Task Given_TitleWithMarkup_When_GeneratingSvg_Then_TextIsEscaped()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync("<script>alert(1)</script> & Co");
        await using var app = WithGeneration();

        // Act
        var svg = await PublishAndReadSvgAsync(app, seller, listing);

        // Assert
        svg.Should().Contain("&lt;script&gt;").And.Contain("&amp;").And.NotContain("<script>");
        (await PhotosOfAsync(listing.Id)).Should().ContainSingle().Which.ContentType.Should().Be(SvgContentType);
    }

    [Fact]
    public async Task Given_TitleLongerThanThreeLines_When_GeneratingSvg_Then_ThreeLinesAndLastEndsWithEllipsis()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(
            "Osciloscop digital pentru laboratorul de electronica analogica si digitala aproape nou", category: Category.LabEquipment);
        await using var app = WithGeneration();

        // Act
        var svg = await PublishAndReadSvgAsync(app, seller, listing);

        // Assert
        var lines = TitleLines(svg);
        lines.Should().HaveCount(3);
        lines[^1].Should().EndWith("…");
        lines.Should().OnlyContain(line => line.Length <= 25);
        svg.Should().Contain("Echipament de laborator");
        (await PhotosOfAsync(listing.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Given_TitleWithOneLongWord_When_GeneratingSvg_Then_WordIsCutWithEllipsis()
    {
        // Arrange
        const string word = "Abcdefghijklmnopqrstuvwxyzabcd";
        var (seller, listing) = await SeedSellerAndListingAsync(word);
        await using var app = WithGeneration();

        // Act
        var svg = await PublishAndReadSvgAsync(app, seller, listing);

        // Assert
        TitleLines(svg).Should().Equal(word[..23] + "…");
        (await PhotosOfAsync(listing.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Given_ListingWithRealPhoto_When_Publishing_Then_NoImageIsGenerated()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        var real = Photo.Create(seller.Id, listing, 0, PngImage, CreatedAt).Value;
        await SeedAsync(real);
        await using var app = WithGeneration();
        using var client = LoggedInClient(app, seller.Id);

        // Act
        var response = await client.PostAsync($"/api/v1/listings/{listing.Id}/publish", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WaitForServiceAsync(listing.Id, Expected)).Should().BeTrue("the service must handle the listing within 5 seconds");
        Logs(ServiceCategory, LogLevel.Debug).Should().ContainSingle().Which.Message.Should().Contain("Photo.ListingHasPhotos");

        var photo = (await PhotosOfAsync(listing.Id)).Single();
        photo.Id.Should().Be(real.Id);
        photo.IsGenerated.Should().BeFalse();
    }

    [Fact]
    public async Task Given_ActiveAndDraftListingsBeforeStart_When_AppStarts_Then_OnlyActiveGetsImage()
    {
        // Arrange
        var (seller, active) = await SeedSellerAndListingAsync(published: true);
        var draft = Listing.Create(seller.Id, "Notite POO", Category.Notes, Condition.Good, 10m, CreatedAt).Value;
        await SeedAsync(draft);
        await using var app = WithGeneration();

        // Act
        app.CreateClient().Dispose();

        // Assert
        (await WaitForServiceAsync(active.Id, Expected)).Should().BeTrue("the service must handle the listing within 5 seconds");
        (await PhotosOfAsync(active.Id)).Should().ContainSingle().Which.IsGenerated.Should().BeTrue();
        (await PhotosOfAsync(draft.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_GeneratedPhoto_When_SellerUploadsRealPhoto_Then_GeneratedPhotoIsReplaced()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        var generated = await SeedGeneratedPhotoAsync(listing);
        AuthenticateAs(seller.Id);
        using var form = Form(ListingPhoto);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        body!.IsGenerated.Should().BeFalse();
        body.ContentType.Should().Be("image/png");

        var photo = (await PhotosOfAsync(listing.Id)).Single();
        photo.Id.Should().Be(body.Id);
        photo.IsGenerated.Should().BeFalse();
        File.Exists(PhotoPath(listing.Id, generated.FileName)).Should().BeFalse();
        (await File.ReadAllBytesAsync(PhotoPath(listing.Id, photo.FileName))).Should().Equal(ListingPhoto);
    }

    [Fact]
    public async Task Given_FourRealPhotosAndGenerated_When_Uploading_Then_Returns201AndGeneratedIsGone()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        var generated = await SeedGeneratedPhotoAsync(listing);
        await SeedRealPhotosAsync(seller, listing, 4);
        AuthenticateAs(seller.Id);
        using var form = Form(ListingPhoto);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<PhotoResponse>(Json))!.IsGenerated.Should().BeFalse();

        var photos = await PhotosOfAsync(listing.Id);
        photos.Should().HaveCount(Photo.MaxPerListing).And.OnlyContain(p => !p.IsGenerated);
        File.Exists(PhotoPath(listing.Id, generated.FileName)).Should().BeFalse();
    }

    [Fact]
    public async Task Given_FiveRealPhotosAndGenerated_When_Uploading_Then_Returns400AndGeneratedStays()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        var generated = await SeedGeneratedPhotoAsync(listing);
        await SeedRealPhotosAsync(seller, listing, Photo.MaxPerListing);
        AuthenticateAs(seller.Id);
        using var form = Form(ListingPhoto);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.LimitReached");
        error.Type.Should().Be(ErrorType.Validation);

        var photos = await PhotosOfAsync(listing.Id);
        photos.Should().HaveCount(Photo.MaxPerListing + 1);
        photos.Should().ContainSingle(p => p.IsGenerated).Which.Id.Should().Be(generated.Id);
        File.Exists(PhotoPath(listing.Id, generated.FileName)).Should().BeTrue();
    }

    [Fact]
    public async Task Given_OllamaReturnsPng_When_Publishing_Then_PngIsSavedAsGeneratedPhoto()
    {
        // Arrange
        var description = new string('d', 250);
        var (seller, listing) = await SeedSellerAndListingAsync(description: description);
        var handler = new FakeOllamaHandler(HttpStatusCode.OK, $$"""{"data":[{"b64_json":"{{Convert.ToBase64String(ListingPhoto)}}"}]}""");
        await using var app = WithOllama(handler);
        using var client = LoggedInClient(app, seller.Id);

        // Act
        var response = await client.PostAsync($"/api/v1/listings/{listing.Id}/publish", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WaitForServiceAsync(listing.Id, Expected)).Should().BeTrue("the service must handle the listing within 5 seconds");

        var photo = (await PhotosOfAsync(listing.Id)).Single();
        photo.ContentType.Should().Be("image/png");
        photo.IsGenerated.Should().BeTrue();
        (await File.ReadAllBytesAsync(PhotoPath(listing.Id, photo.FileName))).Should().Equal(ListingPhoto);

        handler.Calls.Should().Be(1);
        var request = handler.Requests.Single();
        request.Uri.AbsoluteUri.Should().Be($"{OllamaBaseUrl}v1/images/generations");
        using var json = JsonDocument.Parse(request.Body);
        json.RootElement.GetProperty("model").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("response_format").GetString().Should().Be("b64_json");
        var prompt = json.RootElement.GetProperty("prompt").GetString();
        prompt.Should().Contain("Manual POO").And.Contain(description[..200]).And.NotContain(description[..201]);
        Logs(OllamaCategory, LogLevel.Warning).Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, """{"data":[{"b64_json":"%%% not base64 %%%"}]}""")]
    [InlineData(HttpStatusCode.OK, """{"data":[{"b64_json":"aGVsbG8gd29ybGQ="}]}""")]
    [InlineData(HttpStatusCode.OK, "null")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, """{"data":[]}""")]
    [InlineData(HttpStatusCode.OK, """{"data":[{}]}""")]
    public async Task Given_OllamaFails_When_Publishing_Then_SvgIsUsedAndWarningLogged(HttpStatusCode status, string body)
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        var handler = new FakeOllamaHandler(status, body);
        await using var app = WithOllama(handler);
        using var client = LoggedInClient(app, seller.Id);

        // Act
        var response = await client.PostAsync($"/api/v1/listings/{listing.Id}/publish", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WaitForServiceAsync(listing.Id, Expected)).Should().BeTrue("the service must handle the listing within 5 seconds");

        var photo = (await PhotosOfAsync(listing.Id)).Single();
        photo.ContentType.Should().Be(SvgContentType);
        photo.IsGenerated.Should().BeTrue();
        handler.Calls.Should().Be(1);
        Logs(OllamaCategory, LogLevel.Warning).Should().ContainSingle();
    }

    [Fact]
    public async Task Given_GeneratorThrows_When_Publishing_Then_ErrorIsLoggedAndServiceKeepsRunning()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var first = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        var second = Listing.Create(seller.Id, "Manual SGBD", Category.Textbook, Condition.Good, 40m, CreatedAt).Value;
        await SeedAsync(seller, first, second);

        var generator = new ThrowsOnceImageGenerator();
        await using var app = WithGeneration(configureServices: services =>
        {
            services.RemoveAll<IImageGenerator>();
            services.AddSingleton<IImageGenerator>(generator);
        });
        using var client = LoggedInClient(app, seller.Id);

        (await client.PostAsync($"/api/v1/listings/{first.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var logged = await WaitUntilAsync(() => Task.FromResult(Logs(ServiceCategory, LogLevel.Error).Count > 0), Expected);
        logged.Should().BeTrue("the failed image must be logged within 5 seconds");

        // Act
        var response = await client.PostAsync($"/api/v1/listings/{second.Id}/publish", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WaitForServiceAsync(second.Id, Expected)).Should().BeTrue("the service must handle the listing within 5 seconds");

        (await PhotosOfAsync(first.Id)).Should().BeEmpty();
        (await PhotosOfAsync(second.Id)).Should().ContainSingle().Which.ContentType.Should().Be(SvgContentType);
        Logs(ServiceCategory, LogLevel.Error).Should().ContainSingle()
            .Which.Exception.Should().BeOfType<InvalidOperationException>();
        generator.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Given_GenerationOff_When_Publishing_Then_NoImageIsGenerated()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/publish", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WaitForPhotoAsync(listing.Id, NotExpected)).Should().BeFalse();
        (await PhotosOfAsync(listing.Id)).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public async Task Given_UnknownProvider_When_StartingTheApp_Then_ThrowsOptionsValidationException()
    {
        // Arrange
        await using var invalid = WithGeneration("Dall-E");

        // Act
        Action act = () => invalid.CreateClient();

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*ImageGeneration:Provider must be Svg or Ollama.*");

        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(0);
    }

    // Answers every Ollama call with the given status and body, and keeps the requests it got.
    private sealed class FakeOllamaHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<(Uri Uri, string Body)> _requests = new();

        public int Calls => _requests.Count;

        public IReadOnlyCollection<(Uri Uri, string Body)> Requests => _requests.ToArray();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            _requests.Enqueue((request.RequestUri!, content));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    // Throws on the first image, then draws the SVG, so the test sees the service go on after a failure.
    private sealed class ThrowsOnceImageGenerator : IImageGenerator
    {
        private readonly SvgImageGenerator _svg = new();
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<GeneratedImage> GenerateAsync(ImagePrompt prompt, CancellationToken ct = default) =>
            Interlocked.Increment(ref _calls) == 1
                ? throw new InvalidOperationException("Image generation failed on purpose.")
                : _svg.GenerateAsync(prompt, ct);
    }
}
