-- 为 EF 人工重试增加可过期租约和当前尝试标识，避免消息未到 Flow.Host 时永久卡住。
ALTER TABLE "Ef_RunLog"
    ADD COLUMN IF NOT EXISTS "RetryAttemptId" citext NOT NULL DEFAULT '';

ALTER TABLE "Ef_RunLog"
    ADD COLUMN IF NOT EXISTS "RetryLeaseUntil" bigint;
