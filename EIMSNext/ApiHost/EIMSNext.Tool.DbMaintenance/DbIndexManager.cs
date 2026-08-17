using EIMSNext.Auth.Entities;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Service.Entities;

using MongoDB.Driver;

using WorkflowCore.Models;

namespace EIMSNext.Auth.DbMaintenance
{
    public class DbIndexManager
    {
        private readonly EIMSDbContext _dbContext;

        public DbIndexManager(EIMSDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void CreateIndexes()
        {
            var background = new CreateIndexOptions { Background = true };

            CreateAuthIndexes(background);
            CreateCorporateSettingIndexes(background);
            CreateOrganizationIndexes(background);
            CreatePluginStoreIndexes(background);
            CreateDefinitionIndexes(background);
            CreateFormDataIndexes(background);
            CreateFormNotifyIndexes(background);
            CreateWebhookIndexes(background);
            CreateWorkflowBusinessIndexes(background);
            CreateWorkflowRuntimeIndexes(background);
            CreateDataflowScheduleIndexes(background);
            CreateWorkbenchIndexes(background);
            CreateLogIndexes(background);
            CreateDataflowLogIndexes(background);
        }

        private void CreateAuthIndexes(CreateIndexOptions options)
        {
            CreateIndex(_dbContext.Users,
                Builders<User>.IndexKeys.Ascending(x => x.Email).Ascending(x => x.Disabled),
                options,
                "ix_user_email_disabled");

            CreateIndex(_dbContext.Users,
                Builders<User>.IndexKeys.Ascending(x => x.Phone).Ascending(x => x.Disabled),
                options,
                "ix_user_phone_disabled");

            CreateIndex(_dbContext.AuditLogins,
                Builders<AuditLogin>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.DeleteFlag).Descending(x => x.CreateTime),
                options,
                "ix_auditlogin_corp_delete_createtime");
        }

        private void CreateOrganizationIndexes(CreateIndexOptions options)
        {
            CreateCorpIdIndex<Department>(options, "ix_department_corpid");
            CreateCorpIdIndex<Employee>(options, "ix_employee_corpid");
            CreateCorpIdIndex<EmployeeDepartment>(options, "ix_employeedepartment_corpid");
            CreateCorpIdIndex<Role>(options, "ix_role_corpid");

            CreateIndex(GetCollection<Employee>(),
                Builders<Employee>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.UserId),
                options,
                "ix_employee_corp_user");

            CreateIndex(GetCollection<EmployeeDepartment>(),
                Builders<EmployeeDepartment>.IndexKeys.Ascending(x => x.EmployeeId).Ascending(x => x.DepartmentId),
                CreateUniqueOptions(options),
                "ix_employeedepartment_employee_department_unique");

            CreateIndex(GetCollection<EmployeeDepartment>(),
                Builders<EmployeeDepartment>.IndexKeys.Ascending(x => x.DepartmentId).Ascending(x => x.EmployeeId),
                options,
                "ix_employeedepartment_department_employee");

            CreateIndex(GetCollection<EmployeeDepartment>(),
                Builders<EmployeeDepartment>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.EmployeeId)
                    .Ascending(x => x.SortValue),
                options,
                "ix_employeedepartment_corp_employee_sort");

