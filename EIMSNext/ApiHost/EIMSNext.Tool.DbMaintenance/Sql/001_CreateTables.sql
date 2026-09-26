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
    "Status" citext not null default '',
    "PublishedAt" timestamp with time zone,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
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
    CONSTRAINT "PK_AppTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "AuditLog" (
    "Id" citext COLLATE "C" not null default '',
    "Action" citext not null default '',
    "EntityType" citext,
    "DataId" citext COLLATE "C",
    "Detail" citext,
    "OldData" citext,
    "NewData" citext,
    "DataFilter" citext,
    "UpdateExp" citext,
    "ClientIp" citext,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_AuditLog" PRIMARY KEY ("Id")
);


CREATE TABLE "Client" (
    "Id" citext COLLATE "C" not null default '',
    "Enabled" boolean not null default false,
    "ClientSecrets" jsonb not null,
    "RequireClientSecret" boolean not null default false,
    "Name" citext,
    "AllowedGrantTypes" jsonb not null,
    "AllowedScopes" jsonb not null,
    "IdentityTokenLifetime" integer not null default 0,
    "AccessTokenLifetime" integer not null default 0,
    "ApiKey" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
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
    "SourceType" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_CorpOnboardingRequest" PRIMARY KEY ("Id")
);


CREATE TABLE "Corporate" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Description" citext not null default '',
    "Code" citext not null default '',
    "Platform" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_Corporate" PRIMARY KEY ("Id")
);


CREATE TABLE "CorporateSetting" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Value" citext not null default '',
    "Desc" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
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
    "Details" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Department" PRIMARY KEY ("Id")
);


CREATE TABLE "ECoinPrice" (
    "Id" citext COLLATE "C" not null default '',
    "TargetType" citext not null default '',
    "FeatureId" citext COLLATE "C" not null default '',
    "FeatureDesc" citext not null default '',
    "Price" numeric not null default 0,
    "ChargeType" citext not null default '',
    "PluginId" citext COLLATE "C" not null default '',
    CONSTRAINT "PK_ECoinPrice" PRIMARY KEY ("Id")
);


CREATE TABLE "Ef_RunLog" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "EventFlowId" citext COLLATE "C" not null default '',
    "EventFlowName" citext not null default '',
    "EventFlowVersion" integer not null default 0,
    "WfInstanceId" citext COLLATE "C" not null default '',
    "TriggerKind" citext not null default '',
    "EventSource" citext not null default '',
    "EventType" citext not null default '',
    "TriggerBy" jsonb,
    "TriggerTime" bigint not null default 0,
    "StartTime" bigint not null default 0,
    "EndTime" bigint,
    "Success" boolean not null default false,
    "ErrMsg" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Ef_RunLog" PRIMARY KEY ("Id")
);


CREATE TABLE "Ef_RunLogNode" (
    "Id" citext COLLATE "C" not null default '',
    "RunLogId" citext COLLATE "C" not null default '',
    "EventFlowId" citext COLLATE "C" not null default '',
    "WfInstanceId" citext COLLATE "C" not null default '',
    "DataId" citext COLLATE "C" not null default '',
    "NodeId" citext COLLATE "C" not null default '',
    "NodeName" citext not null default '',
    "NodeType" citext not null default '',
    "StartTime" bigint not null default 0,
    "EndTime" bigint,
    "Success" boolean not null default false,
    "ErrMsg" citext not null default '',
    "FailureReason" citext not null default '',
    "TroubleshootingSuggestion" citext not null default '',
    "Summary" citext not null default '',
    "ExecTime" bigint not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Ef_RunLogNode" PRIMARY KEY ("Id")
);


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
    "Status" citext not null default '',
    "ProcessingOwner" citext not null default '',
    "ProcessingStartedTime" bigint not null default 0,
    "LeaseUntil" bigint not null default 0,
    "AttemptCount" integer not null default 0,
    "ResultSnapshot" citext not null default '',
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
    "SourceType" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_EventFlowScheduleItem" PRIMARY KEY ("Id")
);


