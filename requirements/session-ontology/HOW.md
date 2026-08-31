# session-ontology — HOW

## 架构机制

### 1. 正交分类模型与派生视图

系统通过两轴正交模型定义 Session 的本体：
- **ExecutionClass**：区分具备完整执行与上下文能力的 `Work` 会话，与无上下文能力的即用即弃 `InternalLeaf`。
- **Ownership**：区分独立的主会话 `Root`，与附属于特定所有者的 `Attached(ownerSessionId, AttachmentKind)`。

持久化层以最小 `SessionAssociation` 记录 physical-container association，并由投影层提供只读 `SessionOwnershipClassification` 派生视图。派生过程不修改 durable facts，支持 $O(1)$ 的双向所有权与分类查询；该投影不包含 Persona 或 `ParticipantIdentity`。

### 2. 关联不变量与写入守门

`SessionAssociationProjection` 负责维护关联双向映射的一致性：
- 严格拒绝自链、递归挂载 Companion、跨会话侵占 Companion 以及同一子节点冲突 kind 注册。
- 同一对 (owner, child) 的重复链接请求保证幂等处理。
- 当解除关联时，原子清理对应子会话的反向索引，避免失效会话污染后续判别。

### 3. 物理扁平与逻辑层级分离

Host 层的物理展示树深度恒为 2，所有子节点物理上均直挂在 family root 下。物理位置仅作为 Host 观察入口，业务归属、级联取消依赖与恢复匹配完全以 journal association 为唯一真理。identity consumer 必须取得原子 `AuthorityRootAccepted` 中由 `participant-identity` owner 准备的 typed evidence；physical parent 与 association query 均不返回推导 Persona 的捷径。

### 4. 物理容器复用

Session projection 只暴露 physical container classification 与 durable association，不存储 run-scoped identity 字段，也不生成 lifecycle terminal/closure。`interaction-authority` 只在 exact typed lifecycle source 已匹配 accepted root 后持久化 `AuthorityLogicalRunClosed`；participant identity 与 fresh root 再由新的原子 `AuthorityRootAccepted` payload 同时安装/接受。association removal、detach/attach、classification、wall clock、idle/timeout 与 Host tree 均不得替代 closure evidence。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| SESSION-ONTOLOGY-001 | `requirements/session-ontology/tests/session-ownership-ratchet.test.mjs` |
| SESSION-ONTOLOGY-002 | `requirements/session-ontology/tests/session-ontology-classification.test.mjs` |
| SESSION-ONTOLOGY-003 | `requirements/session-ontology/tests/sync-delegate.test.mjs` |
| SESSION-ONTOLOGY-004 | `requirements/session-ontology/tests/session-ontology-classification.test.mjs` |
| SESSION-ONTOLOGY-005 | `requirements/session-ontology/tests/session-association.test.mjs` |
| SESSION-ONTOLOGY-006 | `requirements/session-ontology/tests/session-flattening.test.mjs` |
| SESSION-ONTOLOGY-007 | `requirements/session-ontology/tests/session-ontology-classification.test.mjs` |
| SESSION-ONTOLOGY-008 | `requirements/session-ontology/tests/session-association.test.mjs` |
| SESSION-ONTOLOGY-009 | `requirements/session-ontology/tests/session-association.test.mjs` |
| SESSION-ONTOLOGY-010 | `requirements/session-ontology/tests/session-ownership-ratchet.test.mjs` |
| SESSION-ONTOLOGY-011 | `requirements/session-ontology/tests/session-ontology-classification.test.mjs` |
| SESSION-ONTOLOGY-012 | `requirements/session-ontology/tests/sync-delegate.test.mjs` |
| SESSION-ONTOLOGY-013 | `requirements/session-ontology/tests/terminal-policy.test.mjs` |
| SESSION-ONTOLOGY-014 | `requirements/session-ontology/tests/satellite-kind.test.mjs` |
| SESSION-ONTOLOGY-015 | `requirements/session-ontology/tests/session-reuse-identity.test.mjs` |
