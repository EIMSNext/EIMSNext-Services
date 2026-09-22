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
    "Id" text not null default '',
    "TemplateId" text,
    "Name" text not null default '',
    "Description" text not null default '',
    "Icon" text not null default '',
    "IconColor" text not null default '',
    "GroupId" text,
    "SortIndex" integer not null default 0,
    "HomeEntryIds" jsonb not null,
    "AppMenus" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_AppDef" PRIMARY KEY ("Id")
);


CREATE TABLE "AppProfile" (
    "Id" text not null default '',
    "Name" text not null default '',
    "Summary" text not null default '',
    "Description" text not null default '',
    "Icon" text not null default '',
    "CoverImage" text not null default '',
    "BannerImage" text not null default '',
    "GalleryImages" text[] not null,
    "Category" text not null default '',
    "Industry" text not null default '',
    "Tags" text[] not null,
    "Author" text not null default '',
    "InstallCount" bigint not null default 0,
    "SortIndex" integer not null default 0,
    "IsOfficial" boolean not null default false,
    "IsHot" boolean not null default false,
    "IsRecommended" boolean not null default false,
    "ThemeColor" text not null default '',
    "TemplateId" text not null default '',
    "Status" text not null default '',
    "PublishedAt" timestamp with time zone,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_AppProfile" PRIMARY KEY ("Id")
);


