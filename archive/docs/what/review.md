# Review — 可观察行为

条款前缀：`REVIEW-`。  
Witness / Seal 所有权见 `shape/review.md`。  
Seal 绑定、过程/终末算法见 `how/review.md`。  
Magic Todo 节拍、settlement、surface 见 `what/todo.md`（TODO-001..014）；Finality cohort / record-ready 见 `what/glory.md`（GLORY-*）。

## REVIEW-001：Judge 工具

```json
{ "verdict": "PERFECT | REVISE" }
```

工具名稳定为 `judge`。旧名 `verdict`（工具）非法，无 alias。  
参数字段 `verdict` 保留（模型自创的 typed judgment）。  
工具不接受描述字段。描述由 Reviewer 的 prose / WorkRecord 承担——**无**固定 formal report schema。  
成功回执不 echo verdict。

## REVIEW-002：REVISE

任一 durable REVISE 立即关闭当前 request 的 Reviewer continuation capability 与 cohort：无 confirmation、不等待尚未 durable 的 sibling 新 terminal / 新 effect；未完成的 PERFECT 确认链同时作废，关闭后不得补发 challenge。`FinalityRejected` 必须另行满足 GLORY-072 的 record-ready，不能在 `judge` 时抢先落盘（GLORY-044/055/072）。

已 durable 的 sibling REVISE 不参与「等待新 terminal」：成功路径下先预置 rejecting primary 的 record-ready/`WriteBlob`，再入账 sibling 并物化为 Manager 的 steer continuation（instruction-only `# ` Synthetic TOML，GLORY-044 双轨交付），不得丢弃、不得并入 `FinalityRejected` 工具结果。Primary 硬物化失败 → `FinalityUndecided` 且零 `FinalitySiblingSteered`。任一 durable sibling 的 LWR 无法物化 → fail-closed `FinalityUndecided`，同样不得静默丢弃。

## REVIEW-003：PERFECT 需要因果证明

第一次 PERFECT 产生 challenge 证据（`PerfectChallengeIssued`）。tool result、ReviewConfirmation nudge 与 `AppendReviewChallenge` 使用同一 skeptical 句：语言为该 Reviewer session 的 `ProviderLanguage`（`resources/provider/review/challenge`，PROMPT-019）。可见 Prompt 是 ARCH-010 指令注释（`# …\n`）；`ChallengeContentDigest` 哈希这组 Prompt 字节。`ChallengeTextVersion` 区分文案世代；英文 canonical 字节不变时版本保持 1。

第二次 PERFECT 成立必须同时满足：

1. 同一 Reviewer Session  
2. 同一 ReviewBarrier  
3. 同一 Git tree  
4. 不同 ProviderRunIdentity  
5. 不同 ToolCallId  
6. 第二次 provider input seal **包含**第一次 challenge result  
7. 中间没有 REVISE  
8. 中间没有 tree 变化  
9. `judge` 工具确实成功执行  

禁止：仅凭 AuthorityRoot 或 PhysicalMessageId 确认。

ReviewConfirmation prompt 只让 Host 启动下一次 provider request，**不是**确认事实本身。

双 PERFECT 屏障完全由 Host 执行，Reviewer 提示词不灌输该流程（REVIEW-012）：Reviewer 只提交基于当前 tree 的独立判断，确认与计数由 Host 侧 witness / seal 完成。

本条仅约束 **FinalityReview**（及既有 Orchestrator 终末复审）的因果双 PERFECT。**TodoProcessReview 一次 PERFECT/REVISE 即 terminal**，不适用 challenge 链（REVIEW-013/020）。

## REVIEW-008：Git tree 变化使 witness 无效

任意 Git tree 变化：

- pending challenge → 拒绝  
- confirmed witness → 仍可审计，但不再满足 Guard  

不删除历史 witness。`witness.IsValid(currentBarrier, currentTree)` 是派生谓词。  
Post-rebase 必须全新双 PERFECT（即使 tree hash 碰巧相同）。

