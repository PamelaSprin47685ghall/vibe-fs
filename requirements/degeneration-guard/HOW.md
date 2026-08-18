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
3. 所有非空行分别用 o200k 计 token；当前 p99=59，向上取二次幂 → `HALF_LIFE=64`。
4. 所有可读文字按确定的 git path 顺序连接并 token 化。
5. calibration 从理论最大 distinct steady prior `1/(1-λ)` 扫完整 token 流，取全程最低 `D` 为正常侧：当前 `18.52650592106275`。这些当前统计基线随仓库语料派生；feel free to modify，重跑 calibration 后同步 production 与文档即可。
6. 异常侧不采样：全为同一 token 时 `D` 的理论极限就是 `1`。
7. threshold = `(normal + 1) / 2 = 9.763252960531375`。

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

## 依赖

DEPENDS ON：`provider-attempt-recovery`（桥接目标：命中后由标准 recovery 决定 cursor/budget）、
`host-boundary`（AbortSession 是 Host 物理能力、snapshot 观察由 Host 提供）。理由：DG-009 消费
前者的唯一写入口，DG-002/007 消费后者的 transport 观察边界。

## 验证与测试落点

运行命令：单文件 `node --test requirements/degeneration-guard/tests/<file>.test.mjs`；整包被
`node requirements/verification-system/tests/run.mjs` 自动发现。落点类型：MOVE = 从旧 `tests/unit`
物理移入本包；REUSE = 留在原处；NEW = 本包新写。

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| DG-001 低多样性 loop vs 正常多样输出 | `tests/loop-detector.test.mjs`：`LOOP_003_single_token_repetition_converges_to_theoretical_loop`、`LOOP_003_diverse_programmatic_text_stays_normal`、Markdown table / ASCII graph 两个 normal fixture | MOVE+NEW | `node --test requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-002 传感器只吃 text delta、不写业务事实 | `tests/loop-sensor.test.mjs`：`LOOP_002_sensor_observes_text_delta_only`、`LOOP_007_reasoning_deltas_are_ignored`；`tests/loop-detector.test.mjs`：`LOOP_009_text_delta_decodes_fail_closed` | MOVE | 各文件 `node --test` |
| DG-003 token weighted-distinct 指标 | `tests/loop-detector.test.mjs`：fresh prior、o200k exact step/reference score、whitespace/punctuation 也是 token、single-token loop、diverse programmatic、Markdown table、ASCII graph；`tests/loop-detector-memory.test.mjs`：单次越阈无 latch / 无 consecutive-hit 要求 | MOVE+NEW | 各文件 `node --test` |
| DG-004 固定参数 + 全仓滴定 | `tests/loop-calibration.test.mjs`：Git tracked+unignored strict UTF-8 全文；当前非空行 token p99=59 → half-life=64；正常端=全文最低 weighted-distinct；异常端=理论 1；threshold=中线。当前统计基线随语料派生，feel free to modify；`tests/loop-detector.test.mjs` 校验 production 常量关系 | NEW | `node --test requirements/degeneration-guard/tests/loop-calibration.test.mjs requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-005 O(1) 更新与 vocabulary-bounded 内存 | `tests/loop-detector-memory.test.mjs`：`LOOP_005_detector_memory_is_bounded_by_tokenizer_vocabulary_not_stream_length`；`tests/loop-detector.test.mjs`：reference recurrence exactness | NEW | 各文件 `node --test` |
| DG-006 生命周期绑定单次 ProviderRun | `tests/loop-sensor.test.mjs`：`LOOP_006_reset_detector_preserves_loop_kill_armed`；`tests/loop-detector-memory.test.mjs`：`LOOP_005_two_detectors_are_independent_attempts` | MOVE+NEW | 各文件 `node --test` |
| DG-007 命中只停止当前物理 attempt、恰好一次 | `tests/loop-sensor.test.mjs`：`LOOP_006_owned_low_diversity_stream_aborts_exactly_once`、`LOOP_006_unowned_session_never_aborts`、`LOOP_006_clear_armed_allows_next_attempt_to_arm_again` | MOVE | `node --test requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-008 LoopKillArmed 进程内局部 | `tests/loop-sensor.test.mjs`：`LOOP_001_kill_arm_is_process_local_not_persisted` | MOVE | 同上 |
| DG-009 强杀桥接标准 recovery | `tests/loop-sensor.test.mjs`：`LOOP_006_armed_abort_bridges_to_fallback_advance_once`、`LOOP_008_budget_exhaustion_is_final_and_writes_the_exhausted_fact` | MOVE | 同上 |
| DG-009 桥接静态形状 | `tests/p0-recovery-join-bridge-shape.test.mjs`：`P0_RECOVERY_JOIN_GATE_*` | REUSE | `node --test requirements/degeneration-guard/tests/p0-recovery-join-bridge-shape.test.mjs` |
| DG-010 作用域与豁免 | `tests/loop-sensor.test.mjs`：`LOOP_007_unowned_and_armed_deltas_are_ignored`（非 Owned session / 已武装同 attempt 忽略）、`LOOP_006_unowned_session_never_aborts` | MOVE | 同上 sensor |
| DG-011 continuation 独立叶子 | `tests/loop-sensor.test.mjs`：`LOOP_006_continuation_text_is_the_english_loop_nudge` | MOVE | 同上 sensor |
| DG-012 detector 不是业务 truth / retry controller | `tests/loop-sensor.test.mjs`：`LOOP_008_loop_kill_advances_cursor_only_via_fallback_controller`（FallbackController 唯一推进路径、不直接改 Offset）；`requirements/context-compression/tests/ctx014.test.mjs`：loop-kill 只允许 `weighted_distinct_token_count` 等诊断字段 | MOVE+REUSE | 各文件 `node --test` |

### 包拥有的 semantic anchor id

`scripts/checks/semantic-anchors.mjs` 无本包语义 ID；本包为空清单。

### 独立变化边界

未来可替换 detector 算法与滴定常量，但 attempt-local、bounded、非权威、一次越阈、复用标准 recovery
五条边界（DG-005/006/007/009/012）不得削弱。算法变化必须同步更新 WHAT/HOW 与永久 calibration proof。
