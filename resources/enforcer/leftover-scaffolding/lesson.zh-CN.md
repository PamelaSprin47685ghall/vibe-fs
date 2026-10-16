# leftover-scaffolding — Lesson

## 评价

### 定义

Scaffolding leftover 是为迁移、调试、rollout、实验、一次性生成临时搭的支架，在过渡目的结束后仍留在 delivered tree，却没有被正式提升成长期工具。

临时结构的设计目标是“帮我们跨过这段路”，不是“未来几年都值得维护”。时间过去不会自动把临时性变成架构正当性。

### 支配原则

临时结构与受维护结构有不同的 contract：它为一个过渡而优化，不是为无限期的 ownership。过渡一旦结束，把 scaffolding 留在原地就是在没有指派晋升所需的维护职责时悄悄提升它。仓库于是积累 intended lifetime 未知的 artifact，删除风险只因它们存活得更久而增长。

### 何时触发

- debug/migration script、probe、flag、fixture、temp branch 任务结束后仍存在；
- rollout 已完成，feature flag 仍无 removal owner；
- one-off generator 没有长期 user，却被保留“可能还有用”；
- 临时 bypass/config 成为默认路径的一部分。

### 不应触发

- scaffold 已被有意晋升为 maintained tool，有稳定 purpose/tests/docs/owner；
- regression fixture 已成为长期 executable memory；
- rollout 仍活跃、有明确 owner 与 exit condition；
- shipping implementation 本身仍是 prototype，应归 `spike-not-cleaned`。

### 与相邻规则区分

`spike-not-cleaned` 把实验性设计本身 shipping 出去；`half-finished-refactor` 留下双架构。本规则管的是在它们所服务的过渡结束后仍然存活的附属临时 artifact。Tie-break：如果 leftover 是双 ownership 架构，用 `half-finished-refactor`；如果是本应消失的 probe/script/flag，用本规则。

### 判定程序

临时 artifact 必须回答：现在谁用？长期维护 contract 是什么？什么时候执行？没有答案，就不是“成熟了”，只是忘了拆脚手架。

### 例子

- positive：debug dump script 与 `TEMP_SKIP_AUTH` flag 在创造它们的调查结束后仍然存在。
- near-miss：generator 被晋升为带 tests、docs 与 owner 的 maintained command。
- counterexample：任务结束时删除临时 probes；只有 maintained tools 留下。

## 建议

### 现在该做什么

为每个 transitional artifact 做二选一：删除，或正式晋升。晋升意味着它获得清晰 purpose、owner、tests、docs、entry point 与 maintenance promise；不能只因为“已经在 repo 里”就继续存在。

### 为什么重要

临时 artifact 越老越难删，因为后来的人会把“它活了这么久”误当成“它一定重要”。于是 accidental survival 变成伪需求，repository 不断积累无人敢碰的神秘脚手架。

### 修复策略

复查本次工作引入的 flags、scripts、fixtures、probes、临时文件与 migration helpers。删除没有 ongoing consumer 的；对晋升的 artifact，给出清晰名字、正常 test path 与 owner。

### 决策分支

- 若 transition 已完成且没有 ongoing user，删除 artifact。
- 若 artifact 现在有 durable workflow，带 tests、docs 与 owner 晋升它。

### 常见假修复

- 移到 `scripts/old` / `misc`。
- 加 `temporary` comment，却不设 exit。
- 保留 rollout flag “以后紧急时可能用”。
- 把所有 scaffolding 一律产品化，制造更多长期工具。

### 验证

任务/迁移结束后，搜索其临时 marker、flag、script、probe。剩下的每一个都应能出示长期维护合同，否则删除。

### 完成条件

过渡结束时，支架也结束；留下的东西都是被有意维护的产品/工具，而不是历史事故。

### Nudge

Temporary 的默认终点是删除。要留下，就必须经过一次明确的 promotion decision，而不是靠存活时间获得产权。
