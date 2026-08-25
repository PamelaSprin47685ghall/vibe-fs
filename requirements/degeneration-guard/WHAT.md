# degeneration-guard — WHAT

## DG-001: 双侧退化与非目标

检测目标为流式输出的两种异常：加权相异度低于仓库正常语料极小值时为 `TooRepetitive`，高于仓库正常语料极大值时为 `TooRandom`。非目标包括上下文窗口估算、按错误文本分类失败、AABB/fallback 重试、角色/语言动态阈值，以及把 delta 内容拼装为 durable 业务事实。

## DG-002: 传感器只观察文本流

LoopSensor 仅从 Host 流式事件提取 assistant `text` 与 reasoning/thinking 类 delta。传感器严禁向 Journal 写业务事实，严禁从 delta 推断终态、权限或 fallback 状态。

## DG-003: 判定指标与正常包络

观测单位由 `gpt-tokenizer/o200k_base` 定义。对 token 流维护指数衰减加权相异计数 $D_t$。构建产物提供仓库语料实测 `MIN_WEIGHTED_DISTINCT` 与 `MAX_WEIGHTED_DISTINCT`：

- $D_t < MIN$ → `TooRepetitive`；
- $D_t > MAX$ → `TooRandom`；
- 其余 → `Normal`。

边界值本身属于正常语料；判定必须使用严格不等式。

## DG-004: 固定时间尺度 + Repository SSOT 即用即派生

Detector 的时间尺度固定为 `256` 个 `o200k_base` token；half-life 不由源码换行、formatter 或文件布局推导。Repository 本身是正常包络的唯一 SSOT。每次 build 只读取 Git tracked、strict UTF-8、正常人工可读的 source/document text；corpus 采用正向 source/document 类型 allowlist，机器生成物、vendor/dependency、fixture/golden 与 JSON/JSONL/CSV 等结构化数据不得因可 UTF-8 decode 而进入语料。入选文本按 repository path 顺序连接成一条连续 `o200k_base` token 流。以 $D_0=X$ 做一次仿射 replay，令 normal prior 为 $X = mean(D_t(X))$ 的唯一自洽解，再以该解计算语料实际 $D_t$ 极小/极大值；不得用任意 seed 预热后再二次 replay。生成 JS 仅是编译后 runtime import 所需的临时 artifact，不是配置源，也不得把 `normal/min/max` 数值复制回 tracked 源码。min/max 不设数值快照测试。禁止 Beta 拟合、方差/标准差阈值、置信区间、分位数阈值及其它概率外推。

## DG-005: O(1) 更新与有界内存

每个 token 仅查询并更新该 token 的最近出现步数，更新时间为 $O(1)$。状态只保存有限词表中已见 token 的最近步数映射，内存严格有界，不随输出长度增长。

## DG-006: detector 生命周期绑定单次 ProviderRun

每个 provider attempt 创建独立 fresh detector。Attempt 结束、guard 中断或 session 销毁时丢弃，严禁跨 attempt 复用。

## DG-007: 命中只中断当前物理 attempt

检测命中 `TooRepetitive` 或 `TooRandom` 且会话为具有 physical parent 的 managed sub-session 时，guard 原子记录该次进程内异常类型并调用 Host `InterruptAttempt`。同一 attempt 只允许一次 interrupt；abort 返回前不得发送 continuation。

## DG-008: armed 异常是进程内局部事实

Armed anomaly 仅存在当前进程内存，不写 Journal；崩溃重启后安全丢失。它只用于把随后 reconciled `TurnAborted` 与本次 guard interruption 对齐。

## DG-009: guard 自己拥有 TurnAborted 后的接续

reconcile 消费匹配的 armed anomaly 时，LoopSensor 自己在该既有时点发送 exactly-one continuation，并返回 typed `DegenerationGuard` abort cause。下游 turn/fission workflow 对该 cause 只能 yield/no-op，禁止再调用 nudge、repair、`FallbackController.recordConfirmedFailure` 或任何 AABB 路径。发送失败只作为 guard continuation failure 诊断，不改道 fallback。

## DG-010: 作用域与豁免

仅对插件 Owned 且具有 physical parent 的 managed 会话进行检测。非 Owned 会话、user-facing root、compaction 运行及非 managed 内部运行一律豁免。

## DG-011: 两类异常拥有独立接续语义

Guard continuation 使用专用 `DegenerationGuard` continuation authority，不冒充 `ProviderRetryAttempt`：

- `TooRepetitive`：`你的输出重复字符太多，建议更换表述方式。`
- `TooRandom`：`你的输出重复字符太少，不符合正常语料模式，建议更换表述方式。`

Provider language resource 可翻译措辞，但两类语义必须保持上述区别。

## DG-012: degeneration-guard 是闭合 owner

本包唯一职责闭环为 `observe → classify → interrupt → reconcile-own-cause → continue`。它不修改 fallback Offset/失败预算，不发送普通 interaction repair，不借用 AABB retry prompt；其它模块也不得为 `DegenerationGuard` 再实现第二条 recovery。
