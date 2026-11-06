# Event Management

A small ASP.NET Core .NET 9 API and React + TypeScript interface. All data lives in memory.

## Run locally

Prerequisites: .NET SDK 9.0.318 (or a newer 9.0 patch), Node.js 22.11+, npm.

### Visual Studio (start everything with F5)

1. Open `EventManagement.slnx` in Visual Studio.
2. On the first run, open View > Terminal and run `cd frontend` then `npm ci`.
3. Set `EventManagement.Api` as the startup project and select the `http` launch profile.
4. Press F5. The API starts React automatically and opens your browser. The initial page may briefly show a startup message before redirecting to http://127.0.0.1:5173.

Stop the current debugging session before starting again so the API executable is not locked. Do not run a second API instance from a terminal at the same time.

### Command line

From the Testing directory, install frontend dependencies once:

```powershell
cd frontend
npm ci
cd ..
```

Start the API (it starts the React development server automatically):

```powershell
dotnet run --project EventManagement.Api
```

Open http://localhost:5050 to be redirected to the frontend. The frontend proxies `/api` to http://localhost:5050. There are no global npm tools, database, secrets, or authentication to configure. ASP.NET Core's development-only SpaProxy integration starts React using `npm run dev` and reuses it if it is already running. You can still run that command separately from `frontend` if needed.

## Verify

```powershell
dotnet test EventManagement.slnx
cd frontend
npm run build
```

The React build runs TypeScript checking before generating `frontend/dist`. `npm run preview` can serve that build locally with the same API proxy. Deployment hosting is outside this exercise.

## Try it

1. Create an event with a future local date and capacity of one.
2. Select it and click Register me.
3. Generate a New ID to simulate another attendee. The event is full.
4. Remove the existing registration and register the new attendee.
5. Edit the event or delete it (deletion removes its registrations).

The attendee GUID is saved in browser local storage; use the identity field to restore another GUID. This is a demonstration identity, not verified authentication. Anyone can manage events or remove attendees. Clearing browser storage changes your identity. API restarts clear all events and registrations.

## API

| Method | Path | Result |
| --- | --- | --- |
| GET | /api/events | Events ordered by date |
| GET | /api/events/{eventId} | Event details |
| POST | /api/events | Create event (201 + Location) |
| PUT | /api/events/{eventId} | Replace event details |
| DELETE | /api/events/{eventId} | Delete event and registrations (204) |
| GET | /api/events/{eventId}/registrations | Array of attendee GUIDs |
| POST | /api/events/{eventId}/registrations | Register attendee (201) |
| DELETE | /api/events/{eventId}/registrations/{attendeeId} | Unregister attendee (204) |

Create/update body:

```json
{
  "title": "Team gathering",
  "description": "Meet the team",
  "date": "2027-06-15T18:00:00Z",
  "maxCapacity": 20
}
```

Registration body:

```json
{ "attendeeId": "3db1e7ec-cf23-4b26-91de-176f56a02f25" }
```

Responses include event ID and registration count. Validation failures return 400, missing resources return 404, and business conflicts return 409, with Problem Details JSON. Invalid GUID routes return 404. Missing or empty attendee IDs return 400.

## Design decisions

- Controllers handle HTTP; one singleton EventService owns business logic and storage. No repository or extra abstraction layers.
- Immutable event records and copied attendee arrays prevent callers from changing stored data.
- A single lock makes checks and updates atomic across all operations. Dictionaries and attendee hash sets provide fast ID lookups. This suits a small single-process exercise, not distributed hosting.
- The injected TimeProvider makes time-based tests deterministic. Registration closes exactly at the event start time.
- Dates are stored as UTC. The UI converts local input to UTC and displays dates in the browser timezone. API clients should send ISO 8601 dates with an explicit offset or Z.
- Past events may be created/edited for record keeping; new registration is blocked once they start. Unregistration remains available.
- Capacity must be positive and cannot fall below current registrations. Titles and descriptions are required, trimmed, and length limited.
- The UI uses native forms and a small API helper without routing or state libraries. Refresh reloads shared data; no real-time synchronization is attempted. The API remains authoritative for conflicts.

## Files

- `EventManagement.Api/Controller/EventsController.cs`: HTTP endpoints.
- `EventManagement.Api/EventService.cs`: business rules and in-memory state.
- `EventManagement.Api/Model/Models.cs`: request/response types and validation.
- `EventManagement.Tests/EventServiceTests.cs`: unit tests including concurrent registrations.
- `frontend/src/main.tsx`: event management and attendee interface.
- `frontend/src/api.ts`: HTTP calls and readable API errors.

