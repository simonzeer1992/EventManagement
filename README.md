# Event Management

A small event management application using ASP.NET Core .NET 9 and React with Vite and TypeScript. Events and attendee registrations are stored in memory. Authentication and authorization are intentionally out of scope.

## Features and business rules

- Create, list, view, update, and delete events with title, description, date, and maximum capacity.
- Register attendees using first name, last name, and email. View registered attendees and remove registrations.
- Reject registrations at or after the event start time.
- Reject registrations when capacity is reached, including simultaneous requests for the last place.
- Reject the same email registering twice for the same event ID. Email is trimmed and compared without case sensitivity; changing a name does not bypass this check.
- Allow the same email to register for different events. Different people may share a name.
- Allow separate events with identical titles and start times; each event has a unique event ID.
- Prevent reducing an event's capacity below its registration count. Deleting an event also deletes its registrations.

## Requirements

- .NET SDK **9.0.318** or a newer **9.0 patch**. `global.json` pins this SDK feature band.
- Node.js **22.11+** and npm. Vite 6 is used for compatibility with the installed Node.js version.
- Visual Studio with support for .NET 9 and `.slnx` solutions if using the Visual Studio workflow. The command line also works.

No database, credentials, or global npm tools are needed. NuGet packages restore from NuGet.org using the repository's `NuGet.Config`.

## Run locally

### Visual Studio

1. Open `EventManagement.slnx`.
2. Install frontend dependencies once. From a terminal at the repository root:

   ```powershell
   cd frontend
   npm ci
   cd ..
   ```

3. Set **EventManagement.Api** as the startup project and select the **http** profile.
4. Press **F5**. The API starts the React development server and opens the browser. A startup page may appear briefly before redirecting to the frontend.

Expand **frontend > src** in Solution Explorer to open the React code alongside the C# projects. These entries are solution folders; React uses `package.json`, while C# projects use `.csproj` files.

### Command line

From the repository root:

```powershell
cd frontend
npm ci
cd ..
dotnet run --project EventManagement.Api
```

