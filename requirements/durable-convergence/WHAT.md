# WHAT —— durable-convergence（唯一 normative 合同）

条款前缀 `DURABLE-CONVERGENCE-`。每条的落点测试见 `PROOF.md`。
来源：`changes/completed/storage.md`（§5.3、§10、§11–§19、§38、§42、§48）、
`docs/what/casebook.md`（CASE-011）、`requirements-design/COVERAGE.md` persist 小节
（PERSIST-003 split）。

## DURABLE-CONVERGENCE-001 —— merge = set union，永不丢事实

**规范陈述**：replica merge 是 append-only set union + identity dedupe：按 EventId 去重，
两个不同 EventId 永远都进入 merged history（即使 DomainConflict 亦保留全部 facts）。
禁止 Store 用 wall_clock/revision 裁决而让任一 durable fact 消失。

**含义/动机**：Persist 负责不丢事实；Domain 负责解释事实是否相容。丢分支是数据丢失，
不是策略。
**边界**：同 EventId 异 bytes 的 identity-collision 裁决（fail closed）归
`durable-events`（003）；本命题只钉「不同 EventId 永不因冲突丢失」。
**证据**：→ PROOF.md 001。

## DURABLE-CONVERGENCE-002 —— k-way merge 是统一 primitive

**规范陈述**：`KWayMerge(snapshot[])` 满足 associative / commutative / idempotent /
deterministic：`merge(A, merge(B,C)) = merge(merge(A,B), C)`、`merge(A,B) = merge(B,A)`、
`merge(A,A) = A`、同一组输入无论枚举顺序/由哪个 process 执行都产生相同 canonical
result。输入可同时来自当前 process、其它本地 process、remote tracking、hook 观察、
recovery。

**含义/动机**：统一 primitive 是并发模型的地基——任何同步入口（local append、外部
fetch/pull/push、bootstrap、recovery）都汇入同一个 merge，不各自实现同步协议。
**边界**：merge 的物理实现（structural tree merge 与 oracle 的一致性）见 003。
**证据**：→ PROOF.md 002。

## DURABLE-CONVERGENCE-003 —— 生产 structural merge ≡ union oracle

**规范陈述**：生产 merge 必须与 set-union spec oracle 等价：`merge(snapshots)` 的结果
与 `materialize(union(events))` 相同（相同 canonical root）。实现优先 structural tree
merge（EventId 分片路径直接 union），不得在每次 append/fetch 读全量 events 做 O(N)
set-union；`union(allEvents)` 保留为契约 oracle，不作为生产算法指导。

**含义/动机**：structural merge 让复杂度与 delta/tree-path 相关；oracle 等价保证
「集合相同 → root 相同」不因实现漂移。
**边界**：同 EventId 异 OID 时读 bytes 校验 identity collision → `durable-events`。
**证据**：→ PROOF.md 003。

## DURABLE-CONVERGENCE-004 —— 合法并发 fork → DomainConflict，非 StorageInvalid

**规范陈述**：同一 stream/业务键的合法并发 fork 是物理层正常产物（A、B 离线同见
parent=P 各自 append A1/B1），必须被定义为 DomainConflict，由 projection 表达为
deterministic conflict state。Storage 层永不因自然 fork 进入不可恢复；严禁把领域禁止
的并发 fork 判为 StorageInvalid。history 保留全部 competing heads。

**含义/动机**：append-only union 必然产生物理 fork；它与「全局不可恢复」正交。
「forbidden fork」指业务不可接受态，由 projection 表达并经 resolution 收敛。
**边界**：「不把 DomainConflict 升级为全局 corruption」的反向钉死见
`durable-events` 008；本命题是正向表达律。
**证据**：→ PROOF.md 004。

## DURABLE-CONVERGENCE-005 —— resolution event 以全部 heads 为 parents 才收敛

