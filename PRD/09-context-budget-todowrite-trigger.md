# 09 上下文预算与 todowrite 强制触发策略

## 1. 背景与目标

宿主 LLM 上下文上限 $N$ 固定（典型值 200000 tokens）。`todowrite` 调用能触发 backlog 投影压缩，但压缩存在一代延迟：

- `projectBacklogFor` 折叠边界是**倒数第二个**成功 `todowrite`（`secondToLast`）。
- 一次 `todowrite` 成功后，**上一阶段**（`T_{m-2}` 到 `T_{m-1}` 之间）的完整调试细节才允许压缩。
- 投影本身不会删除当前 backlog；`backlog` 投影随 `todowrite` 次数单调累加。

若等到上下文接近 $N$ 才触发，上一次 `todowrite` 后的未折叠细节仍占据活跃区，`todowrite` 自身再占一次空间，导致投影生效前窗口已先溢出。

compact 仅应作为"backlog 投影与不可压缩基线已大到无法有效腾挪工作"的 fallback，不应在首次触发时就发生。

本 PRD 定义从精确历史数据计算强制 `todowrite` 时刻的函数 $F$，无需预测未来 `todowrite` 大小、无需历史最大值近似、无需字符/token 经验换算。

## 2. 术语与符号

| 符号 | 含义 |
|------|------|
| $N$ / $b$ | LLM 最大输入上下文 token 数 (`maxInputTokens`) |
| $a$ | 当前请求发送前，已精确 token 化的完整上下文占用 (`currentTokens`) |
| $c$ | 本阶段起点时 backlog 的估算 token 占用 (`backlogTokensAtPhaseStart`) |
| $s$ | 本阶段起点时，非 backlog 的基础开销：`phaseBaseTokens - backlogTokensAtPhaseStart` |
| $u$ | 当前阶段自起点以来新增占用：$a - s - c$ |
| $P$ | 当前阶段起点总上下文占用：$s + c$ (`phaseBaseTokens`) |
| $H$ | 阶段可用空间：$b - P$ |

阶段起点：最近一次成功 `todowrite` 经投影完成后的第一个稳定 prompt；首次会话则为当前任务开始点。

## 3. 核心约束

1. 不引入任何未来量：不预测下一次 `todowrite` 大小，不使用历史最大值。
2. 所有输入必须是已发生且可精确 token 化的数据，或基于历史最近已知比例进行推算。
3. 允许使用比率估算作为 Fallback 机制：若当前请求无法直接获取 token 计数，则允许通过公式 `(last token count / last text bytes) * (current text bytes) = estimated token count` 进行比率估计，其中比例由历史最近一次成功测量的值动态提供。
4. 当阶段起点基线占总空间的 80% 以上时（即 `phaseBaseTokens >= (maxInputTokens * 8) / 10`），视为无空间继续折叠，必须回落到系统级的 compact fallback。
5. 所有判定计算和比率估算在内核中使用 `int64` 执行以防止大上下文下的整数乘法溢出。

## 4. 数学推导

当前阶段可用总空间：

$$H = b - P = b - s - c$$

当前阶段已新增工作占用：

$$u = a - s - c$$

由于 `todowrite` 触发后，投影压缩不会立即删除当前阶段新增内容（需要下一次 `todowrite` 才能释放），因此当前阶段只能消费一半可用空间，另一半预留给：

- 强制 `todowrite` 的 assistant 消息与 tool result；
- 强制 `todowrite` 之后可能继续生成的新 assistant 消息与工具输出，直到 `todowrite` 成功。

安全规则：

$$u \le \frac{H}{2}$$

代入：

$$a - s - c \le \frac{b - s - c}{2}$$

化简：

$$2a \le b + s + c$$

触发条件为边界反面：

$$F(a, b, c, s) = 2a \ge b + s + c$$

等价形式：

$$F(a, b, c, s) = 2(a - s - c) \ge b - s - c$$

## 5. 算法伪代码

### 5.1 类型

```
type ContextState = {
    phaseBaseTokens: int64
    backlogTokensAtPhaseStart: int64
}
```

### 5.2 阶段开始

```
function beginPhase(
    totalTokens: int64,
    totalBytes: int64,
    backlogBytes: int64
) -> ContextState:

    backlogTokens = 
        if totalBytes <= 0 then 0
        else (totalTokens * backlogBytes) / totalBytes

    return ContextState(
        phaseBaseTokens = totalTokens,
        backlogTokensAtPhaseStart = backlogTokens
    )
```

### 5.3 强制判定函数

