# Companion — 可观察行为

条款前缀：`COMPANION-`。  
关联与 epoch 边界见 `shape/companion.md`。  
Y 投影形状见 `how/companion.md`。  
失败驱动恢复见 `what/context.md`。

## COMPANION-001：每个 Work Session 都有 Companion

每个由万象术管理、能发普通 provider request 的 Session 是 Work Session（X），**恰好**一个叶子 Companion Blogger（Y）。

该关系**不是**角色资格：与 Role、Tier、工具面、公开/内部、Logical Run、Authority、Fallback 无关。  
禁止任何 eligibility 白名单或从 last message / cache 推断「该不该有 Y」。

## COMPANION-002：Companion 是叶子

```text
X → 恰好一个 Y
Y → 不再递归
深度恒为 1
```

## COMPANION-003：XTrace · Frames · LWR / WorkRecord

| 概念 | 含义 |
|------|------|
| XTrace | X 唯一原始语义轨迹（HOST-005） |
| BlogFrame | Y 历史（Entry/Squash，无 Seed） |
| LWR / WorkRecord | 跨 Session 唯一工作记录；同步/异步共享同一协议 |

```text
WorkRecord(invocation) =
    Opening
  + Chronicle
  + Recent work
  + Closing report
```

| 段 | 含义 |
|----|------|
| Opening | 交托关闭前的完整语义区间（preserved XTrace；见 COMPANION-014） |
| Chronicle | 已由 Y 沉淀的工作叙事 |
| Recent work | Y 尚未覆盖、仍由 X 直接承担的最近工作（表示边界，非「对读者是否新近」） |
| Closing report | 本次 invocation 的 terminal 正式陈述；散文 claim，非固定字段 schema |

```text
OpeningMaterial
  = exact semantic XTrace interval [work start, OpeningBoundary)

OpeningBoundary
  = Opening exclusive end
  = WorkRecordStart（TODO-001；BlindPlan 下含 T1 commitment call/result）

LWR(X) = OpeningMaterial?
       + Chronicle（全部有效 Y frame）
       + Recent work（RawGapFromX，未覆盖 suffix，经 LWR 投影）
       + Closing report（TerminalOutputRaw）
```

Opening 永不送 Y 压缩；Closing 不复经 transform。  
删除 `OpeningPromptRaw = { AssignmentText; AuthoritativeRequirements }` 拼接重建。  
LWR **硬禁止** raw tool call/result 与 call/result linkage（BlindPlan T1 commitment call/result 属 Opening constitutive material，见 COMPANION-014，不属 incidental tool）。  
父 LWR 是 child 输入 context，**不**作 child Seed / Opening 复制。  
Strength Candidate 永不进入 XTrace / LWR / PrefixSnapshot；只有 Promoted frame 经 STRENGTH-008 进入 XTrace 后，Companion 才可消化（STRENGTH-006/008）。

两种 coverage 禁止混用（义务与 Magic Todo lag-1 共用，见 TODO-008/009；本条仍是 LWR/Y 分型所有者）：

```text
RecordCoverage   // XTrace 游标；LWR gap 起点；可落在 turn 中间
PrefixCoverage   // 完整 Host turn 边界；prefix replacement 证明
```

X prefix probe **与** TodoCheckpoint lag-1 rebase 都只能用 CoverableRecordPrefix（Opening + 可证明完整 turn 的 Y prefix），不能用 RawGap。  
`EvidenceKind=TodoCheckpoint` 进入既有 `ActivePrefixEpoch` / `PrefixRebaseCommitted`（COMPANION-009 / TODO-009），不另造 coverage 或 LWR 投影（TODO-008、TODO-012）。

`includeOpening`：父→子 true；子→父 false；同 Session frozen prefix true。  
Canonical record 保留 Opening，即使投影省略。

旧标题 `Opening task / Work log / Uncompressed tail / Final output` **已删除**；无 alias。

## COMPANION-004：Y 的 system

唯一来源：`resources/prompts/blogger-system.md`。  
fast/deep blogger 同 prompt、同 `{chronicle}` 工具面，只模型绑定不同。  
禁止在 prompt 插入动态 token/预算/窗口信息（CTX-001）。

## COMPANION-006：Frame squash

