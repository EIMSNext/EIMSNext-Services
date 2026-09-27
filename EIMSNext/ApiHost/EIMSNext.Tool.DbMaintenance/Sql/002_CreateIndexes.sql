-- 实体表索引。
--
-- 分两部分：
--   1) 模型声明段：夹在 >>> / <<< generated 两行标记之间，由 EF 模型投影生成，勿手工编辑；
--   2) 手写业务索引：企业维度查询以 CorpId 打头、DeleteFlag 收尾，部分带 where 的部分索引，
--      以及模型表达不了的 GIN / citext_pattern_ops。
-- 命名规则：UX_ 唯一 / IX_ 普通，表名用精确实体名单数，字段顺序与查询顺序一致。
--
-- 与 001 同理：索引也应优先在模型里声明；模型未覆盖的只能写在这里。
-- 新增时直接追加到本文件末尾，不要再开新编号的脚本。


-- >>> generated: model-declared indexes
-- 本段由 EF 模型投影生成，请勿手工编辑；新增索引优先写在模型里，模型无法表达的（GIN、部分索引、citext_pattern_ops）写在本标记段之下的手写区。
-- 重新生成：dotnet test Tests/EIMSNext.Core.Tests --filter RegenerateBaselineScripts（需先置环境变量 EIMS_REGENERATE_BASELINE=1）
create index if not exists "IX_Department_CorpId_Code" on "Department" ("CorpId", "Code");

create index if not exists "IX_Employee_CorpId_Code" on "Employee" ("CorpId", "Code");

create index if not exists "IX_EmployeeDepartment_DepartmentId" on "EmployeeDepartment" ("DepartmentId");

create index if not exists "IX_EmployeeDepartment_EmployeeId" on "EmployeeDepartment" ("EmployeeId");

create unique index if not exists "UX_EmployeeDepartment_CorpId_EmployeeId_DepartmentId" on "EmployeeDepartment" ("CorpId", "EmployeeId", "DepartmentId");

create index if not exists "IX_EmployeeGroupMember_EmployeeId" on "EmployeeGroupMember" ("EmployeeId");

create index if not exists "IX_FormData_CorpId_FormId_DeleteFlag" on "FormData" ("CorpId", "FormId", "DeleteFlag");

create index if not exists "IX_FormDef_CorpId_AppId" on "FormDef" ("CorpId", "AppId");

create unique index if not exists "UX_UserCorp_UserId_CorpId" on "UserCorp" ("UserId", "CorpId");
-- <<< generated: model-declared indexes

-- ---------------------------------------------------------------- 核心与多租户
create index if not exists "IX_UserCorp_UserId_IsDefault" on "UserCorp" ("UserId", "IsDefault");

create index if not exists "IX_Employee_CorpId_DeleteFlag" on "Employee" ("CorpId", "DeleteFlag");

create index if not exists "IX_Employee_CorpId_UserId" on "Employee" ("CorpId", "UserId");

create index if not exists "IX_Department_CorpId_DeleteFlag" on "Department" ("CorpId", "DeleteFlag");

create index if not exists "IX_Department_HeriarchyId" on "Department" ("HeriarchyId" citext_pattern_ops);

create index if not exists "IX_EmployeeDepartment_CorpId_DepartmentId_EmployeeId" on "EmployeeDepartment" ("CorpId", "DepartmentId", "EmployeeId");

create index if not exists "IX_EmployeeDepartment_CorpId_EmployeeId_SortValue" on "EmployeeDepartment" ("CorpId", "EmployeeId", "SortValue");

create index if not exists "IX_EmployeeGroup_CorpId_CategoryId_SortValue" on "EmployeeGroup" ("CorpId", "EmployeeGroupCategoryId", "SortValue");

create index if not exists "IX_EmployeeGroupCategory_CorpId_SortValue" on "EmployeeGroupCategory" ("CorpId", "SortValue");