CREATE TABLE "ExportLog" (
    "Id" citext COLLATE "C" not null default '',
    "ExportType" citext not null default '',
    "RequestedFormat" citext not null default '',
    "ActualFormat" citext not null default '',
    "Status" citext not null default '',
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_ExportLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormData" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "FlowStatus" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    "Data" jsonb not null,
    CONSTRAINT "PK_FormData" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDataChangeLog" (
    "Id" citext COLLATE "C" not null default '',
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormDataChangeLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDataImportLog" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "FormName" citext,
    "PermissionGroupId" citext COLLATE "C",
    "FormUsingWorkflow" boolean not null default false,
    "Mode" citext not null default '',
    "TriggerValidation" boolean not null default false,
    "TriggerWorkflow" boolean not null default false,
    "ImportAction" citext not null default '',
    "Status" citext not null default '',
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormDataImportLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDataPermissionGroup" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Desc" citext not null default '',
    "Type" citext not null default '',
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormDataPermissionGroup" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDataPermissionGroupTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "AppTemplateId" citext COLLATE "C" not null default '',
    "FormTemplateId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Desc" citext not null default '',
    "Type" citext not null default '',
    "FormDataPermissions" bigint not null default 0,
    "DataFilter" citext,
    "FormFieldPermissions" jsonb not null,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormDef" PRIMARY KEY ("Id")
);


CREATE TABLE "FormListView" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "PcType" citext not null default '',
    "MobileType" citext not null default '',
    "SortIndex" integer not null default 0,
    "PermissionGroupIds" text[] not null,
    "Settings" citext not null default '',
    "DefaultFilter" citext,
    "DefaultSort" citext,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormListView" PRIMARY KEY ("Id")
);


CREATE TABLE "FormNotify" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "TargetType" citext not null default '',
    "TimeField" citext,
    "FixedTime" citext,
    "Direction" citext not null default '',
    "OffsetValue" integer,
    "OffsetUnit" citext,
    "FieldFormat" citext,
    "StartTime" bigint,
    "EndTime" bigint,
    "RepeatType" citext,
    "RepeatConfig" citext,
    "NextTriggerTime" bigint,
    "LastTriggerTime" bigint,
    "ScheduleVersion" bigint not null default 0,
    "TriggerMode" citext not null default '',
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormNotifyDispatchLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormNotifyScheduleItem" (
    "Id" citext COLLATE "C" not null default '',
    "NotifyId" citext COLLATE "C" not null default '',
    "DataId" citext COLLATE "C",
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "TargetType" citext not null default '',
    "TriggerMode" citext not null default '',
    "ScheduleVersion" bigint not null default 0,
    "TriggerTime" bigint not null default 0,
    "AnchorTime" bigint not null default 0,
    "TimeField" citext,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_FormNotifyScheduleItem" PRIMARY KEY ("Id")
);


CREATE TABLE "FormTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Type" citext not null default '',
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_IdentityLoginAudit" PRIMARY KEY ("Id")
);


CREATE TABLE "OutboxMessage" (
    "Id" citext COLLATE "C" not null default '',
    "QueueName" citext not null default '',
    "MessageType" citext not null default '',
    "IdempotencyKey" citext not null default '',
    "Payload" citext not null default '',
    "Status" citext not null default '',
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Payment" PRIMARY KEY ("Id")
);


CREATE TABLE "PluginInstall" (
    "Id" citext COLLATE "C" not null default '',
    "PluginId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Summary" citext not null default '',
    "Icon" citext not null default '',
    "Status" citext not null default '',
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
    CONSTRAINT "PK_PluginProfile" PRIMARY KEY ("Id")
);


CREATE TABLE "PrintDef" (
    "Id" citext COLLATE "C" not null default '',
    "TemplateId" citext COLLATE "C",
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Content" citext not null default '',
    "PrintType" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_PrintDef" PRIMARY KEY ("Id")
);


CREATE TABLE "PrintDefTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "AppTemplateId" citext COLLATE "C" not null default '',
    "FormTemplateId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Content" citext not null default '',
    "PrintType" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_PrintDefTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "ProcessedMessage" (
    "Id" citext COLLATE "C" not null default '',
    "EventKey" citext not null default '',
    "Target" citext not null default '',
    "Status" citext not null default '',
    "LeaseUntil" timestamp with time zone,
    "LeaseToken" citext,
    "ProcessedTime" bigint,
    "ProcessedAt" timestamp with time zone,
    CONSTRAINT "PK_ProcessedMessage" PRIMARY KEY ("Id")
);


CREATE TABLE "PublicSetting" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "TargetType" citext not null default '',
    "TargetId" citext COLLATE "C" not null default '',
    "Form" jsonb not null,
    "Dashboard" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
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
    "SerialNoType" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
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
    "Category" citext not null default '',
    "MessageType" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_SystemMessage" PRIMARY KEY ("Id")
);