```
function F(
    a: int64, 
    b: int64, 
    c: int64, 
    s: int64
) -> bool:
    return 2 * a >= b + s + c
```

### 5.4 极限收缩拦截函数

```
function isCompactingRequired(
    phaseBaseTokens: int64,
    maxInputTokens: int64
) -> bool:
    return phaseBaseTokens >= (maxInputTokens * 8) / 10
```

### 5.5 成功 todowrite 后重新测量

```
function afterSuccessfulTodo(
    totalTokens: int64,
    totalBytes: int64,
    backlogBytes: int64
) -> ContextState:
    return beginPhase(totalTokens, totalBytes, backlogBytes)
```

## 6. 调用位置

| 位置 | 行为 |
|------|------|
| `MessageTransformPipeline.fs` | 交付 LLM 前判定 `F` 与 `isCompactingRequired`；符合条件则追加 nudge 并将 `NudgeInjected` 置为 true |
| `ContextBudgetStore.fs` | 每次成功 `todowrite` 后调用 `afterSuccessfulTodo` 更新并在 `LastBacklog` 改变时重置 `NudgeInjected` |
| 宿主入口点 | `ContextState` 按 session 存于 `RuntimeScope`；重启后通过事件重放重新 fold 状态 |

## 7. 数值示例

### 示例 1：首次阶段

```
b = 200000
s = 20000
c = 10000
phaseBase = 30000
freeAtPhaseStart = 170000

2 * a >= 200000 + 20000 + 10000
a >= 115000
```

触发时尚有 85000 tokens 余量，避免首次触发就 compact。

### 示例 2：后续阶段

```
s' = 25000
c' = 30000
phaseBase' = 55000

2 * a >= 200000 + 25000 + 30000
a >= 127500
```

阶段预算从 85000 降至 72500，但不会拖到 200000。

### 示例 3：backlog 趋于饱和

```
s = 40000
c = 120000
phaseBase = 160000

freeAtPhaseStart = 40000
a = 180000 触发
```

几乎每次请求都触发 `todowrite`。若投影后仍无法降低 `phaseBase`，宿主 compact 作为 fallback 介入。

## 8. 与事件溯源集成

`ContextState` 不是 durable truth 源；由 `Wanxiangshu.ndjson` 中 `work_backlog_committed` 事件经 `foldWorkBacklogSnapshot` 和 `projectBacklogFor` 重新派生。

重启后：

1. 重放 NDJSON 得到当前 backlog 列表。
2. 用 `projectBacklogFor` 投影得到 `projectedPrompt`。
3. 调用 `beginPhase` 重建 `ContextState`。

不持久化 `ContextState` 本身。

## 9. 验证清单

1. 单元测试：给定 $a, b, c, s$ 四元组，验证边界触发。
2. 集成测试：连续 5 次 `todowrite`，每次更新 `ContextState`，确保触发点 $a < b$。
3. 反例测试：
   - $a=190000, b=200000, c=10000, s=20000$：必须触发。
   - $a=110000, b=200000, c=10000, s=20000$：不能触发。
4. 整数安全：乘法使用 `int64` 或语言等价无溢出整数。

## 10. 边界条件

| 条件 | 行为 |
|------|------|
| `phaseBaseTokens >= maxInputTokens` | 阶段起点已无法容纳。直接走 compact；`F` 可返回 true 或单独路径。 |
| `a < phaseBaseTokens` | 输入非法；断言失败。 |
| `s = 0` 且 `c = 0` | 首次会话，backlog 为空。`phaseBaseTokens` 由不含 backlog 的 prompt 计算。 |
| `b` 动态变化 | 重新计算 `freeAtPhaseStart`；`ContextState` 不缓存 `b`。 |

## 11. 拒绝项

以下方案不采用：

- `a + c >= b` 或 `b - a <= c`：首轮触发太晚，易导致 compact。
- 历史 `todowrite` 最大值：不可预测未来，且不能修复一代延迟。
- 固定硬编码的 token/字符比例（例如硬编码 1 token = 4 字符）：但允许使用基于历史最近一次实际测量的动态比例。
- 字符数直接替代 token 数：但允许使用基于历史比例的估算。
- 在 `F` 内部预测下一次 `todowrite` 大小：违背"只消费已发生数据"约束。

## 12. 一句话总结

> 以阶段起点为锚，将当前阶段新增占用限制在阶段自由空间的一半以内；当 $2a \ge b + s + c$ 时强制 `todowrite`，保证投影生效前仍有足够余量，不使 compact 成为默认触发路径。
