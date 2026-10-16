-- EIMSNext PostgreSQL 基线结构 —— 实体表。
--
-- 【唯一依据】本文件是 EF 模型的投影，模型才是表结构的真源：
--     Core/EIMSNext.Persistence.PostgreSql/PostgreSqlDbContext.cs
-- 生成方式：PostgreSqlBaselineScript.RenderCreateTables()，输入为
--     dbContext.Database.GenerateCreateScript()，之后做三件事：
--   1) 表按表名（Ordinal）排序；
--   2) 列修饰符统一小写（not null）；
--   3) 给 NOT NULL 的标量列补中立默认值（text '' / integer、bigint、numeric 0 / boolean false），
--      便于手工和原生 SQL 插入；EF 写入时始终显式赋值，不依赖这些默认值。
--      刻意不补的类型：jsonb、数组、uuid、时间戳 —— 补默认值会改变语义或直接非法。
--   4) 分区表改写（见 PostgreSqlBaselineScript.PartitionedTables）：FormData 两级哈希
--      16×8（CorpId→FormId）、FormDataChangeLog / Wf_TaskLog 单级哈希 16（CorpId）、
--      log 组按 CreateTime 月度范围 24 个月 + default 兜底。分区键已复合进各表主键，
--      与 EIMSNextModelConfiguration 的 HasKey 一一对应；索引见 002，建在父表上自动级联。
--
-- 重新生成（会同时刷新 002 的模型声明索引段）：
--     置环境变量 EIMS_REGENERATE_BASELINE=1 后执行
--     dotnet test Tests/EIMSNext.Core.Tests --filter RegenerateBaselineScripts
-- 一致性由 Tests/EIMSNext.Core.Tests/BaselineScriptTests 与 SchemaConsistencyTests 守住。
--
-- 因此要改表结构时：先改实体与 DbContext，再重新生成本文件，
-- 不要在迁移链尾部追加「改列」脚本（全新起步，不承担向前兼容）。
--
-- 索引见 002_CreateIndexes.sql；WorkflowCore 与 Quartz 的第三方存储表见 003~006。
-- 表名规则：精确实体名单数 + 引号标识符（Wf_* / Ef_* 沿用下划线前缀的既有约定）。
-- 时间戳列 CreateTime / UpdateTime 为 Unix 毫秒，故用 bigint；审计列 CreateBy / UpdateBy 为 jsonb。


CREATE TABLE "AppDef" (
    "Id" citext COLLATE "C" not null default '',
    "TemplateId" citext COLLATE "C",
    "Name" citext not null default '',
    "Description" citext not null default '',
    "Icon" citext not null default '',
    "IconColor" citext not null default '',
    "GroupId" citext COLLATE "C",
    "SortIndex" integer not null default 0,
    "HomeEntryIds" jsonb not null,
    "AppMenus" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_AppDef" PRIMARY KEY ("Id")
);


CREATE TABLE "AppProfile" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Summary" citext not null default '',
    "Description" citext not null default '',
    "Icon" citext not null default '',
    "CoverImage" citext not null default '',
    "BannerImage" citext not null default '',
    "GalleryImages" text[] not null,
    "Category" citext not null default '',
    "Industry" citext not null default '',
    "Tags" text[] not null,
    "Author" citext not null default '',
    "InstallCount" bigint not null default 0,
    "SortIndex" integer not null default 0,
    "IsOfficial" boolean not null default false,
    "IsHot" boolean not null default false,
    "IsRecommended" boolean not null default false,
    "ThemeColor" citext not null default '',
    "TemplateId" citext COLLATE "C" not null default '',
    "Status" integer not null default 0,
    "PublishedAt" timestamp with time zone,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_AppProfile" PRIMARY KEY ("Id")
);


CREATE TABLE "AppTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Description" citext not null default '',
    "Icon" citext not null default '',
    "Menus" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_AppTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "AuditLog" (
    "Id" citext COLLATE "C" not null default '',
    "CreateTime" bigint not null default 0,
    "Action" integer not null default 0,
    "EntityType" citext,
    "DataId" citext COLLATE "C",
    "Detail" citext,
    "OldData" citext,
    "NewData" citext,
    "DataFilter" citext,
    "UpdateExp" citext,
    "ClientIp" citext,
    "CreateBy" jsonb,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_AuditLog" PRIMARY KEY ("Id", "CreateTime")
) partition by range ("CreateTime");

create table "AuditLog_2026_01" partition of "AuditLog" for values from (1767225600000) to (1769904000000);
create table "AuditLog_2026_02" partition of "AuditLog" for values from (1769904000000) to (1772323200000);
create table "AuditLog_2026_03" partition of "AuditLog" for values from (1772323200000) to (1775001600000);
create table "AuditLog_2026_04" partition of "AuditLog" for values from (1775001600000) to (1777593600000);
create table "AuditLog_2026_05" partition of "AuditLog" for values from (1777593600000) to (1780272000000);
create table "AuditLog_2026_06" partition of "AuditLog" for values from (1780272000000) to (1782864000000);
create table "AuditLog_2026_07" partition of "AuditLog" for values from (1782864000000) to (1785542400000);
create table "AuditLog_2026_08" partition of "AuditLog" for values from (1785542400000) to (1788220800000);
create table "AuditLog_2026_09" partition of "AuditLog" for values from (1788220800000) to (1790812800000);
create table "AuditLog_2026_10" partition of "AuditLog" for values from (1790812800000) to (1793491200000);
create table "AuditLog_2026_11" partition of "AuditLog" for values from (1793491200000) to (1796083200000);
create table "AuditLog_2026_12" partition of "AuditLog" for values from (1796083200000) to (1798761600000);
create table "AuditLog_2027_01" partition of "AuditLog" for values from (1798761600000) to (1801440000000);
create table "AuditLog_2027_02" partition of "AuditLog" for values from (1801440000000) to (1803859200000);
create table "AuditLog_2027_03" partition of "AuditLog" for values from (1803859200000) to (1806537600000);
create table "AuditLog_2027_04" partition of "AuditLog" for values from (1806537600000) to (1809129600000);
create table "AuditLog_2027_05" partition of "AuditLog" for values from (1809129600000) to (1811808000000);
create table "AuditLog_2027_06" partition of "AuditLog" for values from (1811808000000) to (1814400000000);
create table "AuditLog_2027_07" partition of "AuditLog" for values from (1814400000000) to (1817078400000);
create table "AuditLog_2027_08" partition of "AuditLog" for values from (1817078400000) to (1819756800000);
create table "AuditLog_2027_09" partition of "AuditLog" for values from (1819756800000) to (1822348800000);
create table "AuditLog_2027_10" partition of "AuditLog" for values from (1822348800000) to (1825027200000);
create table "AuditLog_2027_11" partition of "AuditLog" for values from (1825027200000) to (1827619200000);
create table "AuditLog_2027_12" partition of "AuditLog" for values from (1827619200000) to (1830297600000);
create table "AuditLog_default" partition of "AuditLog" default;


CREATE TABLE "Client" (
    "Id" citext COLLATE "C" not null default '',
    "Enabled" boolean not null default false,
    "ClientSecret" citext,
    "RequireClientSecret" boolean not null default false,
    "Name" citext,
    "AllowedGrantTypes" citext[] not null,
    "AllowedScopes" citext[] not null,
    "IdentityTokenLifetime" integer not null default 0,
    "AccessTokenLifetime" integer not null default 0,
    "ApiKey" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Client" PRIMARY KEY ("Id")
);


CREATE TABLE "ClientGrant" (
    "Id" citext COLLATE "C" not null default '',
    "ClientId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "AppScope" citext not null default '',
    "AppIds" jsonb not null,
    "ApiScope" citext not null default '',
    "ResourceActions" jsonb not null,
    "IpWhitelist" jsonb not null,
    "Enabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_ClientGrant" PRIMARY KEY ("Id")
);


