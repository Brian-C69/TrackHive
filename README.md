# 🐝 TrackHive

**TrackHive** is a smart, lightweight web-based system that empowers HR teams to efficiently manage employee **attendance**, **leave applications**, and **payroll processing** — all in one seamless dashboard.

---

## 🚀 Features

* ⏱️ **Attendance Tracking**

  * Daily check-in / check-out logging
  * Late arrival detection
  * Attendance history per employee

* 🗕️ **Leave Management**

  * Apply for leave online
  * View entitlement balance and status
  * HR approval with one click

* 💰 **Payroll Automation**

  * Monthly salary generation
  * Calculates based on working days, absences, and unpaid leaves
  * Auto-generated payslips (PDF)

* 📊 **HR Dashboard**

  * Real-time attendance metrics
  * Leave overview and monthly trends
  * Quick access to employee records

* 🔐 **Role-Based Access Control**

  * IT (Super Admin) – Full system control
  * HR (Admin) – Manages employees and payroll
  * Employee – Personal dashboard, leave applications

---

## 🧠 Tech Stack

* **ASP.NET Core MVC** (C#)
* **Entity Framework Core** (EF Code First)
* **SQL Server / SQLite** (Pluggable DB)
* **Bootstrap 5 / Tailwind CSS** (Front-end styling)
* **Chart.js** (Analytics Dashboard)
* **GitHub / Git** (Version Control)

---

## 💡 Monetization Model (for future SaaS scaling)

| Plan           | Features                           | Price       |
| -------------- | ---------------------------------- | ----------- |
| **Starter**    | Attendance & leave tracking only   | RM49/month  |
| **Pro**        | Includes payroll and analytics     | RM99/month  |
| **Enterprise** | All features + custom integrations | RM149/month |

---

## 📦 Setup Instructions (for Devs)

1. Clone this repo:

   ```bash
   git clone https://github.com/YOUR-USERNAME/TrackHive.git
   ```

2. Open in **Visual Studio 2022**

3. Run database migrations:

   ```bash
   dotnet ef database update
   ```

4. Run the app:

   ```bash
   dotnet run
   ```

---

## 🛠️ Work in Progress

This project is being actively developed as part of a university assignment for the **Web & Mobile Systems** course (BMIT2023). Stay tuned for more modules!

---



---

> ✨ **TrackHive – Because Smart HR Starts With Smart Tracking.**