CREATE TABLE "TenantAdminGroup" (
    "Id" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "Description" citext not null default '',
    "Type" citext not null default '',
    "ParentId" citext COLLATE "C" not null default '',
    "SortValue" integer not null default 0,
    "EmployeeIds" jsonb not null,
    "AppIds" text[] not null,
    "CanCreateOrDeleteApp" boolean not null default false,
    "AppDepartmentScopeMode" citext not null default '',
    "AppDepartmentIds" text[] not null,
    "AppEmployeeGroupScopeMode" citext not null default '',
    "AppEmployeeGroupIds" text[] not null,
    "ContactDepartmentPermission" citext not null default '',
    "ContactDepartmentScopeMode" citext not null default '',
    "ContactDepartmentIds" text[] not null,
    "ContactEmployeeGroupPermission" citext not null default '',
    "ContactEmployeeGroupScopeMode" citext not null default '',
    "ContactEmployeeGroupIds" text[] not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_UploadedFile" PRIMARY KEY ("Id")
);


CREATE TABLE "User" (
    "Id" citext COLLATE "C" not null default '',
    "CreateTime" bigint not null default 0,
    "Name" citext not null default '',
    "Email" citext not null default '',
    "Phone" citext not null default '',
    "Password" citext not null default '',
    "Platform" citext not null default '',
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
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "WebHookId" citext COLLATE "C" not null default '',
    "SourceType" citext not null default '',
    "TriggerType" citext not null default '',
    "Url" citext not null default '',
    "EventId" citext COLLATE "C" not null default '',
    "PushObject" citext,
    "HttpCode" integer not null default 0,
    "PushResult" citext,
    "Success" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_WebPushLog" PRIMARY KEY ("Id")
);


CREATE TABLE "Webhook" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "Name" citext,
    "SourceType" citext not null default '',
    "Url" citext not null default '',
    "Secret" citext not null default '',
    "Remark" citext,
    "Triggers" bigint not null default 0,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_WebhookAlias" PRIMARY KEY ("Id")
);


CREATE TABLE "WfDefinitionTemplate" (
    "Id" citext COLLATE "C" not null default '',
    "AppTemplateId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "FlowType" citext not null default '',
    "ExternalTemplateId" citext COLLATE "C" not null default '',
    "Description" citext not null default '',
    "Content" citext not null default '',
    "Metadata" jsonb not null,
    "EventSource" citext not null default '',
    "SourceTemplateId" citext COLLATE "C",
    "EventSetting" jsonb,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_WfDefinitionTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "Wf_Definition" (
    "Id" citext COLLATE "C" not null default '',
    "TemplateId" citext COLLATE "C",
    "AppId" citext COLLATE "C" not null default '',
    "Name" citext not null default '',
    "FlowType" citext not null default '',
    "ExternalId" citext COLLATE "C" not null default '',
    "Description" citext not null default '',
    "Version" integer not null default 0,
    "IsCurrent" boolean not null default false,
    "Released" boolean not null default false,
    "Content" citext not null default '',
    "Metadata" jsonb not null,
    "EventSource" citext not null default '',
    "SourceId" citext COLLATE "C",
    "EventSetting" jsonb,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Wf_Task" PRIMARY KEY ("Id")
);


CREATE TABLE "Wf_TaskLog" (
    "Id" citext COLLATE "C" not null default '',
    "AppId" citext COLLATE "C" not null default '',
    "FormId" citext COLLATE "C" not null default '',
    "FormName" citext not null default '',
    "DataId" citext COLLATE "C" not null default '',
    "WfVersion" integer not null default 0,
    "NodeId" citext COLLATE "C" not null default '',
    "NodeName" citext not null default '',
    "NodeType" citext not null default '',
    "Round" integer not null default 0,
    "Approver" jsonb,
    "Result" citext not null default '',
    "Comment" citext,
    "Signature" citext,
    "ApprovalTime" bigint not null default 0,
    "DataBrief" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_Wf_TaskLog" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkbenchConfig" (
    "Id" citext COLLATE "C" not null default '',
    "EmployeeId" citext COLLATE "C" not null default '',
    "Layout" citext not null default '',
    "PageStyle" citext not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_WorkbenchConfig" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkbenchFavorite" (
    "Id" citext COLLATE "C" not null default '',
    "EmployeeId" citext COLLATE "C" not null default '',
    "TargetType" citext not null default '',
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
    "CorpId" citext COLLATE "C",
    CONSTRAINT "PK_WorkbenchFavorite" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkbenchRecentVisit" (
    "Id" citext COLLATE "C" not null default '',
    "EmployeeId" citext COLLATE "C" not null default '',
    "TargetType" citext not null default '',
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
    "Status" citext not null default '',
    "Error" citext not null default '',
    "CreateTime" bigint not null default 0,
    "UpdateTime" bigint not null default 0,
    CONSTRAINT "PK_WorkflowTransitionExecution" PRIMARY KEY ("Id")
);
