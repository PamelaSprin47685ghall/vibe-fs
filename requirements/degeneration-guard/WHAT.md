# degeneration-guard — WHAT

## DG-001: 双侧退化与非目标

检测目标为流式输出的两种异常：加权相异度低于仓库正常语料双侧经验分位数极小值时为 `TooRepetitive`，高于极大值时为 `TooRandom`。非目标包括上下文窗口估算、按错误文本分类失败、AABB/fallback 重试、角色/语言动态阈值，以及把 delta 内容拼装为 durable 业务事实。

## DG-002: 传感器只观察文本流

LoopSensor 仅从 Host 流式事件提取 assistant `text` 与 reasoning/thinking 类 delta。传感器严禁向 Journal 写业务事实，严禁从 delta 推断终态、权限或 fallback 状态。

## DG-003: 判定指标与正常包络

观测单位由 `gpt-tokenizer/o200k_base` 定义。对 token 流维护指数衰减加权相异计数 $D_t$。构建产物提供仓库语料实测经验分位数 `MIN_WEIGHTED_DISTINCT`（低侧 97.5% 置信度，下侧分位数 $p=0.025$）与 `MAX_WEIGHTED_DISTINCT`（高侧 100% 经验最大值，上侧分位数 $p=1.0$），中央正常覆盖率为 $0.975$：

- $D_t < MIN$ → `TooRepetitive`；
- $D_t > MAX$ → `TooRandom`；
- 其余 → `Normal`。

边界值本身属于正常语料；判定必须使用严格不等式。

## DG-004: 固定时间尺度 + Repository SSOT 即用即派生

Detector 的时间尺度固定为 `256` 个 `o200k_base` token；half-life 不由源码换行、formatter 或文件布局推导。Repository 本身是正常包络的唯一 SSOT。每次 build 的 selector 只返回 Git-tracked filesystem paths；generator boundary拒绝root外路径并将root内路径规范为canonical repository-relative identity。generator、build invocation、selector及每个selector output都必须绑定同一 staged input，且每个输入raw bytes只能经同一tracking reader取得。strict UTF-8与generated marker判定发生在tracked read之后，禁止selector内部或generator下游直接`readFileSync`。corpus采用正向source/document类型allowlist，机器生成物、vendor/dependency、fixture/golden与JSON/JSONL/CSV等结构化数据不得因可UTF-8 decode而进入语料。入选文本按repository path顺序连接成一条连续文本流。多线程编码在安全换行边界（`\n`后紧跟可打印非`/` ASCII字符）分块，分块结果与单流全量编码完全位等价。以 $D_0=X$ 做一次仿射 replay，令 normal prior 为 $X = mean(D_t(X))$ 的唯一自洽解，再以前向投影序列 $D_t(X)=\lambda^t X + b_t$ 的经验分位数（$p=0.025$ 与 $p=1.0$）计算 `minimum` 与 `maximum`；不得用任意seed预热后再二次replay。生成JS仅是编译后runtime import所需的临时artifact，不是配置源，也不得把`normal/min/max`数值复制回tracked源码。它必须由唯一generated artifact row绑定stable identity、output digest、selected-input digest、generator/build/selector lineage、package import target及完整JavaScript traversal；determinism不能消除artifact实际携带的authority。min/max不设数值快照测试。禁止Beta拟合、连续分布拟合、运行期动态分位数及其它概率外推。

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

## DG-013: diagnostic capability 非权威且失败不干扰guard

LoopSensor只接受构造时必填的窄`emitDiagnostic: string -> (string * string) list -> unit` capability，不得引用Host diagnostics implementation。diagnostic调用抛错时，arm、exact-once interrupt、cause consume与continuation结果必须与成功诊断完全相同；诊断不得获得fallback、journal、process-fatal或attempt-control authority。
