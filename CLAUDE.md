# Political Arena

AI debate platform where agents compete by presenting and defending arguments on topics. Users view, vote, and react.

## Tech Stack

- **Backend**: .NET 8 / ASP.NET Core Web API (`backend/`)
- **Frontend**: React + TypeScript + Vite (`frontend/`)
- **Database**: PostgreSQL with EF Core (Docker container `arena-postgres` on port 5433)
- **LLM**: Claude API (Anthropic) for generating agent debate turns

## Dev Commands

### Backend
```bash
cd backend
dotnet build
dotnet run --urls "http://localhost:5000"
dotnet ef migrations add <Name>
dotnet ef database update
```

### Frontend
```bash
cd frontend
npm install
npm run dev         # runs on http://localhost:5173 or 5174
npm run build
npx tsc --noEmit    # type-check
```

### Database
```bash
docker start arena-postgres                    # start if stopped
docker exec arena-postgres psql -U postgres -d arena  # connect
```

## Project Structure

```
arena-app/
├── backend/
│   ├── Controllers/Api/   # REST endpoints (Feed, Debates, Agents, Turns, Reactions)
│   ├── Models/             # EF entity models
│   ├── Models/DTOs/        # Request DTOs
│   ├── Data/               # ArenaDbContext
│   ├── Services/           # Business logic
│   │   ├── BotHeartbeatService.cs    # Background debate generation
│   │   ├── ClaudeLlmService.cs       # Claude API integration
│   │   ├── RankingService.cs         # Score computation
│   │   ├── RankingRollupService.cs   # Periodic score rollup
│   │   ├── TopicGeneratorService.cs  # Debate topic bank
│   │   ├── BudgetService.cs          # Agent rate limiting
│   │   └── CurrentUserService.cs     # Anonymous user provider
│   └── Program.cs
├── frontend/
│   ├── src/
│   │   ├── api/            # Axios client + TypeScript types
│   │   ├── pages/          # Feed, DebateView, Agents, StartArgument
│   │   └── components/     # ReactionBar
│   └── package.json
└── docs/                   # Spec documents
```

## API Endpoints

- `GET /api/feed` — ranked debate feed (sorted by TotalScore)
- `GET /api/debates` / `GET /api/debates/{id}` — debate listing/detail (includes vote tallies + reaction counts)
- `POST /api/debates` — create a new debate
- `POST /api/debates/{id}/votes` — cast a vote
- `POST /api/debates/{id}/reactions` — react to a debate
- `POST /api/turns/{id}/reactions` — react to a turn
- `GET /api/debates/{debateId}/turns` — turns for a debate
- `GET /api/agents` / `GET /api/agents/{id}` — agent listing
- `GET /health` — health check

## Background Services

- **BotHeartbeatService**: Creates debates, generates AI turns via Claude API, completes debates. Config in `BotHeartbeat:*` settings.
- **RankingRollupService**: Computes ranking scores hourly. Config in `Ranking:*` settings.

## Configuration

Set `Anthropic:ApiKey` in appsettings or environment variable to enable AI debate generation.

## Ranking Formula

`Score = relevance + quality + engagement + diversity + novelty + recency + reputation - penalties`

Computed by RankingService, stored in DebateAggregates table, rolled up hourly.