CREATE TABLE "AppTemplate" (
    "Id" text not null default '',
    "Name" text not null default '',
    "Description" text not null default '',
    "Icon" text not null default '',
    "Menus" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_AppTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "AuditLog" (
    "Id" text not null default '',
    "Action" text not null default '',
    "EntityType" text,
    "DataId" text,
    "Detail" text,
    "OldData" text,
    "NewData" text,
    "DataFilter" text,
    "UpdateExp" text,
    "ClientIp" text,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_AuditLog" PRIMARY KEY ("Id")
);


CREATE TABLE "Client" (
    "Id" text not null default '',
    "Enabled" boolean not null default false,
    "ClientSecrets" jsonb not null,
    "RequireClientSecret" boolean not null default false,
    "Name" text,
    "AllowedGrantTypes" jsonb not null,
    "AllowedScopes" jsonb not null,
    "IdentityTokenLifetime" integer not null default 0,
    "AccessTokenLifetime" integer not null default 0,
    "ApiKey" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_Client" PRIMARY KEY ("Id")
);


CREATE TABLE "ClientGrant" (
    "Id" text not null default '',
    "ClientId" text not null default '',
    "Name" text not null default '',
    "AppScope" text not null default '',
    "AppIds" jsonb not null,
    "ApiScope" text not null default '',
    "ResourceActions" jsonb not null,
    "IpWhitelist" jsonb not null,
    "Enabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_ClientGrant" PRIMARY KEY ("Id")
);


CREATE TABLE "CorpOnboardingRequest" (
    "Id" text not null default '',
    "UserId" text not null default '',
    "UserName" text not null default '',
    "TargetCorpId" text not null default '',
    "TargetCorpName" text not null default '',
    "ApplicantName" text not null default '',
    "Phone" text not null default '',
    "Email" text not null default '',
    "EmployeeId" text not null default '',
    "SourceType" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_CorpOnboardingRequest" PRIMARY KEY ("Id")
);


CREATE TABLE "Corporate" (
    "Id" text not null default '',
    "Name" text not null default '',
    "Description" text not null default '',
    "Code" text not null default '',
    "Platform" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_Corporate" PRIMARY KEY ("Id")
);


CREATE TABLE "CorporateSetting" (
    "Id" text not null default '',
    "Name" text not null default '',
    "Value" text not null default '',
    "Desc" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_CorporateSetting" PRIMARY KEY ("Id")
);


CREATE TABLE "CrossBinding" (
    "Id" text not null default '',
    "TargetAppId" text not null default '',
    "SourceAppId" text not null default '',
    "SourceFormId" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_CrossBinding" PRIMARY KEY ("Id")
);


CREATE TABLE "DashboardDef" (
    "Id" text not null default '',
    "TemplateId" text,
    "AppId" text not null default '',
    "Name" text not null default '',
    "Layout" text not null default '',
    "AutoRefreshEnabled" boolean not null default false,
    "AutoRefreshIntervalMinutes" integer not null default 0,
    "MemberPublishEnabled" boolean not null default false,
    "PublishMembers" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_DashboardDef" PRIMARY KEY ("Id")
);


CREATE TABLE "DashboardItemDef" (
    "Id" text not null default '',
    "TemplateId" text,
    "AppId" text not null default '',
    "DashboardId" text not null default '',
    "ItemType" text not null default '',
    "LayoutId" text not null default '',
    "Name" text not null default '',
    "Details" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_DashboardItemDef" PRIMARY KEY ("Id")
);


CREATE TABLE "DashboardItemTemplate" (
    "Id" text not null default '',
    "AppTemplateId" text not null default '',
    "DashboardTemplateId" text not null default '',
    "ItemType" text not null default '',
    "LayoutId" text not null default '',
    "Name" text not null default '',
    "Details" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_DashboardItemTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "DashboardTemplate" (
    "Id" text not null default '',
    "AppTemplateId" text not null default '',
    "Name" text not null default '',
    "Layout" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_DashboardTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "Department" (
    "Id" text not null default '',
    "Code" text not null default '',
    "Name" text not null default '',
    "IsCompany" boolean not null default false,
    "ParentId" text not null default '',
    "ParentName" text not null default '',
    "HeriarchyId" text not null default '',
    "HeriarchyName" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_Department" PRIMARY KEY ("Id")
);


CREATE TABLE "ECoinPrice" (
    "Id" text not null default '',
    "TargetType" text not null default '',
    "FeatureId" text not null default '',
    "FeatureDesc" text not null default '',
    "Price" numeric not null default 0,
    "ChargeType" text not null default '',
    "PluginId" text not null default '',
    CONSTRAINT "PK_ECoinPrice" PRIMARY KEY ("Id")
);


CREATE TABLE "Ef_RunLog" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "EventFlowId" text not null default '',
    "EventFlowName" text not null default '',
    "EventFlowVersion" integer not null default 0,
    "WfInstanceId" text not null default '',
    "TriggerKind" text not null default '',
    "EventSource" text not null default '',
    "EventType" text not null default '',
    "TriggerBy" jsonb,
    "TriggerTime" bigint not null default 0,
    "StartTime" bigint not null default 0,
    "EndTime" bigint,
    "Success" boolean not null default false,
    "ErrMsg" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_Ef_RunLog" PRIMARY KEY ("Id")
);


CREATE TABLE "Ef_RunLogNode" (
    "Id" text not null default '',
    "RunLogId" text not null default '',
    "EventFlowId" text not null default '',
    "WfInstanceId" text not null default '',
    "DataId" text not null default '',
    "NodeId" text not null default '',
    "NodeName" text not null default '',
    "NodeType" text not null default '',
    "StartTime" bigint not null default 0,
    "EndTime" bigint,
    "Success" boolean not null default false,
    "ErrMsg" text not null default '',
    "FailureReason" text not null default '',
    "TroubleshootingSuggestion" text not null default '',
    "Summary" text not null default '',
    "ExecTime" bigint not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_Ef_RunLogNode" PRIMARY KEY ("Id")
);


CREATE TABLE "Employee" (
    "Id" text not null default '',
    "UserId" text not null default '',
    "UserName" text not null default '',
    "Code" text not null default '',
    "EmpName" text not null default '',
    "WorkPhone" text not null default '',
    "WorkEmail" text not null default '',
    "Status" integer not null default 0,
    "IsDummy" boolean not null default false,
    "Invite" text,
    "UserBound" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_Employee" PRIMARY KEY ("Id")
);


CREATE TABLE "EmployeeDepartment" (
    "Id" text not null default '',
    "EmployeeId" text not null default '',
    "DepartmentId" text not null default '',
    "IsManager" boolean not null default false,
    "SortValue" integer not null default 0,
    "HeriarchyId" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_EmployeeDepartment" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EmployeeDepartment_Department_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Department" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_EmployeeDepartment_Employee_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employee" ("Id") ON DELETE CASCADE
);


CREATE TABLE "EmployeeGroup" (
    "Id" text not null default '',
    "Name" text not null default '',
    "Description" text not null default '',
    "EmployeeGroupCategoryId" text not null default '',
    "SortValue" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_EmployeeGroup" PRIMARY KEY ("Id")
);


CREATE TABLE "EmployeeGroupCategory" (
    "Id" text not null default '',
    "Name" text not null default '',
    "Description" text not null default '',
    "SortValue" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_EmployeeGroupCategory" PRIMARY KEY ("Id")
);


CREATE TABLE "EmployeeGroupMember" (
    "Id" text not null default '',
    "EmployeeId" text not null default '',
    "EmployeeGroupId" text not null default '',
    "EmployeeGroupName" text not null default '',
    "SortValue" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_EmployeeGroupMember" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EmployeeGroupMember_Employee_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employee" ("Id") ON DELETE CASCADE
);


CREATE TABLE "EventFlowHookSample" (
    "Id" text not null default '',
    "EventFlowId" text not null default '',
    "AppId" text not null default '',
    "ClientIp" text not null default '',
    "RawJson" text not null default '',
    "FlattenedFieldsJson" text not null default '',
    "CapturedAt" bigint not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_EventFlowHookSample" PRIMARY KEY ("Id")
);


CREATE TABLE "EventFlowNodeExecution" (
    "Id" text not null default '',
    "ExecutionKey" text not null default '',
    "ExecutionId" text not null default '',
    "WorkflowInstanceId" text not null default '',
    "RunLogId" text not null default '',
    "CorpId" text not null default '',
    "EventFlowId" text not null default '',
    "NodeId" text not null default '',
    "ActionType" text not null default '',
    "TargetKey" text not null default '',
    "Ordinal" integer not null default 0,
    "FormId" text,
    "SingleResult" boolean not null default false,
    "Status" text not null default '',
    "ProcessingOwner" text not null default '',
    "ProcessingStartedTime" bigint not null default 0,
    "LeaseUntil" bigint not null default 0,
    "AttemptCount" integer not null default 0,
    "ResultSnapshot" text not null default '',
    "CompletedTime" bigint not null default 0,
    CONSTRAINT "PK_EventFlowNodeExecution" PRIMARY KEY ("Id")
);


CREATE TABLE "EventFlowScheduleItem" (
    "Id" text not null default '',
    "EventFlowId" text not null default '',
    "AppId" text not null default '',
    "FormId" text,
    "DataId" text,
    "TriggerTime" bigint not null default 0,
    "AnchorTime" bigint not null default 0,
    "ScheduleVersion" bigint not null default 0,
    "SourceType" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_EventFlowScheduleItem" PRIMARY KEY ("Id")
);


CREATE TABLE "ExportLog" (
    "Id" text not null default '',
    "ExportType" text not null default '',
    "RequestedFormat" text not null default '',
    "ActualFormat" text not null default '',
    "Status" text not null default '',
    "ColumnsJson" text,
    "FilterJson" text,
    "DedupKey" text,
    "TotalCount" bigint not null default 0,
    "FileName" text,
    "DownloadUrl" text,
    "ErrorMessage" text,
    "FinishTime" bigint,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_ExportLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormData" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "FlowStatus" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    "Data" jsonb not null,
    CONSTRAINT "PK_FormData" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDataChangeLog" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "DataId" text not null default '',
    "Operator" jsonb,
    "OperateTime" bigint not null default 0,
    "Content" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_FormDataChangeLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDataImportLog" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "FormName" text,
    "PermissionGroupId" text,
    "FormUsingWorkflow" boolean not null default false,
    "Mode" text not null default '',
    "TriggerValidation" boolean not null default false,
    "TriggerWorkflow" boolean not null default false,
    "ImportAction" text not null default '',
    "Status" text not null default '',
    "MatchField" text,
    "SheetName" text not null default '',
    "HeaderRowIndex" integer not null default 0,
    "SourceFileName" text not null default '',
    "SourceObjectKey" text not null default '',
    "SourceFileSize" bigint not null default 0,
    "FieldSnapshotJson" text not null default '',
    "MappingJson" text not null default '',
    "DataScopeFilterJson" text,
    "TotalCount" bigint not null default 0,
    "ProcessedCount" bigint not null default 0,
    "AddCount" bigint not null default 0,
    "UpdateCount" bigint not null default 0,
    "FailedCount" bigint not null default 0,
    "ErrorReportFileName" text,
    "ErrorReportObjectKey" text,
    "ErrorReportDownloadUrl" text,
    "EditableErrorRowsJson" text,
    "EditableErrorRowsObjectKey" text,
    "EditableErrorRowCount" integer not null default 0,
    "ErrorMessage" text,
    "RetryCount" integer not null default 0,
    "StartTime" bigint,
    "ProcessingExpireTime" bigint,
    "FinishTime" bigint,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_FormDataImportLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDataPermissionGroup" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "Name" text not null default '',
    "Desc" text not null default '',
    "Type" text not null default '',
    "Members" jsonb not null,
    "FormDataPermissions" bigint not null default 0,
    "DataFilter" text,
    "FormFieldPermissions" jsonb not null,
    "Disabled" boolean not null default false,
    "TemplateId" text,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_FormDataPermissionGroup" PRIMARY KEY ("Id")
);


CREATE TABLE "FormDataPermissionGroupTemplate" (
    "Id" text not null default '',
    "AppTemplateId" text not null default '',
    "FormTemplateId" text not null default '',
    "Name" text not null default '',
    "Desc" text not null default '',
    "Type" text not null default '',
    "FormDataPermissions" bigint not null default 0,
    "DataFilter" text,
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
    "Id" text not null default '',
    "AppId" text not null default '',
    "TemplateId" text,
    "Name" text not null default '',
    "Content" jsonb not null,
    "UsingWorkflow" boolean not null default false,
    "FormSettings" jsonb not null,
    "PublicRelatedFormIds" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_FormDef" PRIMARY KEY ("Id")
);


CREATE TABLE "FormListView" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "Name" text not null default '',
    "PcType" text not null default '',
    "MobileType" text not null default '',
    "SortIndex" integer not null default 0,
    "PermissionGroupIds" text[] not null,
    "Settings" text not null default '',
    "DefaultFilter" text,
    "DefaultSort" text,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_FormListView" PRIMARY KEY ("Id")
);


CREATE TABLE "FormNotify" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "TargetType" text not null default '',
    "TimeField" text,
    "FixedTime" text,
    "Direction" text not null default '',
    "OffsetValue" integer,
    "OffsetUnit" text,
    "FieldFormat" text,
    "StartTime" bigint,
    "EndTime" bigint,
    "RepeatType" text,
    "RepeatConfig" text,
    "NextTriggerTime" bigint,
    "LastTriggerTime" bigint,
    "ScheduleVersion" bigint not null default 0,
    "TriggerMode" text not null default '',
    "ChangeFields" text[],
    "DataFilter" text,
    "DataDynamicFilter" text,
    "DataExpressFilter" text,
    "NotifyText" text,
    "Notifiers" text,
    "Channels" bigint not null default 0,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_FormNotify" PRIMARY KEY ("Id")
);


