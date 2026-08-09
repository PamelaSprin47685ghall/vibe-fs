# 统一持久化底座 — 技术规格

**Session**: unified-storage-2026-08  
**Proposal**: changes/proposed/storage.md  
**优先级**: P0 / 横切型架构  
**兼容性策略**: ExplicitMigration + Clean Cutover

## 目标

将万象术所有动态 durable state 统一到单一 Git Raw + Event Sourcing 架构，删除所有 feature-owned storage mechanisms。

### 目标状态

- **单一持久化系统**: 只有一个 canonical `refs/wanxiang/store` + Git raw object database
- **Append-only events**: 所有 durable facts 表达为 immutable events，禁止 UPDATE/rewrite
- **Canonical NDJSON**: one event = one JSON+LF blob，按 §5.0 归一化（UTF-8/key排序/parents排序）
- **EventId 分片**: 按 EventId hex 分片存储，禁止 ordinal/sequence 命名
- **因果 DAG**: events 通过 `parents` 字段构成显式因果图，deterministic topological fold
- **无版本号**: 删除所有 schemaVersion/storageVersion，采用 additive vocabulary 演进
- **Git raw payload**: 大正文使用 Git blob + PayloadRef，closure 归一（§7.1）
- **K-way merge**: append-only set union，StorageInvalid vs DomainConflict 明确分类（§5.3）
- **统一 GitGateway**: 所有 Wanxiang Git 操作必经唯一入口
- **Dumb server**: remote 不知道 domain/event/projection，纯 Git objects + refs + CAS

### 非目标

- 不改变 repository authored content（docs/resources/AGENTS.md 等）的 Git 管理方式
- 不在首版引入 client-side encryption（§26 选项 B 留给未来）
- 不在首版提供 LocalOnly durable scope（§34 明确禁止）
- 不建立分布式强一致性（§42.1 best-effort convergence）

## 需求

### R1: 单一 Canonical Ref
- 只有 `refs/wanxiang/store` 指向 root tree
- 禁止 feature 创建 `refs/wanxiang/*` 子命名空间
- 首次创建使用 `CAS(ref, expected=Absent, new=R1)`（§9）

### R2: 无版本演进
- Envelope 删除 schemaVersion/storageVersion 字段
- Event vocabulary 单调增长（§5.2）：已 committed 的 event_type + payload shape 永久冻结
- Unknown authoritative event_type → StorageInvalid → fail closed（§5.3）

### R3: 因果模型
- Envelope 新增 `parents: EventId list` 字段（§5.1）
- 同 stream 内前驱必须显式声明，跨 stream 引用允许
- Projection 按 DAG topological fold，parents 未知/成环 → StorageInvalid

### R4: Payload Closure
- Envelope 新增 `payload_refs: GitObjectId list` 字段（§7.1）
- Domain 侧仅见 opaque `PayloadRef`，不直接操作 Git OID（§45）
- Committed root 的 payloads/ tree = ⋃ events.payload_refs，unreferenced payload 不得纳入

### R5: Canonical JSON
- §5.0 协议：UTF-8 无 BOM + 单 LF + key 升序 + parents 去重排序 + 数字字符串归一
- 同 EventId + 不同 canonical bytes → StorageInvalid（identity collision）

### R6: K-Way Merge 语义
- Specification oracle（§10.6）：`merge = union(allEvents)` by EventId
- Production 实现：structural tree merge，仅 blob OID 冲突时读 bytes 校验
- StorageInvalid vs DomainConflict 分类（§5.3）：
  - StorageInvalid：坏 JSON/collision/缺 parent/成环/unknown event → 全局 fail closed
  - DomainConflict：合法并发 fork → projection 进入 deterministic Conflict，等 *Resolved event

### R7: GitGateway 统一
- 所有 Wanxiang Git 操作必经 `Infrastructure/Git/GitGateway.fs`
- ConvergeStore(remote) 永远双向：fetch → merge → validate → CAS local → lease push
- Hook dispatcher：reference-transaction + pre-push shim，recursion guard

### R8: Student QA 迁移
- 从 Git-private file 改为统一 EventStore events
- StudentQaOpened / QuestionAppended / AnswerAppended / Closed
- Confidentiality boundary = repository Git ACL（§26 选项 A）

### R9: Casebook 迁移
- 删除 `refs/wanxiang/inspector-casebook` 及其独立 sync 协议
- CaseCaptured / CaseRefreshed / CaseAccessed / CaseEvicted events
- LWW 降级为 CasebookProjection 规则，不影响 Store merge（§10.10）

### R10: 全域 Migration
- Legacy Journal NDJSON + BlobStore + Student QA → 统一 EventStore
- Causal reconstruction：legacy order → parents 显式映射（§27）
- Migration determinism：同 input → 同 EventId/parents/canonical bytes/root OID
- Projection equivalence proof：LegacyProjection == NewProjection

## 接受标准