## REVIEW-009：Orchestrator 复审

Rebase 后旧 witness 无效，必须重新获得双 PERFECT，再允许 ff publish。

## REVIEW-011：Examiner's Ledger 与 PERFECT+minor

Reviewer 在调用 `judge` 前，须按 Examiner's Ledger 的判断方向（含 Language & Algorithms、Radical Simplicity、Structural Elegance、Bounded Granularity、Imperative Test Coverage、Flawless Logic & Best Practices、Caller Ergonomics、Uncompromised Completeness）在思想上走完一遍；**只在有值得说的地方说话**。

Ledger / Rulebook：

- 指导如何判断，**不是** checklist，**不是**固定 formal report schema  
- **禁止**把八维烙成必填评估报告字段 / Pass 表 / 固定八段标题  
- **禁止** tiny typo → 自动 REVISE  
- **禁止**「测试必须总是跑过」之类万能律  

发现 **material** 缺陷或不达标 → `judge("REVISE")`。  
Acceptance 必须挣得；Rejection 也必须挣得。match 是 observation，defect 是 judgment。

**PERFECT + minor 共存**：`judge("PERFECT")` 可与真实 non-blocking workmanship 观察共存。minor 进入 prose / blessing 层继续完成，**不**撤销已挣得的 acceptance；non-blocking ≠ 不必做。

TodoProcessReview 在给出过程判断前，必须于本 request 内产生具体 prose 工作记录（缺陷/应改项，或 PERFECT 时已检查且未发现的实质问题）。无 prose 的 PERFECT 无效，不得形成 ConsumableReview（REVIEW-014/016）。过程报告同样无固定 DTO 骨架。

## REVIEW-013：TodoProcessReview 与 FinalityReview 分型

Reviewer 请求必须带 typed `ReviewerRequestKind`，至少：

```text
TodoProcessReview(TodoWriteId)
FinalityReview(FinalityRequestId × ReviewBarrierId)
```

| | TodoProcessReview | FinalityReview |
|--|-------------------|----------------|
| 派生 | 每个 `TodoWriteAccepted` 恰好一次 Rk（TODO-006） | 合法 `suicide` / FinalityRequest cohort（GLORY-003） |
| 终端 | 一次 durable PERFECT 或 REVISE 即 terminal | REVISE 立即关闭；PERFECT → challenge → 二次因果 PERFECT（REVIEW-003） |
| 报告 | request-range `ProcessReviewLWR` → ConsumableReview | request-range Finality LWR → Rejected/Blessed 反馈（GLORY-004/072） |
| Witness | **不**进入 dual-PERFECT / ConfirmedReviewWitness 代数 | 进入 REVIEW-006 witness |
| 并发 | Rk 不阻塞 Manager 后续独立工作；仅 T(k+1)/suicide drain 等待 ConsumableReview（TODO-006/010） | cohort 内按 Finality 规则 |

禁止用 `if pendingChallenge` 或其它运行时猜测在同一 controller 中混用两种业务。RequestKind 必须来自 typed authority。

Manager 面只可见过程 outcome/report 与 todowrite 约定文案；不可见 dedicated session、barrier、witness、2N、roster（TODO-013，GLORY-002，REVIEW-015）。GLORY-030 / SURFACE-005 的窄例外仅指向 TODO-013 允许的 process report 出口，不得扩大。

## REVIEW-014：VerdictKnown 与 ConsumableReview

两段式事实，禁止挤成单一中间 Stage/bool（TODO-006/012，GLORY-009）：

```text
VerdictKnown(k)
  = Reviewer 域已有、针对 TodoProcessReview(k) 的 durable verdict
    （PERFECT | REVISE）
  → 立即决定业务 outcome / settlement 规则（TODO-005）
  → 不携带 WorkRecordRef
  → 不单独构成可消费报告
  → 不进入 Finality dual-PERFECT witness

ConsumableReview(k) ≡ TodoReviewConcluded(k)
  = VerdictKnown(k)
    AND 该 verdict frontier 的 canonical ProcessReviewLWR(k) 已 record-ready
    AND 同 snapshot 已 append TodoReviewConcluded
       （含 WorkRecordRef / Digest）
  → 才允许下一 TodoWrite / suicide drain 消费上一报告
```

