-- jsonb 内部键的取值 / 匹配辅助函数。
--
-- 背景
-- ----
-- 表单数据（FormData.Data）、工作流条件、仪表盘等业务都按 `data.<字段>` 的形式
-- 过滤/排序 jsonb 列内部的键，例如 EIMSNext.Component.ConditionList 会产出
-- `data.n_<nodeId>.<field>`、FormDataApiService 会产出 `data.<field>.value`。
-- Mongo 时期这些内部字段是一等公民，直接用字符串路径构造 filter 即可；
-- PostgreSQL 下 jsonb 内部键必须通过 `->` / `->>` / JSONPath 访问，而 EF Core
-- 无法把 ExpandoObject 的索引器翻译成运算符（值转换后的属性在模型里只是一个 jsonb 标量）。
--
-- 方案
-- ----
-- 把「路径取值 / 路径匹配」下沉成这两个 SQL 函数，C# 侧用 DbFunction 映射
-- （见 EIMSNext.Core.Query.PgJsonFunctions），表达式树里就只剩一次普通函数调用，
-- EF Core 可以正常翻译，Where / OrderBy / Count / ExecuteUpdate / ExecuteDelete 都能用。
--
-- 关键语义选择：**JSONPath 的 lax 模式会自动展开数组**。这一点决定了不能用
-- `jsonb_extract_path_text`——它在 `a` 是数组时对 `a.b` 直接返回 NULL；
-- 而 Mongo 的 `a.b` 在 `a` 为数组时表示「任意元素」。
-- 实测（PostgreSQL 18）：
--   jsonb_path_query_first('{"a":[{"b":1},{"b":2}]}', '$."a"."b"')            -- 1
--   jsonb_path_exists('{"a":[{"b":1},{"b":2}]}', '$."a"."b" ? (@ > 1)')       -- true
--   jsonb_path_exists('{"a":[{"b":1},{"b":2}]}', '$."a"."b" ? (@ > 2)')       -- false
--
-- 函数说明
-- --------
-- eims_json_text(jsonb, text)  —— 取路径上的第一个值的文本形式，供「把值当文本」的场景。
--                                 标量用 `#>> '{}'` 去掉 JSON 引号：字符串 'x' → x，数字 1 → 1，
--                                 布尔 true → true；路径不可达或值为 JSON null → NULL。
-- eims_json_match(jsonb, text) —— 路径上是否存在满足条件的元素，供过滤使用。
--                                 调用方传入的 JSONPath 自带 `? (…)` 谓词时即为「任意元素满足」语义，
--                                 正好对应 Mongo 的数组元素匹配。
-- eims_json_sort(jsonb, text)  —— 取路径上的第一个值，**保持 jsonb 类型**，供 ORDER BY 使用。
--                                 存在的理由：eims_json_text 返回 text，排序会退化成字典序
--                                 （10 排在 9 前面）。jsonb 有 btree 比较运算符，数字按数值比较、
--                                 字符串按排序规则比较，才与 Mongo 的排序语义一致。
--                                 路径不可达时返回 jsonb 的 'null'（非 SQL NULL），
--                                 使「字段缺失」落在最小值位置，与 Mongo 一致；详见函数体内注释。
--
-- 其它约定
-- --------
--   * 前两个函数声明 strict：入参为 NULL 时返回 NULL / NULL(boolean)，不会因为某行
--     Data 为 NULL 就把整个条件判成真。eims_json_sort 刻意**不**加 strict，理由见其函数体注释。
--   * jsonpath 由 C# 侧构造（见 DynamicPathAccessor.BuildJsonPath），因此这里是 text 入参再显式转换，
--     便于出错时在语句里直接看到路径原文。

create or replace function "eims_json_text"(jsonb, text)
returns text
language sql
immutable
strict
parallel safe
as $$
    select (jsonb_path_query_first($1, $2::jsonpath)) #>> '{}'
$$;

create or replace function "eims_json_match"(jsonb, text)
returns boolean
language sql
immutable
strict
parallel safe
as $$
    select jsonb_path_exists($1, $2::jsonpath)
$$;

create or replace function "eims_json_sort"(jsonb, text)
returns jsonb
language sql
immutable
parallel safe
as $$
    -- 路径不可达 / 值为 JSON null 时返回 jsonb 的 'null'，而不是 SQL NULL。
    -- jsonb 的 btree 顺序里 Null 是最小一类（Object > Array > Boolean > Number > String > Null），
    -- 于是「缺失字段」自然排在最前（升序）/ 最后（降序），正是 Mongo 的 null 语义；
    -- 若返回 SQL NULL，PostgreSQL 会按 NULLS LAST(ASC) / NULLS FIRST(DESC) 处理，反而对不上。
    -- 同理不声明 strict：Data 为 NULL 时也要落到 'null' 而不是短路成 SQL NULL。
    select coalesce(jsonb_path_query_first($1, $2::jsonpath), 'null'::jsonb)
$$;
