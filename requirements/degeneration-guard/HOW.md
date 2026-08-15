# degeneration-guard — 实现模型与约束

非 normative：本文描述当前实现怎么满足 WHAT，不另造 owner。

## 模块地图

| 模块 | 角色 | 对应命题 |
|---|---|---|
| `src/Wanxiangshu/Execution/Session/LoopDetector.fs` | `gpt-tokenizer/o200k_base` + 指数衰减 weighted-distinct token detector | DG-003/004/005 |
| `src/Wanxiangshu/OpenCode/Host/LoopSensor.fs` | transport 边沿观测器：持有 per-session detectors 与进程内 `LoopKillArmed` 集合；Observe 只吃 text delta；命中 → TryArm → AbortSession | DG-002/006/007/008 |
| `src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs` | loop-kill armed abort 桥接到 `recordConfirmedFailure("loop-kill")` | DG-009 |
| `src/Wanxiangshu/OpenCode/Host/HostTurnObserver.fs` | TurnAborted 消费边界：命中 LoopKillArmed → 清标记 → 标准 recovery；未命中 → 普通 abort | DG-007/009 |
| `requirements/degeneration-guard/tests/loop-calibration*.mjs` | 扫仓库全部 strict UTF-8 文字，重放 half-life / normal / midpoint 滴定 | DG-004 |

## 判定递推（每 token）

```text
tokens = o200k_base.encode(text_delta)
for token in tokens:
  step = Step + 1
  if LastSeen[token] = previous:
      replacement = 1 - λ^(step - previous)
  else:
      replacement = 1
  D = λ·D + replacement
  LastSeen[token] = step
  Step = step

LOOP iff D <= threshold
```

这里的 `D` 直接表示「最近 token vocabulary 中仍有多少不同 token 保有显著权重」。高频结构 token
只会反复重置自己的一个贡献；它们不会因出现次数平方而支配指标。因此 Markdown table / ASCII graph
即使 `|`, `-`, `>`, 空白等结构 token 很多，只要名称、数字、字段和值仍持续多样，`D` 仍高。

## 滴定

1. `git ls-files --cached --others --exclude-standard` 得到仓库集合。
2. `TextDecoder('utf-8', { fatal: true })` 定义「可读文字」；不可 strict UTF-8 解码者排除。
3. 所有非空行分别用 o200k 计 token；p99=56，向上取二次幂 → `HALF_LIFE=64`。
4. 所有可读文字按确定的 git path 顺序连接并 token 化。
5. calibration 从理论最大 distinct steady prior `1/(1-λ)` 扫完整 token 流，取全程最低 `D` 为正常侧：当前 `19.260485812342168`。
6. 异常侧不采样：全为同一 token 时 `D` 的理论极限就是 `1`。
7. threshold = `(normal + 1) / 2 = 10.130242906171084`。

production fresh detector 从正常侧值开始，而不是从 1 开始，避免短输出天然被判 loop。

## 内存

`LastSeenTokenStep` 只以 tokenizer token id 为 key。o200k vocabulary 是固定有限集合，重复输出不会增加
key 数；每 token 更新 O(1)，状态不保存原文，不保存 n-gram，不保存 transcript。相比旧 4096 hash
bucket，当前实现不以 collision 把两个真实 token 合并，指标就是不同 token 本身。

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

`LoopKillArmed` 与 detector 都是进程内局部事实，不写 Journal，重启后自然丢失。

## 历史弃权

旧实现的字符过滤、4-gram、4096 hash buckets、三指数核、HHI / inverse-Simpson、
`NORMAL_EFFECTIVE_COUNT=256`、`GARBAGE_EFFECTIVE_COUNT=24`、`threshold=140` 全部废弃。它们不是兼容层，
也不再作为 fallback 判定保留。

保留的边界只有：attempt-local、bounded、非权威、一次越阈、LoopKillArmed 幂等，以及复用标准 recovery。
