# TrackHive

TrackHive is a role-based HR operations portal built with ASP.NET Core MVC. It helps IT administrators, HR teams, and employees manage onboarding, attendance, leave workflows, payroll, and personal preferences in one place.

## Feature tour

### Authentication & onboarding
- Cookie-based authentication with role claims, organization scoping, and a global filter that forces first-time password changes when required.【F:Program.cs†L11-L59】【F:Models/MustChangePasswordFilter.cs†L1-L52】
- Organization registration flow that lets an IT admin create the first tenant and seed their own account in a single transaction.【F:Controllers/AuthController.cs†L121-L178】
- Account lockout after three failed attempts, self-service password reset e-mails, and support for unlocking accounts after a successful sign-in.【F:Controllers/AuthController.cs†L25-L120】

### Role-specific workspaces
- **IT dashboard** — view the organization name and invite additional HR administrators via e-mail with temporary passwords.【F:Controllers/DashboardController.cs†L28-L88】
- **People management** — shared IT/HR area for searching, sorting, paginating, editing, activating/deactivating, bulk-updating, or deleting HR and employee accounts inside the tenant.【F:Controllers/DashboardController.cs†L90-L251】
- **Global user admin** — IT-only console to filter across organizations, manage users directly, and perform bulk actions.【F:Controllers/UsersController.cs†L13-L340】

### Attendance & employee self-service
- Employee dashboard for daily check-in/out, recent attendance history, live leave balance, and quick leave application form populated with sensible defaults.【F:Controllers/EmployeeDashboardController.cs†L28-L168】
- Leave requests validate entitlement, track pending days, and notify HR users via e-mail when a request is submitted.【F:Controllers/EmployeeDashboardController.cs†L170-L269】

### Medical certificate workflow
- Employees can upload multiple medical certificates (images or PDFs) for sick leave; files are stored in `wwwroot/uploads/leave-documents` and replace prior submissions while enforcing file-size and type limits.【F:Controllers/EmployeeDashboardController.cs†L271-L410】
- HR reviewers approve or reject certificates, with automatic status transitions and notification e-mails back to the employee.【F:Controllers/HrDashboardController.cs†L120-L248】
- Authenticated users may download stored documents when they own the request or belong to the same organization with HR/IT privileges.【F:Controllers/LeaveDocumentsController.cs†L1-L71】

### HR analytics & notifications
- HR dashboard aggregates pending approvals, certificate queues, leave balances, attendance summaries, and six months of leave trends with type breakdowns and dashboard notifications.【F:Controllers/HrDashboardController.cs†L16-L517】
- Quick employee invitations that set initial salary data and seed default leave balances for new hires.【F:Controllers/HrDashboardController.cs†L34-L91】

### Payroll & reporting
- Payroll calculator uses attendance records to compute working days, worked hours, automatic overtime, manual adjustments, deductions, and pay totals with configurable overtime multipliers.【F:Controllers/PayrollController.cs†L16-L266】【F:Controllers/PayrollController.cs†L334-L478】
- Saves monthly payroll records, displays history, and exports QuestPDF-based payslips and organization-wide payroll reports.【F:Controllers/PayrollController.cs†L268-L478】【F:Services/PayrollPdfGenerator.cs†L1-L200】

### Employee profile & preferences
- Self-service profile editor with optional image upload, server-side cropping/resizing (ImageSharp), and support for clearing existing avatars.【F:Controllers/ProfileController.cs†L1-L212】【F:Controllers/ProfileController.cs†L214-L310】
- Preferences screen lets users pick light/dark themes, supported languages, and reorder navigation links; updates are persisted and re-issued as authentication claims.【F:Controllers/ProfileController.cs†L40-L143】【F:Services/NavigationMenu.cs†L1-L53】【F:Models/LanguagePreferences.cs†L1-L31】

