# degeneration-guard — 实现模型与约束

非 normative：本文描述当前实现怎么满足 WHAT，不另造 owner。

## 模块地图

| 模块 | 角色 | 对应命题 |
|---|---|---|
| `src/Wanxiangshu/Session/LoopDetector.fs` | 纯检测器：4-gram 滑动窗口 + 3 个慢指数核 + 正常代码先验；O(1) 递推、固定 4096 桶 | DG-003/004/005 |
| `src/Wanxiangshu/Infrastructure/OpenCode/Host/LoopSensor.fs` | transport 边沿观测器：持有 per-session detectors 与进程内 `LoopKillArmed` 集合；Observe 只吃 text delta；命中 → TryArm → AbortSession | DG-002/006/007/008 |
| `src/Wanxiangshu/Application/Recovery/ProviderRecoveryWorkflow.fs` | `continueAfterLoopKill`：armed abort 走与 provider failure 等价的 FallbackController 路径（reason="loop-kill"） | DG-009 |
| `src/Wanxiangshu/Session/CompletionMailbox.fs` / `TurnCompletionProgram`（经 how/host.md 描述） | TurnAborted 消费边界：命中 LoopKillArmed → 清标记 → 桥接；未命中 → 终止清理、不构造 RunCompletion | DG-007/009 |
| `src/Wanxiangshu/Infrastructure/OpenCode/Host/HostSignalBootstrap.fs` | `SessionIdle` → `LoopSensor.ResetDetector` 后 ObserveIdle（attempt 边界重置） | DG-006 |

## 判定递推（每有效字符）

```text
if character ∈ IGNORED:  return 当前评价（不改状态）
prefix ← append character；|prefix| < 4 → 返回先验评价（NORMAL, N_eff=256）
gram = 最近 4 字符；bucket = stable_hash(gram) mod 4096
materialize(bucket)（按 elapsed 指数衰减）
Cross[j][k] ← λj·λk·Cross[j][k] + λj·old[j] + λk·old[k] + 1
Total[j]    ← λj·Total[j] + 1
Value[b][j] ← λj·old[j] + 1
Step ← Step + 1；滑动 prefix；evaluate（N_eff ≤ 140 → LOOP）
```

并发模型：每 attempt 恰好一个 SSE 事件泵（单线程事件循环），`part.delta` 由该泵串行投递 → 每次
恰好一个 feed 调用；detector 在 attempt 生命周期内不外泄、不被其它线程读取，因此无锁。让步条件
缺一即红线：delta 不得并行进入、detector 禁止跨 attempt 共享、诊断字段在 attempt 终止后一次性
快照读取。

## 强杀与桥接（LOOP-006 动作序列）

```text
Step 1  is_loop 且未 armed → LoopKillArmed.record(sessionId) → HostSDK.abortSession(sessionId)
        （已 armed → ignore 幂等）
Step 2  Host 返回 ReconciledTurn(Outcome=TurnAborted) 且 LoopKillArmed 命中
        → clear → recovery = LoopKillFailure(providerRunIdentity)
Step 3  FallbackController.recordConfirmedFailure(providerRunIdentity)（唯一写入口）
Step 4  verdict.MayContinue → 发 ProviderRetryAttempt continuation（loop-continue 正文）
        else → FallbackExhausted 终局
```

`LoopKillArmed` 与 detector 都是进程内局部事实，不写 Journal，重启崩溃后自然丢失（安全侧）。

## 历史与弃权

以下事实来自历史 why/loop 考古，均为决策记录，不是现行命题：

- **可变状态封装**：拒公开 `mutable Step` / `Value[][]` 等可变数组字段（易被传出 attempt 边界或
  被诊断并发读取）；选私有封装 + 只读快照 + feed 接口。
- **恢复桥接**：拒独立 Loop 恢复机制（第二状态机、破坏 FALLBACK-003 唯一写入口）；选桥接
  FallbackController。
- **检测算法**：拒滑动窗口计数（跨窗遗忘生硬）与精确重复表（无限流不可行）；选 4-gram + 慢指数核
  逼近 Zipf 型无限历史。
- **记忆结构**：拒无限 Map（随流增长）；接受哈希桶碰撞（更敏感），禁止为「更准」改回无限结构。
- **冷启动**：拒 `MIN_NGRAMS` 预热窗（判定盲区）；选正常代码先验（无罪推定）。
- **阈值**：拒角色/语言动态阈值（不可测）；在 N_eff 空间取固定中点（判定物理量是 N_eff）。
- **并发**：无锁依赖 = 每 attempt 单事件泵串行投递（EXEC-024 mailbox 语义）。

## GARBAGE / 弃权裁决

- **当前 4-gram / 指数核算法**（NGRAM_SIZE=4、K=3、半衰期 8/64/512、LOOP_EFFECTIVE_COUNT=140）：
  HOW。boundary card INDEPENDENT CHANGE 明确「换掉当前 detector，只要它仍 attempt-local、
  bounded、非权威并复用标准 recovery」是本包可独立变化点。
- **N_eff / HHI 的具体数值**：HOW（阈值的宿主空间 N_eff 是规范物理量，具体常数可换）。
- **LoopKillArmed 的「允许重复输出」**：DG-008 的推论，不另立命题。