CREATE TABLE "FormNotifyDispatchLog" (
    "Id" text not null default '',
    "NotifyId" text not null default '',
    "DataId" text,
    "TriggerTime" bigint not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_FormNotifyDispatchLog" PRIMARY KEY ("Id")
);


CREATE TABLE "FormNotifyScheduleItem" (
    "Id" text not null default '',
    "NotifyId" text not null default '',
    "DataId" text,
    "AppId" text not null default '',
    "FormId" text not null default '',
    "TargetType" text not null default '',
    "TriggerMode" text not null default '',
    "ScheduleVersion" bigint not null default 0,
    "TriggerTime" bigint not null default 0,
    "AnchorTime" bigint not null default 0,
    "TimeField" text,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_FormNotifyScheduleItem" PRIMARY KEY ("Id")
);


CREATE TABLE "FormTemplate" (
    "Id" text not null default '',
    "Name" text not null default '',
    "Type" text not null default '',
    "Icon" text not null default '',
    "AppTemplateId" text not null default '',
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
    "Id" text not null default '',
    "LoginId" text,
    "GrantType" text,
    "UserId" text,
    "ClientId" text,
    "UserName" text,
    "ClientIp" text,
    "FailReason" text,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_IdentityLoginAudit" PRIMARY KEY ("Id")
);


