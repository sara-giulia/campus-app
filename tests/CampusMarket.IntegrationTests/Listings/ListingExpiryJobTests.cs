using System.Net.Http.Json;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Features.Notifications.GetMyNotifications;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class ListingExpiryJobTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // Starts a copy of the API on the same database with the expiry job turned on and the email service replaced.
    private WebApplicationFactory<Program> WithExpiryJob(RecordingEmailSender emailSender) =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ListingExpiry:Enabled", "true");
            builder.UseSetting("ListingExpiry:Interval", "00:00:00.200");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(emailSender);
            });
        });

    private async Task<ListingStatus> WaitForStatusAsync(Guid listingId, ListingStatus expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await ReadStatusAsync(listingId) == expected)
                return expected;

            await Task.Delay(100);
        }

        return await ReadStatusAsync(listingId);
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50);
    }

    private Task<ListingStatus> ReadStatusAsync(Guid listingId) =>
        QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Where(l => l.Id == listingId)
            .Select(l => l.Status)
            .SingleAsync());

    [Fact]
    public async Task Given_JobOn_When_JobRuns_Then_ExpiresOnlyTheActiveListingPastExpiresAtAndNotifiesAndEmailsTheSeller()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;

        var stale = ExpiryListings.ActivePastExpiry(seller.Id, "Manual POO");
        var notYet = ExpiryListings.Active(seller.Id, "Calculator stiintific", Category.Electronics, CreatedAt.AddHours(1));
        var reserved = ExpiryListings.ActivePastExpiry(seller.Id, "Notite analiza", Category.Notes, CreatedAt.AddHours(2));
        reserved.Reserve(buyer.Id, DateTimeOffset.UtcNow);
        var draft = ExpiryListings.InStatus(seller.Id, ListingStatus.Draft, buyer.Id);
        await SeedAsync(seller, buyer, stale, notYet, reserved, draft);
        var emailSender = new RecordingEmailSender();

        // Act: start the job, wait until it has expired the stale listing, then stop the host,
        // so no further background tick changes the data during the assertions.
        ListingStatus finalStatus;
        await using (var withJob = WithExpiryJob(emailSender))
        {
            withJob.CreateClient();
            finalStatus = await WaitForStatusAsync(stale.Id, ListingStatus.Expired, TimeSpan.FromSeconds(5));
            // The status is committed before the email is tried; stopping the host now would cancel the send.
            await WaitForAsync(() => emailSender.Attempts > 0, TimeSpan.FromSeconds(5));
        }

        // Assert
        finalStatus.Should().Be(
            ListingStatus.Expired,
            "the background job must expire the stale listing within 5 seconds");

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().ToDictionaryAsync(l => l.Id));
        saved[stale.Id].Status.Should().Be(ListingStatus.Expired);
        saved[stale.Id].Version.Should().Be(stale.Version + 1);
        saved[notYet.Id].Status.Should().Be(ListingStatus.Active);
        saved[notYet.Id].Version.Should().Be(notYet.Version);
        saved[reserved.Id].Status.Should().Be(ListingStatus.Reserved);
        saved[reserved.Id].Version.Should().Be(reserved.Version);
        saved[draft.Id].Status.Should().Be(ListingStatus.Draft);
        saved[draft.Id].Version.Should().Be(draft.Version);

        var notifications = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking().ToListAsync());
        var notification = notifications.Should().ContainSingle().Subject;
        notification.MemberId.Should().Be(seller.Id);
        notification.Type.Should().Be(NotificationType.ListingExpired);
        notification.ListingId.Should().Be(stale.Id);

        AuthenticateAs(seller.Id);
        var response = await Client.GetFromJsonAsync<GetMyNotificationsResponse>("/api/v1/members/me/notifications", Json);
        var item = response!.Items.Should().ContainSingle().Subject;
        item.Type.Should().Be(NotificationType.ListingExpired);
        item.ListingId.Should().Be(stale.Id);
        item.Message.Should().Be("Your listing Manual POO expired");

        var email = emailSender.Sent.Should().ContainSingle().Subject;
        email.To.Should().Be("ion@student.ro");
        email.Body.Should().Contain("Manual POO");
    }

    [Fact]
    public async Task Given_EmailSenderThrows_When_JobRuns_Then_ListingStaysExpiredAndNotificationIsSaved()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var stale = ExpiryListings.ActivePastExpiry(seller.Id);
        await SeedAsync(seller, stale);
        var emailSender = new RecordingEmailSender { Throws = true };

        // Act
        ListingStatus finalStatus;
        await using (var withJob = WithExpiryJob(emailSender))
        {
            withJob.CreateClient();
            finalStatus = await WaitForStatusAsync(stale.Id, ListingStatus.Expired, TimeSpan.FromSeconds(5));
            // The status is committed before the email is tried; stopping the host now would cancel the send.
            await WaitForAsync(() => emailSender.Attempts > 0, TimeSpan.FromSeconds(5));
        }

        // Assert
        finalStatus.Should().Be(
            ListingStatus.Expired,
            "a failed email must not undo the expiry");
        emailSender.Attempts.Should().BeGreaterThan(0);
        emailSender.Sent.Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Version.Should().Be(stale.Version + 1);

        var notifications = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking().ToListAsync());
        notifications.Should().ContainSingle(n => n.MemberId == seller.Id && n.ListingId == stale.Id);
    }
}
