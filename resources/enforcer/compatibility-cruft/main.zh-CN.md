# compatibility-cruft — Main

## 现在该做什么
让每一条 compatibility path 出示证件。

命名 external consumer、它仍持有的 old contract、必须存在的 overlap、以及 removal condition。拿不出真实债权人的 path 直接删除。真实 migration 则把 compatibility 隔离在 boundary，让 current internal code 只说一种 ontology。

## 为什么重要
Compatibility code 有一种特别有说服力的自我辩护：删掉它“也许会破坏一个你看不见的人”。

这种可能性有时值得认真对待，也非常擅长让 dead architecture 获得永生。

每一个 surviving alias、decoder、dual writer、fallback、legacy config key、version branch，都会对未来工作征税。工程师必须理解并 test 两个世界；reconciliation boundary 会长出新 bug；新 design 会被最不了解的 historical shape 反向约束。

最后，current system 不再为 current users 设计，而是在为一群可能根本不存在的 hypothetical users 设计。

## 修复策略
按 contract 而不是 code location 盘点 compatibility：

- 列出每个 legacy name/shape/path；
- 通过 public contract、telemetry、repository search、durable data sample、support/version policy、deployment inventory 找真实 consumer；
- 分类为 **external ingress**、**historical durable decode**、**rolling deployment overlap**、或 **speculative internal fallback**；
- 删除 speculative internal fallback；
- 真实 external/historical case 在 boundary 只翻译一次进 current model；
- 除非真实 dual-write migration 要求，否则所有 new write/emission 只使用 current form；
- 每条保留 path 都有 observable exit condition 与 owner。

优先 asymmetric migration：旧格式可以因为有界原因继续 read；新写通常只应有一个 canonical form。对称式“什么都永远支持”保证 migration 永远不会 converge。

## 决策分支
- **没有 named consumer / 没有真实 old data：**删除 compatibility path 与对应 tests。
- **External consumer 仍受支持：**保留 narrow adapter，明确 version/promise/deprecation policy。
- **Historical durable data 存在：**只在 persistence ingress 保留 decode，不让 old representation 泄漏进 current domain。
- **Rolling deployment 需要 overlap：**限定在 deployment window；fleet convergence 被证明后删除。
- **Rollback 暂时需要 dual write：**定义 rollback horizon，以及第二写入必须被移除的 exact condition。
- **因为缺 telemetry 无法识别 consumer：**先增加 observation，再谈是否永久保留。没有 evidence 不能推导“肯定有人用”。

## 常见假修复
- 把 legacy path 重命名成 `compat`，然后宣布 debt 已管理。
- 用 facade 隐藏 compatibility，但两套 ontology 仍在下面到处 live。
- 造 generic normalization layer，接受任意 historical shape “以防万一”。
- 因为 dual write 已经写好了，就永久保留。
- 一边声称 clean break，一边在 type/tool schema/provider surface 继续暴露全部 deprecated aliases。
- 完全不查真实 supported consumers 就激进删除。反 cruft 不是破坏 contract 的许可证。
- 只设 calendar removal date，没有 observable migration condition。日期不会证明 consumer 已经消失。

## 验证
每条 retained compatibility 都必须拿出 creditor 与 exit condition 的 evidence。

每条 removed path 则证明：

- repository-owned callers 全部使用 current surface；
- supported public consumers 不依赖被删 contract；
- 需要时 historical durable data 仍可 decode；
- current writes/emissions 不会再生成 legacy form；
- tests 不再在 explicit compatibility boundary 之外保存 obsolete ontology。

Invariant：

> Current code 只有一个 canonical model；compatibility 只存在于“真实 supported past 仍触碰 present”的 boundary。

## 完成条件
每一条 second path 都有可命名的存在理由，也有可命名的终止理由。

如果它唯一的 owner 叫“what if”，删掉它。
