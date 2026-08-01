using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services.Email;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Arena.UnitTests;

/// <summary>
/// EmailSendLogs backs the durable per-address rate limit for account email, and now also
/// records the operator's daily engagement report. Those two must not interfere: a daily
/// report to the operator's own address can't be allowed to lock that person out of a
/// password reset.
/// </summary>
public class EmailDispatchRateLimitTests
{
    private sealed class RecordingEmailSender : IEmailSender
    {
        public int Sends { get; private set; }

        public Task SendAsync(string toAddress, string subject, string htmlBody, string textBody, CancellationToken ct = default)
        {
            Sends++;
            return Task.CompletedTask;
        }
    }

    private static ArenaDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ArenaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static EmailDispatchService Build(ArenaDbContext db, IEmailSender sender) =>
        new(db,
            sender,
            Options.Create(new EmailOptions
            {
                Provider = "none",
                SenderAddress = "DoNotReply@example.com",
                AppUrls = new Dictionary<string, string> { ["arena"] = "https://arena.example.com" },
                RateLimit = new EmailOptions.RateLimitOptions
                {
                    PerAddressPerHour = 5,
                    PerIpPerHour = 20,
                    WindowMinutes = 60,
                },
            }),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<EmailDispatchService>.Instance);

    private static async Task<User> SeedUser(ArenaDbContext db, string email)
    {
        var user = new User { Id = Guid.NewGuid(), Email = email, Username = "u" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static void SeedLogs(ArenaDbContext db, string email, AccountTokenPurpose purpose, int count)
    {
        for (var i = 0; i < count; i++)
        {
            db.EmailSendLogs.Add(new EmailSendLog
            {
                Id = Guid.NewGuid(),
                Email = email,
                Purpose = purpose,
                SentAt = DateTime.UtcNow.AddMinutes(-i),
            });
        }
        db.SaveChanges();
    }

    [Fact]
    public async Task Daily_report_rows_do_not_consume_the_account_email_allowance()
    {
        using var db = NewDb();
        const string email = "ops@example.com";
        var user = await SeedUser(db, email);
        var mail = new RecordingEmailSender();

        // Well past PerAddressPerHour — but all of it is operator mail, not account mail.
        SeedLogs(db, email, AccountTokenPurpose.DailyReport, 10);

        var result = await Build(db, mail).SendAccountEmailAsync(
            user, AccountTokenPurpose.PasswordReset, "raw-token", "arena", ip: null);

        result.Should().Be(DispatchResult.Sent);
        mail.Sends.Should().Be(1);
    }

    [Fact]
    public async Task Account_email_is_still_rate_limited_by_account_email()
    {
        using var db = NewDb();
        const string email = "user@example.com";
        var user = await SeedUser(db, email);
        var mail = new RecordingEmailSender();

        SeedLogs(db, email, AccountTokenPurpose.EmailVerify, 5);

        var result = await Build(db, mail).SendAccountEmailAsync(
            user, AccountTokenPurpose.EmailVerify, "raw-token", "arena", ip: null);

        result.Should().Be(DispatchResult.RateLimited);
        mail.Sends.Should().Be(0);
    }
}
