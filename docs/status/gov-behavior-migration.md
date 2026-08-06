# GOV-011 行为条款迁移（how/ → what/）

目标：
- 恒定「可观察行为/语义/不变量的权威定义在 what/，how/ 只写机制」（GOV-011）
- 评审红线闭环：已迁移 PROMPT-011、FALLBACK-011/012 到 `what/`

当前：
- GOV-011 定则并写入 `what/document-governance.md`。
- PROMPT-011（what/prompt.md）、FALLBACK-011/012（what/fallback.md）已迁；how/ 只留指针与机制。

缺口：
- 下列 how/ 条款仍混有行为/语义陈述，按 GOV-011 判据应升 what/。未强迁：它们机制与不变量深度交织，
  一次性剥离会撕裂一致性，故登记为本缺口，逐条处理：
  - `EXEC-021` finality / `LegacyFalseAbort` 永不 `RunCompletion`；`EXEC-022` 假 completion 补偿语义
  - `PERSIST-010` 上下文恢复 fold 拒绝条件（不变量）
  - `CTX-013` Blogger delta TOML 行为；`CTX-010` probe 提交语义（how/context.md）
  - `ENFORCER-071` previous_enforcer_tip 低信任呈现
  - `ORCH-007` 恢复唯一动作与 PublishClaimed 三分支（崩溃后 at-most-one 发布语义）

迁移纪律：what/ 落「可观察行为」句，how/ 留机制/类型/算法；同变更跑 `node scripts/checks/spec.mjs` 至绿；
每条完成后删除本列表对应项。全部完成为对齐，删除本 status 条目。

阻塞：无。
