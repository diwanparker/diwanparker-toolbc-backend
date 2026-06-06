CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "FullName" character varying(120) NOT NULL,
        "Email" character varying(160) NOT NULL,
        "PasswordHash" text NOT NULL,
        "Role" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedBy" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE TABLE "DoctorProfiles" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Specialty" text NOT NULL,
        CONSTRAINT "PK_DoctorProfiles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DoctorProfiles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE TABLE "Notifications" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Type" text NOT NULL,
        "Title" text NOT NULL,
        "Message" text NOT NULL,
        "IsRead" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Notifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE TABLE "PatientProfiles" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "MedicalRecordNumber" text NOT NULL,
        "AssignedDoctorId" uuid,
        "TreatmentStartDate" date NOT NULL,
        CONSTRAINT "PK_PatientProfiles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PatientProfiles_DoctorProfiles_AssignedDoctorId" FOREIGN KEY ("AssignedDoctorId") REFERENCES "DoctorProfiles" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_PatientProfiles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE TABLE "Reminders" (
        "Id" uuid NOT NULL,
        "PatientProfileId" uuid NOT NULL,
        "ScheduledAt" timestamp with time zone NOT NULL,
        "Status" text NOT NULL,
        "Message" text NOT NULL,
        CONSTRAINT "PK_Reminders" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Reminders_PatientProfiles_PatientProfileId" FOREIGN KEY ("PatientProfileId") REFERENCES "PatientProfiles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE TABLE "SymptomLogs" (
        "Id" uuid NOT NULL,
        "PatientProfileId" uuid NOT NULL,
        "LoggedAt" timestamp with time zone NOT NULL,
        "PersistentCough" boolean NOT NULL,
        "FeverOrChills" boolean NOT NULL,
        "NightSweats" boolean NOT NULL,
        "WeightLossOrLowAppetite" boolean NOT NULL,
        "RiskLevel" text NOT NULL,
        "Feedback" text NOT NULL,
        CONSTRAINT "PK_SymptomLogs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SymptomLogs_PatientProfiles_PatientProfileId" FOREIGN KEY ("PatientProfileId") REFERENCES "PatientProfiles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE TABLE "TreatmentPlans" (
        "Id" uuid NOT NULL,
        "PatientProfileId" uuid NOT NULL,
        "Phase" text NOT NULL,
        "MedicineSummary" text NOT NULL,
        "TotalDays" integer NOT NULL,
        "Status" text NOT NULL,
        CONSTRAINT "PK_TreatmentPlans" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TreatmentPlans_PatientProfiles_PatientProfileId" FOREIGN KEY ("PatientProfileId") REFERENCES "PatientProfiles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE TABLE "MedicationDoseLogs" (
        "Id" uuid NOT NULL,
        "TreatmentPlanId" uuid NOT NULL,
        "ScheduledAt" timestamp with time zone NOT NULL,
        "ConfirmedAt" timestamp with time zone,
        "Status" text NOT NULL,
        "Notes" text,
        CONSTRAINT "PK_MedicationDoseLogs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MedicationDoseLogs_TreatmentPlans_TreatmentPlanId" FOREIGN KEY ("TreatmentPlanId") REFERENCES "TreatmentPlans" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_DoctorProfiles_UserId" ON "DoctorProfiles" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE INDEX "IX_MedicationDoseLogs_TreatmentPlanId" ON "MedicationDoseLogs" ("TreatmentPlanId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE INDEX "IX_Notifications_UserId" ON "Notifications" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE INDEX "IX_PatientProfiles_AssignedDoctorId" ON "PatientProfiles" ("AssignedDoctorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_PatientProfiles_UserId" ON "PatientProfiles" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE INDEX "IX_Reminders_PatientProfileId" ON "Reminders" ("PatientProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE INDEX "IX_SymptomLogs_PatientProfileId" ON "SymptomLogs" ("PatientProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_TreatmentPlans_PatientProfileId" ON "TreatmentPlans" ("PatientProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605143551_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260605143551_InitialCreate', '8.0.25');
    END IF;
END $EF$;
COMMIT;

