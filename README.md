# ToolBC Backend

Backend .NET untuk aplikasi Flutter ToolBC/TBC Care. Database production/development diarahkan ke Supabase PostgreSQL, sedangkan login memakai JWT custom backend dan tabel `Users` milik aplikasi.

## Stack

- ASP.NET Core Web API `net8.0`
- EF Core + PostgreSQL via `Npgsql`
- Supabase PostgreSQL sebagai database
- JWT Bearer auth
- Swagger/OpenAPI
- OpenAI/Gemini chat proxy via konfigurasi backend

## Menjalankan

Restore tool dan package:

```powershell
dotnet tool restore
dotnet restore
```

Isi connection string Supabase lewat user-secrets:

```powershell
dotnet user-secrets init --project .\Toolbc.Api\Toolbc.Api.csproj
dotnet user-secrets set "ConnectionStrings:Default" "<SUPABASE_POSTGRES_CONNECTION_STRING>" --project .\Toolbc.Api\Toolbc.Api.csproj
```

Atau pakai helper script:

```powershell
.\scripts\set-supabase-connection.ps1 "<SUPABASE_POSTGRES_CONNECTION_STRING>"
```

Apply schema ke Supabase:

```powershell
dotnet tool run dotnet-ef database update --project .\Toolbc.Api\Toolbc.Api.csproj --startup-project .\Toolbc.Api\Toolbc.Api.csproj
```

Atau:

```powershell
.\scripts\apply-database.ps1
```

Alternatif manual: buka Supabase SQL Editor lalu jalankan isi file `supabase/toolbc_schema_supabase.sql`.

Jalankan API:

```powershell
dotnet run --project .\Toolbc.Api\Toolbc.Api.csproj --launch-profile http
```

API berjalan di:

- `http://localhost:5272`
- Swagger: `http://localhost:5272/swagger`
- Health check: `http://localhost:5272/api/health`

## Supabase Connection String

Di Supabase:

1. Buka project.
2. Masuk ke `Project Settings`.
3. Buka menu `Database`.
4. Cari bagian `Connection string`.
5. Pilih mode `Transaction pooler` bila tersedia.
6. Copy string yang berisi host, port, database, user, dan password.

Format yang dibutuhkan backend:

```text
Host=<host>;Port=6543;Database=postgres;Username=<user>;Password=<password>;Ssl Mode=Require;Trust Server Certificate=true
```

Jika memakai direct connection, port biasanya `5432`. Jika memakai transaction pooler, port biasanya `6543`.

## Seed Data

Seed demo dimatikan secara default:

```json
"Database": {
  "ApplyMigrationsOnStartup": false,
  "SeedDemoData": false
}
```

Database Supabase akan dibuat kosong sesuai schema. Buat admin pertama lewat endpoint bootstrap. Endpoint ini hanya jalan kalau tabel `Users` masih kosong:

```http
POST /api/bootstrap/admin
Content-Type: application/json

{
  "fullName": "Admin ToolBC",
  "email": "admin@admin.com",
  "password": "Admin123!",
  "role": "Admin"
}
```

Jika API sudah berjalan, bisa juga pakai helper:

```powershell
.\scripts\bootstrap-admin.ps1
```

## Endpoint Utama

- `POST /api/auth/login`
- `POST /api/bootstrap/admin`
- `POST /api/admin/users`
- `GET /api/admin/users`
- `GET /api/admin/doctors`
- `GET /api/patients/me/dashboard`
- `POST /api/patients/me/medication-logs`
- `POST /api/patients/me/symptom-logs`
- `GET /api/patients/me/history`
- `GET /api/notifications`
- `POST /api/chat/reply`
- `GET /api/doctors/me/dashboard`
- `GET /api/doctors/me/patients`
- `GET /api/doctors/me/adherence`
- `GET /api/doctors/me/reminders`
- `PATCH /api/reminders/{id}/status?status=Resolved`

## AI Providers

Backend chat mendukung OpenAI dan Gemini. Default provider memakai OpenAI bila `OpenAI:ApiKey` tersedia, lalu fallback ke Gemini kalau OpenAI error, rate-limit, atau quota habis.

Untuk memakai OpenAI dari backend:

```powershell
dotnet user-secrets set "AI:Provider" "openai" --project .\Toolbc.Api\Toolbc.Api.csproj
dotnet user-secrets set "OpenAI:ApiKey" "<api-key>" --project .\Toolbc.Api\Toolbc.Api.csproj
dotnet user-secrets set "OpenAI:Model" "gpt-5-mini" --project .\Toolbc.Api\Toolbc.Api.csproj
```

Untuk memakai Gemini dari backend:

```powershell
dotnet user-secrets set "Gemini:ApiKey1" "<primary-gemini-api-key>" --project .\Toolbc.Api\Toolbc.Api.csproj
dotnet user-secrets set "Gemini:ApiKey2" "<backup-gemini-api-key>" --project .\Toolbc.Api\Toolbc.Api.csproj
dotnet user-secrets set "Gemini:Model" "gemini-2.5-flash" --project .\Toolbc.Api\Toolbc.Api.csproj
```

`Gemini:ApiKeys:0`, `Gemini:ApiKeys:1`, dan seterusnya juga didukung bila hosting provider lebih nyaman memakai array config.

Jangan taruh API key di Flutter.
