CREATE TABLE IF NOT EXISTS "Jobs" (
    "Id"           UUID        NOT NULL PRIMARY KEY,
    "Type"         VARCHAR(100) NOT NULL,
    "Payload"      JSONB       NOT NULL DEFAULT '{}',
    "Status"       VARCHAR(50) NOT NULL DEFAULT 'Pending',
    "Attempts"     INT         NOT NULL DEFAULT 0,
    "MaxAttempts"  INT         NOT NULL DEFAULT 3,
    "ScheduledAt"  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "CreatedAt"    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "ProcessedAt"  TIMESTAMPTZ,
    "LastError"    TEXT
);

CREATE INDEX IF NOT EXISTS idx_jobs_status ON "Jobs" ("Status");
CREATE INDEX IF NOT EXISTS idx_jobs_created_at ON "Jobs" ("CreatedAt");
