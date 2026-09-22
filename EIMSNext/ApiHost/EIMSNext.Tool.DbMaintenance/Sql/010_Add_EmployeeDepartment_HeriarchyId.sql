-- 010: EmployeeDepartment 增加部门层级路径快照 HeriarchyId。
-- 用途：级联按部门查员工时直接对该列做 Contains（'|deptId|' 片段整体匹配），
--       省去 EmployeeDepartment → Department 的导航/联表；快照由 DepartmentService 在
--       部门创建/移动/下级层级刷新时同步维护。
-- 幂等性：ADD COLUMN IF NOT EXISTS 保证重复执行安全；回填仅处理仍为空的行。
-- 全新库（由 001 直接建表）本脚本为 no-op（列已存在、无历史行需要回填）。

ALTER TABLE "EmployeeDepartment" ADD COLUMN IF NOT EXISTS "HeriarchyId" text NOT NULL DEFAULT '';

-- 存量数据回填：从 Department 表取当前层级路径。
UPDATE "EmployeeDepartment" ed
SET "HeriarchyId" = d."HeriarchyId"
FROM "Department" d
WHERE ed."DepartmentId" = d."Id"
  AND ed."HeriarchyId" = ''
  AND d."HeriarchyId" IS NOT NULL
  AND d."HeriarchyId" <> '';