CREATE TABLE "CorpOnboardingRequest" (
    "Id" citext COLLATE "C" not null default '',
    "UserId" citext COLLATE "C" not null default '',
    "UserName" citext not null default '',
    "TargetCorpId" citext COLLATE "C" not null default '',
    "TargetCorpName" citext not null default '',
    "ApplicantName" citext not null default '',
    "Phone" citext not null default '',
    "Email" citext not null default '',
    "EmployeeId" citext COLLATE "C" not null default '',
    "SourceType" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_CorpOnboardingRequest" PRIMARY KEY ("Id")
);


CREATE TABLE "Corporate" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Description" citext not null default '',
    "Platform" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_Corporate" PRIMARY KEY ("Id")
);


CREATE TABLE "CorporateSetting" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Value" text not null default '',
    "Desc" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_CorporateSetting" PRIMARY KEY ("Id")
);


CREATE TABLE "CrossBinding" (
    "Id" citext COLLATE "C" not null default '',
    "TargetAppId" citext COLLATE "C" not null default '',
    "SourceAppId" citext COLLATE "C" not null default '',
    "SourceFormId" citext COLLATE "C" not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_CrossBinding" PRIMARY KEY ("Id")
);


CREATE TABLE "DashboardDef" (
    "Id" citext COLLATE "C" not null default '',
    "TemplateId" citext COLLATE "C",
    "AppId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Layout" citext not null default '',
    "AutoRefreshEnabled" boolean not null default false,
    "AutoRefreshIntervalMinutes" integer not null default 0,
    "MemberPublishEnabled" boolean not null default false,
    "PublishMembers" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_DashboardDef" PRIMARY KEY ("Id")
);


CREATE TABLE "DashboardItemDef" (
    "Id" citext COLLATE "C" not null default '',
    "TemplateId" citext COLLATE "C",
    "AppId" citext COLLATE "C" not null default '',
    "DashboardId" citext COLLATE "C" not null default '',
    "ItemType" citext not null default '',
    "LayoutId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Details" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_DashboardItemDef" PRIMARY KEY ("Id")
);


CREATE TABLE "DashboardItemTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "AppTemplateId" citext COLLATE "C" not null default '',
    "DashboardTemplateId" citext COLLATE "C" not null default '',
    "ItemType" citext not null default '',
    "LayoutId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Details" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_DashboardItemTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "DashboardTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "AppTemplateId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Layout" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_DashboardTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "Department" (
    "Id" citext COLLATE "C" not null default '',
    "Code" citext not null default '',
    "Name" citext not null default '',
    "IsCompany" boolean not null default false,
    "ParentId" citext COLLATE "C" not null default '',
    "ParentName" citext not null default '',
    "HeriarchyId" citext COLLATE "C" not null default '',
    "HeriarchyName" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Department" PRIMARY KEY ("Id")
);


CREATE TABLE "ECoinPrice" (
    "Id" citext COLLATE "C" not null default '',
    "TargetType" integer not null default 0,
    "FeatureId" citext COLLATE "C" not null default '',
    "FeatureDesc" citext not null default '',
    "Price" numeric not null default 0,
    "ChargeType" integer not null default 0,
    "PluginId" citext COLLATE "C" not null default '',
    CONSTRAINT "PK_ECoinPrice" PRIMARY KEY ("Id")
);


CREATE TABLE "Ef_RunLog" (
    "Id" citext COLLATE "C" not null default '',
    "CreateTime" bigint not null default 0,
    "AppId" citext COLLATE "C" not null default '',
    "EventFlowId" citext COLLATE "C" not null default '',
    "EventFlowName" citext not null default '',
    "EventFlowVersion" integer not null default 0,
    "WfInstanceId" citext COLLATE "C" not null default '',
    "TriggerKind" integer not null default 0,
    "EventSource" integer not null default 0,
    "EventType" integer not null default 0,
    "TriggerBy" jsonb,
    "TriggerTime" bigint not null default 0,
    "StartTime" bigint not null default 0,
    "EndTime" bigint,
    "Success" boolean not null default false,
    "ErrMsg" citext not null default '',
    "CreateBy" jsonb,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Ef_RunLog" PRIMARY KEY ("Id", "CreateTime")
) partition by range ("CreateTime");

create table "Ef_RunLog_2026_01" partition of "Ef_RunLog" for values from (1767225600000) to (1769904000000);
create table "Ef_RunLog_2026_02" partition of "Ef_RunLog" for values from (1769904000000) to (1772323200000);
create table "Ef_RunLog_2026_03" partition of "Ef_RunLog" for values from (1772323200000) to (1775001600000);
create table "Ef_RunLog_2026_04" partition of "Ef_RunLog" for values from (1775001600000) to (1777593600000);
create table "Ef_RunLog_2026_05" partition of "Ef_RunLog" for values from (1777593600000) to (1780272000000);
create table "Ef_RunLog_2026_06" partition of "Ef_RunLog" for values from (1780272000000) to (1782864000000);
create table "Ef_RunLog_2026_07" partition of "Ef_RunLog" for values from (1782864000000) to (1785542400000);
create table "Ef_RunLog_2026_08" partition of "Ef_RunLog" for values from (1785542400000) to (1788220800000);
create table "Ef_RunLog_2026_09" partition of "Ef_RunLog" for values from (1788220800000) to (1790812800000);
create table "Ef_RunLog_2026_10" partition of "Ef_RunLog" for values from (1790812800000) to (1793491200000);
create table "Ef_RunLog_2026_11" partition of "Ef_RunLog" for values from (1793491200000) to (1796083200000);
create table "Ef_RunLog_2026_12" partition of "Ef_RunLog" for values from (1796083200000) to (1798761600000);
create table "Ef_RunLog_2027_01" partition of "Ef_RunLog" for values from (1798761600000) to (1801440000000);
create table "Ef_RunLog_2027_02" partition of "Ef_RunLog" for values from (1801440000000) to (1803859200000);
create table "Ef_RunLog_2027_03" partition of "Ef_RunLog" for values from (1803859200000) to (1806537600000);
create table "Ef_RunLog_2027_04" partition of "Ef_RunLog" for values from (1806537600000) to (1809129600000);
create table "Ef_RunLog_2027_05" partition of "Ef_RunLog" for values from (1809129600000) to (1811808000000);
create table "Ef_RunLog_2027_06" partition of "Ef_RunLog" for values from (1811808000000) to (1814400000000);
create table "Ef_RunLog_2027_07" partition of "Ef_RunLog" for values from (1814400000000) to (1817078400000);
create table "Ef_RunLog_2027_08" partition of "Ef_RunLog" for values from (1817078400000) to (1819756800000);
create table "Ef_RunLog_2027_09" partition of "Ef_RunLog" for values from (1819756800000) to (1822348800000);
create table "Ef_RunLog_2027_10" partition of "Ef_RunLog" for values from (1822348800000) to (1825027200000);
create table "Ef_RunLog_2027_11" partition of "Ef_RunLog" for values from (1825027200000) to (1827619200000);
create table "Ef_RunLog_2027_12" partition of "Ef_RunLog" for values from (1827619200000) to (1830297600000);
create table "Ef_RunLog_default" partition of "Ef_RunLog" default;


CREATE TABLE "Ef_RunLogNode" (
    "Id" citext COLLATE "C" not null default '',
    "CreateTime" bigint not null default 0,
    "RunLogId" citext COLLATE "C" not null default '',
    "EventFlowId" citext COLLATE "C" not null default '',
    "WfInstanceId" citext COLLATE "C" not null default '',
    "DataId" citext COLLATE "C" not null default '',
    "NodeId" citext COLLATE "C" not null default '',
    "NodeName" citext not null default '',
    "NodeType" integer not null default 0,
    "StartTime" bigint not null default 0,
    "EndTime" bigint,
    "Success" boolean not null default false,
    "ErrMsg" citext not null default '',
    "FailureReason" citext not null default '',
    "TroubleshootingSuggestion" citext not null default '',
    "Summary" citext not null default '',
    "ExecTime" bigint not null default 0,
    "CreateBy" jsonb,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Ef_RunLogNode" PRIMARY KEY ("Id", "CreateTime")
) partition by range ("CreateTime");

