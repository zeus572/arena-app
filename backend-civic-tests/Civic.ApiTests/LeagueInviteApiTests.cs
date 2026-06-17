using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Covers the friend-invite experiences on leagues: open share links, personal invites by email
/// (with per-address outcomes + dedupe), and joining by code.
/// </summary>
[Collection("Database")]
public class LeagueInviteApiTests
{
    private readonly DatabaseFixture _fixture;

    public LeagueInviteApiTests(DatabaseFixture fixture) => _fixture = fixture;

    private HttpClient ClientFor(Guid userId, string email)
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.MintAccessToken(userId, email));
        return client;
    }

    // Mirrors the frontend, which snapshots the signed-in user's email onto their membership.
    private async Task<LeagueDetailDto> CreateLeagueAsync(HttpClient owner, string name, string ownerEmail = "owner@example.com")
    {
        var res = await owner.PostAsJsonAsync("/api/leagues",
            new CreateLeagueRequest { Name = name, DisplayName = "Owner", Email = ownerEmail });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<LeagueDetailDto>())!;
    }

    [Fact]
    public async Task InviteByEmail_CreatesPersonalInvites_AndReportsPerAddressOutcomes()
    {
        await _fixture.ResetMutableAsync();
        var owner = ClientFor(Guid.NewGuid(), "owner@example.com");
        var league = await CreateLeagueAsync(owner, "Email Crew");

        // Two valid + one malformed address.
        var res = await owner.PostAsJsonAsync(
            $"/api/leagues/{league.Id}/invites/email",
            new InviteByEmailRequest { Emails = new() { "Friend.One@Example.com", "two@example.com", "not-an-email" } });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = (await res.Content.ReadFromJsonAsync<List<EmailInviteResultDto>>())!;
        results.Should().HaveCount(3);
        results.Single(r => r.Email == "friend.one@example.com").Status.Should().Be("invited");
        results.Single(r => r.Email == "two@example.com").Status.Should().Be("invited");
        results.Single(r => r.Status == "invalid").Email.Should().Be("not-an-email");

        // Each invited address got a single-use personal invite with a code.
        var invited = results.Where(r => r.Status == "invited").ToList();
        invited.Should().OnlyContain(r => r.Invite != null && r.Invite!.MaxUses == 1 && r.Invite.Email != null);

        // They show up in the invite list, as pending (not yet accepted).
        var list = (await owner.GetFromJsonAsync<List<LeagueInviteDto>>($"/api/leagues/{league.Id}/invites"))!;
        list.Where(i => i.Email != null).Should().HaveCount(2);
        list.Where(i => i.Email != null).Should().OnlyContain(i => i.Accepted == false);
    }

    [Fact]
    public async Task InviteByEmail_IsIdempotent_AndSkipsExistingMembers()
    {
        await _fixture.ResetMutableAsync();
        var owner = ClientFor(Guid.NewGuid(), "owner@example.com");
        var league = await CreateLeagueAsync(owner, "Dedupe Crew");

        // First invite.
        await owner.PostAsJsonAsync($"/api/leagues/{league.Id}/invites/email",
            new InviteByEmailRequest { Emails = new() { "pal@example.com" } });

        // Re-inviting the same address returns the existing invite, not a duplicate.
        var second = (await (await owner.PostAsJsonAsync($"/api/leagues/{league.Id}/invites/email",
            new InviteByEmailRequest { Emails = new() { "pal@example.com" } }))
            .Content.ReadFromJsonAsync<List<EmailInviteResultDto>>())!;
        second.Single().Status.Should().Be("already_invited");

        var list = (await owner.GetFromJsonAsync<List<LeagueInviteDto>>($"/api/leagues/{league.Id}/invites"))!;
        list.Count(i => i.Email == "pal@example.com").Should().Be(1);

        // The owner's own email is already a member.
        var ownerSelf = (await (await owner.PostAsJsonAsync($"/api/leagues/{league.Id}/invites/email",
            new InviteByEmailRequest { Emails = new() { "owner@example.com" } }))
            .Content.ReadFromJsonAsync<List<EmailInviteResultDto>>())!;
        ownerSelf.Single().Status.Should().Be("already_member");
    }

    [Fact]
    public async Task PersonalInvite_CanBeJoinedByCode_ThenShowsAccepted_AndIsSingleUse()
    {
        await _fixture.ResetMutableAsync();
        var owner = ClientFor(Guid.NewGuid(), "owner@example.com");
        var league = await CreateLeagueAsync(owner, "Join Crew");

        var invited = (await (await owner.PostAsJsonAsync($"/api/leagues/{league.Id}/invites/email",
            new InviteByEmailRequest { Emails = new() { "friend@example.com" } }))
            .Content.ReadFromJsonAsync<List<EmailInviteResultDto>>())!;
        var code = invited.Single().Invite!.Code;

        // The friend previews then joins by code, snapshotting the same email it was sent to.
        var friend = ClientFor(Guid.NewGuid(), "friend@example.com");
        var preview = await friend.GetFromJsonAsync<LeagueInvitePreviewDto>($"/api/leagues/join/{code}");
        preview!.LeagueName.Should().Be("Join Crew");
        preview.IsValid.Should().BeTrue();

        var joinRes = await friend.PostAsJsonAsync($"/api/leagues/join/{code}",
            new JoinLeagueRequest { DisplayName = "Friend", Email = "friend@example.com" });
        joinRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Owner now sees the personal invite as accepted.
        var list = (await owner.GetFromJsonAsync<List<LeagueInviteDto>>($"/api/leagues/{league.Id}/invites"))!;
        list.Single(i => i.Email == "friend@example.com").Accepted.Should().BeTrue();

        // Single-use: a different person can't reuse the spent code.
        var stranger = ClientFor(Guid.NewGuid(), "stranger@example.com");
        var reuse = await stranger.PostAsJsonAsync($"/api/leagues/join/{code}", new JoinLeagueRequest { DisplayName = "Nope" });
        reuse.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task OpenLinkInvite_IsReusable_AndDistinctFromPersonalInvites()
    {
        await _fixture.ResetMutableAsync();
        var owner = ClientFor(Guid.NewGuid(), "owner@example.com");
        var league = await CreateLeagueAsync(owner, "Link Crew");

        // An open link invite has no email and no use cap.
        var link = (await (await owner.PostAsJsonAsync($"/api/leagues/{league.Id}/invites", new CreateInviteRequest()))
            .Content.ReadFromJsonAsync<LeagueInviteDto>())!;
        link.Email.Should().BeNull();
        link.MaxUses.Should().BeNull();

        // Two different people can both join via the same link.
        foreach (var who in new[] { "a@example.com", "b@example.com" })
        {
            var c = ClientFor(Guid.NewGuid(), who);
            var r = await c.PostAsJsonAsync($"/api/leagues/join/{link.Code}", new JoinLeagueRequest { DisplayName = who });
            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var detail = await owner.GetFromJsonAsync<LeagueDetailDto>($"/api/leagues/{league.Id}");
        detail!.Members.Should().HaveCount(3); // owner + a + b
    }

    [Fact]
    public async Task PublicPreview_WorksWithoutAuth_AndShowsHeadcountAndOrganizer()
    {
        await _fixture.ResetMutableAsync();
        var owner = ClientFor(Guid.NewGuid(), "owner@example.com");
        var league = await CreateLeagueAsync(owner, "Public Crew");

        var link = (await (await owner.PostAsJsonAsync($"/api/leagues/{league.Id}/invites", new CreateInviteRequest()))
            .Content.ReadFromJsonAsync<LeagueInviteDto>())!;

        // A friend joins so headcount > 1.
        var friend = ClientFor(Guid.NewGuid(), "friend@example.com");
        await friend.PostAsJsonAsync($"/api/leagues/join/{link.Code}", new JoinLeagueRequest { DisplayName = "Friend" });

        // No Authorization header: a signed-out visitor still gets the enticing preview.
        var anon = _fixture.Factory.CreateClient();
        var res = await anon.GetAsync($"/api/leagues/join/{link.Code}/public");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = (await res.Content.ReadFromJsonAsync<LeagueInvitePublicPreviewDto>())!;
        preview.LeagueName.Should().Be("Public Crew");
        preview.MemberCount.Should().Be(2); // owner + friend
        preview.OrganizerDisplayName.Should().Be("Owner");
        preview.IsValid.Should().BeTrue();
        preview.IsFull.Should().BeFalse();
    }

    [Fact]
    public async Task PublicPreview_RequiresAuth_OnTheIdentityAwareEndpoint()
    {
        await _fixture.ResetMutableAsync();
        var owner = ClientFor(Guid.NewGuid(), "owner@example.com");
        var league = await CreateLeagueAsync(owner, "Gated Crew");
        var link = (await (await owner.PostAsJsonAsync($"/api/leagues/{league.Id}/invites", new CreateInviteRequest()))
            .Content.ReadFromJsonAsync<LeagueInviteDto>())!;

        // The identity-aware preview still demands a signed-in caller; only the /public variant is open.
        var anon = _fixture.Factory.CreateClient();
        (await anon.GetAsync($"/api/leagues/join/{link.Code}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InviteByEmail_RequiresOwner()
    {
        await _fixture.ResetMutableAsync();
        var owner = ClientFor(Guid.NewGuid(), "owner@example.com");
        var league = await CreateLeagueAsync(owner, "Owner Only Crew");

        // A non-member can't even see the league (404), let alone invite.
        var outsider = ClientFor(Guid.NewGuid(), "outsider@example.com");
        var res = await outsider.PostAsJsonAsync($"/api/leagues/{league.Id}/invites/email",
            new InviteByEmailRequest { Emails = new() { "x@example.com" } });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
