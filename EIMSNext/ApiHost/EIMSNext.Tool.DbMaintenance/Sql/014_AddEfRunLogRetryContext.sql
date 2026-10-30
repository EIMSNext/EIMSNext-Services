-- 为已有数据库补充 EF 人工重试上下文。
ALTER TABLE "Ef_RunLog"
    ADD COLUMN IF NOT EXISTS "RetryContextJson" citext NOT NULL DEFAULT '';

ALTER TABLE "Ef_RunLog"
    ADD COLUMN IF NOT EXISTS "RetryInProgress" boolean NOT NULL DEFAULT false;