create table "Ef_RunLogNode_2026_01" partition of "Ef_RunLogNode" for values from (1767225600000) to (1769904000000);
create table "Ef_RunLogNode_2026_02" partition of "Ef_RunLogNode" for values from (1769904000000) to (1772323200000);
create table "Ef_RunLogNode_2026_03" partition of "Ef_RunLogNode" for values from (1772323200000) to (1775001600000);
create table "Ef_RunLogNode_2026_04" partition of "Ef_RunLogNode" for values from (1775001600000) to (1777593600000);
create table "Ef_RunLogNode_2026_05" partition of "Ef_RunLogNode" for values from (1777593600000) to (1780272000000);
create table "Ef_RunLogNode_2026_06" partition of "Ef_RunLogNode" for values from (1780272000000) to (1782864000000);
create table "Ef_RunLogNode_2026_07" partition of "Ef_RunLogNode" for values from (1782864000000) to (1785542400000);
create table "Ef_RunLogNode_2026_08" partition of "Ef_RunLogNode" for values from (1785542400000) to (1788220800000);
create table "Ef_RunLogNode_2026_09" partition of "Ef_RunLogNode" for values from (1788220800000) to (1790812800000);
create table "Ef_RunLogNode_2026_10" partition of "Ef_RunLogNode" for values from (1790812800000) to (1793491200000);
create table "Ef_RunLogNode_2026_11" partition of "Ef_RunLogNode" for values from (1793491200000) to (1796083200000);
create table "Ef_RunLogNode_2026_12" partition of "Ef_RunLogNode" for values from (1796083200000) to (1798761600000);
create table "Ef_RunLogNode_2027_01" partition of "Ef_RunLogNode" for values from (1798761600000) to (1801440000000);
create table "Ef_RunLogNode_2027_02" partition of "Ef_RunLogNode" for values from (1801440000000) to (1803859200000);
create table "Ef_RunLogNode_2027_03" partition of "Ef_RunLogNode" for values from (1803859200000) to (1806537600000);
create table "Ef_RunLogNode_2027_04" partition of "Ef_RunLogNode" for values from (1806537600000) to (1809129600000);
create table "Ef_RunLogNode_2027_05" partition of "Ef_RunLogNode" for values from (1809129600000) to (1811808000000);
create table "Ef_RunLogNode_2027_06" partition of "Ef_RunLogNode" for values from (1811808000000) to (1814400000000);
create table "Ef_RunLogNode_2027_07" partition of "Ef_RunLogNode" for values from (1814400000000) to (1817078400000);
create table "Ef_RunLogNode_2027_08" partition of "Ef_RunLogNode" for values from (1817078400000) to (1819756800000);
create table "Ef_RunLogNode_2027_09" partition of "Ef_RunLogNode" for values from (1819756800000) to (1822348800000);
create table "Ef_RunLogNode_2027_10" partition of "Ef_RunLogNode" for values from (1822348800000) to (1825027200000);
create table "Ef_RunLogNode_2027_11" partition of "Ef_RunLogNode" for values from (1825027200000) to (1827619200000);
create table "Ef_RunLogNode_2027_12" partition of "Ef_RunLogNode" for values from (1827619200000) to (1830297600000);
create table "Ef_RunLogNode_default" partition of "Ef_RunLogNode" default;


CREATE TABLE "Employee" (
    "Id" citext COLLATE "C" not null default '',
    "UserId" citext COLLATE "C" not null default '',
    "UserName" citext not null default '',
    "Code" citext not null default '',
    "EmpName" citext not null default '',
    "WorkPhone" citext not null default '',
    "WorkEmail" citext not null default '',
    "Status" integer not null default 0,
    "IsDummy" boolean not null default false,
    "Invite" citext,
    "UserBound" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Employee" PRIMARY KEY ("Id")
);


CREATE TABLE "EmployeeDepartment" (
    "Id" citext COLLATE "C" not null default '',
    "EmployeeId" citext COLLATE "C" not null default '',
    "DepartmentId" citext COLLATE "C" not null default '',
    "IsManager" boolean not null default false,
    "SortValue" integer not null default 0,
    "HeriarchyId" citext COLLATE "C" not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_EmployeeDepartment" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EmployeeDepartment_Department_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Department" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_EmployeeDepartment_Employee_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employee" ("Id") ON DELETE CASCADE
);


CREATE TABLE "EmployeeGroup" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Description" citext not null default '',
    "EmployeeGroupCategoryId" citext COLLATE "C" not null default '',
    "SortValue" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_EmployeeGroup" PRIMARY KEY ("Id")
);


CREATE TABLE "EmployeeGroupCategory" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Description" citext not null default '',
    "SortValue" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_EmployeeGroupCategory" PRIMARY KEY ("Id")
);


CREATE TABLE "EmployeeGroupMember" (
    "Id" citext COLLATE "C" not null default '',
    "EmployeeId" citext COLLATE "C" not null default '',
    "EmployeeGroupId" citext COLLATE "C" not null default '',
    "EmployeeGroupName" citext not null default '',
    "SortValue" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_EmployeeGroupMember" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EmployeeGroupMember_Employee_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employee" ("Id") ON DELETE CASCADE
);


CREATE TABLE "EventFlowHookSample" (
    "Id" citext COLLATE "C" not null default '',
    "EventFlowId" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "ClientIp" citext not null default '',
    "RawJson" citext not null default '',
    "FlattenedFieldsJson" citext not null default '',
    "CapturedAt" bigint not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_EventFlowHookSample" PRIMARY KEY ("Id")
);


CREATE TABLE "EventFlowNodeExecution" (
    "Id" citext COLLATE "C" not null default '',
    "ExecutionKey" citext not null default '',
    "ExecutionId" citext COLLATE "C" not null default '',
    "WorkflowInstanceId" citext COLLATE "C" not null default '',
    "RunLogId" citext COLLATE "C" not null default '',
    "CorpId" citext COLLATE "C" not null default '',
    "EventFlowId" citext COLLATE "C" not null default '',
    "NodeId" citext COLLATE "C" not null default '',
    "ActionType" citext not null default '',
    "TargetKey" citext not null default '',
    "Ordinal" integer not null default 0,
    "FormId" citext COLLATE "C",
    "SingleResult" boolean not null default false,
    "Status" integer not null default 0,
    "ProcessingOwner" citext not null default '',
    "ProcessingStartedTime" bigint not null default 0,
    "LeaseUntil" bigint not null default 0,
    "AttemptCount" integer not null default 0,
    "ResultSnapshot" text not null default '',
    "CompletedTime" bigint not null default 0,
    CONSTRAINT "PK_EventFlowNodeExecution" PRIMARY KEY ("Id")
);


CREATE TABLE "EventFlowScheduleItem" (
    "Id" citext COLLATE "C" not null default '',
    "EventFlowId" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C",
    "DataId" citext COLLATE "C",
    "TriggerTime" bigint not null default 0,
    "AnchorTime" bigint not null default 0,
    "ScheduleVersion" bigint not null default 0,
    "SourceType" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_EventFlowScheduleItem" PRIMARY KEY ("Id")
);


CREATE TABLE "ExportLog" (
    "Id" citext COLLATE "C" not null default '',
    "ExportType" integer not null default 0,
    "RequestedFormat" integer not null default 0,
    "ActualFormat" integer not null default 0,
    "Status" integer not null default 0,
    "ColumnsJson" citext,
    "FilterJson" citext,
    "DedupKey" citext,
    "TotalCount" bigint not null default 0,
    "FileName" citext,
    "DownloadUrl" citext,
    "ErrorMessage" citext,
    "FinishTime" bigint,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_ExportLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormData" (
    "Id" citext COLLATE "C" not null default '',
    "CorpId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FlowStatus" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "Data" jsonb not null,
    CONSTRAINT "PK_FormData" PRIMARY KEY ("CorpId", "FormId", "Id")
) partition by hash ("CorpId");

