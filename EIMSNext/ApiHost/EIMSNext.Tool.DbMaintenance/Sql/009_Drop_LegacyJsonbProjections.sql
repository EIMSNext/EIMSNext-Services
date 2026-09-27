-- 009: 删除 Employee / User 上遗留的 jsonb 兼容投影列。
-- 这三列在 MongoDB 时代以 jsonb 内嵌数组存储「员工↔部门 / 员工↔员工组 / 用户↔企业」关系；
-- 迁移到 PostgreSQL 后已提升为独立关系表（EmployeeDepartment / EmployeeGroupMember / UserCorp），
-- 实体属性（Employee.Depts、Employee.EmployeeGroups、User.Crops）已删除，列随之废弃。
--
-- 安全性：
--   该脚本对所有库安全。DbMaintenance 按台账保证每个库只执行一次。
--   - 全新库（再生后的 001 已不再生成这三列）：DROP COLUMN IF EXISTS 全部 no-op；
--   - 既有库（仍保留这三列）：执行实际删除。
-- 使用 PostgreSQL 原生的 DROP COLUMN IF EXISTS，无需 pg_catalog 预检。

ALTER TABLE "Employee" DROP COLUMN IF EXISTS "Depts";
ALTER TABLE "Employee" DROP COLUMN IF EXISTS "EmployeeGroups";
ALTER TABLE "User" DROP COLUMN IF EXISTS "Crops";
