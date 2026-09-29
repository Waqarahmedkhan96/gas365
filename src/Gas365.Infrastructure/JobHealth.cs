using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

// Upserts the health row for a platform job so operators can see stalled or failing background work.
public static class JobHealth
{
    public static Task RecordAsync(Gas365DbContext db, string name, Exception? error, CancellationToken ct)
    {
        var message = error is null ? "" : error.GetType().Name + ": " + (error.Message.Length > 250 ? error.Message[..250] : error.Message);
        return error is null
            ? db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "JobStates" ("Name", "LastStartedAt", "LastSucceededAt", "LastError", "ConsecutiveFailures") VALUES ({name}, now(), now(), '', 0)
                ON CONFLICT ("Name") DO UPDATE SET "LastStartedAt" = now(), "LastSucceededAt" = now(), "ConsecutiveFailures" = 0
                """, ct)
            : db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "JobStates" ("Name", "LastStartedAt", "LastFailedAt", "LastError", "ConsecutiveFailures") VALUES ({name}, now(), now(), {message}, 1)
                ON CONFLICT ("Name") DO UPDATE SET "LastStartedAt" = now(), "LastFailedAt" = now(), "LastError" = {message}, "ConsecutiveFailures" = "JobStates"."ConsecutiveFailures" + 1
                """, ct);
    }
}
