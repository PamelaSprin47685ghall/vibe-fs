# WHAT —— durable-convergence（唯一 normative 合同）

条款前缀 `DURABLE-CONVERGENCE-`。每条的落点测试见 `HOW.md`。
来源：历史 change（storage）（§5.3、§10、§11–§19、§38、§42、§48）、
历史 what/casebook（CASE-011）、历史 COVERAGE persist 小节
（PERSIST-003 split）。

## DURABLE-CONVERGENCE-001 —— merge = set union，永不丢事实

**规范陈述**：replica merge 是 append-only set union + identity dedupe：按 EventId 去重，
两个不同 EventId 永远都进入 merged history（即使 DomainConflict 亦保留全部 facts）。
禁止 Store 用 wall_clock/revision 裁决而让任一 durable fact 消失。

**含义/动机**：Persist 负责不丢事实；Domain 负责解释事实是否相容。丢分支是数据丢失，
不是策略。
**边界**：同 EventId 异 bytes 的 identity-collision 裁决（fail closed）归
`durable-events`（003）；本命题只钉「不同 EventId 永不因冲突丢失」。
**证据**：→ HOW.md 001。

## DURABLE-CONVERGENCE-002 —— k-way merge 是统一 primitive

**规范陈述**：`KWayMerge(writerStreams[])` 满足 associative / commutative / idempotent /
deterministic：`merge(A, merge(B,C)) = merge(merge(A,B), C)`、`merge(A,B) = merge(B,A)`、
`merge(A,A) = A`、同一组有序 writer streams 无论枚举顺序/由哪个 process/哪台 machine 执行都产生
相同 canonical event order。输入可来自当前 process、其它本地 process、remote snapshot、recovery。

**含义/动机**：统一 primitive 是并发模型的地基——boot/recovery 和 remote sync 共用同一个 k-way
ordering/identity primitive；ordinary local append 只延长自己的单 writer file，不需要先全局 merge。
**边界**：writer file / blob 的物理映射见 003 与 `durable-events` 005/011/018。
**证据**：→ HOW.md 002。

## DURABLE-CONVERGENCE-003 —— 生产 writer-stream k-way merge ≡ union oracle

**规范陈述**：生产 merge 必须与 set-union spec oracle 等价：本地每个
`.git/wanxiang/events/<WriterId>.ndjson` 与远端每个 WriterId blob 都是一个完整有序流；读取这些流执行
k-way merge + EventId identity dedupe，结果必须等价于 `union(all events)`。不得引入 segment/chunk、
EventId→blob index、Git structural merge 或 delta protocol。

**含义/动机**：一个 process 一个完整文件让单机多进程与多机完全同构；生产算法只处理 k 个顺序流，
而不是 Git tree 的物理偶然结构。
**边界**：same EventId 异 canonical bytes → identity collision；业务含义只由 canonical Integrator 解释。
**证据**：→ HOW.md 003。

## DURABLE-CONVERGENCE-004 —— 合法并发 fork → DomainConflict，非 StorageInvalid

**规范陈述**：同一 stream/业务键的合法并发 fork 是物理层正常产物（A、B 离线同见
parent=P 各自 append A1/B1），必须被定义为 DomainConflict，由 projection 表达为
deterministic conflict state。Storage 层永不因自然 fork 进入不可恢复；严禁把领域禁止
的并发 fork 判为 StorageInvalid。history 保留全部 competing heads。

**含义/动机**：append-only union 必然产生物理 fork；它与「全局不可恢复」正交。
「forbidden fork」指业务不可接受态，由 projection 表达并经 resolution 收敛。
**边界**：「不把 DomainConflict 升级为全局 corruption」的反向钉死见
`durable-events` 008；本命题是正向表达律。
**证据**：→ HOW.md 004。

## DURABLE-CONVERGENCE-005 —— resolution event 以全部 heads 为 parents 才收敛

**规范陈述**：resolution event（`FooConflictResolved` / 领域具体 `*Resolved`）必须以
**所有 competing heads 为 parents**（至少包含需裁决的 heads 集合），在 DAG 上显式声明
「已知并裁决了这些并发分支」；仅当 resolution 及其全部 parents 已 fold，projection
才离开 conflict state。

**含义/动机**：收敛不是遗忘：resolution 必须承认并覆盖它裁决的每个分支，否则未来重放
无法重建「为什么离开 conflict」。
**边界**：resolution 的领域语义（裁决了什么、为什么）归各 domain owner。
**证据**：→ HOW.md 005。

## DURABLE-CONVERGENCE-006 —— 禁止 wall_clock/revision LWW

