# Changelog

Notable TaskFlow changes are recorded here. This file is a release draft; no v1.0.0 tag or deployment has been created.

## [1.0.0] - Unreleased

### Added

- JWT registration, login, current-user lookup, BCrypt password hashing, and protected API routes.
- User-owned project CRUD and project-scoped task CRUD.
- Task status, priority, due dates, optional assignees, and nullable-field clearing on updates.
- API-backed dashboard summaries, task breakdowns, recent tasks, and upcoming deadlines.
- Request validation and Problem Details responses for API errors.
- Ownership checks preventing access to another user's projects and their tasks.
- xUnit controller and HTTP integration coverage for authentication, ownership, validation, nullable updates, and health checks.
- Playwright workflows for authentication, projects, tasks, dashboard states, and responsive navigation.
- Docker packaging for the API and a GitHub Actions workflow configured for build, tests, lint, frontend build, and browser tests.
- Production configuration checks, health/readiness endpoints, and Render configuration for a static client plus a SQLite-backed API with persistent storage.

### Known limitations

- The application is not deployed; no live URL is available.
- The GitHub Actions workflow has not run remotely.
- SQLite deployment is single-instance and needs an independently tested backup process.
- Authentication has no refresh tokens or rate limiting; the client stores access tokens in `localStorage`.
- Assignees are entered by numeric user ID, and lists are not paginated.
- Portfolio screenshots have not been captured.

## Planned

- User directory and selection controls for assignees.
- Task filtering and pagination.
- Authentication rate limiting and a session refresh strategy.
- Automated, independently stored database backups.
- Managed database support if the application needs multiple API instances or higher write concurrency.
