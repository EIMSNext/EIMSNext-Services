create unique index if not exists "UX_UserCorp_UserId_CorpId" on "UserCorp" ("UserId", "CorpId");
create unique index if not exists "UX_EmployeeDepartment_CorpId_EmployeeId_DepartmentId" on "EmployeeDepartment" ("CorpId", "EmployeeId", "DepartmentId");
create index if not exists "IX_EmployeeDepartment_CorpId_EmployeeId_SortValue" on "EmployeeDepartment" ("CorpId", "EmployeeId", "SortValue");
create index if not exists "IX_EmployeeDepartment_CorpId_DepartmentId_EmployeeId" on "EmployeeDepartment" ("CorpId", "DepartmentId", "EmployeeId");
create index if not exists "IX_Employee_CorpId_Code" on "Employee" ("CorpId", "Code");
create index if not exists "IX_Department_CorpId_Code" on "Department" ("CorpId", "Code");
create index if not exists "IX_FormData_CorpId_FormId_DeleteFlag" on "FormData" ("CorpId", "FormId", "DeleteFlag");
create index if not exists "IX_FormData_Data_Gin" on "FormData" using gin ("Data" jsonb_path_ops);
