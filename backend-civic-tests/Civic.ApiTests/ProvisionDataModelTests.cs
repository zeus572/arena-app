using System.Security.Cryptography;
using System.Text;
using Civic.API.Data;
using Civic.API.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Phase 0.1 gate (07 plan, Layer 0): the provision + structured-engagement
/// schema round-trips; CRUD works across all six entities; and a sub-question
/// can be added to an EXISTING provision without a new migration (principle A4 —
/// the load-bearing "late sub-question" path).
///
/// Runs against the shared civic_test Postgres DB (the new coalition tables are
/// reset by the Respawner between tests, like other mutable tables).
/// </summary>
[Collection("Database")]
public class ProvisionDataModelTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;

    public ProvisionDataModelTests(DatabaseFixture fx) => _fx = fx;

    public async Task InitializeAsync() => await _fx.ResetMutableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private CivicDbContext Db(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<CivicDbContext>();

    private static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ---------------------------------------------------------------
    // 1. Schema round-trips: a provision can hold positions, amendments,
    //    versions (incl. the extracted jsonb vector) and acceptance records.
    // ---------------------------------------------------------------
    [Fact]
    public async Task FullProvisionGraph_RoundTrips()
    {
        var provisionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = Db(scope);
            var provision = new Provision
            {
                Id = provisionId,
                Slug = $"data-center-fee-{provisionId:N}",
                Title = "Grid-cost fee on large data centers",
                NeutralText = "Large data centers should pay a fee reflecting the grid costs they impose.",
                SourceBriefingSlug = "agency-ai-hiring-20260101",
                State = ProvisionState.Open,
                RelevantAxes = new[] { "market-vs-precaution", "local-vs-national" },
                Deadline = DateTime.UtcNow.AddDays(7),
                GenerationSource = CivicGenerationSource.Seed,
                SubQuestions =
                {
                    new SubQuestion
                    {
                        Key = "facility-scope", Prompt = "Which facilities are covered?",
                        TradeoffDescription = "All facilities vs. only large ones.",
                        PositionOptions = new[] { "all", "large-only" },
                        Origin = SubQuestionOrigin.Birth, OrderIndex = 0,
                    },
                    new SubQuestion
                    {
                        Key = "cost-basis", Prompt = "Marginal or average cost?",
                        PositionOptions = new[] { "marginal", "average" },
                        Origin = SubQuestionOrigin.Birth, OrderIndex = 1,
                    },
                },
            };

            var baseVersion = new ProvisionVersion
            {
                Id = versionId,
                ProvisionId = provisionId,
                Label = "base",
                Text = provision.NeutralText,
                TextHash = Hash(provision.NeutralText),
                ExtractedPositions = new() { ["facility-scope"] = "all", ["cost-basis"] = "marginal" },
                IsExtracted = true,
                ExtractionModel = "stub",
                ExtractedAt = DateTime.UtcNow,
            };
            provision.Versions.Add(baseVersion);

            provision.Positions.Add(new ProvisionPosition
            {
                UserId = "user-alice", Stance = "For, but only large facilities",
                Intensity = AnswerIntensity.High, ReasoningTag = "governance",
            });

            provision.Amendments.Add(new Amendment
            {
                UserId = "user-bob",
                FreeFormText = "Grandfather facilities built before 2025.",
                ProposedVersionId = versionId,
            });

            provision.AcceptanceRecords.Add(new AcceptanceRecord
            {
                ProvisionId = provisionId, VersionId = versionId,
                UserId = "user-alice", Accept = true, Intensity = AnswerIntensity.Medium,
            });

            db.Provisions.Add(provision);
            await db.SaveChangesAsync();
        }

        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = Db(scope);
            var loaded = await db.Provisions
                .Include(p => p.SubQuestions)
                .Include(p => p.Positions)
                .Include(p => p.Amendments)
                .Include(p => p.Versions)
                .Include(p => p.AcceptanceRecords)
                .SingleAsync(p => p.Id == provisionId);

            loaded.Title.Should().Be("Grid-cost fee on large data centers");
            loaded.State.Should().Be(ProvisionState.Open);
            loaded.RelevantAxes.Should().Equal("market-vs-precaution", "local-vs-national");
            loaded.Deadline.Should().NotBeNull();

            loaded.SubQuestions.Should().HaveCount(2);
            loaded.SubQuestions.Single(s => s.Key == "facility-scope")
                .PositionOptions.Should().Equal("all", "large-only");

            loaded.Positions.Should().ContainSingle()
                .Which.Intensity.Should().Be(AnswerIntensity.High);

            loaded.Amendments.Should().ContainSingle()
                .Which.ProposedVersionId.Should().Be(versionId);

            var version = loaded.Versions.Should().ContainSingle().Subject;
            version.TextHash.Should().Be(Hash(loaded.NeutralText));
            // The extracted sub-question-position vector survives the jsonb round-trip.
            version.ExtractedPositions.Should().HaveCount(2);
            version.ExtractedPositions["facility-scope"].Should().Be("all");
            version.ExtractedPositions["cost-basis"].Should().Be("marginal");

            loaded.AcceptanceRecords.Should().ContainSingle()
                .Which.Accept.Should().BeTrue();
        }
    }

    // ---------------------------------------------------------------
    // 2. CRUD: update a field; delete the aggregate and confirm children
    //    cascade away.
    // ---------------------------------------------------------------
    [Fact]
    public async Task Provision_Update_And_CascadingDelete()
    {
        var id = Guid.NewGuid();
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = Db(scope);
            var p = new Provision
            {
                Id = id, Slug = $"phones-{id:N}", Title = "Phone limits in schools",
                NeutralText = "Schools should limit phone use during the day.",
                State = ProvisionState.Birth,
            };
            p.SubQuestions.Add(new SubQuestion { Key = "scope", Prompt = "Bell-to-bell or class-only?" });
            var v = new ProvisionVersion
            {
                ProvisionId = id, Text = p.NeutralText, TextHash = Hash(p.NeutralText),
            };
            p.Versions.Add(v);
            db.Provisions.Add(p);
            await db.SaveChangesAsync();
        }

        // Update
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = Db(scope);
            var p = await db.Provisions.SingleAsync(x => x.Id == id);
            p.State = ProvisionState.Contested;
            p.Deadline = DateTime.UtcNow.AddDays(5);
            await db.SaveChangesAsync();
        }
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = Db(scope);
            (await db.Provisions.SingleAsync(x => x.Id == id)).State
                .Should().Be(ProvisionState.Contested);
        }

        // Delete cascades to children.
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = Db(scope);
            var p = await db.Provisions.SingleAsync(x => x.Id == id);
            db.Provisions.Remove(p);
            await db.SaveChangesAsync();
        }
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = Db(scope);
            (await db.Provisions.AnyAsync(x => x.Id == id)).Should().BeFalse();
            (await db.SubQuestions.AnyAsync(s => s.ProvisionId == id)).Should().BeFalse();
            (await db.ProvisionVersions.AnyAsync(v => v.ProvisionId == id)).Should().BeFalse();
        }
    }

    // ---------------------------------------------------------------
    // 3. THE CRITICAL GATE (A4): a sub-question can be added to an EXISTING
    //    provision without a new migration. We prove "no migration" by
    //    asserting the __EFMigrationsHistory row count is unchanged before and
    //    after, and prove the path works end-to-end: a later version resolves
    //    the emergent sub-question via the jsonb vector (a new key, no new
    //    column).
    // ---------------------------------------------------------------
    [Fact]
    public async Task LateSubQuestion_AddedWithoutMigration_AndResolvedByNewVersion()
    {
        var id = Guid.NewGuid();

        // Birth: provision with two sub-questions only.
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = Db(scope);
            var p = new Provision
            {
                Id = id, Slug = $"late-sq-{id:N}", Title = "Data-center grid-cost fee",
                NeutralText = "Large data centers pay a grid-cost fee.",
                State = ProvisionState.Contested,
            };
            p.SubQuestions.Add(new SubQuestion { Key = "facility-scope", Prompt = "Which facilities?", OrderIndex = 0 });
            p.SubQuestions.Add(new SubQuestion { Key = "cost-basis", Prompt = "Marginal or average?", OrderIndex = 1 });
            db.Provisions.Add(p);
            await db.SaveChangesAsync();
        }

        var migrationsBefore = await CountMigrationsAsync();

        // Bargaining surfaces a hidden crux NOBODY anticipated at birth: an
        // amendment introduces "grandfathering". This is a plain INSERT of a new
        // SubQuestion row + a new Version whose extracted vector carries the new
        // key. No schema change.
        var emergentVersionId = Guid.NewGuid();
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = Db(scope);

            var emergentVersion = new ProvisionVersion
            {
                Id = emergentVersionId,
                ProvisionId = id,
                Label = "grandfather carve-out",
                Text = "Large data centers pay a grid-cost fee; facilities built before 2025 are exempt.",
                TextHash = Hash("late-version-grandfather"),
                ExtractedPositions = new()
                {
                    ["facility-scope"] = "large-only",
                    ["cost-basis"] = "marginal",
                    ["grandfathering"] = "exempt-pre-2025", // <- key for a sub-question that didn't exist at birth
                },
                IsExtracted = true,
            };
            db.ProvisionVersions.Add(emergentVersion);

            db.SubQuestions.Add(new SubQuestion
            {
                ProvisionId = id,
                Key = "grandfathering",
                Prompt = "Are existing facilities grandfathered?",
                TradeoffDescription = "Exempt pre-existing builds vs. apply to all.",
                PositionOptions = new[] { "exempt-pre-2025", "apply-to-all" },
                Origin = SubQuestionOrigin.Emergent,
                IntroducedByVersionId = emergentVersionId,
                OrderIndex = 2,
            });

            await db.SaveChangesAsync();
        }

        // Verify: no migration was applied, and the late sub-question + its
        // resolution persisted.
        var migrationsAfter = await CountMigrationsAsync();
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = Db(scope);

            migrationsAfter.Should().Be(migrationsBefore,
                "adding a sub-question to an existing provision must NOT require a new migration (principle A4)");

            var subQs = await db.SubQuestions.Where(s => s.ProvisionId == id).OrderBy(s => s.OrderIndex).ToListAsync();
            subQs.Should().HaveCount(3);
            var emergent = subQs.Single(s => s.Key == "grandfathering");
            emergent.Origin.Should().Be(SubQuestionOrigin.Emergent);
            emergent.IntroducedByVersionId.Should().Be(emergentVersionId);

            var version = await db.ProvisionVersions.SingleAsync(v => v.Id == emergentVersionId);
            version.ExtractedPositions.Should().ContainKey("grandfathering");
            version.ExtractedPositions["grandfathering"].Should().Be("exempt-pre-2025");
        }
    }

    private static async Task<long> CountMigrationsAsync()
    {
        // Raw count of applied migrations — the auditable proof that the late
        // sub-question path required no schema change. Uses its own connection
        // so it doesn't disturb any DbContext's connection lifecycle.
        await using var conn = new Npgsql.NpgsqlConnection(CivicApiFactory.TestConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }
}