create table "FormData_p00" partition of "FormData" for values with (modulus 16, remainder 0) partition by hash ("FormId");
create table "FormData_p00_h00" partition of "FormData_p00" for values with (modulus 8, remainder 0);
create table "FormData_p00_h01" partition of "FormData_p00" for values with (modulus 8, remainder 1);
create table "FormData_p00_h02" partition of "FormData_p00" for values with (modulus 8, remainder 2);
create table "FormData_p00_h03" partition of "FormData_p00" for values with (modulus 8, remainder 3);
create table "FormData_p00_h04" partition of "FormData_p00" for values with (modulus 8, remainder 4);
create table "FormData_p00_h05" partition of "FormData_p00" for values with (modulus 8, remainder 5);
create table "FormData_p00_h06" partition of "FormData_p00" for values with (modulus 8, remainder 6);
create table "FormData_p00_h07" partition of "FormData_p00" for values with (modulus 8, remainder 7);
create table "FormData_p01" partition of "FormData" for values with (modulus 16, remainder 1) partition by hash ("FormId");
create table "FormData_p01_h00" partition of "FormData_p01" for values with (modulus 8, remainder 0);
create table "FormData_p01_h01" partition of "FormData_p01" for values with (modulus 8, remainder 1);
create table "FormData_p01_h02" partition of "FormData_p01" for values with (modulus 8, remainder 2);
create table "FormData_p01_h03" partition of "FormData_p01" for values with (modulus 8, remainder 3);
create table "FormData_p01_h04" partition of "FormData_p01" for values with (modulus 8, remainder 4);
create table "FormData_p01_h05" partition of "FormData_p01" for values with (modulus 8, remainder 5);
create table "FormData_p01_h06" partition of "FormData_p01" for values with (modulus 8, remainder 6);
create table "FormData_p01_h07" partition of "FormData_p01" for values with (modulus 8, remainder 7);
create table "FormData_p02" partition of "FormData" for values with (modulus 16, remainder 2) partition by hash ("FormId");
create table "FormData_p02_h00" partition of "FormData_p02" for values with (modulus 8, remainder 0);
create table "FormData_p02_h01" partition of "FormData_p02" for values with (modulus 8, remainder 1);
create table "FormData_p02_h02" partition of "FormData_p02" for values with (modulus 8, remainder 2);
create table "FormData_p02_h03" partition of "FormData_p02" for values with (modulus 8, remainder 3);
create table "FormData_p02_h04" partition of "FormData_p02" for values with (modulus 8, remainder 4);
create table "FormData_p02_h05" partition of "FormData_p02" for values with (modulus 8, remainder 5);
create table "FormData_p02_h06" partition of "FormData_p02" for values with (modulus 8, remainder 6);
create table "FormData_p02_h07" partition of "FormData_p02" for values with (modulus 8, remainder 7);
create table "FormData_p03" partition of "FormData" for values with (modulus 16, remainder 3) partition by hash ("FormId");
create table "FormData_p03_h00" partition of "FormData_p03" for values with (modulus 8, remainder 0);
create table "FormData_p03_h01" partition of "FormData_p03" for values with (modulus 8, remainder 1);
create table "FormData_p03_h02" partition of "FormData_p03" for values with (modulus 8, remainder 2);
create table "FormData_p03_h03" partition of "FormData_p03" for values with (modulus 8, remainder 3);
create table "FormData_p03_h04" partition of "FormData_p03" for values with (modulus 8, remainder 4);
create table "FormData_p03_h05" partition of "FormData_p03" for values with (modulus 8, remainder 5);
create table "FormData_p03_h06" partition of "FormData_p03" for values with (modulus 8, remainder 6);
create table "FormData_p03_h07" partition of "FormData_p03" for values with (modulus 8, remainder 7);
create table "FormData_p04" partition of "FormData" for values with (modulus 16, remainder 4) partition by hash ("FormId");
create table "FormData_p04_h00" partition of "FormData_p04" for values with (modulus 8, remainder 0);
create table "FormData_p04_h01" partition of "FormData_p04" for values with (modulus 8, remainder 1);
create table "FormData_p04_h02" partition of "FormData_p04" for values with (modulus 8, remainder 2);
create table "FormData_p04_h03" partition of "FormData_p04" for values with (modulus 8, remainder 3);
create table "FormData_p04_h04" partition of "FormData_p04" for values with (modulus 8, remainder 4);
create table "FormData_p04_h05" partition of "FormData_p04" for values with (modulus 8, remainder 5);
create table "FormData_p04_h06" partition of "FormData_p04" for values with (modulus 8, remainder 6);
create table "FormData_p04_h07" partition of "FormData_p04" for values with (modulus 8, remainder 7);
create table "FormData_p05" partition of "FormData" for values with (modulus 16, remainder 5) partition by hash ("FormId");
create table "FormData_p05_h00" partition of "FormData_p05" for values with (modulus 8, remainder 0);
create table "FormData_p05_h01" partition of "FormData_p05" for values with (modulus 8, remainder 1);
create table "FormData_p05_h02" partition of "FormData_p05" for values with (modulus 8, remainder 2);
create table "FormData_p05_h03" partition of "FormData_p05" for values with (modulus 8, remainder 3);
create table "FormData_p05_h04" partition of "FormData_p05" for values with (modulus 8, remainder 4);
create table "FormData_p05_h05" partition of "FormData_p05" for values with (modulus 8, remainder 5);
create table "FormData_p05_h06" partition of "FormData_p05" for values with (modulus 8, remainder 6);
create table "FormData_p05_h07" partition of "FormData_p05" for values with (modulus 8, remainder 7);
create table "FormData_p06" partition of "FormData" for values with (modulus 16, remainder 6) partition by hash ("FormId");
create table "FormData_p06_h00" partition of "FormData_p06" for values with (modulus 8, remainder 0);
create table "FormData_p06_h01" partition of "FormData_p06" for values with (modulus 8, remainder 1);
create table "FormData_p06_h02" partition of "FormData_p06" for values with (modulus 8, remainder 2);
create table "FormData_p06_h03" partition of "FormData_p06" for values with (modulus 8, remainder 3);
create table "FormData_p06_h04" partition of "FormData_p06" for values with (modulus 8, remainder 4);
create table "FormData_p06_h05" partition of "FormData_p06" for values with (modulus 8, remainder 5);
create table "FormData_p06_h06" partition of "FormData_p06" for values with (modulus 8, remainder 6);
create table "FormData_p06_h07" partition of "FormData_p06" for values with (modulus 8, remainder 7);
create table "FormData_p07" partition of "FormData" for values with (modulus 16, remainder 7) partition by hash ("FormId");
create table "FormData_p07_h00" partition of "FormData_p07" for values with (modulus 8, remainder 0);
create table "FormData_p07_h01" partition of "FormData_p07" for values with (modulus 8, remainder 1);
create table "FormData_p07_h02" partition of "FormData_p07" for values with (modulus 8, remainder 2);
create table "FormData_p07_h03" partition of "FormData_p07" for values with (modulus 8, remainder 3);
create table "FormData_p07_h04" partition of "FormData_p07" for values with (modulus 8, remainder 4);
create table "FormData_p07_h05" partition of "FormData_p07" for values with (modulus 8, remainder 5);
create table "FormData_p07_h06" partition of "FormData_p07" for values with (modulus 8, remainder 6);
create table "FormData_p07_h07" partition of "FormData_p07" for values with (modulus 8, remainder 7);
create table "FormData_p08" partition of "FormData" for values with (modulus 16, remainder 8) partition by hash ("FormId");
create table "FormData_p08_h00" partition of "FormData_p08" for values with (modulus 8, remainder 0);
create table "FormData_p08_h01" partition of "FormData_p08" for values with (modulus 8, remainder 1);
create table "FormData_p08_h02" partition of "FormData_p08" for values with (modulus 8, remainder 2);
create table "FormData_p08_h03" partition of "FormData_p08" for values with (modulus 8, remainder 3);
create table "FormData_p08_h04" partition of "FormData_p08" for values with (modulus 8, remainder 4);
create table "FormData_p08_h05" partition of "FormData_p08" for values with (modulus 8, remainder 5);
create table "FormData_p08_h06" partition of "FormData_p08" for values with (modulus 8, remainder 6);
create table "FormData_p08_h07" partition of "FormData_p08" for values with (modulus 8, remainder 7);
create table "FormData_p09" partition of "FormData" for values with (modulus 16, remainder 9) partition by hash ("FormId");
create table "FormData_p09_h00" partition of "FormData_p09" for values with (modulus 8, remainder 0);
create table "FormData_p09_h01" partition of "FormData_p09" for values with (modulus 8, remainder 1);
create table "FormData_p09_h02" partition of "FormData_p09" for values with (modulus 8, remainder 2);
create table "FormData_p09_h03" partition of "FormData_p09" for values with (modulus 8, remainder 3);
create table "FormData_p09_h04" partition of "FormData_p09" for values with (modulus 8, remainder 4);
create table "FormData_p09_h05" partition of "FormData_p09" for values with (modulus 8, remainder 5);
create table "FormData_p09_h06" partition of "FormData_p09" for values with (modulus 8, remainder 6);
create table "FormData_p09_h07" partition of "FormData_p09" for values with (modulus 8, remainder 7);
create table "FormData_p10" partition of "FormData" for values with (modulus 16, remainder 10) partition by hash ("FormId");
create table "FormData_p10_h00" partition of "FormData_p10" for values with (modulus 8, remainder 0);
create table "FormData_p10_h01" partition of "FormData_p10" for values with (modulus 8, remainder 1);
create table "FormData_p10_h02" partition of "FormData_p10" for values with (modulus 8, remainder 2);
create table "FormData_p10_h03" partition of "FormData_p10" for values with (modulus 8, remainder 3);
create table "FormData_p10_h04" partition of "FormData_p10" for values with (modulus 8, remainder 4);
create table "FormData_p10_h05" partition of "FormData_p10" for values with (modulus 8, remainder 5);
create table "FormData_p10_h06" partition of "FormData_p10" for values with (modulus 8, remainder 6);
create table "FormData_p10_h07" partition of "FormData_p10" for values with (modulus 8, remainder 7);
create table "FormData_p11" partition of "FormData" for values with (modulus 16, remainder 11) partition by hash ("FormId");
create table "FormData_p11_h00" partition of "FormData_p11" for values with (modulus 8, remainder 0);
create table "FormData_p11_h01" partition of "FormData_p11" for values with (modulus 8, remainder 1);
create table "FormData_p11_h02" partition of "FormData_p11" for values with (modulus 8, remainder 2);
create table "FormData_p11_h03" partition of "FormData_p11" for values with (modulus 8, remainder 3);
create table "FormData_p11_h04" partition of "FormData_p11" for values with (modulus 8, remainder 4);
create table "FormData_p11_h05" partition of "FormData_p11" for values with (modulus 8, remainder 5);
create table "FormData_p11_h06" partition of "FormData_p11" for values with (modulus 8, remainder 6);
create table "FormData_p11_h07" partition of "FormData_p11" for values with (modulus 8, remainder 7);
create table "FormData_p12" partition of "FormData" for values with (modulus 16, remainder 12) partition by hash ("FormId");
create table "FormData_p12_h00" partition of "FormData_p12" for values with (modulus 8, remainder 0);
create table "FormData_p12_h01" partition of "FormData_p12" for values with (modulus 8, remainder 1);
create table "FormData_p12_h02" partition of "FormData_p12" for values with (modulus 8, remainder 2);
create table "FormData_p12_h03" partition of "FormData_p12" for values with (modulus 8, remainder 3);
create table "FormData_p12_h04" partition of "FormData_p12" for values with (modulus 8, remainder 4);
create table "FormData_p12_h05" partition of "FormData_p12" for values with (modulus 8, remainder 5);
create table "FormData_p12_h06" partition of "FormData_p12" for values with (modulus 8, remainder 6);
create table "FormData_p12_h07" partition of "FormData_p12" for values with (modulus 8, remainder 7);
create table "FormData_p13" partition of "FormData" for values with (modulus 16, remainder 13) partition by hash ("FormId");
create table "FormData_p13_h00" partition of "FormData_p13" for values with (modulus 8, remainder 0);
create table "FormData_p13_h01" partition of "FormData_p13" for values with (modulus 8, remainder 1);
create table "FormData_p13_h02" partition of "FormData_p13" for values with (modulus 8, remainder 2);
create table "FormData_p13_h03" partition of "FormData_p13" for values with (modulus 8, remainder 3);
create table "FormData_p13_h04" partition of "FormData_p13" for values with (modulus 8, remainder 4);
create table "FormData_p13_h05" partition of "FormData_p13" for values with (modulus 8, remainder 5);
create table "FormData_p13_h06" partition of "FormData_p13" for values with (modulus 8, remainder 6);
create table "FormData_p13_h07" partition of "FormData_p13" for values with (modulus 8, remainder 7);
create table "FormData_p14" partition of "FormData" for values with (modulus 16, remainder 14) partition by hash ("FormId");
create table "FormData_p14_h00" partition of "FormData_p14" for values with (modulus 8, remainder 0);
create table "FormData_p14_h01" partition of "FormData_p14" for values with (modulus 8, remainder 1);
create table "FormData_p14_h02" partition of "FormData_p14" for values with (modulus 8, remainder 2);
create table "FormData_p14_h03" partition of "FormData_p14" for values with (modulus 8, remainder 3);
create table "FormData_p14_h04" partition of "FormData_p14" for values with (modulus 8, remainder 4);
create table "FormData_p14_h05" partition of "FormData_p14" for values with (modulus 8, remainder 5);
create table "FormData_p14_h06" partition of "FormData_p14" for values with (modulus 8, remainder 6);
create table "FormData_p14_h07" partition of "FormData_p14" for values with (modulus 8, remainder 7);
create table "FormData_p15" partition of "FormData" for values with (modulus 16, remainder 15) partition by hash ("FormId");
create table "FormData_p15_h00" partition of "FormData_p15" for values with (modulus 8, remainder 0);
create table "FormData_p15_h01" partition of "FormData_p15" for values with (modulus 8, remainder 1);
create table "FormData_p15_h02" partition of "FormData_p15" for values with (modulus 8, remainder 2);
create table "FormData_p15_h03" partition of "FormData_p15" for values with (modulus 8, remainder 3);
create table "FormData_p15_h04" partition of "FormData_p15" for values with (modulus 8, remainder 4);
create table "FormData_p15_h05" partition of "FormData_p15" for values with (modulus 8, remainder 5);
create table "FormData_p15_h06" partition of "FormData_p15" for values with (modulus 8, remainder 6);
create table "FormData_p15_h07" partition of "FormData_p15" for values with (modulus 8, remainder 7);


