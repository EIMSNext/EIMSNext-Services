-- 业务字符列使用 citext（不区分大小写）。
--
-- EF 把业务 string / 文本枚举映射为 citext 后，Npgsql 会连标量参数一起按 citext 发送；
-- 密码、凭证、正文等需要大小写敏感的列仍保持 text；数组列继续使用 text[]，由查询语义决定比较规则。
-- 列侧与参数侧类型一致，等值 / IN / LIKE 天然大小写无关且能命中索引，
-- 因此不需要补 (citext, text) 之类的比较操作符。
-- 版本号 000 < 001，随迁移链先于建表执行。

create extension if not exists citext;
