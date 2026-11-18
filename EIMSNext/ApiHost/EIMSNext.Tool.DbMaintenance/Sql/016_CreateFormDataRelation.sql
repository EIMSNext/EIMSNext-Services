-- 关联数据关系表（方案 A）增量迁移：补齐既有库缺失的表与索引。
-- 与基线 001/002 中由 EF 模型重生成的 FormDataRelation DDL 完全一致（列/类型/排序规则/索引均对齐）。
-- 既有库 001/002 已应用（旧校验和），故用本脚本作为增量迁移补齐，且全部 IF NOT EXISTS 幂等，
-- 对新库（001 已建表）亦为安全 no-op。
-- 应用方式见实现规划第 11 节：先 --accept-checksum-change 放行 001/002 校验和，再正常跑 DbMaintenance 应用本脚本。

CREATE TABLE IF NOT EXISTS "FormDataRelation" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "FieldKey" citext not null default '',
    "DataId" citext COLLATE "C" not null default '',
    "MainFormId" citext COLLATE "C" not null default '',
    "MainDataId" citext COLLATE "C" not null default '',
    "Status" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormDataRelation" PRIMARY KEY ("Id")
);

create index if not exists "IX_FormDataRelation_CorpId_MainFormId_MainDataId" on "FormDataRelation" ("CorpId", "MainFormId", "MainDataId");
create unique index if not exists "UX_FormDataRelation_CorpId_FormId_FieldKey_DataId" on "FormDataRelation" ("CorpId", "FormId", "FieldKey", "DataId");