CREATE TABLE "OutboxMessage" (
    "Id" text not null default '',
    "QueueName" text not null default '',
    "MessageType" text not null default '',
    "IdempotencyKey" text not null default '',
    "Payload" text not null default '',
    "Status" text not null default '',
    "OutAt" bigint not null default 0,
    "Attempt" integer not null default 0,
    "LastAttemptTime" bigint,
    "Error" text,
    "SentTime" bigint,
    "SentAt" timestamp with time zone,
    CONSTRAINT "PK_OutboxMessage" PRIMARY KEY ("Id")
);


CREATE TABLE "Payment" (
    "Id" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_Payment" PRIMARY KEY ("Id")
);


CREATE TABLE "PluginInstall" (
    "Id" text not null default '',
    "PluginId" text not null default '',
    "Name" text not null default '',
    "Summary" text not null default '',
    "Icon" text not null default '',
    "Status" text not null default '',
    "Enabled" boolean not null default false,
    "InstalledAt" bigint not null default 0,
    "InstalledBy" jsonb,
    "LastEnabledAt" bigint,
    "LastDisabledAt" bigint,
    "UninstalledAt" bigint,
    "Settings" text,
    "Source" text not null default '',
    "OrderNo" text not null default '',
    "ExpireAt" bigint,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_PluginInstall" PRIMARY KEY ("Id")
);


