-- Persist the form name snapshot on workflow tasks.
-- 列允许为 NULL：历史任务在创建时未必能解析出表单名，且回填场景可能为空。
alter table "Wf_Task"
    add column if not exists "FormName" citext;