CREATE TABLE "FormDataChangeLog" (
    "Id" citext COLLATE "C" not null default '',
    "CorpId" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "DataId" citext COLLATE "C" not null default '',
    "Operator" jsonb,
    "OperateTime" bigint not null default 0,
    "Content" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_FormDataChangeLog" PRIMARY KEY ("CorpId", "Id")
) partition by hash ("CorpId");

create table "FormDataChangeLog_p00" partition of "FormDataChangeLog" for values with (modulus 16, remainder 0);
create table "FormDataChangeLog_p01" partition of "FormDataChangeLog" for values with (modulus 16, remainder 1);
create table "FormDataChangeLog_p02" partition of "FormDataChangeLog" for values with (modulus 16, remainder 2);
create table "FormDataChangeLog_p03" partition of "FormDataChangeLog" for values with (modulus 16, remainder 3);
create table "FormDataChangeLog_p04" partition of "FormDataChangeLog" for values with (modulus 16, remainder 4);
create table "FormDataChangeLog_p05" partition of "FormDataChangeLog" for values with (modulus 16, remainder 5);
create table "FormDataChangeLog_p06" partition of "FormDataChangeLog" for values with (modulus 16, remainder 6);
create table "FormDataChangeLog_p07" partition of "FormDataChangeLog" for values with (modulus 16, remainder 7);
create table "FormDataChangeLog_p08" partition of "FormDataChangeLog" for values with (modulus 16, remainder 8);
create table "FormDataChangeLog_p09" partition of "FormDataChangeLog" for values with (modulus 16, remainder 9);
create table "FormDataChangeLog_p10" partition of "FormDataChangeLog" for values with (modulus 16, remainder 10);
create table "FormDataChangeLog_p11" partition of "FormDataChangeLog" for values with (modulus 16, remainder 11);
create table "FormDataChangeLog_p12" partition of "FormDataChangeLog" for values with (modulus 16, remainder 12);
create table "FormDataChangeLog_p13" partition of "FormDataChangeLog" for values with (modulus 16, remainder 13);
create table "FormDataChangeLog_p14" partition of "FormDataChangeLog" for values with (modulus 16, remainder 14);
create table "FormDataChangeLog_p15" partition of "FormDataChangeLog" for values with (modulus 16, remainder 15);