### Notifications & messaging
- Centralized SMTP service for invitations, leave updates, and certificate workflows with graceful error handling on delivery failure.【F:Models/EmailService.cs†L1-L37】【F:Controllers/DashboardController.cs†L49-L113】【F:Controllers/HrDashboardController.cs†L196-L247】【F:Controllers/EmployeeDashboardController.cs†L211-L335】

## Architecture overview
- **ASP.NET Core MVC** (`net9.0`) with controllers per functional area and global MVC filters for security cross-cutting concerns.【F:TrackHive.csproj†L1-L20】【F:Program.cs†L11-L59】
- **Entity Framework Core** with a single `AppDbContext` coordinating organizations, users, attendance, leave, payroll, and uploaded documents plus declarative constraints (indexes, cascading rules, and column shapes).【F:Models/AppDbContext.cs†L1-L89】【F:Models/AppUser.cs†L1-L61】【F:Models/LeaveRequest.cs†L1-L63】【F:Models/LeaveDocument.cs†L1-L21】【F:Models/PayrollRecord.cs†L1-L33】
- **Services** encapsulate UI navigation metadata and QuestPDF document generation for payslips and monthly reports.【F:Services/NavigationMenu.cs†L1-L55】【F:Services/PayrollPdfGenerator.cs†L1-L200】
- Startup automatically applies pending EF Core migrations, configures cookie authentication, SMTP options, and registers the payroll PDF generator as a singleton service.【F:Program.cs†L11-L59】

## Data model highlights
- `AppUser` tracks role, organization, salary, profile details, preferences, and account state (lockouts, password resets).【F:Models/AppUser.cs†L1-L61】
- `AttendanceRecord` enforces one record per user per day for check-in/out times.【F:Models/AppDbContext.cs†L33-L47】【F:Models/AttendanceRecord.cs†L1-L11】
- `LeaveRequest`/`LeaveBalance` record entitlement usage, review status, reviewers, and attached documents.【F:Models/LeaveRequest.cs†L1-L63】
- `LeaveDocument` stores metadata for uploaded certificates; physical files live under `wwwroot` and cascade delete with their parent request.【F:Models/AppDbContext.cs†L49-L70】【F:Models/LeaveDocument.cs†L1-L21】
- `PayrollRecord` keeps historical payroll calculations for re-downloads without recalculation.【F:Models/PayrollRecord.cs†L1-L33】

## Running TrackHive locally
1. **Install prerequisites**
   - [.NET SDK 9.0](https://dotnet.microsoft.com/) and a SQL Server instance (e.g., LocalDB).
2. **Configure secrets**
   - Update `ConnectionStrings:DefaultConnection` and SMTP values in `appsettings.json` or user secrets/environment variables before running.【F:appsettings.json†L1-L19】
3. **Restore & build**
   ```bash
   dotnet restore
   dotnet build
   ```
4. **Apply database migrations** (optional; the app applies them on startup, but you can run manually):
   ```bash
   dotnet ef database update
   ```
5. **Run the app**
   ```bash
   dotnet run
   ```
   The site listens on the configured ASP.NET Core URL (defaults to `https://localhost:5001`).

## Working with data & storage
- EF Core migrations live under `Models/*_<Timestamp>*.cs` and are executed automatically during startup; use `dotnet ef migrations add` for schema changes.【F:Models/AppDbContext.cs†L13-L89】
- Uploaded profile pictures and medical certificates are stored under `wwwroot/uploads` by default; ensure the process has write access.【F:Controllers/EmployeeDashboardController.cs†L302-L392】【F:Controllers/ProfileController.cs†L214-L310】

## Project structure
```
Controllers/    // MVC controllers per feature area
Models/         // EF entities, view models, filters, services
Services/       // Cross-cutting helpers (navigation, PDF generation)
Views/          // Razor views for dashboards and forms
wwwroot/        // Static files and upload targets
```

## Next steps
- Add automated tests for business rules (leave entitlement math, payroll calculations, etc.).
- Wire up background jobs for reminder e-mails or daily attendance summaries if needed.

TrackHive centralizes HR processes so teams can onboard staff quickly, keep attendance & leave compliant, and close payroll faster.
