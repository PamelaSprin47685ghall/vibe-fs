# session-ontology — HOW

## 架构机制

### 1. 正交分类模型与派生视图

系统通过两轴正交模型定义 Session 的本体：
- **ExecutionClass**：区分具备完整执行与上下文能力的 `Work` 会话，与无上下文能力的即用即弃 `InternalLeaf`。
- **Ownership**：区分独立的主会话 `Root`，与附属于特定所有者的 `Attached(ownerSessionId, AttachmentKind)`。

持久化层以向后兼容的最小结构记录 `SessionAssociation`，并由内存投影层提供只读的 `SessionOwnershipClassification` 派生视图。派生过程不修改持久化结构，支持 $O(1)$ 的双向所有权与分类查询。

### 2. 关联不变量与写入守门

`SessionAssociationProjection` 负责维护关联双向映射的一致性：
- 严格拒绝自链、递归挂载 Companion、跨会话侵占 Companion 以及同一子节点冲突 kind 注册。
- 同一对 (owner, child) 的重复链接请求保证幂等处理。
- 当解除关联时，原子清理对应子会话的反向索引，避免失效会话污染后续判别。

### 3. 物理扁平与逻辑层级分离

Host 层的物理展示树深度恒为 2，所有子节点物理上均直挂在 family root 下。物理位置仅作为 Host 观察入口，所有真正的业务归属、级联取消依赖与恢复匹配逻辑完全以 journal 事实记录为唯一真理。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| SESSION-ONTOLOGY-001 | `requirements/session-ontology/tests/session-ownership-ratchet.test.mjs::WHAT[SESSION-ONTOLOGY-001] session_ownership_ratchet_documents_closed_kind_set` |
| SESSION-ONTOLOGY-002 | `requirements/session-ontology/tests/session-ownership-ratchet.test.mjs::WHAT[SESSION-ONTOLOGY-002] session_ownership_ratchet_questionnaire_requires_owner_field` |
| SESSION-ONTOLOGY-003 | `requirements/session-ontology/tests/sync-delegate.test.mjs::WHAT[SESSION-ONTOLOGY-003] HOST_008_delegate_role_maps_to_attachment` |
| SESSION-ONTOLOGY-004 | `requirements/session-ontology/tests/session-ontology-classification.test.mjs::WHAT[SESSION-ONTOLOGY-004] HOST_008_companion_is_internal_leaf_attached` |
| SESSION-ONTOLOGY-005 | `requirements/session-ontology/tests/session-association.test.mjs::WHAT[SESSION-ONTOLOGY-005] HOST_008_companion_cannot_serve_two_work_sessions` |
| SESSION-ONTOLOGY-006 | `requirements/session-ontology/tests/session-flattening.test.mjs::WHAT[SESSION-ONTOLOGY-006] HOST_015_child_of_child_is_physically_parented_to_family_root` |
| SESSION-ONTOLOGY-007 | `requirements/session-ontology/tests/session-ontology-classification.test.mjs::WHAT[SESSION-ONTOLOGY-007] HOST_008_durable_link_derives_work_and_leaf_cells` |
| SESSION-ONTOLOGY-008 | `requirements/session-ontology/tests/session-association.test.mjs::WHAT[SESSION-ONTOLOGY-008] HOST_008_linking_records_both_directions` |
| SESSION-ONTOLOGY-009 | `requirements/session-ontology/tests/session-association.test.mjs::WHAT[SESSION-ONTOLOGY-009] COMPANION_001_every_work_session_may_have_a_companion` |
| SESSION-ONTOLOGY-010 | `requirements/session-ontology/tests/session-association.test.mjs::WHAT[SESSION-ONTOLOGY-010] COMPANION_001_unknown_session_is_not_a_companion` |
| SESSION-ONTOLOGY-011 | `requirements/session-ontology/tests/session-ownership-ratchet.test.mjs::WHAT[SESSION-ONTOLOGY-011] session_ownership_ratchet_attachment_tokens_include_strength_replica` |
| SESSION-ONTOLOGY-012 | `requirements/session-ontology/tests/sync-delegate.test.mjs::WHAT[SESSION-ONTOLOGY-012] HOST_008_root_and_attached_helpers_are_explicit` |
| SESSION-ONTOLOGY-013 | `requirements/session-ontology/tests/terminal-policy.test.mjs::WHAT[SESSION-ONTOLOGY-013] TPOL_roleName_uses_catalog_labels_and_rejects_none` |
| SESSION-ONTOLOGY-014 | `requirements/session-ontology/tests/satellite-kind.test.mjs::WHAT[SESSION-ONTOLOGY-014] HOST_014_satellite_kind_is_companion_only` |
