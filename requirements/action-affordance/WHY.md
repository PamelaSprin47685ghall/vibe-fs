# WHY —— 不可替代的存在理由

## 为什么必须独立存在

participant 在做决定的那一点上，唯一可靠的输入是**当前 decision surface 上写着的合同**。
被调用方（另一个 office）的 Role Law 不在调用方眼前；正确理解长期 world model（`cognitive-environment`）
也不等于知道「此刻这个 verb 到底做什么」。

历史教训（archive/docs/why/prompt.md「Tool description：tooltip vs 调用合同」）：

> 拒一句正向描述。调用方看不见被调用方 Role Law；`inspect` 若只说 "Ask an Inspector to establish a
> repository fact"，Coder 会把修复写进 charge。

一个动作的五问必须**在调用瞬间**可回答：

```text
1. What act happens?
2. When does this act fit?
3. What tempting nearby act does this NOT perform?
4. What does a successful return establish?
5. What does each non-obvious argument mean?
```

**唯一不可替代的 WHY**：调用瞬间的局部认知合同。它可以整体重写所有动作说明与参数语义，而长期认知
（`cognitive-environment`）与 office capability model（`office-capability`）完全不动；反过来，office
consequence 重写也不应要求每个 description 语义重写——mirror 机制保证边界完整性。

## RED 长什么样（失败模式）

| 症状 | 历史出处 |
|---|---|
| `inspect` 只说正向（"Ask an Inspector to establish a repository fact"），无因果只读/不修码负边界 → Coder 把修复写进 charge | archive/docs/why/prompt.md；office-boundary eval case `coder-inspect-ownership` |
| `repair-behavior` 把 mechanical 理解成「物理上很小」→ DevOps 用 repair 做产品含义选择 | PROMPT-020；TOOL_DESCRIPTION_ANCHORS `meaning-decided` |
| 同一名字两个 contract（`join` 两处语义不同、legacy `executor` 角色名/工具名共用） | ARCH-006/007：`A tool name names one contract everywhere.` |
| `calling` 当裸 enum → 模型看不出它是 authority/capability 选择 | PROMPT-020：`calling` 不是普通 enum（ARCH-017） |
| 因为被调用方 Role Law 已写就从调用方 tool contract 删掉同一区别 | PROMPT-021：`Single semantic ownership does not require single presentation.` |

## 为什么不是「capability」包

`office-capability` 回答「office 有资格产生什么后果」；`capability-enforcement` 回答「provider 看见的
与 runtime 能执行的是否同源」。本包回答第三件完全不同的事：**在调用瞬间，参与者的 decision surface
上是否写清了 act 的边界与后果。** 本包可以多次镜像 canonical 事实（PROMPT-021），但镜像不产生第二
semantic ownership。

## 被拒方案（考古）

- **tooltip 式一句话正向描述。** 拒绝（见上）。
- **DRY 掉调用方合同。** 拒绝：`Single semantic ownership does not require single presentation.`
  机器已知的 office ontology 必须完整成为 participant 能够据以行动的世界知识（archive/docs/why/prompt.md）。
- **同工具名复用（schema 相同即可）。** 拒绝：`join` 可在 Manager 与 Orchestrator 共享，当且仅当
  语义合同完全同一（ARCH-007）。仅 schema 相同不足。
- **名字当 authority 证据。** 拒绝：禁止让模型从词汇推断 authority；世界已经知道合同时，必须把合同
  送到做决定的模型面前（PROMPT-021）。