create index if not exists "IX_EmployeeGroupMember_CorpId_EmployeeGroupId_EmployeeId" on "EmployeeGroupMember" ("CorpId", "EmployeeGroupId", "EmployeeId");

create index if not exists "IX_EmployeeGroupMember_CorpId_EmployeeId" on "EmployeeGroupMember" ("CorpId", "EmployeeId");

create unique index if not exists "UX_EmployeeGroupMember_CorpId_EmployeeId_EmployeeGroupId" on "EmployeeGroupMember" ("CorpId", "EmployeeId", "EmployeeGroupId");

create index if not exists "IX_TenantAdminGroup_CorpId_ParentId" on "TenantAdminGroup" ("CorpId", "ParentId");

create index if not exists "IX_CorporateSetting_CorpId_Name" on "CorporateSetting" ("CorpId", "Name");

-- ---------------------------------------------------------------- 表单
create index if not exists "IX_FormDef_Content_Gin" on "FormDef" using gin ("Content" jsonb_path_ops);

create index if not exists "IX_FormDef_CorpId_AppId_DeleteFlag" on "FormDef" ("CorpId", "AppId", "DeleteFlag");

create index if not exists "IX_FormDef_CorpId_Name" on "FormDef" ("CorpId", "Name");

create index if not exists "IX_FormData_CorpId_AppId_DeleteFlag" on "FormData" ("CorpId", "AppId", "DeleteFlag");

create index if not exists "IX_FormData_Data_Gin" on "FormData" using gin ("Data" jsonb_path_ops);

create index if not exists "IX_FormListView_CorpId_FormId" on "FormListView" ("CorpId", "FormId");

create index if not exists "IX_FormNotify_CorpId_FormId" on "FormNotify" ("CorpId", "FormId");

create index if not exists "IX_FormNotifyDispatchLog_CorpId_NotifyId" on "FormNotifyDispatchLog" ("CorpId", "NotifyId");

create index if not exists "IX_FormDataChangeLog_CorpId_DataId" on "FormDataChangeLog" ("CorpId", "DataId");

create index if not exists "IX_FormDataPermissionGroup_CorpId_FormId" on "FormDataPermissionGroup" ("CorpId", "FormId");

create index if not exists "IX_PrintDef_CorpId_FormId" on "PrintDef" ("CorpId", "FormId");

create index if not exists "IX_SerialNoSequence_CorpId_AppId_FormId_Key" on "SerialNoSequence" ("CorpId", "AppId", "FormId", "Key");

create unique index if not exists "UX_SerialNoSequence_Scope" on "SerialNoSequence" ("SerialNoType", "CorpId", "AppId", "FormId", "Key");

-- ---------------------------------------------------------------- 应用与仪表盘
create index if not exists "IX_AppDef_CorpId_DeleteFlag" on "AppDef" ("CorpId", "DeleteFlag");

create index if not exists "IX_AppDef_CorpId_SortIndex" on "AppDef" ("CorpId", "SortIndex");

create index if not exists "IX_PluginInstall_CorpId_PluginId" on "PluginInstall" ("CorpId", "PluginId");

create index if not exists "IX_DashboardDef_CorpId_AppId_DeleteFlag" on "DashboardDef" ("CorpId", "AppId", "DeleteFlag");

create index if not exists "IX_DashboardItemDef_CorpId_DashboardId" on "DashboardItemDef" ("CorpId", "DashboardId");

-- ---------------------------------------------------------------- 工作流与事件流
create index if not exists "IX_Wf_Definition_CorpId_AppId_IsCurrent" on "Wf_Definition" ("CorpId", "AppId", "IsCurrent");

create unique index if not exists "UX_Wf_Definition_CorpId_ExternalId_Version" on "Wf_Definition" ("CorpId", "ExternalId", "Version");

create index if not exists "IX_Wf_Task_CorpId_DataId" on "Wf_Task" ("CorpId", "DataId");