            CreateIndex(GetCollection<AdminGroup>(),
                Builders<AdminGroup>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.DeleteFlag)
                    .Ascending(x => x.EmployeeIds)
                    .Ascending(x => x.Type),
                options,
                "ix_admingroup_corp_delete_employee_type");

            CreateIndex(GetCollection<Employee>(),
                Builders<Employee>.IndexKeys.Ascending("Roles.RoleId").Ascending(x => x.Status).Ascending(x => x.IsDummy),
                options,
                "ix_employee_role_status_dummy");
        }

        private void CreatePluginStoreIndexes(CreateIndexOptions options)
        {
            CreateIndex(GetCollection<PluginProfile>(),
                Builders<PluginProfile>.IndexKeys.Ascending(x => x.PluginId).Ascending(x => x.Version),
                CreateUniqueOptions(options),
                "ix_pluginprofile_plugin_version_unique");

            CreateIndex(GetCollection<PluginProfile>(),
                Builders<PluginProfile>.IndexKeys.Ascending(x => x.Status).Descending(x => x.SortIndex),
                options,
                "ix_pluginprofile_status_sortindex");

            CreateIndex(GetCollection<PluginProfile>(),
                Builders<PluginProfile>.IndexKeys.Ascending(x => x.Category),
                options,
                "ix_pluginprofile_category");

            CreateIndex(GetCollection<PluginProfile>(),
                Builders<PluginProfile>.IndexKeys.Ascending(x => x.Scenario),
                options,
                "ix_pluginprofile_scenario");

            CreateIndex(GetCollection<PluginInstall>(),
                Builders<PluginInstall>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.PluginId),
                options,
                "ix_plugininstall_corp_plugin");

            CreateIndex(GetCollection<PluginInstall>(),
                Builders<PluginInstall>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.Status),
                options,
                "ix_plugininstall_corp_status");

            CreateIndex(GetCollection<PluginInstall>(),
                Builders<PluginInstall>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.Enabled),
                options,
                "ix_plugininstall_corp_enabled");

            CreateIndex(GetCollection<ECoinPrice>(),
                Builders<ECoinPrice>.IndexKeys.Ascending(x => x.TargetType).Ascending(x => x.FeatureId),
                CreateUniqueOptions(options),
                "ix_ecoinprice_target_feature_unique");
        }

        private void CreateCorporateSettingIndexes(CreateIndexOptions options)
        {
            var indexOptions = CreateUniqueOptions(options);
            CreateIndex(
                GetCollection<CorporateSetting>(),
                Builders<CorporateSetting>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.Name)
                    .Ascending(x => x.DeleteFlag),
                indexOptions,
                "ix_corporatesetting_corp_name_unique");
        }

        private void CreateDefinitionIndexes(CreateIndexOptions options)
        {
            CreateIndex(GetCollection<AppDef>(),
                Builders<AppDef>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.DeleteFlag),
                options,
                "ix_appdef_corp_delete");

            CreateIndex(GetCollection<FormDef>(),
                Builders<FormDef>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.AppId).Ascending(x => x.DeleteFlag),
                options,
                "ix_formdef_corp_app_delete");

            CreateIndex(GetCollection<CrossBinding>(),
                Builders<CrossBinding>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.TargetAppId)
                    .Ascending(x => x.SourceFormId)
                    .Ascending(x => x.DeleteFlag),
                CreateUniqueOptions(options),
                "ix_crossbinding_target_form_delete_unique");

            CreateIndex(GetCollection<DashboardDef>(),
                Builders<DashboardDef>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.AppId).Ascending(x => x.DeleteFlag),
                options,
                "ix_dashboarddef_corp_app_delete");

            CreateIndex(GetCollection<SerialNoSequence>(),
                Builders<SerialNoSequence>.IndexKeys
                    .Ascending(x => x.SerialNoType)
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.AppId)
                    .Ascending(x => x.FormId)
                    .Ascending(x => x.Key),
                CreateUniqueOptions(options),
                "ix_serialnosequence_scope_unique");

            CreateIndex(GetCollection<Wf_Definition>(),
                Builders<Wf_Definition>.IndexKeys.Ascending(x => x.ExternalId).Ascending(x => x.Version),
                options,
                "ix_wfdefinition_external_version");

            CreateIndex(GetCollection<Wf_Definition>(),
                Builders<Wf_Definition>.IndexKeys.Ascending(x => x.ExternalId).Ascending(x => x.IsCurrent),
                options,
                "ix_wfdefinition_external_current");

            CreateIndex(GetCollection<Wf_Definition>(),
                Builders<Wf_Definition>.IndexKeys.Ascending(x => x.AppId).Ascending(x => x.DeleteFlag).Ascending(x => x.IsCurrent),
                options,
                "ix_wfdefinition_app_delete_current");

            CreateIndex(GetCollection<Wf_Definition>(),
                Builders<Wf_Definition>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.FlowType)
                    .Ascending(x => x.EventSource)
                    .Ascending(x => x.SourceId)
                    .Ascending(x => x.DeleteFlag)
                    .Ascending(x => x.Disabled),
                options,
                "ix_wfdefinition_runtime_lookup");
        }

        private void CreateFormDataIndexes(CreateIndexOptions options)
        {
            CreateIndex(GetCollection<FormData>(),
                Builders<FormData>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.DeleteFlag).Ascending(x => x.FormId),
                options,
                "ix_formdata_corp_delete_form");

            CreateIndex(GetCollection<FormData>(),
                Builders<FormData>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.DeleteFlag).Ascending(x => x.AppId),
                options,
                "ix_formdata_corp_delete_app");

            CreateIndex(GetCollection<FormDataChangeLog>(),
                Builders<FormDataChangeLog>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.DataId).Descending(x => x.OperateTime),
                options,
                "ix_formdatachangelog_corp_data_operatetime");

            CreateIndex(GetCollection<FormDataChangeLog>(),
                Builders<FormDataChangeLog>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.FormId).Descending(x => x.OperateTime),
                options,
                "ix_formdatachangelog_corp_form_operatetime");

            CreateIndex(GetCollection<FormDataImportLog>(),
                Builders<FormDataImportLog>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.FormId).Ascending(x => x.Status).Descending(x => x.CreateTime),
                options,
                "ix_formdataimportlog_corp_form_status_createtime");

            CreateIndex(GetCollection<FormDataImportLog>(),
                Builders<FormDataImportLog>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.CreateBy!.Value).Descending(x => x.CreateTime),
                options,
                "ix_formdataimportlog_corp_createby_createtime");
        }

        private void CreateFormNotifyIndexes(CreateIndexOptions options)
        {
            CreateCorpIdIndex<FormNotify>(options, "ix_formnotify_corpid");
            CreateCorpIdIndex<FormNotifyDispatchLog>(options, "ix_formnotifydispatchlog_corpid");
            CreateCorpIdIndex<FormNotifyScheduleItem>(options, "ix_formnotifyscheduleitem_corpid");

            CreateIndex(GetCollection<FormNotify>(),
                Builders<FormNotify>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.Disabled)
                    .Ascending(x => x.TriggerMode)
                    .Ascending(x => x.NextTriggerTime),
                options,
                "ix_formnotify_corp_disabled_trigger_next");

            CreateIndex(GetCollection<FormNotify>(),
                Builders<FormNotify>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.FormId)
                    .Ascending(x => x.TriggerMode)
                    .Ascending(x => x.Disabled),
                options,
                "ix_formnotify_corp_form_trigger_disabled");

            CreateIndex(GetCollection<FormNotifyDispatchLog>(),
                Builders<FormNotifyDispatchLog>.IndexKeys
                    .Ascending(x => x.NotifyId)
                    .Ascending(x => x.DataId)
                    .Ascending(x => x.TriggerTime),
                CreateUniqueOptions(options),
                "ix_formnotifydispatchlog_notify_data_trigger_unique");

            CreateIndex(GetCollection<FormNotifyDispatchLog>(),
                Builders<FormNotifyDispatchLog>.IndexKeys.Ascending(x => x.CreateTime),
                options,
                "ix_formnotifydispatchlog_createtime");

            CreateIndex(GetCollection<FormNotifyScheduleItem>(),
                Builders<FormNotifyScheduleItem>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.TriggerTime)
                    .Ascending(x => x.TriggerMode),
                options,
                "ix_formnotifyscheduleitem_corp_trigger_triggermode");

            CreateIndex(GetCollection<FormNotifyScheduleItem>(),
                Builders<FormNotifyScheduleItem>.IndexKeys
                    .Ascending(x => x.NotifyId)
                    .Ascending(x => x.DataId),
                CreateUniqueOptions(options),
                "ix_formnotifyscheduleitem_notify_data_unique");
        }

        private void CreateWebhookIndexes(CreateIndexOptions options)
        {
            CreateIndex(GetCollection<Webhook>(),
                Builders<Webhook>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.AppId).Ascending(x => x.FormId).Ascending(x => x.Disabled),
                options,
                "ix_webhook_corp_app_form_disabled");

            CreateIndex(GetCollection<WebhookAlias>(),
                Builders<WebhookAlias>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.AppId).Ascending(x => x.FormId),
                CreateUniqueOptions(options),
                "ix_webhookalias_corp_app_form_unique");
        }

        private void CreateWorkflowBusinessIndexes(CreateIndexOptions options)
        {
            CreateIndex(GetCollection<Wf_Task>(),
                Builders<Wf_Task>.IndexKeys.Ascending(x => x.DataId).Ascending(x => x.EmployeeId),
                options,
                "ix_wftask_data_employee");

            CreateIndex(GetCollection<Wf_Task>(),
                Builders<Wf_Task>.IndexKeys.Ascending(x => x.DataId).Ascending(x => x.ApproveNodeId).Ascending(x => x.EmployeeId),
                options,
                "ix_wftask_data_node_employee");

            CreateIndex(GetCollection<Wf_Task>(),
                Builders<Wf_Task>.IndexKeys.Ascending(x => x.WfInstanceId).Ascending(x => x.ApproveNodeId),
                options,
                "ix_wftask_instance_node");

            CreateIndex(GetCollection<Wf_Task>(),
                Builders<Wf_Task>.IndexKeys.Ascending(x => x.CorpId).Descending(x => x.ApproveNodeStartTime),
                options,
                "ix_wftask_corp_starttime");

            CreateIndex(GetCollection<Wf_Task>(),
                Builders<Wf_Task>.IndexKeys.Ascending(x => x.ExpireHandled).Ascending(x => x.ExpireTime),
                options,
                "ix_wftask_expire");

            CreateIndex(GetCollection<Wf_TaskLog>(),
                Builders<Wf_TaskLog>.IndexKeys.Ascending(x => x.DataId).Ascending(x => x.Round).Ascending(x => x.ApprovalTime),
                options,
                "ix_wftasklog_data_round_time");

            CreateIndex(GetCollection<Wf_TaskLog>(),
                Builders<Wf_TaskLog>.IndexKeys.Ascending(x => x.DataId).Descending(x => x.ApprovalTime),
                options,
                "ix_wftasklog_data_time");

            CreateIndex(GetCollection<Wf_TaskLog>(),
                Builders<Wf_TaskLog>.IndexKeys.Ascending(x => x.DataId).Ascending(x => x.NodeType),
                options,
                "ix_wftasklog_data_nodetype");
        }

        private void CreateWorkflowRuntimeIndexes(CreateIndexOptions options)
        {
            CreateIndex(_dbContext.WorkflowInstances,
                Builders<WorkflowInstance>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.NextExecution),
                options,
                "ix_workflowinstance_status_nextexecution");

            CreateIndex(_dbContext.WorkflowInstances,
                Builders<WorkflowInstance>.IndexKeys.Ascending(x => x.Reference).Ascending(x => x.Status).Descending(x => x.CreateTime),
                options,
                "ix_workflowinstance_reference_status_createtime");

            CreateIndex(_dbContext.WorkflowInstances,
                Builders<WorkflowInstance>.IndexKeys.Ascending(x => x.WorkflowDefinitionId).Ascending(x => x.Status),
                options,
                "ix_workflowinstance_definition_status");

            CreateIndex(_dbContext.WorkflowInstances,
                Builders<WorkflowInstance>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.CompleteTime),
                options,
                "ix_workflowinstance_status_completetime");

            CreateIndex(_dbContext.WorkflowEventSubscriptions,
                Builders<EventSubscription>.IndexKeys.Ascending(x => x.EventName).Ascending(x => x.EventKey).Ascending(x => x.SubscribeAsOf).Ascending(x => x.ExternalToken),
                options,
                "ix_subscription_event_lookup");

            CreateIndex(_dbContext.WorkflowEventSubscriptions,
                Builders<EventSubscription>.IndexKeys.Ascending(x => x.WorkflowId),
                options,
                "ix_subscription_workflowid");

            CreateIndex(_dbContext.WorkflowEvents,
                Builders<Event>.IndexKeys.Ascending(x => x.IsProcessed).Ascending(x => x.EventTime),
                options,
                "ix_event_processed_time");

            CreateIndex(_dbContext.WorkflowEvents,
                Builders<Event>.IndexKeys.Ascending(x => x.EventName).Ascending(x => x.EventKey).Ascending(x => x.EventTime),
                options,
                "ix_event_name_key_time");

            CreateIndex(_dbContext.WorkflowScheduledCommands,
                Builders<ScheduledCommand>.IndexKeys.Ascending(x => x.CommandName).Ascending(x => x.Data),
                CreateUniqueOptions(options),
                "ix_scheduledcommand_name_data_unique");

            CreateIndex(_dbContext.WorkflowScheduledCommands,
                Builders<ScheduledCommand>.IndexKeys.Ascending(x => x.ExecuteTime),
                options,
                "ix_scheduledcommand_executetime");
        }

        private void CreateLogIndexes(CreateIndexOptions options)
        {
            CreateIndex(GetCollection<AuditLog>(),
                Builders<AuditLog>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.DeleteFlag).Descending(x => x.CreateTime),
                options,
                "ix_auditlog_corp_delete_createtime");

            CreateIndex(GetCollection<AuditLog>(),
                Builders<AuditLog>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.EntityType).Ascending(x => x.Action).Descending(x => x.CreateTime),
                options,
                "ix_auditlog_corp_entity_action_createtime");
        }

        private void CreateDataflowLogIndexes(CreateIndexOptions options)
        {
            // Df_RunLog
            CreateIndex(GetCollection<Df_RunLog>(),
                Builders<Df_RunLog>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.DataflowId)
                    .Ascending(x => x.DeleteFlag)
                    .Descending(x => x.TriggerTime),
                options,
                "ix_dfrunlog_corp_dataflow_delete_triggertime");

            CreateIndex(GetCollection<Df_RunLog>(),
                Builders<Df_RunLog>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.AppId)
                    .Ascending(x => x.DeleteFlag)
                    .Descending(x => x.TriggerTime),
                options,
                "ix_dfrunlog_corp_app_delete_triggertime");

            // Df_RunLogNode
            CreateIndex(GetCollection<Df_RunLogNode>(),
                Builders<Df_RunLogNode>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.RunLogId)
                    .Ascending(x => x.StartTime),
                options,
                "ix_dfrunlognode_corp_runlog_starttime");

            CreateIndex(GetCollection<Df_RunLogNode>(),
                Builders<Df_RunLogNode>.IndexKeys
                    .Ascending(x => x.RunLogId)
                    .Ascending(x => x.StartTime),
                options,
                "ix_dfrunlognode_runlog_starttime");
        }

        private void CreateDataflowScheduleIndexes(CreateIndexOptions options)
        {
            CreateCorpIdIndex<DataflowScheduleItem>(options, "ix_dataflowscheduleitem_corpid");

            CreateIndex(GetCollection<DataflowScheduleItem>(),
                Builders<DataflowScheduleItem>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.DataflowId)
                    .Ascending(x => x.TriggerTime),
                options,
                "ix_dataflowscheduleitem_corp_dataflow_triggertime");

            CreateIndex(GetCollection<DataflowScheduleItem>(),
                Builders<DataflowScheduleItem>.IndexKeys
                    .Ascending(x => x.DataflowId)
                    .Ascending(x => x.SourceType)
                    .Ascending(x => x.FormId)
                    .Ascending(x => x.DataId),
                CreateUniqueOptions(options),
                "ix_dataflowscheduleitem_dataflow_source_form_data_unique");

            CreateIndex(GetCollection<DataflowScheduleItem>(),
                Builders<DataflowScheduleItem>.IndexKeys
                    .Ascending(x => x.ScheduleVersion)
                    .Ascending(x => x.TriggerTime),
                options,
                "ix_dataflowscheduleitem_version_triggertime");
        }

        private void CreateWorkbenchIndexes(CreateIndexOptions options)
        {
            CreateIndex(GetCollection<WorkbenchConfig>(),
                Builders<WorkbenchConfig>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.EmployeeId).Ascending(x => x.DeleteFlag),
                options,
                "ix_workbenchconfig_corp_employee_delete");

            CreateIndex(GetCollection<WorkbenchFavorite>(),
                Builders<WorkbenchFavorite>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.EmployeeId)
                    .Ascending(x => x.TargetType)
                    .Ascending(x => x.TargetId)
                    .Ascending(x => x.DeleteFlag),
                options,
                "ix_workbenchfavorite_target");

            CreateIndex(GetCollection<WorkbenchFavorite>(),
                Builders<WorkbenchFavorite>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.EmployeeId).Ascending(x => x.SortIndex),
                options,
                "ix_workbenchfavorite_sort");

            CreateIndex(GetCollection<WorkbenchRecentVisit>(),
                Builders<WorkbenchRecentVisit>.IndexKeys
                    .Ascending(x => x.CorpId)
                    .Ascending(x => x.EmployeeId)
                    .Ascending(x => x.TargetType)
                    .Ascending(x => x.TargetId)
                    .Ascending(x => x.DeleteFlag),
                options,
                "ix_workbenchrecent_target");

            CreateIndex(GetCollection<WorkbenchRecentVisit>(),
                Builders<WorkbenchRecentVisit>.IndexKeys.Ascending(x => x.CorpId).Ascending(x => x.EmployeeId).Descending(x => x.LastVisitTime),
                options,
                "ix_workbenchrecent_lastvisit");
        }

        private void CreateCorpIdIndex<T>(CreateIndexOptions options, string name) where T : CorpEntityBase
        {
            CreateIndex(GetCollection<T>(), Builders<T>.IndexKeys.Ascending(x => x.CorpId), options, name);
        }

        private IMongoCollection<T> GetCollection<T>()
        {
            return _dbContext.GetCollection<T>();
        }

        private IMongoCollection<T> GetCollection<T>(string name)
        {
            return _dbContext.GetCollection<T>(name);
        }

        private static CreateIndexOptions CreateUniqueOptions(CreateIndexOptions source)
        {
            return new CreateIndexOptions { Background = source.Background, Unique = true };
        }

        private static void CreateIndex<T>(IMongoCollection<T> collection, IndexKeysDefinition<T> keys, CreateIndexOptions options, string name)
        {
            var indexOptions = new CreateIndexOptions
            {
                Background = options.Background,
                Unique = options.Unique,
                Name = name
            };
            collection.Indexes.CreateOne(new CreateIndexModel<T>(keys, indexOptions));
        }
    }
}
