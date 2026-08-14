# WHAT —— 唯一 normative 合同

命题前缀 `ACTION-AFFORDANCE-`。每条都是**当前世界必须同时成立**的事实。
证据指针 → [`PROOF.md`](PROOF.md)。

## act contract

### ACTION-AFFORDANCE-001：Tool description 是调用时局部 contract，不是 tooltip

每个非平凡 verb 必须使调用方能回答五问：

```text
1. What act happens?
2. When does this act fit?
3. What tempting nearby act does this NOT perform?
4. What does a successful return establish?
5. What does each non-obvious argument mean?
```

必须包含足够的：positive affordance、negative affordance、boundary mirror、returned consequence、
argument semantics（PROMPT-020）。

### ACTION-AFFORDANCE-002：高风险 verb 有最低合同集合

现行最低集合（PROMPT-020）：

```text
fork, commission, inspect, run, query-shell,
establish-behavior, repair-behavior, fetch, join, horizon,
judge, suicide, fission, chronicle, js-*
```

现行 semantic-anchor 义务（ARCH-016 Gate C）：`fork / inspect / commission / establish-behavior /
repair-behavior / run / query-shell` 七个 description 必须双语同 ID 命中认知锚点。
清单是证据，可随产品重构；「高风险 verb 必须有合同」是不可替代的命题。

### ACTION-AFFORDANCE-003：inspect 必须写「does not implement or repair code」

`inspect` 必须出现「does not implement or repair code」这一认知区别，不只写 `read-only`
（PROMPT-020）。因果只读 = 可检查 source/history/config/artifact 与建立既有事实所需的静态调查，
但**不**修改文件、**不**让项目跑起来制造新行为证据。

### ACTION-AFFORDANCE-004：repair-behavior 必须说明 mechanical 的语义

`repair-behavior` 必须说明 mechanical = **含义已被决定**，不是物理上很小（PROMPT-020）。
返回的 WorkRecord 不是 repair 已经通过的证明（TOOL_DESCRIPTION_ANCHORS `not-passing-proof`）。

### ACTION-AFFORDANCE-005：establish-behavior 分离 mutation 与 execution evidence

`establish-behavior` 必须写明：Coder 写入/修改 source（受托含义内），Coder 完成**不**等于执行证据、
不运行那些测试（TOOL_DESCRIPTION_ANCHORS `not-execution-evidence`）。DevOps 仍须观察运行中的世界
（PROMPT-020）。

### ACTION-AFFORDANCE-006：run/query-shell 是 act，不是预测

`run`：command 是 act、经济承诺，不是运行时预测（`economic commitments, not runtime predictions`）。
`query-shell`：observation, not execution；不适合 build/test/lint（TOOL_DESCRIPTION_ANCHORS）。

## 名字与合同同一性

### ACTION-AFFORDANCE-007：动作名称表达 semantic act，不表达 runtime topology

人是名词（Role / Persona / office）；工具是动词。不同硬语义必须不同名（`commission` ≠ `fork`）；
禁止「用户面同名方便」让 Role 与 Tool 共名承载不同语义（ARCH-006，已删 Executor 角色名/工具名案例）。

### ACTION-AFFORDANCE-008：同一工具名 = 同一 contract 处处成立

```text
same tool name
⇒ same semantic act
   same argument schema
   same meaning of every argument
   same lifecycle consequence
   same return semantics
   same important failure semantics
```

仅 schema 相同不足；role visibility / 永不同时出现不削弱此不变量。`join` 可在 Manager 与 Orchestrator
共享，当且仅当语义合同完全同一（ARCH-007）。schema/名字唯一性执行面归 `capability-enforcement`；
本包拥有「semantic act 合同同一」的认知面。

### ACTION-AFFORDANCE-009：capability choice 不能退化成裸 enum

`calling` 不是普通 enum；它是 authority / capability 选择（PROMPT-020 / ARCH-017）。
两个 calling 名（如 navigator/researcher）只差 persona 与推理深度，不差 office authority。

## boundary mirror

### ACTION-AFFORDANCE-010：fork/commission 必须回答「我把工作交给什么样的人」

`fork` / `commission` 还须回答：**我把工作交给什么样的人？**（PROMPT-020）
`fork` description 必须按五个 Office 的 entitled consequence 写明选择依据，并写明
`navigator`（Fast Browser）与 `researcher`（Deep Browser）只从 public web 建立事实、不得用于本地
文件或仓库；两个 calling 名只差 persona/深度，不差 authority（AGENT-009）。

### ACTION-AFFORDANCE-011：关键区别出现在每个会改变行动的决策面

> **A critical distinction belongs at every decision boundary where forgetting it can change the action.**
> **Single semantic ownership does not require single presentation.**（PROMPT-021）

- 同一事实可以、并且常常必须出现在多个决策面（Inspector 因果只读 → Inspector Role Law + `inspect`
  description；Coder 不执行 → Coder Role Law + establish/repair-behavior + DevOps Role Law；
  五 Office 按后果选择 → Manager Role Law + `fork` description）；
- 各投影承担不同认知功能；canonical consequence 只有一处（ARCH-017）；
- **禁止**因为某边界已在被调用方 Role Law 写过，就从调用方 tool contract 删掉；
- **禁止**让模型从词汇（persona 名、工具名、「看起来能干」）推断 authority。

### ACTION-AFFORDANCE-012：caller-facing boundary mirror 的完整性

调用方 tool description 必须让 caller 看见最容易被混淆的相邻行为（负边界），包括禁止的请求形状：
`inspect` 的 caller 不得把「修复」写进 charge（office-boundary eval case `coder-inspect-ownership`）；
`commission` 的 caller 不得把它当 fork / lifecycle stage。缺失镜像 = 本包 RED。

### ACTION-AFFORDANCE-013：description 覆盖可见纪律且不泄露隐藏编排

tool description（如 `todowrite`）必须覆盖 Manager 可见纪律（`kind` 规则、completed 门禁、lag-1 消费、
同 message 多 `todowrite` 全拒），且**禁止**泄露隐藏编排（dedicated reviewer、hidden session、Finality
cohort、barrier、witness、2N）（HOST-018 / TODO-013 / PROMPT-013）。
description 资源成对本地化 + 语义 anchor 双语同 ID 命中（PROMPT-019/020 / ARCH-016 Gate C）。

## 反向覆盖

本包吸收的 OWNED clause（COVERAGE.md 归属）：PROMPT-020、PROMPT-021、AGENT-009（fork description
部分）、AGENT-012（inspect description 见证者）、ARCH-006、ARCH-007（semantic contract 面）、
ARCH-016 Gate A/C（本包部分）、HOST-018（description 覆盖纪律部分）。
