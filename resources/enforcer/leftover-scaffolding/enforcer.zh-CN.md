# leftover-scaffolding — Enforcer 中文版

## 定义
Scaffolding leftover 是为迁移、调试、rollout、实验、一次性生成临时搭的支架，在过渡目的结束后仍留在 delivered tree，却没有被正式提升成长期工具。

临时结构的设计目标是“帮我们跨过这段路”，不是“未来几年都值得维护”。时间过去不会自动把临时性变成架构正当性。

## 何时触发
- debug/migration script、probe、flag、fixture、temp branch 任务结束后仍存在；
- rollout 已完成，feature flag 仍无 removal owner；
- one-off generator 没有长期 user，却被保留“可能还有用”；
- 临时 bypass/config 成为默认路径的一部分。

## 不要误判
- scaffold 已被有意晋升为 maintained tool，有稳定 purpose/tests/docs/owner；
- regression fixture 已成为长期 executable memory；
- rollout 仍活跃、有明确 owner 与 exit condition；
- shipping implementation 本身仍是 prototype，应归 `spike-not-cleaned`。

## 刀口
临时 artifact 必须回答：**现在谁用？长期维护 contract 是什么？什么时候执行？** 没有答案，就不是“成熟了”，只是忘了拆脚手架。

## 提醒
Temporary 的默认终点是删除。要留下，就必须经过一次明确的 promotion decision，而不是靠存活时间获得产权。
