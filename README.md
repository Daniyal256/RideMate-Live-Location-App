RideMate

RideMate is a real-time family and friends location sharing web application built with ASP.NET Core, Blazor Server, SignalR, Entity Framework Core, and SQL Server. The application allows users to create private circles, invite trusted members, share live location, view member presence, and manage profile details from an interactive map-first interface.

The project is structured as a layered .NET solution so that the user interface, business services, persistence, and domain model remain separated and easier to maintain.

## Table of Contents

- [Features](#features)
- [Technology Stack](#technology-stack)
- [Solution Architecture](#solution-architecture)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Configuration](#configuration)
- [Running the Application](#running-the-application)
- [Database Migrations](#database-migrations)
- [Testing](#testing)
- [Important Browser Notes](#important-browser-notes)
- [Future Improvements](#future-improvements)

## Features

- User registration and login with ASP.NET Core Identity.
- Email confirmation before account access.
- Profile photo upload during registration and from profile settings.
- Uploaded profile images stored under `wwwroot/uploads`.
- Automatic default `Family` circle creation for every new user.
- Circle management with invite codes, renaming, joining, and leaving.
- Automatic circle cleanup when the last member leaves a circle.
- Live map view powered by Leaflet and OpenStreetMap tiles.
- Real-time location sharing through SignalR.
- Member cards showing name, photo, location status, battery state when available, and last seen time.
- Offline users are greyed out in both the member list and map markers.
- Location permission denied state is shown separately from normal offline state.
- Map zoom controls that fit all circle members or focus a selected marker/member.
- Interactive sidebar, settings menu, profile editing, and location sharing controls.

## Technology Stack

- .NET 10
- ASP.NET Core Blazor Server
- ASP.NET Core Identity
- SignalR
- Entity Framework Core
- SQL Server
- MailKit SMTP email service
- Leaflet.js
- OpenStreetMap
- Tailwind CSS

## Solution Architecture

RideMate follows a layered architecture inspired by Clean Architecture principles. The main idea is that domain concepts remain independent, application logic sits above the domain, infrastructure implements external concerns, and the web project acts as the delivery layer.

### Domain Layer

Project: `src/RideMate.Domain`

The Domain layer contains the core business entities used by the system. It represents the concepts that RideMate is built around and does not depend on the web UI or infrastructure implementation.

Current domain entities include:

- `Circle`: a private group that users can join using an invite code.
- `CircleMember`: the membership relationship between a user and a circle.
- `LocationLog`: saved location, battery, permission, and timestamp records.
- `User`: a simple user model retained in the domain project.

### Application Layer

Project: `src/RideMate.Application`

The Application layer contains usecase oriented services, DTOs, and interfaces. It is responsible for application-specific operations that should not be tied directly to UI rendering or database implementation details.

Current responsibilities include:

- Authentication DTOs for login and registration workflows.
- Invite code generation through `InviteService`.
- Service contracts such as `IMapService`.

### Infrastructure Layer

Project: `src/RideMate.Infrastructure`

The Infrastructure layer contains the concrete implementations for persistence, real-time communication, and external services.

Current responsibilities include:

- `AppDbContext` for Entity Framework Core and ASP.NET Core Identity persistence.
- SQL Server migrations.
- `LocationHub` for SignalR-based live location updates.
- Online/offline presence tracking.
- Location unavailable and permission-denied reporting.
- Email delivery through `EmailService` using SMTP and MailKit.

### Web Layer

Project: `src/RideMate.Web`

The Web layer is the application entry point. It hosts the Blazor Server UI, authentication endpoints, static files, JavaScript interop, routing, and application startup configuration.

Current responsibilities include:

- Login, registration, logout, and email confirmation endpoints.
- Blazor components for the live map and circle management.
- Profile photo upload and profile editing UI.
- Leaflet map interop through `wwwroot/js/mapInterop.js`.
- Static assets, styles, and uploaded user images.
- Service registration and middleware configuration in `Program.cs`.

## Project Structure

```text
RideMate/
├── src/
│   ├── RideMate.Domain/          # Domain entities and core models
│   ├── RideMate.Application/     # DTOs, interfaces, and application services
│   ├── RideMate.Infrastructure/  # EF Core, SignalR hub, email service, migrations
│   └── RideMate.Web/             # Blazor Server UI and app entry point
├── tests/
│   └── RideMate.Tests/           # Test project
├── RideMate.slnx                 # Solution file
└── README.md
```

## Prerequisites

Install the following before running the project:

- .NET 10 SDK
- SQL Server or SQL Server Express
- Node.js only if you need to rebuild frontend CSS tooling
- A browser with location permission support
- SMTP credentials for email confirmation

## Configuration

The web project reads configuration from `src/RideMate.Web/appsettings.json`, optional local overrides in `src/RideMate.Web/appsettings.Local.json`, environment variables, and user secrets.

Required configuration values:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=RideMateDB;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "EmailSettings": {
    "SenderEmail": "your-email@example.com",
    "Password": "your-app-password",
    "SmtpServer": "smtp.example.com",
    "Port": "465"
  }
}
```

For local development, keep private values in `appsettings.Local.json` or use user secrets so they are not committed to GitHub:

```bash
cd src/RideMate.Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=RideMateDB;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet user-secrets set "EmailSettings:SenderEmail" "your-email@example.com"
dotnet user-secrets set "EmailSettings:Password" "your-app-password"
dotnet user-secrets set "EmailSettings:SmtpServer" "smtp.example.com"
dotnet user-secrets set "EmailSettings:Port" "465"
```

## Running the Application

From the repository root:

```bash
dotnet restore
dotnet run --project src/RideMate.Web/RideMate.Web.csproj
```

Then open the URL shown in the terminal. A local development URL commonly looks like:

```text
http://localhost:5000
```

You can also run it on a specific URL:

```bash
dotnet run --project src/RideMate.Web/RideMate.Web.csproj --urls http://127.0.0.1:5720
```

## Database Migrations

The application applies pending EF Core migrations automatically at startup:

```csharp
db.Database.Migrate();
```

If you prefer to update the database manually, use:

```bash
dotnet ef database update --project src/RideMate.Infrastructure --startup-project src/RideMate.Web
```

To create a new migration after changing entities or the DbContext:

```bash
dotnet ef migrations add MigrationName --project src/RideMate.Infrastructure --startup-project src/RideMate.Web
```

## Testing

Run the test project from the repository root:

```bash
dotnet test
```

Build the main web project:

```bash
dotnet build src/RideMate.Web/RideMate.Web.csproj
```

## Important Browser Notes

- Live location requires browser location permission.
- If a user denies location permission, RideMate can still show other circle members and will grey out that user's own location state.
- Battery percentage depends on the browser's Battery Status API. Some browsers, including Safari and many desktop browsers, do not expose battery level to websites. In that case, RideMate shows `Unavailable`.
- Real-time updates require the browser tab to remain open and connected.
