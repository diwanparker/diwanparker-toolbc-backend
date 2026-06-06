using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toolbc.Api.Contracts;
using Toolbc.Api.Data;
using Toolbc.Api.Domain;
using Toolbc.Api.Services;

namespace Toolbc.Api.Endpoints;

public static class ToolbcEndpoints
{
    public static IEndpointRouteBuilder MapToolbcEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "ToolBC Backend",
            time = DateTimeOffset.UtcNow
        }));

        api.MapPost("/auth/login", async (
            LoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var response = await authService.LoginAsync(request, cancellationToken);
            return response is null
                ? Results.Unauthorized()
                : Results.Ok(response);
        });

        api.MapPost("/bootstrap/admin", async (
            CreateUserRequest request,
            ToolbcDbContext db,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            if (await db.Users.AnyAsync(cancellationToken))
            {
                return Results.Conflict(new { error = "Bootstrap ditutup karena user sudah ada." });
            }

            if (request.Role != UserRole.Admin)
            {
                return Results.BadRequest(new { error = "Bootstrap pertama hanya boleh membuat admin." });
            }

            var result = await authService.CreateUserAsync(request, "bootstrap", cancellationToken);
            return result.User is null
                ? Results.BadRequest(new { error = result.Error })
                : Results.Created($"/api/admin/users/{result.User.Id}", result.User);
        });

        api.MapPost("/admin/users", async (
            CreateUserRequest request,
            ClaimsPrincipal principal,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var createdBy = principal.FindFirstValue(ClaimTypes.Email) ?? "admin";
            var result = await authService.CreateUserAsync(request, createdBy, cancellationToken);
            return result.User is null
                ? Results.BadRequest(new { error = result.Error })
                : Results.Created($"/api/admin/users/{result.User.Id}", result.User);
        }).RequireAuthorization("AdminOnly");

        api.MapGet("/admin/users", async (
            ToolbcDbContext db,
            UserRole? role,
            CancellationToken cancellationToken) =>
        {
            var query = db.Users.AsNoTracking();
            if (role.HasValue)
            {
                query = query.Where(user => user.Role == role.Value);
            }

            var users = await query
                .OrderBy(user => user.FullName)
                .Select(user => AuthService.ToUserResponse(user))
                .ToListAsync(cancellationToken);

            return Results.Ok(users);
        }).RequireAuthorization("AdminOnly");

        api.MapGet("/admin/doctors", async (
            ToolbcDbContext db,
            CancellationToken cancellationToken) =>
        {
            var doctors = await db.DoctorProfiles
                .AsNoTracking()
                .Include(profile => profile.User)
                .OrderBy(profile => profile.User.FullName)
                .Select(profile => new
                {
                    profile.Id,
                    profile.User.FullName,
                    profile.User.Email,
                    profile.Specialty
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(doctors);
        }).RequireAuthorization("AdminOnly");

        api.MapGet("/patients/me/dashboard", GetPatientDashboardAsync)
            .RequireAuthorization("PatientOnly");

        api.MapPost("/patients/me/medication-logs", ConfirmMedicationLogAsync)
            .RequireAuthorization("PatientOnly");

        api.MapPost("/patients/me/symptom-logs", CreateSymptomLogAsync)
            .RequireAuthorization("PatientOnly");

        api.MapGet("/patients/me/history", GetPatientHistoryAsync)
            .RequireAuthorization("PatientOnly");

        api.MapGet("/notifications", GetNotificationsAsync)
            .RequireAuthorization();

        api.MapPost("/chat/reply", async (
            ChatReplyRequest request,
            IGeminiChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var response = await chatService.GenerateReplyAsync(request, cancellationToken);
            return Results.Ok(response);
        }).RequireAuthorization();

        api.MapGet("/doctors/me/dashboard", GetDoctorDashboardAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapGet("/doctors/me/patients", GetDoctorPatientsAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapGet("/doctors/me/adherence", GetDoctorAdherenceAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapGet("/doctors/me/reminders", GetDoctorRemindersAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapPatch("/reminders/{id:guid}/status", UpdateReminderStatusAsync)
            .RequireAuthorization("DoctorOnly");

        return app;
    }

    private static async Task<IResult> GetPatientDashboardAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var profile = await LoadPatientProfile(db, principal.GetUserId(), cancellationToken);
        if (profile is null || profile.TreatmentPlan is null)
        {
            return Results.NotFound(new { error = "Profil pasien belum tersedia." });
        }

        var notifications = await db.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == profile.UserId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(5)
            .Select(notification => new NotificationDto(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.IsRead,
                notification.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PatientDashboardResponse(
            AuthService.ToUserResponse(profile.User),
            profile.MedicalRecordNumber,
            profile.AssignedDoctor?.User.FullName,
            BuildTreatmentSummary(profile),
            notifications));
    }

    private static async Task<IResult> ConfirmMedicationLogAsync(
        MedicationLogRequest request,
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var doseLog = await db.MedicationDoseLogs
            .AsTracking()
            .Include(log => log.TreatmentPlan)
            .ThenInclude(plan => plan.PatientProfile)
            .FirstOrDefaultAsync(
                log => log.Id == request.DoseLogId && log.TreatmentPlan.PatientProfile.UserId == userId,
                cancellationToken);

        if (doseLog is null)
        {
            return Results.NotFound(new { error = "Log obat tidak ditemukan." });
        }

        doseLog.Status = request.Status;
        doseLog.Notes = request.Notes ?? doseLog.Notes;
        doseLog.ConfirmedAt = request.Status == DoseStatus.Taken ? DateTimeOffset.UtcNow : doseLog.ConfirmedAt;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new
        {
            doseLog.Id,
            doseLog.Status,
            doseLog.ConfirmedAt
        });
    }

    private static async Task<IResult> CreateSymptomLogAsync(
        SymptomLogRequest request,
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var profile = await db.PatientProfiles.FirstOrDefaultAsync(
            item => item.UserId == principal.GetUserId(),
            cancellationToken);
        if (profile is null)
        {
            return Results.NotFound(new { error = "Profil pasien belum tersedia." });
        }

        var risk = AnalyzeRisk(request);
        var feedback = risk switch
        {
            RiskLevel.High => "Risiko tinggi. Hubungi dokter atau fasilitas kesehatan segera, terutama bila sesak, batuk darah, atau demam tinggi menetap.",
            RiskLevel.Moderate => "Risiko sedang. Tetap minum obat dan laporkan gejala yang menetap ke dokter.",
            _ => "Risiko rendah. Gejala tercatat, lanjutkan pengobatan sesuai jadwal."
        };

        var symptomLog = new SymptomLog
        {
            PatientProfileId = profile.Id,
            PersistentCough = request.PersistentCough,
            FeverOrChills = request.FeverOrChills,
            NightSweats = request.NightSweats,
            WeightLossOrLowAppetite = request.WeightLossOrLowAppetite,
            RiskLevel = risk,
            Feedback = feedback
        };

        db.SymptomLogs.Add(symptomLog);
        db.Notifications.Add(new AppNotification
        {
            UserId = profile.UserId,
            Type = NotificationType.Alert,
            Title = "Symptom log submitted",
            Message = $"{risk} risk feedback",
            IsRead = false
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created(
            $"/api/patients/me/symptom-logs/{symptomLog.Id}",
            new SymptomLogResponse(symptomLog.Id, symptomLog.RiskLevel, symptomLog.Feedback, symptomLog.LoggedAt));
    }

    private static async Task<IResult> GetPatientHistoryAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var profile = await db.PatientProfiles
            .AsNoTracking()
            .Include(item => item.TreatmentPlan)
            .FirstOrDefaultAsync(item => item.UserId == principal.GetUserId(), cancellationToken);
        if (profile?.TreatmentPlan is null)
        {
            return Results.NotFound(new { error = "Profil pasien belum tersedia." });
        }

        var doseItems = await db.MedicationDoseLogs
            .AsNoTracking()
            .Where(log => log.TreatmentPlanId == profile.TreatmentPlan.Id)
            .OrderByDescending(log => log.ScheduledAt)
            .Take(20)
            .Select(log => new HistoryItemDto(
                log.Status == DoseStatus.Taken ? "Medicine completed" : "Medicine reminder",
                log.Notes ?? log.ScheduledAt.ToString("u"),
                "medication",
                log.ConfirmedAt ?? log.ScheduledAt))
            .ToListAsync(cancellationToken);

        var symptomItems = await db.SymptomLogs
            .AsNoTracking()
            .Where(log => log.PatientProfileId == profile.Id)
            .OrderByDescending(log => log.LoggedAt)
            .Take(20)
            .Select(log => new HistoryItemDto(
                "Symptom log submitted",
                $"{log.RiskLevel} risk feedback",
                "symptom",
                log.LoggedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(doseItems.Concat(symptomItems).OrderByDescending(item => item.CreatedAt).ToList());
    }

    private static async Task<IResult> GetNotificationsAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        [FromQuery] NotificationType? type,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var query = db.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);
        if (type.HasValue)
        {
            query = query.Where(notification => notification.Type == type.Value);
        }

        var response = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .Select(notification => new NotificationDto(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.IsRead,
                notification.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetDoctorDashboardAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctor = await LoadDoctorProfile(db, principal.GetUserId(), cancellationToken);
        if (doctor is null)
        {
            return Results.NotFound(new { error = "Profil dokter belum tersedia." });
        }

        var patientIds = doctor.Patients.Select(patient => patient.Id).ToArray();
        var urgentAlerts = await db.SymptomLogs.CountAsync(
            log => patientIds.Contains(log.PatientProfileId) && log.RiskLevel == RiskLevel.High,
            cancellationToken);
        var pendingFollowUp = await db.Reminders.CountAsync(
            reminder => patientIds.Contains(reminder.PatientProfileId) &&
                        (reminder.Status == ReminderStatus.Pending || reminder.Status == ReminderStatus.Escalated),
            cancellationToken);

        return Results.Ok(new DoctorDashboardResponse(
            AuthService.ToUserResponse(doctor.User),
            doctor.Patients.Count,
            urgentAlerts,
            doctor.Patients.Count,
            pendingFollowUp));
    }

    private static async Task<IResult> GetDoctorPatientsAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctor = await LoadDoctorProfile(db, principal.GetUserId(), cancellationToken);
        if (doctor is null)
        {
            return Results.NotFound(new { error = "Profil dokter belum tersedia." });
        }

        var response = doctor.Patients.Select(patient =>
        {
            var summary = BuildTreatmentSummary(patient);
            return new DoctorPatientDto(
                patient.Id,
                patient.User.FullName,
                patient.MedicalRecordNumber,
                summary.TreatmentDay,
                summary.AdherencePercent,
                RiskLevel.Low,
                summary.AdherencePercent < 80 ? "Needs review" : "Stable");
        });

        return Results.Ok(response.ToList());
    }

    private static async Task<IResult> GetDoctorAdherenceAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctor = await LoadDoctorProfile(db, principal.GetUserId(), cancellationToken);
        if (doctor is null)
        {
            return Results.NotFound(new { error = "Profil dokter belum tersedia." });
        }

        var summaries = doctor.Patients.Select(BuildTreatmentSummary).ToArray();
        var buckets = new[]
        {
            new AdherenceBucketDto("High Risk", summaries.Count(item => item.AdherencePercent < 75)),
            new AdherenceBucketDto("Moderate Risk", summaries.Count(item => item.AdherencePercent is >= 75 and < 90)),
            new AdherenceBucketDto("Stable", summaries.Count(item => item.AdherencePercent >= 90))
        };

        return Results.Ok(buckets);
    }

    private static async Task<IResult> GetDoctorRemindersAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctor = await LoadDoctorProfile(db, principal.GetUserId(), cancellationToken);
        if (doctor is null)
        {
            return Results.NotFound(new { error = "Profil dokter belum tersedia." });
        }

        var patientIds = doctor.Patients.Select(patient => patient.Id).ToArray();
        var reminders = await db.Reminders
            .AsNoTracking()
            .Include(reminder => reminder.PatientProfile)
            .ThenInclude(patient => patient.User)
            .Where(reminder => patientIds.Contains(reminder.PatientProfileId))
            .OrderByDescending(reminder => reminder.ScheduledAt)
            .Select(reminder => new ReminderDto(
                reminder.Id,
                reminder.PatientProfile.User.FullName,
                reminder.Message,
                reminder.Status,
                reminder.ScheduledAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(reminders);
    }

    private static async Task<IResult> UpdateReminderStatusAsync(
        Guid id,
        ReminderStatus status,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var reminder = await db.Reminders.FindAsync([id], cancellationToken);
        if (reminder is null)
        {
            return Results.NotFound(new { error = "Reminder tidak ditemukan." });
        }

        reminder.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { reminder.Id, reminder.Status });
    }

    private static async Task<PatientProfile?> LoadPatientProfile(
        ToolbcDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.PatientProfiles
            .AsNoTracking()
            .AsSplitQuery()
            .Include(profile => profile.User)
            .Include(profile => profile.AssignedDoctor)
            .ThenInclude(doctor => doctor!.User)
            .Include(profile => profile.TreatmentPlan)
            .ThenInclude(plan => plan!.DoseLogs)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    private static async Task<DoctorProfile?> LoadDoctorProfile(
        ToolbcDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.DoctorProfiles
            .AsNoTracking()
            .AsSplitQuery()
            .Include(profile => profile.User)
            .Include(profile => profile.Patients)
            .ThenInclude(patient => patient.User)
            .Include(profile => profile.Patients)
            .ThenInclude(patient => patient.TreatmentPlan)
            .ThenInclude(plan => plan!.DoseLogs)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    private static TreatmentSummaryDto BuildTreatmentSummary(PatientProfile profile)
    {
        var plan = profile.TreatmentPlan!;
        var startDate = profile.TreatmentStartDate.ToDateTime(TimeOnly.MinValue);
        var treatmentDay = Math.Max(1, (DateTime.UtcNow.Date - startDate.Date).Days + 1);
        var completion = Math.Min(100, treatmentDay * 100 / plan.TotalDays);
        var consideredLogs = plan.DoseLogs
            .Where(log => log.Status is DoseStatus.Taken or DoseStatus.Missed)
            .ToArray();
        var adherence = consideredLogs.Length == 0
            ? 100
            : (int)Math.Round(consideredLogs.Count(log => log.Status == DoseStatus.Taken) * 100d / consideredLogs.Length);
        var streak = plan.DoseLogs
            .Where(log => log.Status == DoseStatus.Taken)
            .Select(log => DateOnly.FromDateTime((log.ConfirmedAt ?? log.ScheduledAt).UtcDateTime))
            .Distinct()
            .OrderDescending()
            .TakeWhileConsecutiveDays();
        var nextDose = plan.DoseLogs
            .Where(log => log.Status == DoseStatus.Pending)
            .OrderBy(log => log.ScheduledAt)
            .FirstOrDefault();

        return new TreatmentSummaryDto(
            treatmentDay,
            plan.TotalDays,
            completion,
            adherence,
            streak,
            plan.MedicineSummary,
            nextDose is null ? "No pending dose" : nextDose.ScheduledAt.ToLocalTime().ToString("ddd HH:mm"));
    }

    private static RiskLevel AnalyzeRisk(SymptomLogRequest request)
    {
        var count = new[]
        {
            request.PersistentCough,
            request.FeverOrChills,
            request.NightSweats,
            request.WeightLossOrLowAppetite
        }.Count(value => value);

        if (count >= 3 || (request.FeverOrChills && (request.NightSweats || request.WeightLossOrLowAppetite)))
        {
            return RiskLevel.High;
        }

        return count >= 2 ? RiskLevel.Moderate : RiskLevel.Low;
    }

    private static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Token tidak memiliki user id valid.");
    }

    private static int TakeWhileConsecutiveDays(this IEnumerable<DateOnly> dates)
    {
        var expected = DateOnly.FromDateTime(DateTime.UtcNow);
        var streak = 0;
        foreach (var date in dates)
        {
            if (date != expected)
            {
                if (streak == 0 && date == expected.AddDays(-1))
                {
                    expected = date;
                }
                else
                {
                    break;
                }
            }

            streak++;
            expected = expected.AddDays(-1);
        }

        return streak;
    }
}