CREATE TABLE "PluginProfile" (
    "Id" text not null default '',
    "PluginId" text not null default '',
    "Version" text not null default '',
    "Name" text not null default '',
    "Summary" text not null default '',
    "Description" text not null default '',
    "Icon" text not null default '',
    "CoverImage" text not null default '',
    "BannerImage" text not null default '',
    "GalleryImages" text[] not null,
    "Category" text not null default '',
    "Scenario" text not null default '',
    "Tags" text[] not null,
    "DeveloperName" text not null default '',
    "DeveloperCorpId" text not null default '',
    "IsOfficial" boolean not null default false,
    "IsHot" boolean not null default false,
    "IsRecommended" boolean not null default false,
    "InstallCount" bigint not null default 0,
    "SortIndex" integer not null default 0,
    "Status" text not null default '',
    "PublishedAt" timestamp with time zone,
    "HelpDocUrl" text not null default '',
    "TemplateUrl" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_PluginProfile" PRIMARY KEY ("Id")
);


CREATE TABLE "PrintDef" (
    "Id" text not null default '',
    "TemplateId" text,
    "AppId" text not null default '',
    "FormId" text not null default '',
    "Name" text not null default '',
    "Content" text not null default '',
    "PrintType" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_PrintDef" PRIMARY KEY ("Id")
);


CREATE TABLE "PrintDefTemplate" (
    "Id" text not null default '',
    "AppTemplateId" text not null default '',
    "FormTemplateId" text not null default '',
    "Name" text not null default '',
    "Content" text not null default '',
    "PrintType" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    CONSTRAINT "PK_PrintDefTemplate" PRIMARY KEY ("Id")
);


CREATE TABLE "ProcessedMessage" (
    "Id" text not null default '',
    "EventKey" text not null default '',
    "Target" text not null default '',
    "Status" text not null default '',
    "LeaseUntil" timestamp with time zone,
    "LeaseToken" text,
    "ProcessedTime" bigint,
    "ProcessedAt" timestamp with time zone,
    CONSTRAINT "PK_ProcessedMessage" PRIMARY KEY ("Id")
);