Open [http://localhost:5050](http://localhost:5050). It redirects to [http://127.0.0.1:5173](http://127.0.0.1:5173). The frontend sends `/api` requests through Vite to the API on port 5050.

The launch profile enables ASP.NET Core SpaProxy in development. It runs `npm run dev` in `frontend` and reuses that server if it is already running. If necessary, you can run that command separately from the frontend directory.

Stop a previous API instance before restarting. Running the API from both Visual Studio and a terminal can cause port conflicts or lock its executable during a build. If the browser shows an old interface, refresh it with Ctrl+F5 after both servers are ready.

## Try the application

1. Create an event with a future local date and capacity of two. Creating an event does not register an attendee.
2. Select the event. Enter an attendee's first name, last name, and email in **Add an attendee**, then click **Register attendee**.
3. The attendee appears above the form. A successful registration clears all three fields for the next person. A failed registration preserves the details so they can be corrected.
4. Try the same email with different capitalization or surrounding spaces. The API rejects the duplicate without consuming another place.
5. Register a different email to fill the event. Remove an attendee to free a place.
6. Move the event date into the past. New registrations are blocked with an explanation. Unregistration remains available.
7. Create a separate event with the same title and time. This is allowed; registrations belong to their specific event ID.

## Build and tests

From the repository root:

```powershell
dotnet test EventManagement.slnx --configuration Release
cd frontend
npm run build
```

Latest verification on November 6, 2026: **30 unit test cases passed** and the frontend production build passed. These are service-level tests, not automated browser or HTTP integration tests.

| Coverage | What is checked |
| --- | --- |
| Event operations | Create/update values, UTC normalization, delete cleanup, missing resources, invalid input, failed update preserving state |
| Event identity | Matching event details are allowed, IDs stay distinct, registrations remain separate |
| Registration identity | Required names and valid email, length limits, email normalization, duplicates despite changed names, same email across events, shared names with different emails |
| Event timing | Before-start registration, rejection at/after start, clock advancing after creation, unregistration after start |
| Capacity | Full event rejection, duplicate not consuming a place, capacity reduction/increase, unregistration freeing capacity, repeated removal not making counts negative |
| Concurrency | Simultaneous registrations cannot overbook or register the same normalized email twice |

The test clock is controllable, so time tests do not sleep or depend on the actual date. Test helpers use descriptive names: `CreateEventRequest`, `CreateRegistrationRequest`, and `AssertStatus`.

The browser workflow was also checked manually: create event, register two attendees, confirm empty fields after success, and confirm a duplicate-email error preserves fields. TypeScript checking runs before the Vite production build. `npm run preview` serves the production frontend locally and uses the same API proxy; the API must also run.

## API endpoints

| Method | Path | Success result |
| --- | --- | --- |
| GET | `/api/events` | 200, events ordered by date |
| GET | `/api/events/{eventId}` | 200, event details |
| POST | `/api/events` | 201, event and Location header |
| PUT | `/api/events/{eventId}` | 200, updated event |
| DELETE | `/api/events/{eventId}` | 204, event and its registrations removed |
| GET | `/api/events/{eventId}/registrations` | 200, names, emails, and internal registration IDs |
| POST | `/api/events/{eventId}/registrations` | 201, created registration |
| DELETE | `/api/events/{eventId}/registrations/{attendeeId}` | 204, registration removed |

Create/update an event:

```json
{
  "title": "Team gathering",
  "description": "Meet the team",
  "date": "2027-06-15T18:00:00Z",
  "maxCapacity": 20
}
```

Register an attendee:

```json
{
  "firstName": "Alex",
  "lastName": "Smith",
  "email": "alex@example.com"
}
```

Registration responses include `id`, `firstName`, `lastName`, and normalized `email`. The API generates the ID; use it as `{attendeeId}` in the DELETE URL. It identifies that event's registration, not a global user account. Event responses include `id`, event fields, and `registrationCount`. IDs are not displayed in the UI.

## Validation and exception handling

- Event title: required, up to 200 characters. Description: required, up to 4000 characters. Stored text is trimmed.
- Date: required and non-default; use ISO 8601 with `Z` or an explicit UTC offset. The browser converts local input to UTC; stored dates are UTC.
- Capacity: positive integer. An update cannot lower it below current registrations.
- Attendee first and last names: required, up to 100 characters each. Email: required, valid format, up to 254 characters; stored trimmed and lowercase.
- Browser form validation provides immediate feedback. API model validation and service validation enforce checks independently of the UI.

| Status | Meaning and examples |
| --- | --- |
| 400 | Invalid fields, malformed request JSON, missing date, invalid email, nonpositive capacity |
| 404 | Event or registration not found; invalid GUID route values do not match a route |
| 409 | Event started, event full, email already registered, capacity reduction below registration count |
| 500 | Unexpected server exception handled by ASP.NET Core's exception handler |

Expected service failures throw `EventException` with a status and readable message. Middleware in `Program.cs` converts them to Problem Details JSON. `[ApiController]` supplies automatic 400 validation responses. `AddProblemDetails` and `UseExceptionHandler` handle unexpected exceptions. No custom retry mechanism is implemented.

The frontend `request` helper reads Problem Details, including validation errors, and throws a JavaScript Error. `performAction` shows the error and always clears the busy flag in `finally`. Registration fields clear only when the registration and refresh succeed. If a follow-up refresh fails after a successful write, the write may already have completed; refresh before retrying.

## Why these technologies

- **.NET 9 / ASP.NET Core:** explicitly requested for this exercise and installed locally. Controllers, model validation, dependency injection, and exception middleware provide the needed API behavior with little additional code. A `TimeProvider` makes date-based tests deterministic. This choice does not depend on a feature exclusive to .NET 9.
- **React / Vite / TypeScript:** React was selected with approval to keep this small SPA to a few function components. Local state is sufficient; no router or state management library is needed. Vite provides development serving, an API proxy, and production builds. TypeScript checks the shapes used by the frontend, though it does not validate JSON at runtime.
- **In-memory dictionaries:** directly meet the exercise requirement. A singleton service keeps shared data for one running API process. A lock makes rule checks and writes atomic. ID lookups are direct; duplicate-email checks scan the selected event's registrations. No database or repository layer is needed for this scope.

## Code map

| File or type | Responsibility |
| --- | --- |
| `EventManagement.Api/Program.cs` | Registers dependencies, configures expected/unexpected exception handling, maps controllers, starts API |
| `Controller/EventsController.cs` | Maps HTTP endpoints to service methods and returns HTTP results |
| `EventService.cs` | Owns in-memory state, validation, business rules, event and registration operations |
| `Model/Models.cs` | `EventRequest`, `EventResponse`, `RegistrationRequest`, `RegistrationResponse`, and `EventException` |
| `EventManagement.Tests/EventServiceTests.cs` | Business logic tests, request factories, status assertions, controllable `FixedClock` |
| `frontend/src/main.tsx` | Mounts the React application and loads styles |
| `frontend/src/App.tsx` | Coordinates event selection, loading, shared refresh logic, actions, and messages |
| `frontend/src/EventForm.tsx` | Creates/edits events and converts between local date input and UTC |
| `frontend/src/RegistrationForm.tsx` | Collects attendee details and clears fields only after success |
| `frontend/src/api.ts` | Shared TypeScript data shapes and `request` HTTP/error helper |
| `frontend/src/styles.css` | Responsive layout, attendee list, form, messages, and button styles |
| `frontend/vite.config.ts` | Frontend port and `/api` proxy for development and preview |
| API `.csproj` and `Properties/launchSettings.json` | .NET target, SpaProxy dependency, frontend startup and browser launch profile |
| `global.json`, `NuGet.Config`, `package.json`, `package-lock.json` | SDK selection, package source, frontend scripts/dependencies, locked npm versions |

For the complete walkthrough, see [the Word technical guide](docs/EventManagement-Technical-Guide.docx).

## Final changes and scope

- Registration now collects first name, last name, and email. Email replaced the browser-generated GUID as the duplicate-registration key.
- Visible GUID fields and identity-switching controls were removed; the backend generates internal registration IDs.
- Duplicate-event prevention was removed to match the original story. Events may have matching names and times.
- The attendee list is separate from the form, name fields have equal widths, and successful registration clears the form.
- Closed/full-event explanations remain visible and registration controls are disabled.
- Controller and model files are in their requested folders. React source appears in the same Visual Studio solution. F5 starts both applications.
- Tests now cover 30 cases and include state preservation, advancing time, capacity changes, and repeat unregistration.
- The final readability review separated the React forms from the page, consolidated refresh logic and proxy settings, removed repeated style declarations, and formatted the source/configuration files. Comments explain the concurrency lock, email identity, date conversion, obsolete requests, form reset, and empty HTTP responses.

Documentation is provided through descriptive names, focused comments for non-obvious decisions, the API/code map in this README, and unit tests that describe expected behavior. Straightforward controller methods are not repeated in comments. The implementation keeps the existing small service/controller structure and introduces no repository or state management layers.

Data is lost when the API restarts, and multiple API instances would have independent data. There are no accounts or ownership permissions. Anyone using the application can manage events and registrations. Email identifies duplicate registrations but is not verified, so it cannot prove a person's identity. Production deployment and distributed storage are outside this exercise. No claim of load testing, security certification, or complete test coverage is made.

## Submission

Commit and push the final backend, frontend, tests, README, and technical guide. The assignment requires the repository to be **public** when its link is submitted. Making the repository public is a separate GitHub action; local files alone do not complete submission.
