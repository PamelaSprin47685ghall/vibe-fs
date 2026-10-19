# compatibility-cruft — Lesson

## 评价

### 定义

当同一种 capability 仍存在第二套 representation、call path、config、storage shape 或 execution path，但已经没有 concrete external obligation 真正要求它时，就是 compatibility cruft。

这是一种由“没有名字的幽灵消费者”治理的 architecture。

### 支配原则

Compatibility 是一张 debt instrument。可以借，但必须有债权人。

真实 compatibility obligation 应回答：谁会 break？它手里仍持有什么 old contract？old/new overlap 必须持续多久？什么 event 允许删除？

如果这些事实说不出来，“backward compatibility”就会退化成一句永久阻止 simplification 的道德咒语。

成本也不只是多几行 code。Two live paths 意味着两套 ontology、两组 tests、两类 failure mode、routing rule、migration ambiguity，以及未来每次 change 都必须再问一句：“我现在改的是哪个世界？”

### 何时触发

当 legacy alias/adapter/format/path 主要因为 unspecified fear 而继续 live，而不是因为 named supported contract 时触发。例如：

- 所有 repository-owned caller 都已迁移，old/new API/tool name 仍永久共存；
- old config key 因“可能还有人用”被永远接受，却没有 supported-version policy；
- migration 后仍 dual read/write，实际已没有 durable old data 或 external producer；
- compatibility adapter 长期在两套都由 repository 自己拥有的 internal model 之间 routing；
- deprecated branch 没有 telemetry、consumer list、removal date/condition、version boundary；
- 为从未在真实 data 中见过的 speculative historical shape 保留 normalization；
- 新 code 每次都必须同时更新 legacy/current representation 才能保持同步；
- Product 已决定 clean break，但 provider-facing surface 仍偷偷保留 old alias/decode fallback。

### 不应触发

- Named external consumer/version 仍受支持，移除会违反真实 promise。
- Historical durable data 的确需要 old decode 做 recovery；但 new write 已只用 current format，legacy decode 被隔离在 persistence ingress。
- Migration 有 explicit overlap window、telemetry/consumer tracking 与 concrete removal criterion。
- Standards/protocol 当前 contract 本身就要求多版本/多 representation。
- Compatibility 本身就是明确 product requirement，而不是 implementation superstition。

### 与相邻规则区分

`half-finished-refactor` 重点是 old/new ownership model 都在内部保持 authoritative。`compatibility-cruft` 即使 ownership 已清楚，也可能存在：只是 obsolete external shape 还被继续接受。

`legacy-cruft-retained` 更广，包含各种历史 debris。本规则专门抓以“compatibility”为理由保留的 duplicate interface/representation。

`guessed-migration` 可能因为没看历史 data 而发明不必要 compatibility；中心失败是“凭空猜 migration target”时用它，中心失败是“duplicate path 没债权人还不删”时用本规则。

### 判定程序

每条 legacy path 必须回答四个问题：

1. **Consumer：**谁现在仍使用？
2. **Contract：**哪一个 supported promise 要求它？
3. **Overlap：**为什么 old/new 必须同时 live？
4. **Exit：**什么 observable condition 允许删除？

第 1/2 项说不出 concrete answer，这条 compatibility 就不是在保护 contract，而是在保护焦虑。

1–3 真实但第 4 项不存在，说明 migration 没有任何机制可以结束。

### 例子

- positive：所有 first-party callers 已迁移，`oldTool()` 与 `newTool()` 仍无限期共存，而且没有 public API consumer。
- positive：decoder 接受三种从旧 comment 猜出来的 legacy JSON shape，却找不到任何 persisted sample/supported version 真正包含它们。
- positive：rollback 早已不可能，write 仍同时更新 v1/v2 tables，“为了 rollback safety”。
- near-miss：public API 明确支持 v1 clients 六个月；usage telemetry 与 published deprecation date 定义 overlap。
- counterexample：current write 全部 v2，但在 retention horizon 结束前 recovery 仍可读真实 v1 durable record。

