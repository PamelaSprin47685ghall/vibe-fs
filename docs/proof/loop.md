# LOOP — 证明

## LOOP-011：可测契约

第 1 层（纯函数）：

```text
新建检测器：state=NORMAL，N_eff≈256，HHI≈1/256
过滤后不足 4 有效字符：保持 NORMAL 先验
纯被忽略字符流（空白 / `-`）：不推进 step，保持 NORMAL 先验
单字符长循环：最终 LOOP
高多样性文本：保持 NORMAL
逐字符与批处理 N_eff 一致（浮点容差）
```

第 2–3 层：

```text
delta 序列触发后恰好一次 AbortSession
同一 attempt 重复 delta 不二次 abort
armed 后 recordConfirmedFailure 前进 cursor 一次；同 ProviderRun 去重
用户主动 abort（无 LoopKillArmed）不前进 cursor
```

禁止 sleep / repeat-until-pass 掩盖竞态（VERIFY-004）。

## 测试落点

| 层 | 位置 |
|----|------|
| unit 检测器 / 递推 | `tests/unit/domain/loop-*.test.mjs` |
| facade 导出 | `tests/unit/support/domain.mjs`（`loopDetector` / `loopSensor`） |
| 强杀后 Fallback 推进 | 与 `tests/unit/fallback/*`、host-turn reconcile 路径交叉 |
| 门禁纪律 | VERIFY-004（`docs/proof/verify.md`） |

新增阈值或忽略字符集必须改 LOOP-004 定义 + 上表 unit，禁止只改代码。

---

## 设计摘要

```text
流式字符
  → 丢弃 ' ' / '\t' / '\r' / '\n' / '-'
  → 重叠 4-gram
  → 慢衰减 3 指数核（半衰期 8/64/512）+ 正常代码先验（N_eff=256）
  → N_eff ≤ 140（HHI ≥ 1/140）
     // (N_good + N_bad) / 2 = (256 + 24) / 2
  → AbortSession + LoopKillArmed
  → Reconciler 产出 TurnAborted
  → TurnCompletionProgram 命中 LoopKillArmed 后桥接到 provider failure 路径
  → FallbackController 推进 AABB
  → ProviderRetryAttempt:
       "Continue from the interruption without repeating already produced content."
```

传感器在边沿，恢复在 AABB，预算在 Fallback，压缩在 CTX。四者边界不得粘连。
