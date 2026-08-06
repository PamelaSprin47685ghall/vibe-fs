# LOOP — 边界

## LOOP-002：传感器边界（ARCH-002 的唯一定点例外）

ARCH-002 要求碎片事件在最早边界丢弃，业务层只消费粗信号。本条款在该边界上设置一个纯传感器：

```text
Host events 流
  ├─ session.status / session.deleted / session.error
  │     → HostEventCodec → HostSignal → 既有业务（HOST-001…004）
  │
  └─ message.part.delta（field=text）
        → LoopSensor 仅提取新增字符
        → 更新 O(1) 检测器（滑动 4-gram）
        → 判定 is_loop=false：丢弃，不进业务
        → 判定 is_loop=true：发射 LoopKill 动作（见 LOOP-006）
```

不变量：

```text
1. 传感器不得写 Journal 业务事实（除 LOOP-006 规定的强杀路径副作用）
2. 传感器不得从 delta 推断 terminal / completion / tool 结果 / Authority
3. 传感器不得把原始 payload 交给 Reconciler 或 FallbackController
4. 业务层永远看不到 part.delta；只看到「某次 attempt 被强杀后的 reconcile 结局」
```

`LoopSensor` 是 transport 边沿上的观测器，不是第二套 Reconciler。

## LoopDetector 可变状态物理封闭（LOOP-011 证明约束）

`LoopDetector` 的内部数组与可变字段（`Step`、`PrefixLength`、`Value`、`Cross`）属于模块私有实现细节：

1. **禁止导出**：不得在 `shape/loop.md` 或 API 接口中暴露公开可变字段。
2. **生命周期隔离**：严格绑定到单次 `ProviderRunIdentity`，attempt 结束立即释放。
3. **并发安全护栏**：禁止跨线程引用或跨 attempt 复用；外部只能通过 `feed` 提交字符或读取只读快照。

---

## LOOP-009：事件选型与 Host 能力

优先订阅与粗信号同一条 transport（`events.listen` 或 `/global/event`，HOST-003）：

```text
isLoopTextDelta   → LoopSensor.push
isHostSignalEvent → HostEventCodec
else              → 丢弃
```

无法稳定提取 sessionId 或新增文本 → 丢弃该事件。不要求 Host 新增 Hook（ARCH-003）。

---
