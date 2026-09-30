-- 审批日志补流程实例归属，并约束同一条数据只允许一个在途流程实例。
--
-- 001 的模型段已带 "Wf_TaskLog"."WfInstanceId"，这里用 if not exists 前向补齐已存在的库，
-- 因此全新库（001 已建列）与本库（本脚本建列）执行同一条迁移链都能得到一致结构。

alter table "Wf_TaskLog" add column if not exists "WfInstanceId" citext COLLATE "C" not null default '';

create index if not exists "IX_Wf_TaskLog_WfInstanceId" on "Wf_TaskLog" ("WfInstanceId");

-- 同一条数据只允许一个在途实例（Runnable=0 / Suspended=1）。已完成/已终止的实例不参与，
-- 流程走完后仍可重新发起。并发 Start 时后到者会撞这条约束而不是静默建出第二个实例。
create unique index if not exists "UX_WorkflowInstance_Active_Reference"
    on "WorkflowInstance" (lower("Reference"))
 where "Status" in (0, 1);

-- 历史日志按 数据Id + 流程版本 回填实例Id；仅当该数据恰好对应单一实例时才写，避免归属写错。
update "Wf_TaskLog" l
   set "WfInstanceId" = i."Id"
  from "WorkflowInstance" i
 where l."WfInstanceId" = ''
   and lower(i."Reference") = lower(l."DataId")
   and i."Version" = l."WfVersion"
   and (select count(*) from "WorkflowInstance" i2
         where lower(i2."Reference") = lower(l."DataId") and i2."Version" = l."WfVersion") = 1;
