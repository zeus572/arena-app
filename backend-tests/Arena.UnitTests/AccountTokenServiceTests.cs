using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services;
using Arena.API.Services.Email;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Arena.UnitTests;

public class AccountTokenServiceTests
{
    private static ArenaDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ArenaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<User> SeedUser(ArenaDbContext db)
    {
        var user = new User { Id = Guid.NewGuid(), Email = "u@example.com", Username = "u" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Issue_stores_only_the_hash_never_the_raw_token()
    {
        using var db = NewDb();
        var user = await SeedUser(db);
        var svc = new AccountTokenService(db);

        var raw = await svc.IssueAsync(user, AccountTokenPurpose.EmailVerify);

        var stored = await db.AccountTokens.SingleAsync();
        stored.TokenHash.Should().NotBe(raw);
        stored.TokenHash.Should().Be(TokenHasher.Hash(raw));
    }

    [Fact]
    public async Task Consume_succeeds_once_then_fails_second_time()
    {
        using var db = NewDb();
        var user = await SeedUser(db);
        var svc = new AccountTokenService(db);
        var raw = await svc.IssueAsync(user, AccountTokenPurpose.PasswordReset);

        var first = await svc.ConsumeAsync(raw, AccountTokenPurpose.PasswordReset);
        var second = await svc.ConsumeAsync(raw, AccountTokenPurpose.PasswordReset);

        first.Should().NotBeNull();
        first!.Id.Should().Be(user.Id);
        second.Should().BeNull(); // single-use
    }

    [Fact]
    public async Task Consume_rejects_wrong_purpose()
    {
        using var db = NewDb();
        var user = await SeedUser(db);
        var svc = new AccountTokenService(db);
        var raw = await svc.IssueAsync(user, AccountTokenPurpose.EmailVerify);

        var result = await svc.ConsumeAsync(raw, AccountTokenPurpose.PasswordReset);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Consume_rejects_expired_token()
    {
        using var db = NewDb();
        var user = await SeedUser(db);
        var svc = new AccountTokenService(db);
        var raw = await svc.IssueAsync(user, AccountTokenPurpose.PasswordReset);

        // Force expiry in the past.
        var token = await db.AccountTokens.SingleAsync();
        token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await svc.ConsumeAsync(raw, AccountTokenPurpose.PasswordReset);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Issue_invalidates_prior_outstanding_token_of_same_purpose()
    {
        using var db = NewDb();
        var user = await SeedUser(db);
        var svc = new AccountTokenService(db);

        var first = await svc.IssueAsync(user, AccountTokenPurpose.EmailVerify);
        var second = await svc.IssueAsync(user, AccountTokenPurpose.EmailVerify);

        (await svc.ConsumeAsync(first, AccountTokenPurpose.EmailVerify)).Should().BeNull();
        (await svc.ConsumeAsync(second, AccountTokenPurpose.EmailVerify)).Should().NotBeNull();
    }
}
