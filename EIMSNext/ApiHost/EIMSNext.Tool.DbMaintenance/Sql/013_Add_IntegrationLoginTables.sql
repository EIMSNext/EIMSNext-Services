-- 013: 三方集成登录两张表（feature/easyun 合并新增实体，存量库补建）
-- IntegrationLoginSetting: provider 配置（Type/DisplayName/ClientId/RedirectUri/...）
-- UserIntegrationBinding: 本地用户与第三方身份绑定
-- 幂等：IF NOT EXISTS，可重复执行。

CREATE TABLE IF NOT EXISTS "IntegrationLoginSetting" (
    "Id" citext COLLATE "C" not null default '',
    "Type" citext not null default '',
    "DisplayName" citext not null default '',
    "Enabled" boolean not null default false,
    "ClientId" citext not null default '',
    "ClientSecret" citext not null default '',
    "RedirectUri" citext not null default '',
    "AppId" citext not null default '',
    "AppSecret" citext not null default '',
    "CorpId" citext not null default '',
    "AgentId" citext not null default '',
    "TenantId" citext not null default '',
    "ExtraParameters" jsonb not null default '[]'::jsonb,
    CONSTRAINT "PK_IntegrationLoginSetting" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS "UserIntegrationBinding" (
    "Id" citext COLLATE "C" not null default '',
    "UserId" citext COLLATE "C" not null default '',
    "IntegrationType" citext not null default '',
    "OpenId" citext not null default '',
    "UnionId" citext not null default '',
    "ExternalUserId" citext not null default '',
    "CorpId" citext COLLATE "C" not null default '',
    "TenantId" citext not null default '',
    "NickName" citext not null default '',
    "Avatar" citext not null default '',
    "Enabled" boolean not null default true,
    CONSTRAINT "PK_UserIntegrationBinding" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_UserIntegrationBinding_UserId" ON "UserIntegrationBinding" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_UserIntegrationBinding_OpenId" ON "UserIntegrationBinding" ("OpenId");
CREATE UNIQUE INDEX IF NOT EXISTS "UX_UserIntegrationBinding_Type_OpenId" ON "UserIntegrationBinding" ("IntegrationType", "OpenId");
