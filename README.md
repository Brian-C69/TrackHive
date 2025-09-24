# TrackHive ✨

TrackHive is a **role-based HR operations portal** built with **ASP.NET Core MVC**. It helps **IT admins**, **HR teams**, and **employees** manage **onboarding**, **attendance**, **leave**, **payroll**, and **preferences** — all in one place. 🚀

---

## Table of Contents

- [Feature Tour](#-feature-tour)
- [Architecture Overview](#️-architecture-overview)
- [Data Model Highlights](#%EF%B8%8F-data-model-highlights)
- [Requirements](#-requirements)
- [Configuration](#-configuration)
- [Quick Start](#-quick-start)
- [Data & Storage](#-data--storage)
- [Project Structure](#-project-structure)
- [Next Steps](#-next-steps)
- [License](#-license)

---

## 🎯 Feature Tour

### 🔐 Authentication & Onboarding
- Cookie auth with **role claims**, **organization scoping**, and a global filter that forces first-time password changes.
- Tenant bootstrap: register an organization and seed the first admin in one transaction.
- Account lockout (after 3 failed attempts), password reset emails, and auto-unlock on successful sign-in.

### 🧭 Role-Specific Workspaces
- **IT Dashboard**: view org info and invite HR admins via email with temporary passwords.
- **People Management**: search/sort/paginate, edit, activate/deactivate, bulk update, or delete HR/employee accounts.
- **Global User Admin (IT-only)**: cross-org filtering with direct user management and bulk actions.

### ⏰ Attendance & Self-Service
- **Employee Dashboard**: daily check-in/out, recent history, live leave balance, and quick leave application with smart defaults.
- **Leave Requests**: entitlement validation, pending-day tracking, and HR email notifications.

### 🩺 Medical Certificate Workflow
- Employees upload **images/PDFs** for sick leave (size/type-checked) stored under `wwwroot/uploads/leave-documents`; new uploads replace prior ones.
- HR reviewers **approve/reject**, with status transitions and email notifications.
- Secure downloads for owners and HR/IT in the same organization.

### 📈 HR Analytics & Notifications
- HR dashboard aggregates pending approvals, certificate queues, leave balances, attendance summaries, and **6-month trends** (with type breakdowns) + dashboard toasts.
- Quick invites can seed **salary data** and **default leave balances** for new hires.

### 💸 Payroll & Reporting
- Payroll calculator derives **working days**, **worked hours**, **auto overtime**, **manual adjustments**, **deductions**, and totals with a configurable **overtime multiplier**.
- Saves monthly payrolls, shows history, and exports **QuestPDF**-based payslips & org-wide payroll reports (PDF). 🧾

### 👤 Profile & Preferences
- Self-service profile editor with optional image upload and server-side crop/resize (ImageSharp) + “clear avatar.”
- Preferences for **theme** (light/dark), **language**, and **nav layout** — persisted and re-issued as claims.

### ✉️ Notifications & Messaging
- Centralized **SMTP** service for invitations, leave updates, and certificate workflow messages with graceful error handling.

---

## 🏗️ Architecture Overview

- **ASP.NET Core MVC** (`net9.0`) with per-area controllers and global security filters.
- **Entity Framework Core** single `AppDbContext` for orgs, users, attendance, leave, payroll, and documents (indexes, cascades, column shapes).
- **Services**: UI navigation metadata + QuestPDF document generation (payslips & monthly reports).
- Startup: applies **EF migrations**, configures **cookies**, **SMTP**, and registers the **PDF generator**.

---

## 🗃️ Data Model Highlights

- `AppUser`: role, organization, salary, profile, preferences, lockouts, resets.
- `AttendanceRecord`: one record per user per day; check-in/out timestamps.
- `LeaveRequest` & `LeaveBalance`: entitlement usage, review status, reviewer links, document attachments.
- `LeaveDocument`: metadata for uploaded certificates stored under `wwwroot`; cascade deletes with parent.
- `PayrollRecord`: historical payroll calculations for re-download without recompute.

---

## ✅ Requirements

- **.NET SDK 9.0**
- **SQL Server** (LocalDB is fine for development)
- (Optional) SMTP account for email features

---

## ⚙️ Configuration

Set your connection string and SMTP in `appsettings.json` (or via user secrets / environment variables):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=TrackHive;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "User": "no-reply@example.com",
    "Password": "your-smtp-password",
    "UseSsl": true
  }
}
```

---

## 🚀 Quick Start

```bash
# 1) Restore & build
dotnet restore
dotnet build

# 2) Apply database migrations (optional; also applied on startup)
dotnet ef database update

# 3) Run
dotnet run
```

The app listens on your configured ASP.NET Core URL (e.g., `https://localhost:5001`).  
For LAN/Internet access during dev, bind Kestrel to `0.0.0.0` and open/forward the port as needed.

---

## 📦 Data & Storage

- EF Core migrations live under `Models/*_<Timestamp>*.cs`. Create new migrations with:
  ```bash
  dotnet ef migrations add <Name>
  ```
- Uploaded avatars and medical certificates live under `wwwroot/uploads`. Ensure the process has write permissions.

---

## 🧹 Data Retention

- Organizations on the **Free plan** only retain the most recent 90 days of attendance, leave requests (and their documents), and payroll records.
- A background cleanup service runs daily to remove data outside this window, and dashboards/payroll history endpoints filter out older records for Free tenants.
- Paid plans keep their full history untouched.

---

## 🗂️ Project Structure

```
Controllers/    // MVC controllers per feature area
Models/         // EF entities, view models, filters, services
Services/       // Cross-cutting helpers (navigation, PDF generation)
Views/          // Razor views for dashboards and forms
wwwroot/        // Static files and upload targets
```

---

## 🔮 Next Steps

- Add automated tests for **leave math**, **payroll calculations**, and core workflows.
- Add background jobs for **reminder emails** or **daily attendance summaries**.
- Optional: use **Dev Tunnels/ngrok** for quick external testing or deploy to **Azure App Service**. 🌐

---

## 📜 License

MIT (or your preferred license). Update this section as needed.