CREATE TABLE "FormDataImportLog" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "FormName" citext,
    "PermissionGroupId" citext COLLATE "C",
    "FormUsingWorkflow" boolean not null default false,
    "Mode" integer not null default 0,
    "TriggerValidation" boolean not null default false,
    "TriggerWorkflow" boolean not null default false,
    "ImportAction" integer not null default 0,
    "Status" integer not null default 0,
    "MatchField" citext,
    "SheetName" citext not null default '',
    "HeaderRowIndex" integer not null default 0,
    "SourceFileName" citext not null default '',
    "SourceObjectKey" citext not null default '',
    "SourceFileSize" bigint not null default 0,
    "FieldSnapshotJson" citext not null default '',
    "MappingJson" citext not null default '',
    "DataScopeFilterJson" citext,
    "TotalCount" bigint not null default 0,
    "ProcessedCount" bigint not null default 0,
    "AddCount" bigint not null default 0,
    "UpdateCount" bigint not null default 0,
    "FailedCount" bigint not null default 0,
    "ErrorReportFileName" citext,
    "ErrorReportObjectKey" citext,
    "ErrorReportDownloadUrl" citext,
    "EditableErrorRowsJson" citext,
    "EditableErrorRowsObjectKey" citext,
    "EditableErrorRowCount" integer not null default 0,
    "ErrorMessage" citext,
    "RetryCount" integer not null default 0,
    "StartTime" bigint,
    "ProcessingExpireTime" bigint,
    "FinishTime" bigint,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormDataImportLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDataPermissionGroup" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Desc" citext not null default '',
    "Type" integer not null default 0,
    "Members" jsonb not null,
    "FormDataPermissions" bigint not null default 0,
    "DataFilter" citext,
    "FormFieldPermissions" jsonb not null,
    "Disabled" boolean not null default false,
    "TemplateId" citext COLLATE "C",
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormDataPermissionGroup" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDataPermissionGroupTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "AppTemplateId" citext COLLATE "C" not null default '',
    "FormTemplateId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Desc" citext not null default '',
    "Type" integer not null default 0,
    "FormDataPermissions" bigint not null default 0,
    "DataFilter" citext,
    "FormFieldPermissions" jsonb not null,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_FormDataPermissionGroupTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDef" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "TemplateId" citext COLLATE "C",
    "Name" citext not null default '',
    "Content" jsonb not null,
    "UsingWorkflow" boolean not null default false,
    "FormSettings" jsonb not null,
    "PublicRelatedFormIds" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormDef" PRIMARY KEY ("Id")
);


CREATE TABLE "FormListView" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "PcType" integer not null default 0,
    "MobileType" integer not null default 0,
    "SortIndex" integer not null default 0,
    "PermissionGroupIds" citext[] not null,
    "Settings" citext not null default '',
    "DefaultFilter" citext,
    "DefaultSort" citext,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormListView" PRIMARY KEY ("Id")
);


CREATE TABLE "FormNotify" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "TargetType" integer not null default 0,
    "TimeField" citext,
    "FixedTime" citext,
    "Direction" integer not null default 0,
    "OffsetValue" integer,
    "OffsetUnit" integer,
    "FieldFormat" citext,
    "StartTime" bigint,
    "EndTime" bigint,
    "RepeatType" integer,
    "RepeatConfig" citext,
    "NextTriggerTime" bigint,
    "LastTriggerTime" bigint,
    "ScheduleVersion" bigint not null default 0,
    "TriggerMode" integer not null default 0,
    "ChangeFields" text[],
    "DataFilter" citext,
    "DataDynamicFilter" citext,
    "DataExpressFilter" citext,
    "NotifyText" citext,
    "Notifiers" citext,
    "Channels" bigint not null default 0,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormNotify" PRIMARY KEY ("Id")
);


CREATE TABLE "FormNotifyDispatchLog" (
    "Id" citext COLLATE "C" not null default '',
    "NotifyId" citext COLLATE "C" not null default '',
    "DataId" citext COLLATE "C",
    "TriggerTime" bigint not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormNotifyDispatchLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormNotifyScheduleItem" (
    "Id" citext COLLATE "C" not null default '',
    "NotifyId" citext COLLATE "C" not null default '',
    "DataId" citext COLLATE "C",
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "TargetType" integer not null default 0,
    "TriggerMode" integer not null default 0,
    "ScheduleVersion" bigint not null default 0,
    "TriggerTime" bigint not null default 0,
    "AnchorTime" bigint not null default 0,
    "TimeField" citext,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormNotifyScheduleItem" PRIMARY KEY ("Id")
);


CREATE TABLE "FormTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Type" integer not null default 0,
    "Icon" citext not null default '',
    "AppTemplateId" citext COLLATE "C" not null default '',
    "Content" jsonb not null,
    "UsingWorkflow" boolean not null default false,
    "FormSettings" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_FormTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "IdentityLoginAudit" (
    "Id" citext COLLATE "C" not null default '',
    "LoginId" citext COLLATE "C",
    "GrantType" citext,
    "UserId" citext COLLATE "C",
    "ClientId" citext COLLATE "C",
    "UserName" citext,
    "ClientIp" citext,
    "FailReason" citext,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_IdentityLoginAudit" PRIMARY KEY ("Id")
);


CREATE TABLE "OutboxMessage" (
    "Id" citext COLLATE "C" not null default '',
    "QueueName" citext not null default '',
    "MessageType" citext not null default '',
    "IdempotencyKey" citext not null default '',
    "Payload" citext not null default '',
    "Status" integer not null default 0,
    "OutAt" bigint not null default 0,
    "Attempt" integer not null default 0,
    "LastAttemptTime" bigint,
    "Error" citext,
    "SentTime" bigint,
    "SentAt" timestamp with time zone,
    CONSTRAINT "PK_OutboxMessage" PRIMARY KEY ("Id")
);


CREATE TABLE "Payment" (
    "Id" citext COLLATE "C" not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Payment" PRIMARY KEY ("Id")
);


CREATE TABLE "PluginInstall" (
    "Id" citext COLLATE "C" not null default '',
    "PluginId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Summary" citext not null default '',
    "Icon" citext not null default '',
    "Status" integer not null default 0,
    "Enabled" boolean not null default false,
    "InstalledAt" bigint not null default 0,
    "InstalledBy" jsonb,
    "LastEnabledAt" bigint,
    "LastDisabledAt" bigint,
    "UninstalledAt" bigint,
    "Settings" citext,
    "Source" citext not null default '',
    "OrderNo" citext not null default '',
    "ExpireAt" bigint,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_PluginInstall" PRIMARY KEY ("Id")
);


