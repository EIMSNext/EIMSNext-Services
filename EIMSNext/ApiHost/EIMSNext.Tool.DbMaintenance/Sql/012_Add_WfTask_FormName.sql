-- Persist the form name snapshot on workflow tasks.
alter table "Wf_Task"
    add column if not exists "FormName" citext not null default '';
