# interaction-authority — HOW

## 架构机制与权限状态机

`interaction-authority` 通过纯函数式事实折叠维护唯一的权威状态投影：

1. **Ingress 授权把关**：
   `PromptIngress.handle` 是外部物理消息升级为权限实体的唯一入口。仅当当前无活跃 Profile（`ActiveProfile = None`）且消息显式指定了合法 managed agent 时，才允许生成 `HumanRoot` 并持久化 `AuthorityRootAccepted` 事实。处于活跃交互过程中的未知消息一律判定为 `UnknownOrigin` 并终止流程。

2. **来源判定管线（Resolution Pipeline）**：
   按固定顺序扫描当前状态事实：
   - 物理确认接收的消息（`AcceptedContinuationIds`）→ 判定为对应 Continuation
   - 挂起的 PromptKey Claim → 判定为已登记的意图来源
   - Host 压缩/合成提示 → 判定为 HostInternal
   - 证明合法的物理用户输入 → 判定为 HumanRoot
   - 未命中任何规则 → fail-closed 判定为 UnknownOrigin

3. **权威事实折叠（Authority Fold）**：
   投影严格从持久化事件中重放生成，内存中不维护独立的可变 authority 状态副本。`LifeCompleted` 事件在折叠时自动清空 `ActiveLogicalRun` 及关联的局部 claims，保证会话终结后权限原子回收。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| INTERACTION-AUTHORITY-001 | `requirements/interaction-authority/tests/authority-root.test.mjs` |
| INTERACTION-AUTHORITY-002 | `requirements/interaction-authority/tests/authority-root.test.mjs` |
| INTERACTION-AUTHORITY-003 | `requirements/interaction-authority/tests/authority-root.test.mjs` |
| INTERACTION-AUTHORITY-004 | `requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| INTERACTION-AUTHORITY-005 | `requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| INTERACTION-AUTHORITY-006 | `requirements/interaction-authority/tests/authority-root.test.mjs` |
| INTERACTION-AUTHORITY-007 | `requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| INTERACTION-AUTHORITY-008 | `requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| INTERACTION-AUTHORITY-009 | `requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| INTERACTION-AUTHORITY-010 | `requirements/interaction-authority/tests/join-guard.test.mjs` |
| INTERACTION-AUTHORITY-011 | `requirements/interaction-authority/tests/chat-params-hook.test.mjs` |
| INTERACTION-AUTHORITY-012 | `requirements/interaction-authority/tests/assistance-host.test.mjs`, `requirements/interaction-authority/tests/assistance-abort-successor-trace.test.mjs` |
| INTERACTION-AUTHORITY-013 | `requirements/interaction-authority/tests/assistance-host.test.mjs` |
| INTERACTION-AUTHORITY-014 | `requirements/interaction-authority/tests/join-guard-execution.test.mjs` |
| INTERACTION-AUTHORITY-015 | `requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| INTERACTION-AUTHORITY-016 | `requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| INTERACTION-AUTHORITY-017 | `requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| INTERACTION-AUTHORITY-018 | `requirements/interaction-authority/tests/logical-run-close.test.mjs` |
