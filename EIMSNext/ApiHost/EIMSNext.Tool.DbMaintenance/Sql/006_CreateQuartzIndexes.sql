-- Quartz 索引。与 005 同源，见 tables_postgres.sql。

-- Source: https://github.com/quartznet/quartznet/blob/v3.18.2/database/tables/tables_postgres.sql
-- Pinned to Quartz 3.18.2. Destructive bootstrap removed; run once through DbMaintenance.
CREATE INDEX idx_qrtz_j_req_recovery ON qrtz_job_details (requests_recovery);
CREATE INDEX idx_qrtz_t_next_fire_time ON qrtz_triggers (next_fire_time);
CREATE INDEX idx_qrtz_t_state ON qrtz_triggers (trigger_state);
CREATE INDEX idx_qrtz_t_nft_st ON qrtz_triggers (next_fire_time, trigger_state);
CREATE INDEX idx_qrtz_ft_trig_name ON qrtz_fired_triggers (trigger_name);
CREATE INDEX idx_qrtz_ft_trig_group ON qrtz_fired_triggers (trigger_group);
CREATE INDEX idx_qrtz_ft_trig_nm_gp ON qrtz_fired_triggers (sched_name, trigger_name, trigger_group);
CREATE INDEX idx_qrtz_ft_trig_inst_name ON qrtz_fired_triggers (instance_name);
CREATE INDEX idx_qrtz_ft_job_name ON qrtz_fired_triggers (job_name);
CREATE INDEX idx_qrtz_ft_job_group ON qrtz_fired_triggers (job_group);
CREATE INDEX idx_qrtz_ft_job_req_recovery ON qrtz_fired_triggers (requests_recovery);