**规范陈述**：resolution event（`FooConflictResolved` / 领域具体 `*Resolved`）必须以
**所有 competing heads 为 parents**（至少包含需裁决的 heads 集合），在 DAG 上显式声明
「已知并裁决了这些并发分支」；仅当 resolution 及其全部 parents 已 fold，projection
才离开 conflict state。

**含义/动机**：收敛不是遗忘：resolution 必须承认并覆盖它裁决的每个分支，否则未来重放
无法重建「为什么离开 conflict」。
**边界**：resolution 的领域语义（裁决了什么、为什么）归各 domain owner。
**证据**：→ PROOF.md 005。

## DURABLE-CONVERGENCE-006 —— 禁止 wall_clock/revision LWW

**规范陈述**：merge 不得使用 wall-clock、revision、timestamp 裁决 winner；收敛必须是
event 集合的纯函数，与 replica 的到达顺序、append 时刻无关。revision/wall_clock/
deterministic tie 最多只能作为 **projection 层**从完整历史派生当前视图的规则，不允许
删除 loser event、不允许影响其它 domain。

**含义/动机**：时间戳不证明内容未变；revision 排序制造第二真相。LWW 的合法残余位置
是 projection 规则（如 Casebook 从完整 history 派生 `CurrentCase(session)`），不是
replication 规则。
**边界**：Casebook 对象层面的禁 LWW 语义归 `knowledge-reuse`；本命题钉 general merge 律。
**证据**：→ PROOF.md 006。

## DURABLE-CONVERGENCE-007 —— 相同 merged snapshot → 相同 projection

**规范陈述**：收敛公式是 `Projection(KWayMerge(S1..Sk)) = Fold(Union(Events(S1..Sk)))`，
不是 `Merge(Projection(S1), Projection(S2))`。相同 merged snapshot 必须得到相同
projection（确定性 fold 由 `durable-events` 014 保证）。

**含义/动机**：投影不是第二真相源；它只是「从完整历史折叠出的当下」。replica 收敛的
终点是「相同事件集合 → 相同世界」。
**边界**：fold 本身的确定性机制 → `durable-events`。
**证据**：→ PROOF.md 007。

## DURABLE-CONVERGENCE-008 —— Converge 永远双向；无单向 API

**规范陈述**：唯一同步 primitive 是 `ConvergeStore(remote)`：fetch remote store ref →
merge append-only event sets → validate merged history → CAS local → lease-push merged
root to remote。禁止提供 `PullStore`/`PushStore`/`DownloadStore`/`UploadStore` 这类可被
调用方选成单向复制的 API。offline 时允许 local 已 committed、remote 尚未收敛——但那是
replication pending，不是合法的 local-only 同步模式；下一次同步机会必须重做完整双向。

**含义/动机**：`Local={A,B}, Remote={A,C}` 的任何成功同步终态都必须是
`Local=Remote={A,B,C}`。没有成功的单向 Store synchronization。
**边界**：transport 物理故障（offline/auth/lease contention）的失败形态 → `host-boundary`
与具体 transport；本命题钉协议方向。
**证据**：→ PROOF.md 008。

## DURABLE-CONVERGENCE-009 —— dumb remote 无 domain 逻辑

**规范陈述**：remote 是完全 dumb 的 Git remote：只提供 objects / refs / fetch / push /
lease / CAS / authentication；不知道 Event / Projection / 任何 Wanxiang domain。
同步智能全部在 client。禁止 server-side merge、pre-receive domain reducer、
post-receive projection、Wanxiang-specific server API。

**含义/动机**：普通 GitHub/GitLab/Gitea/bare repository 即可作为 Store remote；把领域
逻辑塞进 server 等于再造一套领域运行时。
**边界**：hook 安装/chain 的安全规则（不覆盖用户 hook）→ `Infrastructure/Git` 实现面
（proof 见 `durable-events` 的 hook-dispatcher 测试）。
**证据**：→ PROOF.md 009。