**规范陈述**：merge 不得使用 wall-clock、revision、timestamp 裁决 winner；收敛必须是
event 集合的纯函数，与 replica 的到达顺序、append 时刻无关。revision/wall_clock/
deterministic tie 最多只能作为 **projection 层**从完整历史派生当前视图的规则，不允许
删除 loser event、不允许影响其它 domain。

**含义/动机**：时间戳不证明内容未变；revision 排序制造第二真相。LWW 的合法残余位置
是 projection 规则（如 Casebook 从完整 history 派生 `CurrentCase(session)`），不是
replication 规则。
**边界**：Casebook 对象层面的禁 LWW 语义归 `knowledge-reuse`；本命题钉 general merge 律。
**证据**：→ HOW.md 006。

## DURABLE-CONVERGENCE-007 —— 相同 merged history → 同一个 Integrator Current

**规范陈述**：收敛公式是 `Current = CanonicalIntegrator(KWayMerge(writerStreams))`，不是
`Merge(CurrentA, CurrentB)`，也不是各业务模块各自重扫历史。相同 writer histories 必须得到相同
Current；唯一 Integrator 与注册规则由 `durable-events` 014/019 保证。

**含义/动机**：Current 不是第二真相源；它只是唯一正规积分器对完整事实历史的最终积分状态。
**边界**：业务 integration rule 的语义归各 domain owner。
**证据**：→ HOW.md 007。

## DURABLE-CONVERGENCE-008 —— durability activation ensure hooks；用户 Git 进程独立触发双向 sync

**规范陈述**：Wanxiangshu 不提供 timer/background/event-count 同步器，也不从 OpenCode/Wanxiangshu 产品进程
主动调用 fetch/pull/push。OpenCode 等待 plugin init 返回的 Load Phase 不得修改 Git；第一次真实 workspace 业务交互进入 durability activation 时才 ensure `reference-transaction` / `pre-push` hook 以及各已知
remote 的 Wanxiang store fetch-refspec 正确安装；安装失败明确诊断，但不得反向让 plugin load 卡死。之后同步由
**用户自己的 Git 进程启动的 hook 子进程**执行，即使 OpenCode/Wanxiangshu 已退出也必须可工作。hook shim
不得固化安装时宿主的 `process.execPath`（OpenCode/Bun/其它 host binary 都不是 Node runtime）；它必须通过
`/usr/bin/env node <package>/resources/git/wanxiang-hook.mjs` 调起随包 runner，由 package 的 Node `>=20` runtime 独立解释，
且不得依赖 runner 文件本身具有 executable bit。随后读取 local writer
files + remote writer blobs → k-way merge/validate → 直接替换本地同步后的 writer-file 集合 → 将每个完整 writer
file 编码为一个 blob 并发布 remote snapshot。成功终态必须 local/remote 表示同一 event history；不得提供可成功的
Store-only 单向 Download/Upload 模式。

`reference-transaction` 消费用户 fetch/pull 已更新的 Wanxiang remote-tracking ref后，**仍执行完整双向收敛**：
以该 observed remote root 为输入，local+remote k-way merge，替换本地 writer truth，并把统一后的 Wanxiang store ref 发布回
remote；`pre-push` 也在用户 push 真正发送普通 refs 前执行同一完整双向收敛。两者差别只在 remote root 的发现方式，
不是同步方向。hook 内部为完成该次用户操作而进行的 store-ref fetch/push 使用递归 guard，不构成产品进程主动同步。

**含义/动机**：`Local={A,B}, Remote={A,C}` 成功后都是 `{A,B,C}`；`git push` 可能发生在 Wanxiangshu 完全未运行时，
因此同步执行权必须属于已安装 hook，而不是某个 process-local EventStore/GitGateway object。
**边界**：transport 物理故障（offline/auth/lease contention）可使 remote pending，但不得撤销已本地 committed facts。
**证据**：→ HOW.md 008。

## DURABLE-CONVERGENCE-009 —— dumb remote 无 domain 逻辑

**规范陈述**：remote 是完全 dumb 的 Git remote：只提供 objects / refs / fetch / push /
lease / CAS / authentication；不知道 Event / Projection / 任何 Wanxiang domain。
同步智能全部在 client。禁止 server-side merge、pre-receive domain reducer、
post-receive projection、Wanxiang-specific server API。

**含义/动机**：普通 GitHub/GitLab/Gitea/bare repository 即可作为 Store remote；把领域
逻辑塞进 server 等于再造一套领域运行时。
**边界**：hook 安装/chain 的安全规则（不覆盖用户 hook）→ `Infrastructure/Git` 实现面
（proof 见 `durable-events` 的 hook-dispatcher 测试）。
**证据**：→ HOW.md 009。
