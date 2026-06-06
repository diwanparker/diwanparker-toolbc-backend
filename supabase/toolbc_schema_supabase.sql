-- ToolBC schema for Supabase SQL Editor.
-- Run this file instead of the EF idempotent migration script if Supabase rejects DO/EF blocks.

create table if not exists "Users" (
    "Id" uuid not null,
    "FullName" varchar(120) not null,
    "Email" varchar(160) not null,
    "PasswordHash" text not null,
    "Role" text not null,
    "IsActive" boolean not null,
    "CreatedBy" text null,
    "CreatedAt" timestamptz not null,
    constraint "PK_Users" primary key ("Id")
);

create table if not exists "DoctorProfiles" (
    "Id" uuid not null,
    "UserId" uuid not null,
    "Specialty" text not null,
    constraint "PK_DoctorProfiles" primary key ("Id"),
    constraint "FK_DoctorProfiles_Users_UserId"
        foreign key ("UserId") references "Users" ("Id") on delete cascade
);

create table if not exists "PatientProfiles" (
    "Id" uuid not null,
    "UserId" uuid not null,
    "MedicalRecordNumber" text not null,
    "AssignedDoctorId" uuid null,
    "TreatmentStartDate" date not null,
    constraint "PK_PatientProfiles" primary key ("Id"),
    constraint "FK_PatientProfiles_Users_UserId"
        foreign key ("UserId") references "Users" ("Id") on delete cascade,
    constraint "FK_PatientProfiles_DoctorProfiles_AssignedDoctorId"
        foreign key ("AssignedDoctorId") references "DoctorProfiles" ("Id") on delete set null
);

create table if not exists "TreatmentPlans" (
    "Id" uuid not null,
    "PatientProfileId" uuid not null,
    "Phase" text not null,
    "MedicineSummary" text not null,
    "TotalDays" integer not null,
    "Status" text not null,
    constraint "PK_TreatmentPlans" primary key ("Id"),
    constraint "FK_TreatmentPlans_PatientProfiles_PatientProfileId"
        foreign key ("PatientProfileId") references "PatientProfiles" ("Id") on delete cascade
);

create table if not exists "MedicationDoseLogs" (
    "Id" uuid not null,
    "TreatmentPlanId" uuid not null,
    "ScheduledAt" timestamptz not null,
    "ConfirmedAt" timestamptz null,
    "Status" text not null,
    "Notes" text null,
    constraint "PK_MedicationDoseLogs" primary key ("Id"),
    constraint "FK_MedicationDoseLogs_TreatmentPlans_TreatmentPlanId"
        foreign key ("TreatmentPlanId") references "TreatmentPlans" ("Id") on delete cascade
);

create table if not exists "SymptomLogs" (
    "Id" uuid not null,
    "PatientProfileId" uuid not null,
    "LoggedAt" timestamptz not null,
    "PersistentCough" boolean not null,
    "FeverOrChills" boolean not null,
    "NightSweats" boolean not null,
    "WeightLossOrLowAppetite" boolean not null,
    "RiskLevel" text not null,
    "Feedback" text not null,
    constraint "PK_SymptomLogs" primary key ("Id"),
    constraint "FK_SymptomLogs_PatientProfiles_PatientProfileId"
        foreign key ("PatientProfileId") references "PatientProfiles" ("Id") on delete cascade
);

create table if not exists "Reminders" (
    "Id" uuid not null,
    "PatientProfileId" uuid not null,
    "ScheduledAt" timestamptz not null,
    "Status" text not null,
    "Message" text not null,
    constraint "PK_Reminders" primary key ("Id"),
    constraint "FK_Reminders_PatientProfiles_PatientProfileId"
        foreign key ("PatientProfileId") references "PatientProfiles" ("Id") on delete cascade
);

create table if not exists "Notifications" (
    "Id" uuid not null,
    "UserId" uuid not null,
    "Type" text not null,
    "Title" text not null,
    "Message" text not null,
    "IsRead" boolean not null,
    "CreatedAt" timestamptz not null,
    constraint "PK_Notifications" primary key ("Id"),
    constraint "FK_Notifications_Users_UserId"
        foreign key ("UserId") references "Users" ("Id") on delete cascade
);

create unique index if not exists "IX_Users_Email" on "Users" ("Email");
create unique index if not exists "IX_DoctorProfiles_UserId" on "DoctorProfiles" ("UserId");
create unique index if not exists "IX_PatientProfiles_UserId" on "PatientProfiles" ("UserId");
create unique index if not exists "IX_TreatmentPlans_PatientProfileId" on "TreatmentPlans" ("PatientProfileId");
create index if not exists "IX_PatientProfiles_AssignedDoctorId" on "PatientProfiles" ("AssignedDoctorId");
create index if not exists "IX_MedicationDoseLogs_TreatmentPlanId" on "MedicationDoseLogs" ("TreatmentPlanId");
create index if not exists "IX_Notifications_UserId" on "Notifications" ("UserId");
create index if not exists "IX_Reminders_PatientProfileId" on "Reminders" ("PatientProfileId");
create index if not exists "IX_SymptomLogs_PatientProfileId" on "SymptomLogs" ("PatientProfileId");

create table if not exists "__EFMigrationsHistory" (
    "MigrationId" varchar(150) not null,
    "ProductVersion" varchar(32) not null,
    constraint "PK___EFMigrationsHistory" primary key ("MigrationId")
);

insert into "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
values ('20260605143551_InitialCreate', '8.0.25')
on conflict ("MigrationId") do nothing;