squash 只处理本 X 的 frames，不混父 context（动作细节 CTX-012）。

## COMPANION-007：Semantic 投影与 TOML delta

送 Y 的 delta 与 LWR gap 同源 XTrace、不同投影：delta 可含 tool 作压缩输入；LWR gap 剔除 raw tool。  
canonical digest 用 Semantic projection，禁止反向解析 TOML。

## COMPANION-008：Busy skip 与 coverage 不推进

Blogger busy：不打断、不排队、**不推进** RecordCoverage；失败/空/XML-only 不推进。  
仅 `BlogEntryCommitted` 原子推进 frame 可见性与 RecordCoverage（PERSIST-010）。  
Host compaction 只作废 PrefixCoverage 映射，不得清零 RecordCoverage/Frames。  
`PrefixRebaseCommitted`（含 `EvidenceKind=TodoCheckpoint`，TODO-009）切换 ActivePrefixEpoch / PrefixCoverage 证明边界，**不**推进 RecordCoverage，也不得把 LWR RawGap 写进 prefix Y bundle（TODO-008/009）。

## COMPANION-010：低信任注入

FrozenRecordPrefix 以明确标记的 context block 注入 X，不伪装人类/system 指令，同 epoch 内内容冻结。

## COMPANION-011：Cutoff 证明

Cutoff 只在完整 semantic turn 边界；投影前重算 CoveredPrefixDigest，失配 fail closed。  
digest 失配不是 compaction 善后手段——善后是 HOST-006 重锚。

## COMPANION-012：Provider-visible projection

缓存比较只用进模型的字段（排除 timestamp/cost/usage/runtimeId…）。

## COMPANION-013：Synthetic 稳定身份

Synthetic id 由 SealRoot / frameEpoch / ordinal 等确定性派生；禁止 GUID/random/时间/Host runtimeId。  
probe 成功 promote 时继承同一 SealRoot，避免多余冷边界。

## COMPANION-014：OpeningMaterial 是 preserved，不是 reconstructed

```text
OpeningMaterial = exact XTrace [work start, OpeningBoundary)
```

禁止拼 `AssignmentText` / `AuthoritativeRequirements`、重编号 requirements、或任何第二事实源重建 Opening。

Opening closes at role-defined commitment boundary（OpeningPolicy，GLORY-074）；一旦关闭永不移动。

```text
Immediate:
    Opening = InitialCharge

BlindPlan:
    Opening = InitialCharge
            + pre-commitment reasoning
            + investigation
            + delegated returns
            + user clarifications
            + commitment call
            + canonical accepted commitment result
```

BlindPlan 下 T1 `todowrite` call + canonical accepted result 是 constitutive Opening material：  
`XTrace.forOpening` 保留；不得当 incidental tool 滤入 Recent work。

Opening always raw：never Blogger / never Y / never prefix-replaced；survives Host compaction、reanchor、recovery。  
after Opening（WorkRecordStart）才进入 ordinary Chronicle / Recent / Y machinery。

## COMPANION-015：WorkRecord 核心不变量

```text
① A WorkRecord belongs to a piece of work, not to a receiver.
② Its boundary is causal, not conversational.
③ Chronicle and Recent work describe representation, not who has seen the material.
④ Reuse preserves memory; it does not enlarge the next WorkRecord.
⑤ Recent work ≠ receiver-relative recentness：是 bounded invocation 内 Y 未覆盖的 X-derived safe suffix。
⑥ Canonical record retains Opening even when projection omits it.
⑦ parent→child includeOpening=true；child→parent includeOpening=false（冻结）。
⑧ Opening is preserved, not reconstructed（COMPANION-014）。
⑨ BlindPlan：commitment call/result 属 constitutive Opening material。
⑩ One invocation. One record. Everywhere.
```

Sync 与 Async 只差等待时机，不差表示：`inspect` / `fork`+`join` 均物化同一 WorkRecord 协议。  
SyncDelegate：每次 call 自有 `InvocationStartCursor..InvocationEndCursor`；reusable session memory 可留存，但 caller 只见当前 range；`includeOpening=false`（不回 charge echo）。

Closing report = prose claim：约束诚实，不约束骨架。禁止 universal fixed report schema（`### Summary` / files/tests/…）。machine-semantic 结构只留协议真需处。
