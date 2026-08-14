# Durable truth / convergence

## `durable-events`

WHY: 动态 durable state 只能有一个解释权；否则每个 feature 自造 journal/blob/ref/fold 后，restart 与跨 feature consistency 会分裂成多个世界。

OWNS:
- immutable event facts 与 stable event identity。
- append/publish atomicity；durable commit 成功后才更新权威内存 projection。
- canonical payload bytes/content-addressed payload closure。
- deterministic fold/projection。
- corrupt/invalid history fail closed；不能跳过破损中间事实继续折叠后文。
- additive event vocabulary；storage envelope/version 不冒充领域事实。
- query 从 projection 读取，而不是每次全历史扫描。
- 统一 durable substrate；feature-owned parallel journal/store 非法。

DOES NOT OWN:
- 多 replica domain conflict 的收敛规则。
- Requested/Accepted effect semantics。
- 各 domain event 的业务意义。
- Git raw ODB/refs 必须永久保持。
- 旧 NDJSON/feature store compatibility。

DEPENDS ON: 无。

PROVIDES: 所有 durable packages 的单一可重放 truth substrate。

FAILURE MEANING: RED = durable facts 可被覆盖/部分提交/跳过损坏，或同一动态真相存在多个互相漂移的 durable owner。

INDEPENDENT CHANGE: Git ODB/CAS 换成另一 append+atomic-publish store，而各 domain event semantics 不变。

CURRENT EVIDENCE: PERSIST-001..008；EventStore/GitGateway/FactCodec/Fold；`docs/{why,what,shape,how,proof}/persist.md`。

---

## `durable-convergence`

WHY: 两个 replicas 都可能各自合法发展；同步不能靠 wall-clock/revision 选“较新”世界，而必须保留共同事实并把真正 domain conflict 显式交还领域。

OWNS:
- replica exchange/set-union/object convergence。
- concurrent durable heads 的识别与 deterministic merge。
- dumb remote：remote 只交换/保存 objects，不拥有 domain policy。
- 同一 domain object 的合法并发分叉显式成为 DomainConflict，不用 LWW 偷删一边。
- 相同 object set 应在各 replica 折叠为同一 durable state。

DOES NOT OWN:
- 单一 store append/CAS。
- Casebook/Orchestrator 等 domain-specific conflict resolution。
- network transport/provider。
- wall-clock ordering。

DEPENDS ON: `durable-events`。

PROVIDES: 多副本保留事实、显式冲突、最终收敛的 substrate guarantee。

FAILURE MEANING: RED = 同步静默丢掉合法分支，或相同 object set 在不同 replica 上折叠成不同 durable world。

INDEPENDENT CHANGE: 替换 remote transport/packing 或优化 object exchange，而 event/store semantics 不动。

CURRENT EVIDENCE: Persist convergence/CAS；Casebook 并发 fork → DomainConflict；dumb remote/storage changes。