CREATE TABLE "PluginProfile" (
    "Id" citext COLLATE "C" not null default '',
    "PluginId" citext COLLATE "C" not null default '',
    "Version" citext not null default '',
    "Name" citext not null default '',
    "Summary" citext not null default '',
    "Description" citext not null default '',
    "Icon" citext not null default '',
    "CoverImage" citext not null default '',
    "BannerImage" citext not null default '',
    "GalleryImages" text[] not null,
    "Category" citext not null default '',
    "Scenario" citext not null default '',
    "Tags" text[] not null,
    "DeveloperName" citext not null default '',
    "DeveloperCorpId" citext COLLATE "C" not null default '',
    "IsOfficial" boolean not null default false,
    "IsHot" boolean not null default false,
    "IsRecommended" boolean not null default false,
    "InstallCount" bigint not null default 0,
    "SortIndex" integer not null default 0,
    "Status" citext not null default '',
    "PublishedAt" timestamp with time zone,
    "HelpDocUrl" citext not null default '',
    "TemplateUrl" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_PluginProfile" PRIMARY KEY ("Id")
);


CREATE TABLE "PrintDef" (
    "Id" citext COLLATE "C" not null default '',
    "TemplateId" citext COLLATE "C",
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Content" text not null default '',
    "PrintType" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_PrintDef" PRIMARY KEY ("Id")
);


CREATE TABLE "PrintDefTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "AppTemplateId" citext COLLATE "C" not null default '',
    "FormTemplateId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Content" citext not null default '',
    "PrintType" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_PrintDefTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "ProcessedMessage" (
    "Id" citext COLLATE "C" not null default '',
    "EventKey" citext not null default '',
    "Target" citext not null default '',
    "Status" integer not null default 0,
    "LeaseUntil" timestamp with time zone,
    "LeaseToken" citext,
    "ProcessedTime" bigint,
    "ProcessedAt" timestamp with time zone,
    CONSTRAINT "PK_ProcessedMessage" PRIMARY KEY ("Id")
);


CREATE TABLE "PublicSetting" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "TargetType" integer not null default 0,
    "TargetId" citext COLLATE "C" not null default '',
    "Form" jsonb not null,
    "Dashboard" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_PublicSetting" PRIMARY KEY ("Id")
);


CREATE TABLE "SerialNoSequence" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "Key" citext not null default '',
    "CurrDate" timestamp with time zone,
    "CurrId" integer,
    "SerialNoType" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_SerialNoSequence" PRIMARY KEY ("Id")
);


CREATE TABLE "SystemMessage" (
    "Id" citext COLLATE "C" not null default '',
    "NotifyId" citext COLLATE "C",
    "Title" citext,
    "Detail" citext,
    "Url" citext,
    "ReceiverEmpId" citext COLLATE "C",
    "ReceiverName" citext,
    "IsRead" boolean not null default false,
    "ReadTime" bigint,
    "ExpireTime" bigint not null default 0,
    "Category" integer not null default 0,
    "MessageType" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_SystemMessage" PRIMARY KEY ("Id")
);


CREATE TABLE "TenantAdminGroup" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Description" citext not null default '',
    "Type" integer not null default 0,
    "ParentId" citext COLLATE "C" not null default '',
    "SortValue" integer not null default 0,
    "EmployeeIds" jsonb not null,
    "AppIds" citext[] not null,
    "CanCreateOrDeleteApp" boolean not null default false,
    "AppDepartmentScopeMode" integer not null default 0,
    "AppDepartmentIds" citext[] not null,
    "AppEmployeeGroupScopeMode" integer not null default 0,
    "AppEmployeeGroupIds" citext[] not null,
    "ContactDepartmentPermission" integer not null default 0,
    "ContactDepartmentScopeMode" integer not null default 0,
    "ContactDepartmentIds" citext[] not null,
    "ContactEmployeeGroupPermission" integer not null default 0,
    "ContactEmployeeGroupScopeMode" integer not null default 0,
    "ContactEmployeeGroupIds" citext[] not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_TenantAdminGroup" PRIMARY KEY ("Id")
);


CREATE TABLE "UploadedFile" (
    "Id" citext COLLATE "C" not null default '',
    "FileName" citext not null default '',
    "SavePath" citext not null default '',
    "ThumbPath" citext,
    "FileExt" citext,
    "FileSize" bigint not null default 0,
    "RefCount" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_UploadedFile" PRIMARY KEY ("Id")
);


CREATE TABLE "User" (
    "Id" citext COLLATE "C" not null default '',
    "CreateTime" bigint not null default 0,
    "Name" citext not null default '',
    "Email" citext not null default '',
    "Phone" citext not null default '',
    "Password" text not null default '',
    "Platform" integer not null default 0,
    "Disabled" boolean not null default false,
    "Avatar" citext,
    "UserType" citext,
    CONSTRAINT "PK_User" PRIMARY KEY ("Id")
);


CREATE TABLE "UserCorp" (
    "Id" citext COLLATE "C" not null default '',
    "UserId" citext COLLATE "C" not null default '',
    "CorpId" citext COLLATE "C" not null default '',
    "IsCorpOwner" boolean not null default false,
    "CorpType" citext not null default '',
    "IsDefault" boolean not null default false,
    CONSTRAINT "PK_UserCorp" PRIMARY KEY ("Id")
);


CREATE TABLE "WebPushLog" (
    "Id" citext COLLATE "C" not null default '',
    "CreateTime" bigint not null default 0,
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "WebHookId" citext COLLATE "C" not null default '',
    "SourceType" integer not null default 0,
    "TriggerType" citext not null default '',
    "Url" citext not null default '',
    "EventId" citext COLLATE "C" not null default '',
    "PushObject" citext,
    "HttpCode" integer not null default 0,
    "PushResult" citext,
    "Success" boolean not null default false,
    "CreateBy" jsonb,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_WebPushLog" PRIMARY KEY ("Id", "CreateTime")
) partition by range ("CreateTime");

create table "WebPushLog_2026_01" partition of "WebPushLog" for values from (1767225600000) to (1769904000000);
create table "WebPushLog_2026_02" partition of "WebPushLog" for values from (1769904000000) to (1772323200000);
create table "WebPushLog_2026_03" partition of "WebPushLog" for values from (1772323200000) to (1775001600000);
create table "WebPushLog_2026_04" partition of "WebPushLog" for values from (1775001600000) to (1777593600000);
create table "WebPushLog_2026_05" partition of "WebPushLog" for values from (1777593600000) to (1780272000000);
create table "WebPushLog_2026_06" partition of "WebPushLog" for values from (1780272000000) to (1782864000000);
create table "WebPushLog_2026_07" partition of "WebPushLog" for values from (1782864000000) to (1785542400000);
create table "WebPushLog_2026_08" partition of "WebPushLog" for values from (1785542400000) to (1788220800000);
create table "WebPushLog_2026_09" partition of "WebPushLog" for values from (1788220800000) to (1790812800000);
create table "WebPushLog_2026_10" partition of "WebPushLog" for values from (1790812800000) to (1793491200000);
create table "WebPushLog_2026_11" partition of "WebPushLog" for values from (1793491200000) to (1796083200000);
create table "WebPushLog_2026_12" partition of "WebPushLog" for values from (1796083200000) to (1798761600000);
create table "WebPushLog_2027_01" partition of "WebPushLog" for values from (1798761600000) to (1801440000000);
create table "WebPushLog_2027_02" partition of "WebPushLog" for values from (1801440000000) to (1803859200000);
create table "WebPushLog_2027_03" partition of "WebPushLog" for values from (1803859200000) to (1806537600000);
create table "WebPushLog_2027_04" partition of "WebPushLog" for values from (1806537600000) to (1809129600000);
create table "WebPushLog_2027_05" partition of "WebPushLog" for values from (1809129600000) to (1811808000000);
create table "WebPushLog_2027_06" partition of "WebPushLog" for values from (1811808000000) to (1814400000000);
create table "WebPushLog_2027_07" partition of "WebPushLog" for values from (1814400000000) to (1817078400000);
create table "WebPushLog_2027_08" partition of "WebPushLog" for values from (1817078400000) to (1819756800000);
create table "WebPushLog_2027_09" partition of "WebPushLog" for values from (1819756800000) to (1822348800000);
create table "WebPushLog_2027_10" partition of "WebPushLog" for values from (1822348800000) to (1825027200000);
create table "WebPushLog_2027_11" partition of "WebPushLog" for values from (1825027200000) to (1827619200000);
create table "WebPushLog_2027_12" partition of "WebPushLog" for values from (1827619200000) to (1830297600000);
create table "WebPushLog_default" partition of "WebPushLog" default;