顺序冻结：

```text
VerdictKnown(k)
→ await ProcessReviewLWR(k) record-ready（REVIEW-017）
→ append TodoReviewConcluded(k)
→ T(k+1) / suicide 可消费
```

禁止：

- 在「仅有 verdict、报告未 ready」时提前 append `TodoReviewConcluded`
- 为 Magic Todo 另造 `TodoVerdictKnown` bool / `AwaitingReport` Stage（TODO-012）
- 用 raw terminal、summary、issue list、第二 summarizer 顶替 `WorkRecordRef`
- 用同一个 `TodoReviewConcluded` 假装「只有 verdict、尚无 report」

`WorkRecordRef` 唯一来源：REVIEW-016 的 request-range canonical ProcessReviewLWR。

## REVIEW-015：Dedicated 过程 Reviewer（每 Life 一个）

每个 Manager Life 恰好一个 logical `DedicatedTodoReviewer`（TODO-008）：

- 首次 `TodoWriteAccepted` 时若尚不存在 → Host-owned hidden session 创建并 durable enlist
- 后续 checkpoint：同一 logical reviewer，优先同一 physical session；fresh `TodoReviewId` 与 process assignment
- Manager 不得 fork / join / horizon / resume / inspect 该 session（TODO-013，GLORY-002）
- 不得经 Manager 可见 `task` 创建 dedicated reviewer

资源生命周期与 Finality graduate 拆开（TODO-008/010）：

```text
Finality cohort membership  → 可按 ordinary 规则 graduate（GLORY-003/045）
process-review duty / session → 至少保留到 LifeCompleted
                                 或 REVIEW-019 proven-loss replacement
```

Blessing 或 Finality REVISE 后：dedicated **不** Dispose、不丢过程历史，继续服务后续 todowrite process review。第二次及以后 suicide 仍须 drain 最新 ConsumableReview（TODO-010）；blessed fast-path 不免除过程门。

首次进入 terminal Finality 时，dedicated 作为 ordinary cohort member enlist，其后 ordinary graduate；不强制每轮 Finality 回流（GLORY-003/045，TODO-010）。process PERFECT ≠ terminal first PERFECT（REVIEW-020，GLORY-058）。

## REVIEW-016：有界 canonical LWR 与 safety seal

过程/终末审查的工作证据唯一表示：既有 canonical `LifecycleWorkRecord`（LWR）。禁止第二套 Todo 专用工作记录投影或「纯 Y」renderer（TODO-008/012，GLORY-004/050）。

同一 renderer，三个 request-range 用途（`includeOpening=false`）：

```text
ManagerCheckpointLWR(k)  → Life work cursor .. ReviewFrontier(k)=Before(Tk)
ProcessReviewLWR(k)      → ReviewWorkStartCursor(k) .. ReviewerRecordFrontier(k)
Finality reviewer LWR    → FinalityReviewWorkStartCursor .. FinalityVerdictFrontier
```

`ReviewWorkStartCursor` / Finality 对应 cursor = 本次 assignment authority 完整落 XTrace 后的 exclusive end，**不含** assignment prompt 自身。`ReviewFrontier(k)=Before(Tk)` 含同 message 中 tool-call 之前的最后一条助手文本；pending before-hook 不得把该文本的未来 cursor 当成 frontier。禁止取 session 当前 head 冒充任一条有界 LWR。

Process 输入 LWR 使用 **RecordCoverage**（Y 主体 + 未覆盖 frontier 的 canonical RawGap）。Manager Y 未追到 frontier ≠ 不可开始 Rk。Prefix 可替换性仍只认 **PrefixCoverage** / proven Y（TODO-009）；LWR RawGap 永不得直接做 prefix replacement（TODO-008）。

