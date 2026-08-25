# migration-ledger — WHY

## 不可替代的存在理由

63 节点 DAG 是万象术从过渡态迈向终态的施工事实。若该施工图仅以散文或静态清单存在而无机械门禁，PENDING 节点可冒充 GREEN 成功，READY 节点可无 owner 合同却宣称就绪，DONE 节点可无实现提交、无生产变更、无证明门禁却标记完成，分类与结果可错配，closure 依赖可指向未 DONE 节点，覆盖可无归属，基线可静默增长——每种都会让 ledger 从“可执行的施工图”退化为“文字报告”。

`migration-ledger` 的核心存在理由是：**把 DAG 的拓扑、状态机、分类机、证据机、证明机、提交机、变更机、依赖机、覆盖机与基线机全部变成可红可绿的机械断言。**

## 核心张力与元规则定位

领域包拥有具体业务事实，回答“世界是什么”；而 `migration-ledger` 不拥有业务事实，它拥有的是**施工事实的治理**：

```text
这个节点是否真的就绪？
这个 DONE 是否真的闭环？
这个提交是否真的存在且为 HEAD 祖先？
这个变更是否真的动了生产？
```

这些治理规则必须由单一包集中拥有，并由 `scripts/checks/migration-ledger.mjs` 机械执行。

## 为什么独立于 requirement-system

- `requirement-system` 解决所有权与规范合同问题。
- `migration-ledger` 解决施工 DAG 与状态机的时序与完整性问题。

两者边界清晰：前者管“谁拥有什么”，后者管“何时算做完”。

## 依赖关系

**DEPENDS ON** `requirement-system`, `verification-system`

- 依赖 `requirement-system` 的 owner 唯一性与 WHAT 唯一性：每个节点的 primary_owner 必须为合法 package。
- 依赖 `verification-system` 的 fail-closed 与可红性：门禁必须在损坏、缺参、未知异常时安全失败并传播非零退出码。

## 核心不变量与违约状态（RED）

仓库处于 RED 状态，当且仅当出现以下任一 ledger 违约：
1. PENDING 节点 evidence 含 verified/complete/GREEN 成功标记。
2. READY 节点无 owner 图或无 proof/gate。
3. DONE 节点结果仍 PENDING 或分类/结果错配。
4. DONE 节点缺 implementation_commit 或提交非 HEAD 祖先。
5. DONE 节点无 touched_paths 生产变更或无 proofs/gates。
6. closure 边目标非 DONE。
7. 仅 coverage 无 owner 图。
8. 基线/抑制文件增长无显式 admission。
9. DAG 出现环或覆盖不完整。
