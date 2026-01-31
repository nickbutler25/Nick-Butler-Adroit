# Nick-Butler-Adroit

A URL shortener proof-of-concept with a C# ASP.NET Core Web API backend and React TypeScript frontend. All data is stored in-memory, the application
could be extended to support persistent storage by implementing IUrlRepository with a database for instance.

## Features

- Create shortened URLs with auto-generated or custom user supplied short codes
- Per-code short code and aggregate long url click statistics
- Delete shortened URLs
- Real-time updates via SignalR (create, click, delete events)
- Paginated all created links page with search filtering and virtual scrolling
- Rate limiting on creation, resolution, and redirect endpoints to prevent abuse
- URL validation and normalization (HTTP/HTTPS only, case-insensitive, trailing slash removal)
- Supports concurrent requests from multiple users

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)

## Running the Application

### Backend

```bash
cd Nick-Butler-Adroit.Api
dotnet run
```

The API starts at `https://localhost:7055` by default.

### Frontend

```bash
cd nick-butler-adroit-frontend
npm install
npm start
```

The React app starts at `http://localhost:3000` and proxies API calls to the backend.

## Running Tests

```bash
# Backend (xUnit)
dotnet test

# Frontend (ViTest)
cd nick-butler-adroit-frontend
npm test

# E2E (Playwright)
cd nick-butler-adroit-frontend
npx playwright test
```

## Project Structure

```
Nick-Butler-Adroit.Api/
  Controllers/     Thin REST controllers + redirect endpoint
  Services/        Business logic (validation, code generation, click tracking)
  Repositories/    Thread-safe in-memory storage
  Models/          Request/response DTOs and storage entities
  Hubs/            SignalR hub for real-time events
  Exceptions/      Custom exception types

Nick-Butler-Adroit.Tests/
  Unit/            Service and repository unit tests
  Integration/     API endpoint tests via WebApplicationFactory

nick-butler-adroit-frontend/
  src/
    pages/         HomePage (shortener form) and AllLinksPage (virtual scroll list of every saved link)
    components/    Reusable UI components (form, URL list, stats display)
    services/      REST API client and SignalR connection management
    types/         Shared TypeScript interfaces
```