进入 LWR 的禁止项：raw tool call/result 与 linkage、frontier 之后的未来工作、其它 Life 材料。todowrite 自身 raw call/result 不是被审工作内容；old/proposed todo 结构化旁路提供。

Manager-facing ProcessReviewLWR 复用 Finality safety-seal（TODO-013）：

- canonical LWR **不** regex / 任意清洗
- 无法证明对 Manager 安全 → fail closed，不得伪造「洗过的报告」
- 仅放宽过程协议本身允许的 PERFECT / REVISE / review 用词（GLORY-030 窄例外 → TODO-013）

PERFECT 与 REVISE 在过程判断前都必须产生本 request 的 canonical review work record；否则 ProcessReviewLWR 可能永不 record-ready，后续 TodoWrite / suicide 永久阻塞。

## REVIEW-017：同 snapshot record-ready 与禁止轮询

ConsumableReview 与 Finality 拒绝记录的就绪判定复用 GLORY-072/073（TODO-012）：

```text
同一 Journal snapshot
→ 判断 record-ready
→ 同 snapshot materialize canonical LWR
→ append TodoReviewConcluded / FinalityRejected
→ 未 ready → await AgentJournal change → 重读
```

禁止：timer、sleep、wall-clock polling、用较晚 XTrace head 替换冻结 frontier、coverage snapshot 与 LWR materialization 分两次读取、process-local waiter 消失即视为放弃。

恢复：从 durable assignment、`VerdictKnown`、冻结 frontier 重建同一等待；`TodoReviewConcluded` 已在则直接消费，不再等。

## REVIEW-018：基础设施失败语义

下列失败**永远不是**过程/终末业务 PERFECT 或 REVISE，不伪造 settlement / semantic merge，不推进 ConsumableReview（TODO-012）：

```text
dedicated create / resume
process assignment
Y / LWR materialization
Host contract 破坏
其它 infrastructure failure
```

处理：

```text
Accepted 派生的 Rk obligation 保持 outstanding
可证明可恢复 → event-driven ensureReview / ensureAssignment（REVIEW-017）
不可证明 / 契约破坏 → 当前操作返回 typed infrastructure failure
  → Finality 不得越过该 outstanding Rk
  → 下一 TodoWrite 继续阻塞在同一 Rk
```

## REVIEW-019：仅 proven loss 后替换 Dedicated

仅当 Host **已证明**原 physical session 永久不可恢复时，才可 `DedicatedTodoReviewerReplaced`（logical id 不变，新 session id + EvidenceRef）（TODO-008/012）。

不确定 → fail closed，不替换。

替换后的新 session 必须先重新获得后再继续过程评审：

```text
OpeningRaw
+ 当前 Life 截止最新已消费 checkpoint 的 frontier-bounded Manager LWR
+ 全部既往 process-review WorkRecordRef
```

禁止：超时、单次 resume 失败、或「换一个更快模型」作为替换理由。

## REVIEW-020：过程 verdict 不是终末 witness

```text
process PERFECT  ≠ terminal first/second PERFECT
process REVISE   ≠ FinalityRejected 事实本身
ConfirmedReviewWitness / dual-PERFECT 代数仅服务 FinalityReview（与 Orchestrator 复审）
```

Dedicated 被 enlist 进某次 FinalityRequest 时，即使刚完成 process PERFECT，仍须（TODO-010，GLORY-058）：

```text
fresh FinalityRequestId / BarrierId / GitTreeHash / Authority Root
fresh dual-PERFECT chain（REVIEW-003）
```

可复用同一 physical session/context，不可复用过程因果证明。过程报告 LWR 与终末 record LWR 仍按 REVIEW-016 各自有界，不得把历史 process turns 整段塞进终末 LWR。

suicide 前序的 tail drain 与零 checkpoint fail-closed 义务见 TODO-010；本条只冻结「过程结论不是终末 witness」与 drain 时的审查语义。