CREATE TABLE "PublicSetting" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "TargetType" text not null default '',
    "TargetId" text not null default '',
    "Form" jsonb not null,
    "Dashboard" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_PublicSetting" PRIMARY KEY ("Id")
);


CREATE TABLE "SerialNoSequence" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "Key" text not null default '',
    "CurrDate" timestamp with time zone,
    "CurrId" integer,
    "SerialNoType" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_SerialNoSequence" PRIMARY KEY ("Id")
);


CREATE TABLE "SystemMessage" (
    "Id" text not null default '',
    "NotifyId" text,
    "Title" text,
    "Detail" text,
    "Url" text,
    "ReceiverEmpId" text,
    "ReceiverName" text,
    "IsRead" boolean not null default false,
    "ReadTime" bigint,
    "ExpireTime" bigint not null default 0,
    "Category" text not null default '',
    "MessageType" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_SystemMessage" PRIMARY KEY ("Id")
);


CREATE TABLE "TenantAdminGroup" (
    "Id" text not null default '',
    "Name" text not null default '',
    "Description" text not null default '',
    "Type" text not null default '',
    "ParentId" text not null default '',
    "SortValue" integer not null default 0,
    "EmployeeIds" jsonb not null,
    "AppIds" text[] not null,
    "CanCreateOrDeleteApp" boolean not null default false,
    "AppDepartmentScopeMode" text not null default '',
    "AppDepartmentIds" text[] not null,
    "AppEmployeeGroupScopeMode" text not null default '',
    "AppEmployeeGroupIds" text[] not null,
    "ContactDepartmentPermission" text not null default '',
    "ContactDepartmentScopeMode" text not null default '',
    "ContactDepartmentIds" text[] not null,
    "ContactEmployeeGroupPermission" text not null default '',
    "ContactEmployeeGroupScopeMode" text not null default '',
    "ContactEmployeeGroupIds" text[] not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_TenantAdminGroup" PRIMARY KEY ("Id")
);


CREATE TABLE "UploadedFile" (
    "Id" text not null default '',
    "FileName" text not null default '',
    "SavePath" text not null default '',
    "ThumbPath" text,
    "FileExt" text,
    "FileSize" bigint not null default 0,
    "RefCount" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_UploadedFile" PRIMARY KEY ("Id")
);


CREATE TABLE "User" (
    "Id" text not null default '',
    "CreateTime" bigint not null default 0,
    "Name" text not null default '',
    "Email" text not null default '',
    "Phone" text not null default '',
    "Password" text not null default '',
    "Platform" text not null default '',
    "Disabled" boolean not null default false,
    "Avatar" text,
    "UserType" text,
    CONSTRAINT "PK_User" PRIMARY KEY ("Id")
);


CREATE TABLE "UserCorp" (
    "Id" text not null default '',
    "UserId" text not null default '',
    "CorpId" text not null default '',
    "IsCorpOwner" boolean not null default false,
    "CorpType" text not null default '',
    "IsDefault" boolean not null default false,
    CONSTRAINT "PK_UserCorp" PRIMARY KEY ("Id")
);


CREATE TABLE "WebPushLog" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "WebHookId" text not null default '',
    "SourceType" text not null default '',
    "TriggerType" text not null default '',
    "Url" text not null default '',
    "EventId" text not null default '',
    "PushObject" text,
    "HttpCode" integer not null default 0,
    "PushResult" text,
    "Success" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_WebPushLog" PRIMARY KEY ("Id")
);


CREATE TABLE "Webhook" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "Name" text,
    "SourceType" text not null default '',
    "Url" text not null default '',
    "Secret" text not null default '',
    "Remark" text,
    "Triggers" bigint not null default 0,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_Webhook" PRIMARY KEY ("Id")
);


CREATE TABLE "WebhookAlias" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "FieldAlias" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_WebhookAlias" PRIMARY KEY ("Id")
);


