-- 业务字符列使用 citext（不区分大小写）。
--
-- EF 把 string / 文本枚举映射为 citext 后，Npgsql 会连参数一起按 citext 发送（含 citext[] 数组），
-- 列侧与参数侧类型一致，等值 / IN / LIKE 天然大小写无关且能命中索引，
-- 因此不需要补 (citext, text) 之类的比较操作符。
-- 版本号 000 < 001，随迁移链先于建表执行。

create extension if not exists citext;