CREATE TABLE "Webhook" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "Name" citext,
    "SourceType" integer not null default 0,
    "Url" citext not null default '',
    "Secret" text not null default '',
    "Remark" citext,
    "Triggers" bigint not null default 0,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Webhook" PRIMARY KEY ("Id")
);


CREATE TABLE "WebhookAlias" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "FieldAlias" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_WebhookAlias" PRIMARY KEY ("Id")
);


CREATE TABLE "WfDefinitionTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "AppTemplateId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "FlowType" integer not null default 0,
    "ExternalTemplateId" citext COLLATE "C" not null default '',
    "Description" citext not null default '',
    "Content" citext not null default '',
    "Metadata" jsonb not null,
    "EventSource" integer not null default 0,
    "SourceTemplateId" citext COLLATE "C",
    "EventSetting" jsonb,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_WfDefinitionTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "Wf_Definition" (
    "Id" citext COLLATE "C" not null default '',
    "TemplateId" citext COLLATE "C",
    "AppId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "FlowType" integer not null default 0,
    "ExternalId" citext COLLATE "C" not null default '',
    "Description" citext not null default '',
    "Version" integer not null default 0,
    "IsCurrent" boolean not null default false,
    "Released" boolean not null default false,
    "Content" text not null default '',
    "Metadata" jsonb not null,
    "EventSource" integer not null default 0,
    "SourceId" citext COLLATE "C",
    "EventSetting" jsonb,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Wf_Definition" PRIMARY KEY ("Id")
);


CREATE TABLE "Wf_ExecLog" (
    "Id" citext COLLATE "C" not null default '',
    "WfInstanceId" citext COLLATE "C" not null default '',
    "DataId" citext COLLATE "C" not null default '',
    "NodeId" citext COLLATE "C" not null default '',
    "EmpId" citext COLLATE "C" not null default '',
    "Success" boolean not null default false,
    "ErrMsg" citext not null default '',
    "ExecTime" bigint not null default 0,
    CONSTRAINT "PK_Wf_ExecLog" PRIMARY KEY ("Id")
);


CREATE TABLE "Wf_Task" (
    "Id" citext COLLATE "C" not null default '',
    "FormName" citext,
    "WfInstanceId" citext COLLATE "C" not null default '',
    "ApproveNodeId" citext COLLATE "C" not null default '',
    "ApproveNodeName" citext not null default '',
    "EmployeeId" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "DataId" citext COLLATE "C" not null default '',
    "FormType" integer not null default 0,
    "Starter" jsonb,
    "ApproveNodeStartTime" bigint not null default 0,
    "DataBrief" jsonb not null,
    "ExpireTime" bigint,
    "ExpireHandled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Wf_Task" PRIMARY KEY ("Id")
);


CREATE TABLE "Wf_TaskLog" (
    "Id" citext COLLATE "C" not null default '',
    "CorpId" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "FormName" citext not null default '',
    "DataId" citext COLLATE "C" not null default '',
    "WfInstanceId" citext COLLATE "C" not null default '',
    "WfVersion" integer not null default 0,
    "NodeId" citext COLLATE "C" not null default '',
    "NodeName" citext not null default '',
    "NodeType" integer not null default 0,
    "Round" integer not null default 0,
    "Approver" jsonb,
    "Result" integer not null default 0,
    "Comment" citext,
    "Signature" citext,
    "ApprovalTime" bigint not null default 0,
    "DataBrief" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    CONSTRAINT "PK_Wf_TaskLog" PRIMARY KEY ("CorpId", "Id")
) partition by hash ("CorpId");

create table "Wf_TaskLog_p00" partition of "Wf_TaskLog" for values with (modulus 16, remainder 0);
create table "Wf_TaskLog_p01" partition of "Wf_TaskLog" for values with (modulus 16, remainder 1);
create table "Wf_TaskLog_p02" partition of "Wf_TaskLog" for values with (modulus 16, remainder 2);
create table "Wf_TaskLog_p03" partition of "Wf_TaskLog" for values with (modulus 16, remainder 3);
create table "Wf_TaskLog_p04" partition of "Wf_TaskLog" for values with (modulus 16, remainder 4);
create table "Wf_TaskLog_p05" partition of "Wf_TaskLog" for values with (modulus 16, remainder 5);
create table "Wf_TaskLog_p06" partition of "Wf_TaskLog" for values with (modulus 16, remainder 6);
create table "Wf_TaskLog_p07" partition of "Wf_TaskLog" for values with (modulus 16, remainder 7);
create table "Wf_TaskLog_p08" partition of "Wf_TaskLog" for values with (modulus 16, remainder 8);
create table "Wf_TaskLog_p09" partition of "Wf_TaskLog" for values with (modulus 16, remainder 9);
create table "Wf_TaskLog_p10" partition of "Wf_TaskLog" for values with (modulus 16, remainder 10);
create table "Wf_TaskLog_p11" partition of "Wf_TaskLog" for values with (modulus 16, remainder 11);
create table "Wf_TaskLog_p12" partition of "Wf_TaskLog" for values with (modulus 16, remainder 12);
create table "Wf_TaskLog_p13" partition of "Wf_TaskLog" for values with (modulus 16, remainder 13);
create table "Wf_TaskLog_p14" partition of "Wf_TaskLog" for values with (modulus 16, remainder 14);
create table "Wf_TaskLog_p15" partition of "Wf_TaskLog" for values with (modulus 16, remainder 15);


CREATE TABLE "WorkbenchConfig" (
    "Id" citext COLLATE "C" not null default '',
    "EmployeeId" citext COLLATE "C" not null default '',
    "Layout" text not null default '',
    "PageStyle" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_WorkbenchConfig" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkbenchFavorite" (
    "Id" citext COLLATE "C" not null default '',
    "EmployeeId" citext COLLATE "C" not null default '',
    "TargetType" integer not null default 0,
    "TargetId" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "Title" citext not null default '',
    "Icon" citext not null default '',
    "IconColor" citext not null default '',
    "SortIndex" bigint not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_WorkbenchFavorite" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkbenchRecentVisit" (
    "Id" citext COLLATE "C" not null default '',
    "EmployeeId" citext COLLATE "C" not null default '',
    "TargetType" integer not null default 0,
    "TargetId" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "Title" citext not null default '',
    "Icon" citext not null default '',
    "IconColor" citext not null default '',
    "LastVisitTime" bigint not null default 0,
    "VisitCount" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "DeleteBy" jsonb,
    "DeleteTime" bigint,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_WorkbenchRecentVisit" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkflowTransitionExecution" (
    "Id" citext COLLATE "C" not null default '',
    "ExecutionId" citext COLLATE "C" not null default '',
    "WorkflowInstanceId" citext COLLATE "C" not null default '',
    "CorpId" citext COLLATE "C" not null default '',
    "WfNodeId" citext COLLATE "C" not null default '',
    "NodeAction" citext not null default '',
    "Status" integer not null default 0,
    "Error" text not null default '',
    "CreateTime" bigint not null default 0,
    "UpdateTime" bigint not null default 0,
    CONSTRAINT "PK_WorkflowTransitionExecution" PRIMARY KEY ("Id")
);
