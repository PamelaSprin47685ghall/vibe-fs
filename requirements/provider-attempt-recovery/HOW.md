# provider-attempt-recovery — 实现模型与约束

非 normative：本文描述当前实现怎么满足 WHAT，不另造 owner。读者可按本节定位代码，再回 WHAT.md
对照命题。

## 模块地图

| 模块 | 角色 | 对应命题 |
|---|---|---|
| `src/Wanxiangshu/Domain/AgentPairCursor.fs` | 纯 A/A/B/B cursor 算术（Offset/预算/verdict/attemptIdentity/effectiveAgent/isRecoverySlot） | PAR-001/002/004/005/006/009 |
| `src/Wanxiangshu/Domain/RecoverySlot.fs` | 纯槽决策（arming、squash/main outcome → SlotDecision、advancesCursor/nextArming） | PAR-008/010/011 |
| `src/Wanxiangshu/Application/Recovery/FallbackLedger.fs` | **唯一写入口**：confirmed failure → dedupe → advance/exhaust → append 事实；`admitConfirmedFailure` 投影 host-facing admission | PAR-003/005/007 |
| `src/Wanxiangshu/Application/Recovery/FallbackEvidence.fs` | 只读查询（currentCursor/currentSide/effectiveAgent/mayContinue） | PAR-004/013 |
| `src/Wanxiangshu/Application/Recovery/ProviderRecoveryWorkflow.fs` | 失败后的恢复编排：记录失败 → 等 coverage material → 决定 continuation；`continueAfterLoopKill` 桥接 degeneration-guard | PAR-003/010/014 |
| `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Projection.fs` / `FallbackFactFold.fs` | 持久事实的 fold 与拒绝条件 | PAR-002/007 |
| `src/Wanxiangshu/Session/EnforcerRepair.fs` | `interrupted=true` 残留的判定 | PAR-012 |

## 一次已确认失败的主路径（代码时序）

```text
Host 粗粒度信号（idle / retry）            // 只唤醒，不裁决（HOST-004 归 host-boundary）
→ Reconciler 从完整 Host snapshot 识别失败的 provider attempt
→ ProviderRecoveryWorkflow.continueAfterConfirmedFailure(turn, error, continuationPrompt)
→ FallbackLedger.recordConfirmedFailure(journal, DefaultAutoRecoveryBudget, session, providerRun, reason)
     → FallbackEvidence.tryCurrentState：无 cursor → NoActiveRun（无事实）
     → applyAdvance 拒绝 AlreadyObserved/AlreadyExhausted/DifferentRun/NoCursor → AlreadyRecorded/NoActiveRun
     → applyAdvance 拒绝 InvalidTransition/InvalidFallbackOffset → Error（fail closed）
     → Ok → append FallbackCursorAdvanced（唯一写入口）
     → recoveryVerdict budget：
         MayContinue → RecoveryAdvanced → awaitRecoveryMaterial → 发 ProviderRetryAttempt continuation
         Exhausted   → append FallbackExhausted → RecoveryExhausted（无自动下一步）
```

关键约束：cursor 推进发生在 **reconcile 出的已确认失败**，不在 Host retry 事件处理器里
（retry 只负责唤醒）；`awaitRecoveryMaterial` 等 coverage 是为 CTX-006 armed 槽争取 material，
超时仍发普通主请求（CTX-011 no-candidate 路径，fail open）。

## 持久事实形状

```fsharp
FallbackCursorAdvanced = { SessionId; LogicalRunId; AuthorityRootUserMessageId
                           ProviderRun; PreviousOffset; NextOffset; ConsecutiveFailureCount; Reason }
FallbackExhausted      = { SessionId; LogicalRunId; AuthorityRootUserMessageId
                           FinalConsecutiveFailureCount; FinalOffset }
```

成功不写 cursor 事实：归零从 Host snapshot 的 Completed 派生（PAR-004 无第二写入口）。

## 历史与弃权

以下事实来自历史 why/fallback 与归档 changes 考古，均为决策记录，不是现行命题：

- **Offset 表示**：拒 byte/int 裸计数（0–255 皆可构造，side 对非法字节无分支）；拒 decode 抛
  `invalidOp`（持久化损坏是可预见失败）；选 `Result<FallbackOffset, FallbackOffsetDecodeError>`。
- **armed 标志**：拒把 armed 写盘或仅凭持久化奇数 Offset 判定（上次主请求成功时 Offset 可停奇数）；
  选内存局部 `armedByFailure`，崩溃后归零（安全侧）。
- **成功写归零事实**：拒多一个 `FallbackCursorAdvanced` 变体（VERIFY-005 单一写入口）；选派生。
- **侧循环判死 vs 预算判死**：拒侧上限（换侧是合法恢复策略）；判死收敛到有界预算。
- **Host Attempt vs 领域计数**：拒混用（量纲不同，重启会错误清零/耗尽）。
- **预算固定 vs 动态**：拒按模型/上下文调阈值（不可测，特例森林）；固定有限正整数。
- **切边**：拒随 fallback 重写 Persona/prompt/language（伪造新身份、打碎 KV-cache 前缀）；
  只换 EffectiveAgent；cursor/Side/Offset/count 不投影给 provider。
- **FALLBACK-011 槽算法与 FALLBACK-012 armed 合取**：维护子请求失败即槽失败；armed 只由真实失败
  推进产生。算法细节在历史 how/fallback 条款已并入上文模块地图，不再另列。

## GARBAGE / 弃权裁决

- **当前 AABB 名字 / Offset 表示 / 具体预算数值**（`fast-coder`/`deep-coder`、Fork0..3、12）：
  HOW，不是规范命题（boundary card DOES NOT OWN）。换名字/表示/默认值不改变 PAR 命题。
- **budget 的配置渠道**：本包只要求「有限正整数、必要时可配置」，不拥有配置系统。
- **Cursor wire 附着（Pair Hint）**：属 `provider-projection`，本包只消费 `effectiveAgent`。
