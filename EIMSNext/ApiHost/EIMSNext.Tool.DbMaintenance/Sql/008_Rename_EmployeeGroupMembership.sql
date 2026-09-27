-- 008: 将员工-员工组关系表 EmployeeGroupMembership 重命名为 EmployeeGroupMember。
-- 与实体类（EmployeeGroupMember）、EDM 实体集（EmployeeGroupMember）及基线 001/002 的命名保持一致。
--
-- 安全性：
--   该脚本对所有库安全。DbMaintenance 按台账保证每个库只执行一次。
--   - 全新库（001/002 已直接以 EmployeeGroupMember 建表建索引）：下方各分支均不命中，整体 no-op；
--   - 既有库（仍使用旧名 EmployeeGroupMembership）：执行实际重命名。
-- 采用存在性判断而非 IF EXISTS 子句，因 PostgreSQL 的 ALTER ... RENAME CONSTRAINT / RENAME
-- 不支持 IF EXISTS，需用 pg_catalog 预检以避免在全新库上抛错。

DO $$
BEGIN
    -- 表
    IF EXISTS (
        SELECT 1 FROM pg_tables
        WHERE schemaname = current_schema() AND tablename = 'EmployeeGroupMembership'
    ) THEN
        ALTER TABLE "EmployeeGroupMembership" RENAME TO "EmployeeGroupMember";
    END IF;

    -- 主键约束
    IF EXISTS (
        SELECT 1 FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        WHERE t.relname = 'EmployeeGroupMember' AND c.conname = 'PK_EmployeeGroupMembership'
    ) THEN
        ALTER TABLE "EmployeeGroupMember" RENAME CONSTRAINT "PK_EmployeeGroupMembership" TO "PK_EmployeeGroupMember";
    END IF;

    -- 外键约束
    IF EXISTS (
        SELECT 1 FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        WHERE t.relname = 'EmployeeGroupMember' AND c.conname = 'FK_EmployeeGroupMembership_Employee_EmployeeId'
    ) THEN
        ALTER TABLE "EmployeeGroupMember" RENAME CONSTRAINT "FK_EmployeeGroupMembership_Employee_EmployeeId" TO "FK_EmployeeGroupMember_Employee_EmployeeId";
    END IF;

    -- 索引（4 个，与 001/002 新旧命名一一对应）
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = current_schema() AND indexname = 'IX_EmployeeGroupMembership_EmployeeId') THEN
        ALTER INDEX "IX_EmployeeGroupMembership_EmployeeId" RENAME TO "IX_EmployeeGroupMember_EmployeeId";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = current_schema() AND indexname = 'IX_EmployeeGroupMembership_CorpId_EmployeeGroupId_EmployeeId') THEN
        ALTER INDEX "IX_EmployeeGroupMembership_CorpId_EmployeeGroupId_EmployeeId" RENAME TO "IX_EmployeeGroupMember_CorpId_EmployeeGroupId_EmployeeId";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = current_schema() AND indexname = 'IX_EmployeeGroupMembership_CorpId_EmployeeId') THEN
        ALTER INDEX "IX_EmployeeGroupMembership_CorpId_EmployeeId" RENAME TO "IX_EmployeeGroupMember_CorpId_EmployeeId";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = current_schema() AND indexname = 'UX_EmployeeGroupMembership_CorpId_EmployeeId_EmployeeGroupId') THEN
        ALTER INDEX "UX_EmployeeGroupMembership_CorpId_EmployeeId_EmployeeGroupId" RENAME TO "UX_EmployeeGroupMember_CorpId_EmployeeId_EmployeeGroupId";
    END IF;
END $$;
