-- 017 日志表字段收敛：日志只追加，移除逻辑删除/更新字段与已停用的全量数据列；
-- 补齐 Wf_ExecLog 的租户、时间与节点定位字段，以及登录日志的显式结果。
-- 不做旧数据迁移或兼容保留。

-- 1) 日志实体不再继承 CorpEntityBase 的 5 个字段（Wf_ExecLog 原本就不含这些列）
ALTER TABLE "AuditLog"
    DROP COLUMN IF EXISTS "UpdateBy",
    DROP COLUMN IF EXISTS "UpdateTime",
    DROP COLUMN IF EXISTS "DeleteFlag",
    DROP COLUMN IF EXISTS "DeleteBy",
    DROP COLUMN IF EXISTS "DeleteTime";

ALTER TABLE "IdentityLoginAudit"
    DROP COLUMN IF EXISTS "UpdateBy",
    DROP COLUMN IF EXISTS "UpdateTime",
    DROP COLUMN IF EXISTS "DeleteFlag",
    DROP COLUMN IF EXISTS "DeleteBy",
    DROP COLUMN IF EXISTS "DeleteTime";

ALTER TABLE "Wf_TaskLog"
    DROP COLUMN IF EXISTS "UpdateBy",
    DROP COLUMN IF EXISTS "UpdateTime",
    DROP COLUMN IF EXISTS "DeleteFlag",
    DROP COLUMN IF EXISTS "DeleteBy",
    DROP COLUMN IF EXISTS "DeleteTime";

ALTER TABLE "Ef_RunLog"
    DROP COLUMN IF EXISTS "UpdateBy",
    DROP COLUMN IF EXISTS "UpdateTime",
    DROP COLUMN IF EXISTS "DeleteFlag",
    DROP COLUMN IF EXISTS "DeleteBy",
    DROP COLUMN IF EXISTS "DeleteTime";

ALTER TABLE "Ef_RunLogNode"
    DROP COLUMN IF EXISTS "UpdateBy",
    DROP COLUMN IF EXISTS "UpdateTime",
    DROP COLUMN IF EXISTS "DeleteFlag",
    DROP COLUMN IF EXISTS "DeleteBy",
    DROP COLUMN IF EXISTS "DeleteTime";

-- 2) 审计日志已停用的全量对象列与批量表达式列
ALTER TABLE "AuditLog"
    DROP COLUMN IF EXISTS "OldData",
    DROP COLUMN IF EXISTS "NewData",
    DROP COLUMN IF EXISTS "UpdateExp";

-- 3) EF 节点日志中与 EndTime 恒等的时间列
ALTER TABLE "Ef_RunLogNode" DROP COLUMN IF EXISTS "ExecTime";

-- 4) 登录日志显式结果；存量行均为成功，DEFAULT 仅用于填充历史行，随后去掉
ALTER TABLE "IdentityLoginAudit"
    ADD COLUMN IF NOT EXISTS "Success" boolean NOT NULL DEFAULT true;
ALTER TABLE "IdentityLoginAudit" ALTER COLUMN "Success" DROP DEFAULT;

-- 5) Wf_ExecLog 补齐租户隔离、创建时间/人与节点定位
ALTER TABLE "Wf_ExecLog"
    ADD COLUMN IF NOT EXISTS "CorpId" citext COLLATE "C",
    ADD COLUMN IF NOT EXISTS "CreateBy" jsonb,
    ADD COLUMN IF NOT EXISTS "CreateTime" bigint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "NodeName" citext NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "NodeType" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "Round" integer NOT NULL DEFAULT 0;