## 建议

### 现在该做什么

让每一条 compatibility path 出示证件。

命名 external consumer、它仍持有的 old contract、必须存在的 overlap、以及 removal condition。拿不出真实债权人的 path 直接删除。真实 migration 则把 compatibility 隔离在 boundary，让 current internal code 只说一种 ontology。

### 为什么重要

Compatibility code 有一种特别有说服力的自我辩护：删掉它“也许会破坏一个你看不见的人”。

这种可能性有时值得认真对待，也非常擅长让 dead architecture 获得永生。

每一个 surviving alias、decoder、dual writer、fallback、legacy config key、version branch，都会对未来工作征税。工程师必须理解并 test 两个世界；reconciliation boundary 会长出新 bug；新 design 会被最不了解的 historical shape 反向约束。

最后，current system 不再为 current users 设计，而是在为一群可能根本不存在的 hypothetical users 设计。

### 修复策略

按 contract 而不是 code location 盘点 compatibility：

- 列出每个 legacy name/shape/path；
- 通过 public contract、telemetry、repository search、durable data sample、support/version policy、deployment inventory 找真实 consumer；
- 分类为 **external ingress**、**historical durable decode**、**rolling deployment overlap**、或 **speculative internal fallback**；
- 删除 speculative internal fallback；
- 真实 external/historical case 在 boundary 只翻译一次进 current model；
- 除非真实 dual-write migration 要求，否则所有 new write/emission 只使用 current form；
- 每条保留 path 都有 observable exit condition 与 owner。

优先 asymmetric migration：旧格式可以因为有界原因继续 read；新写通常只应有一个 canonical form。对称式“什么都永远支持”保证 migration 永远不会 converge。

### 决策分支

- **没有 named consumer / 没有真实 old data：**删除 compatibility path 与对应 tests。
- **External consumer 仍受支持：**保留 narrow adapter，明确 version/promise/deprecation policy。
- **Historical durable data 存在：**只在 persistence ingress 保留 decode，不让 old representation 泄漏进 current domain。
- **Rolling deployment 需要 overlap：**限定在 deployment window；fleet convergence 被证明后删除。
- **Rollback 暂时需要 dual write：**定义 rollback horizon，以及第二写入必须被移除的 exact condition。
- **因为缺 telemetry 无法识别 consumer：**先增加 observation，再谈是否永久保留。没有 evidence 不能推导“肯定有人用”。

### 常见假修复

- 把 legacy path 重命名成 `compat`，然后宣布 debt 已管理。
- 用 facade 隐藏 compatibility，但两套 ontology 仍在下面到处 live。
- 造 generic normalization layer，接受任意 historical shape “以防万一”。
- 因为 dual write 已经写好了，就永久保留。
- 一边声称 clean break，一边在 type/tool schema/provider surface 继续暴露全部 deprecated aliases。
- 完全不查真实 supported consumers 就激进删除。反 cruft 不是破坏 contract 的许可证。
- 只设 calendar removal date，没有 observable migration condition。日期不会证明 consumer 已经消失。

### 验证

每条 retained compatibility 都必须拿出 creditor 与 exit condition 的 evidence。

每条 removed path 则证明：

- repository-owned callers 全部使用 current surface；
- supported public consumers 不依赖被删 contract；
- 需要时 historical durable data 仍可 decode；
- current writes/emissions 不会再生成 legacy form；
- tests 不再在 explicit compatibility boundary 之外保存 obsolete ontology。

Invariant：

> Current code 只有一个 canonical model；compatibility 只存在于“真实 supported past 仍触碰 present”的 boundary。

### 完成条件

每一条 second path 都有可命名的存在理由，也有可命名的终止理由。

如果它唯一的 owner 叫“what if”，删掉它。

### Nudge

没有 named consumer 的 compatibility，就是带 API 的恐惧。

说出债权人，说出退出条件，否则删掉这笔债。