create index if not exists "IX_Wf_Task_CorpId_EmployeeId_DeleteFlag" on "Wf_Task" ("CorpId", "EmployeeId", "DeleteFlag");

create index if not exists "IX_Wf_Task_ExpireTime" on "Wf_Task" ("ExpireTime") where "ExpireHandled" = false and "ExpireTime" is not null;

create index if not exists "IX_Wf_TaskLog_CorpId_DataId_Round" on "Wf_TaskLog" ("CorpId", "DataId", "Round");

create index if not exists "IX_Wf_ExecLog_WfInstanceId_ExecTime" on "Wf_ExecLog" ("WfInstanceId", "ExecTime");

create index if not exists "IX_Ef_RunLogNode_RunLogId" on "Ef_RunLogNode" ("RunLogId");

-- ---------------------------------------------------------------- 身份与能力
create index if not exists "IX_ClientGrant_ClientId" on "ClientGrant" ("ClientId");

create index if not exists "IX_IdentityLoginAudit_UserId_CreateTime" on "IdentityLoginAudit" ("UserId", "CreateTime");

create index if not exists "IX_Webhook_CorpId_DeleteFlag" on "Webhook" ("CorpId", "DeleteFlag");

create index if not exists "IX_UploadedFile_CorpId_CreateTime" on "UploadedFile" ("CorpId", "CreateTime");

-- ---------------------------------------------------------------- 基础设施
create index if not exists "IX_OutboxMessage_Status_OutAt" on "OutboxMessage" ("Status", "OutAt");

create unique index if not exists "UX_OutboxMessage_IdempotencyKey" on "OutboxMessage" ("IdempotencyKey");

create index if not exists "IX_ProcessedMessage_Status_LeaseUntil" on "ProcessedMessage" ("Status", "LeaseUntil");

create unique index if not exists "UX_ProcessedMessage_EventKey_Target" on "ProcessedMessage" ("EventKey", "Target");

-- ---------------------------------------------------------------- 随模型对齐重建的索引
-- 旧脚本里下面这些索引引用的列在实体上已不存在（迁移前那批表被写成「单一 JSON 大字段」形状，
-- 或列在实体重构时改了名）。这里按实体上的对应列重建，保留原本的查询意图；
-- 无法一一对应的（如 Payment.OrderNo、WebhookAlias.WebhookId 在实体上已无对应字段）不再重建。

-- 消息中心：按企业取某个接收人的未读/已读消息。原索引列为 UserId/Read，实体上是 ReceiverEmpId/IsRead。
create index if not exists "IX_SystemMessage_CorpId_ReceiverEmpId_IsRead" on "SystemMessage" ("CorpId", "ReceiverEmpId", "IsRead");

-- 导出记录列表：按企业倒序翻页。原索引含 UserId，实体上已无该列。
create index if not exists "IX_ExportLog_CorpId_CreateTime" on "ExportLog" ("CorpId", "CreateTime");

-- 事件流运行日志：按企业 + 事件流取执行历史。原索引列为 FlowId/ExecTime，实体上是 EventFlowId/StartTime。
create index if not exists "IX_Ef_RunLog_CorpId_EventFlowId_StartTime" on "Ef_RunLog" ("CorpId", "EventFlowId", "StartTime");

-- 客户端唯一性：原索引列为 ClientId，实体上改用 ApiKey 作为应用凭证。
create unique index if not exists "UX_Client_ApiKey" on "Client" ("ApiKey") where "DeleteFlag" = false and "ApiKey" <> '';

-- 调度扫描：只索引未执行的触发点。原索引列为 ExecuteTime，实体上是 TriggerTime。
create index if not exists "IX_FormNotifyScheduleItem_TriggerTime" on "FormNotifyScheduleItem" ("TriggerTime") where "TriggerTime" is not null;

create index if not exists "IX_EventFlowScheduleItem_TriggerTime" on "EventFlowScheduleItem" ("TriggerTime") where "TriggerTime" is not null;