### AC1: 物理唯一性
- ✓ 只有一个 durable EventStore 实现
- ✓ 只有一个 canonical store ref
- ✓ Git object database 是唯一 durable bytes backend
- ✓ 无 RuntimePath blob backend
- ✓ 无独立 Student QA file backend
- ✓ 无 feature-owned storage refs

### AC2: 无版本
- ✓ Envelope 无 schemaVersion/storageVersion 字段
- ✓ 无 v1/v2 dual reader in runtime
- ✓ 无 dual write
- ✓ Unknown authoritative event → fail closed

### AC3: 因果完整
- ✓ 所有 events 含 parents 字段
- ✓ Migration 重建 legacy 因果约束
- ✓ Projection fold 验证 DAG（缺 parent/成环 → fail closed）

### AC4: Merge 正确性
- ✓ K-way merge associative/commutative/idempotent/deterministic
- ✓ Identity collision → fail closed
- ✓ 自然 fork 不升级为 StorageInvalid
- ✓ DomainConflict 保留全部 heads，等 *Resolved

### AC5: GitGateway
- ✓ GitGateway 是 production Git 唯一入口
- ✓ ConvergeStore 永远双向
- ✓ Hook dispatcher 正确处理 nested fetch（§14）
- ✓ Dumb server 不链接 Domain 代码

### AC6: Migration
- ✓ LegacyProjection == NewProjection
- ✓ Migration bytes 级确定性
- ✓ Legacy writers 已从 runtime 删除
- ✓ Migration reader 仅在 one-shot tool 中

### AC7: 测试与门禁
- ✓ Static architecture gates green（§35-37）
- ✓ Unit green
- ✓ Integration green (含 dumb-server.test.mjs)
- ✓ Migration tests green
- ✓ Affected e2e green
- ✓ npm run check green

## 相关系统

### 现有实现
- `src/Wanxiangshu/Journal/Envelope.fs` - 现有 Envelope（含 schemaVersion）
- `src/Wanxiangshu/Journal/Writer.fs` - NDJSON 文件 writer
- `src/Wanxiangshu/Journal/Boot.fs` - k-way merge by LocalSeq/ObservedAt
- `src/Wanxiangshu/Infrastructure/Git/GitOperations.fs` - Git CAS primitives
- `src/Wanxiangshu/Infrastructure/OpenCode/Host/StudentQaStore.fs` - Student QA file store

### 提案依赖
- `changes/proposed/perm-inspector.md` - Casebook 需收口 storage 部分
- `changes/proposed/rulebook.md` - Observation 需使用统一 events
- `changes/proposed/strength.md` - CandidatePrepared 需使用统一 events

### 规范文档
- `docs/what/persist.md` - 需全面重写（append/CommitUnknown/projection）
- `docs/shape/persist.md` - 需改为 Git raw 边界
- `docs/how/persist.md` - 需改为 EventStore 算法
- `docs/proof/persist.md` - 需改为新证明矩阵

## 约束

- **所有权红线**（§45）：Domain 不得出现 GitObjectId/RootOid/StoreSnapshot/AppendCandidate
- **No LocalOnly**（§34）：凡进入 canonical ref 的 facts 均为 RepositoryShared
- **Clean Cutover**（§28）：切换后禁止 legacy reader 进入 runtime
- **Hook installation**（§20）：无法安全 chain → Git integration incomplete，禁止标为 acceleration disabled
- **Bytes 确定性**（§27）：同 legacy input → 同 canonical bytes + root OID

## 风险

### R-RISK-1: Migration 数据丢失
- **影响**: 旧 Journal/QA 迁移时遗漏 durable facts
- **缓解**: Projection equivalence proof；deterministic migration test；每个 domain 独立验证

### R-RISK-2: 因果重建不完整
- **影响**: Legacy 隐式顺序丢失 → projection 不等价
- **缓解**: 每个 domain 显式给出 legacy order → parents 映射；migration test 覆盖 causal 边

### R-RISK-3: Canonical 规则不一致
- **影响**: 两 replica 产生不同 root OID
- **缓解**: §5.0 精确定义；unit test 覆盖 key/parents 排序；契约测试验证 idempotence

### R-RISK-4: Hook recursion
- **影响**: ConvergeStore 触发 hook → 再次 ConvergeStore → 死锁
- **缓解**: WANXIANG_GIT_SYNC_ACTIVE guard；§14 ConvergeStoreWithObservedRemote 复用已观察 snapshot

### R-RISK-5: 未知 event fail closed
- **影响**: 旧 client 遇到新 event → 永久不可用
- **缓解**: §5.3 明确这是 vocabulary monotonicity 必然边界；告知用户需升级；不视为缺陷

## 未知

- [ ] EnforcerCatalog schemaVersion=1 是否属于 §36 禁止的 storage-version？需明确：authored resource 的 schemaVersion 不属于 durable event store version
- [ ] Completion blob schemaVersion=2 是否需要重新审视？需确认：Host protocol schemaVersion 不属于 Journal Envelope schemaVersion
- [ ] Hook installation failure 的产品级 UX？需定义：Git integration incomplete 时的用户可见诊断与恢复指导
