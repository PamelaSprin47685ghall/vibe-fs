# leftover-scaffolding — Enforcer 中文版

## 定义

Scaffolding leftover 是为迁移、调试、rollout、实验、一次性生成临时搭的支架，在过渡目的结束后仍留在 delivered tree，却没有被正式提升成长期工具。

临时结构的设计目标是“帮我们跨过这段路”，不是“未来几年都值得维护”。时间过去不会自动把临时性变成架构正当性。

## 支配原则

临时结构与受维护结构有不同的 contract：它为一个过渡而优化，不是为无限期的 ownership。过渡一旦结束，把 scaffolding 留在原地就是在没有指派晋升所需的维护职责时悄悄提升它。仓库于是积累 intended lifetime 未知的 artifact，删除风险只因它们存活得更久而增长。

## 何时触发

- debug/migration script、probe、flag、fixture、temp branch 任务结束后仍存在；
- rollout 已完成，feature flag 仍无 removal owner；
- one-off generator 没有长期 user，却被保留“可能还有用”；
- 临时 bypass/config 成为默认路径的一部分。

## 不应触发

- scaffold 已被有意晋升为 maintained tool，有稳定 purpose/tests/docs/owner；
- regression fixture 已成为长期 executable memory；
- rollout 仍活跃、有明确 owner 与 exit condition；
- shipping implementation 本身仍是 prototype，应归 `spike-not-cleaned`。

## 与相邻规则区分

`spike-not-cleaned` 把实验性设计本身 shipping 出去；`half-finished-refactor` 留下双架构。本规则管的是在它们所服务的过渡结束后仍然存活的附属临时 artifact。Tie-break：如果 leftover 是双 ownership 架构，用 `half-finished-refactor`；如果是本应消失的 probe/script/flag，用本规则。

## 判定程序

临时 artifact 必须回答：现在谁用？长期维护 contract 是什么？什么时候执行？没有答案，就不是“成熟了”，只是忘了拆脚手架。

## 例子

- positive：debug dump script 与 `TEMP_SKIP_AUTH` flag 在创造它们的调查结束后仍然存在。
- near-miss：generator 被晋升为带 tests、docs 与 owner 的 maintained command。
- counterexample：任务结束时删除临时 probes；只有 maintained tools 留下。

## Nudge

Temporary 的默认终点是删除。要留下，就必须经过一次明确的 promotion decision，而不是靠存活时间获得产权。