CREATE TABLE "WfDefinitionTemplate" (
    "Id" text not null default '',
    "AppTemplateId" text not null default '',
    "Name" text not null default '',
    "FlowType" text not null default '',
    "ExternalTemplateId" text not null default '',
    "Description" text not null default '',
    "Content" text not null default '',
    "Metadata" jsonb not null,
    "EventSource" text not null default '',
    "SourceTemplateId" text,
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
    "Id" text not null default '',
    "TemplateId" text,
    "AppId" text not null default '',
    "Name" text not null default '',
    "FlowType" text not null default '',
    "ExternalId" text not null default '',
    "Description" text not null default '',
    "Version" integer not null default 0,
    "IsCurrent" boolean not null default false,
    "Released" boolean not null default false,
    "Content" text not null default '',
    "Metadata" jsonb not null,
    "EventSource" text not null default '',
    "SourceId" text,
    "EventSetting" jsonb,
    "Disabled" boolean not null default false,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_Wf_Definition" PRIMARY KEY ("Id")
);


CREATE TABLE "Wf_ExecLog" (
    "Id" text not null default '',
    "WfInstanceId" text not null default '',
    "DataId" text not null default '',
    "NodeId" text not null default '',
    "EmpId" text not null default '',
    "Success" boolean not null default false,
    "ErrMsg" text not null default '',
    "ExecTime" bigint not null default 0,
    CONSTRAINT "PK_Wf_ExecLog" PRIMARY KEY ("Id")
);


CREATE TABLE "Wf_Task" (
    "Id" text not null default '',
    "WfInstanceId" text not null default '',
    "ApproveNodeId" text not null default '',
    "ApproveNodeName" text not null default '',
    "EmployeeId" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "DataId" text not null default '',
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
    "CorpId" text,
    CONSTRAINT "PK_Wf_Task" PRIMARY KEY ("Id")
);


CREATE TABLE "Wf_TaskLog" (
    "Id" text not null default '',
    "AppId" text not null default '',
    "FormId" text not null default '',
    "FormName" text not null default '',
    "DataId" text not null default '',
    "WfVersion" integer not null default 0,
    "NodeId" text not null default '',
    "NodeName" text not null default '',
    "NodeType" text not null default '',
    "Round" integer not null default 0,
    "Approver" jsonb,
    "Result" text not null default '',
    "Comment" text,
    "Signature" text,
    "ApprovalTime" bigint not null default 0,
    "DataBrief" jsonb not null,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_Wf_TaskLog" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkbenchConfig" (
    "Id" text not null default '',
    "EmployeeId" text not null default '',
    "Layout" text not null default '',
    "PageStyle" text not null default '',
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_WorkbenchConfig" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkbenchFavorite" (
    "Id" text not null default '',
    "EmployeeId" text not null default '',
    "TargetType" text not null default '',
    "TargetId" text not null default '',
    "AppId" text not null default '',
    "Title" text not null default '',
    "Icon" text not null default '',
    "IconColor" text not null default '',
    "SortIndex" bigint not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_WorkbenchFavorite" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkbenchRecentVisit" (
    "Id" text not null default '',
    "EmployeeId" text not null default '',
    "TargetType" text not null default '',
    "TargetId" text not null default '',
    "AppId" text not null default '',
    "Title" text not null default '',
    "Icon" text not null default '',
    "IconColor" text not null default '',
    "LastVisitTime" bigint not null default 0,
    "VisitCount" integer not null default 0,
    "CreateBy" jsonb,
    "CreateTime" bigint not null default 0,
    "UpdateBy" jsonb,
    "UpdateTime" bigint,
    "DeleteFlag" boolean not null default false,
    "CorpId" text,
    CONSTRAINT "PK_WorkbenchRecentVisit" PRIMARY KEY ("Id")
);


CREATE TABLE "WorkflowTransitionExecution" (
    "Id" text not null default '',
    "ExecutionId" text not null default '',
    "WorkflowInstanceId" text not null default '',
    "CorpId" text not null default '',
    "WfNodeId" text not null default '',
    "NodeAction" text not null default '',
    "Status" text not null default '',
    "Error" text not null default '',
    "CreateTime" bigint not null default 0,
    "UpdateTime" bigint not null default 0,
    CONSTRAINT "PK_WorkflowTransitionExecution" PRIMARY KEY ("Id")
);
