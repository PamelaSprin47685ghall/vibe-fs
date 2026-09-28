# context-compression — WHAT

## [001] 不观察容量

不得读取、推导或缓存模型上下文容量，不设余量、Token 计数、容量表或字节换算。仅允许计量 200 KiB 输入合同及合法的文件、进程用量。

## [002] 不主动预测溢出

恢复只由真实 provider attempt 失败触发，不按长度、比例、余量或累计 Token 预测压缩时机。

## [003] 200 KiB 输入合同

单次 delta 的渲染上限为 200 KiB，超限须确定性切块、截断。该上限是固定输入合同，不估算窗口、不动态调参、不触发恢复。

## [004] 输出预算属 provider

不计算 squash 的 Token 预算或压缩比例。压缩输出只校验非空、非纯 XML。

## [005] 失败不分类

控制流只消费 typed Outcome（Completed、Failed、Aborted），不按错误正文或异常类型细分溢出、网络、限流等原因。

## [006] 重试材料当次决策，失败预算有界

每次物理重试使用新身份，按当前已提交事实一次性决定恢复材料，不依赖历史位置、奇偶或跨回调临时状态。X 获准 probe 后实际选择候选，返回候选或明确无候选原因；Y 按 typed request 与 durable frames 决定 squash。无材料则发送普通主请求，不等待未来 X 材料或把决定留给无关请求。

每次确认失败记账并推进统一的连续失败预算；耗尽停止自动重试，只有有效 WorkMain/BloggerMain 成功清零。

## [007] 按 RequestKind 分派结局

RequestKind 只取自 attempt 的 typed context、durable receipt 或已承接的 continuation 证据，不按角色或错误正文猜测。同种请求的同种结局确定性分派；BloggerSquash、InteractionRepair 成功不清零连续失败计数。

## [008] X 不发压缩请求

主工作会话不得要求主模型压缩历史。压缩仅通过 Y squash 或 X prefix 替换进行；只有 WorkMain 可携带 prefix probe。

## [009] 候选未提交不是事实

候选前缀只属于当前 attempt 的执行配置，不修改已提交 epoch。失败即丢弃，不产生 rebase 事实，不需要回滚。

## [010] 候选选择严格新于已提交 epoch

候选须通过 coverage 证明且严格新于已提交前缀：cutoff 不回退、候选不可与已提交前缀无区别。无候选时发送普通请求，不构造空 probe。

## [011] 提交语义分型

X probe 的物理 attempt 一旦可用成功（包括 tool-calls），须先原子提交新 epoch 并继承 SealRoot，才允许组装下一请求；失败或不可用回应不产生 rebase。durable append 成功后才消费 probe plan，失败须 fail closed。后续普通 WorkMain 持续投影已提交前缀；不允许新 probe 不等于退回 raw X。

Y squash 成功才提交新 observation 并递增 FrameEpoch；失败不改 frames 或 coverage。仅一个 frame 时仍可真实重写，新正文及 digest 不要求等于旧值。

## [012] Blogger delta TOML 合同

delta 以 data-only TOML 冻结于 blob，指令头仅在投影时注入。渲染遵守 200 KiB 上限与确定性切块，包含决策相关的可见推理，与 LWR gap 分立投影。

## [013] 诊断不是控制输入

诊断日志及字段不得驱动 retry、probe 或 squash 的决策。

## [014] squash 只处理本 X 的 frames

Squash 只处理当前 X 的 frames，不混入父级 LWR 或跨会话上下文。

## [015] busy/失败不推进 coverage

busy、失败、空或纯 XML 结果不推进 RecordCoverage。只有 BlogObservationCommitted 原子发布 frame 并推进 RecordCoverage。

## [016] Y prefix 只物化完整 turn

Y prefix 只使用有 PrefixCoverage 完整 turn 证明的 Y 产物，不使用 RawGap；覆盖截止点只在完整 Host turn 边界推进。

## [017] 只有真实 Opening 构成不可压缩 floor

真实 Opening 永久保留 raw，在 compaction/recovery 中不丢失，不交给 Y 改写。same-session FrozenRecordPrefix 排除 Opening；canonical WorkRecord 仍保存该事实，写回须按 XTrace stable Host identity 保留原消息，不能复制进 Y 后删除。

压缩 floor 仅为真实 Opening 后的首个 XTrace 位置，不随 Manager 阶段或动态 head 扩张；Blogger 起点取 RecordCoverage 与该 floor 的较大者。同会话前缀替换不包装成 delegation 材料。

## [018] Blogger 连续追平，quiet 只等待事件

一次唤醒可连续消费多个不超过 200 KiB 的块。每次提交后重读最新 coverage 与 XTrace Current，不冻结追赶终点。暂无材料只表示暂时追平；本次执行存活期间等待 MaterialAvailable(typed context) 或 Cancelled，无 deadline、timeout 或等待寿命。

durable open producer 在两个物理 step 间暂无 flight 时，新材料仍投给同一 producer；关联 Blogger 已有 active authority profile 时，后续材料使用该 profile 的 typed continuation。均不得再发第二个 Authority Root；进程死亡后不自动恢复旧 continuation。

## [019] X→Y 后旧辅助注入不跨 horizon 保留

ContextReanchored 与成功 PrefixRebaseCommitted 都退休旧 horizon 的辅助注入可见性，保留审计历史，只在后续正常触发时重新生成。rebase 的退休与 epoch 提升在同一投影事务内完成，保留的 raw 回合不能夹带旧辅助呈现。

当前 WorkMain 选择 probe 后，即按 typed tentative cold horizon 处理本次呈现，后续历史辅助投影器跳过注入，避免候选因旧材料膨胀而无法成功。该状态仅作为当前调用的返回值顺序传递，不写跨回调临时标记；成功提交才成为跨请求 durable horizon。

## [020] 待办历史不另设永久豁免

待办与画板回合的保留遵循 017、028、029 的 Opening、阶段窗口和覆盖边界，不按旧 `todowrite` 工具名永久保留全部历史。成功画板结果的退休还须满足 cognitive-workspace-009 的当前快照可见性证明。

## [021] Y retry 由失败会话当场拥有

BloggerMain 失败且策略选中 squash 时，下次 continuation 先发送 typed BloggerSquash，不先重发 Main、不等待未来 X transform。squash 成功后从 durable Blog 与 XTrace 重建 Main；失败则记账并按策略继续。任何 retry 前关闭失败请求的 durable open materialization，新物理请求绑定自己的 PromptKey。

## [022] Blogger Main 共用一个重建规则

正常追平、squash 后 Main、Main retry 与 crash refresh 共用同一重建规则：Opening floor、XTrace generation、ingest 位置、200 KiB chunk 及 coverage digest 一致。输入只来自 canonical XTrace projection，不来自请求级呈现；协调与恢复不得复制另一套推导。

## [023] retry/park 全事件驱动且时间无关

压缩与重试的正确性路径不读 wall clock、不安排 timer/deadline/delay，不用超时转移状态。park 返回 typed event，不能返回布尔后另取材料。

WorkMain 等待正在生产的 Y 时，仅 durable open BloggerRequestMaterialized 可证明 producer 存在。订阅已提交变化，每次重读投影，直到 coverage 严格更新或该请求 commit/abandon；live flight、staged offer 和时间窗口都不是该证明。

## [024] materialization 串行，flight 不覆盖异主

同一 BloggerSession 的 materialize、PromptKey bind、abandon 跨插件实例共享进程内串行准入，取得准入后重读 canonical journal 再决策。准入只保护命令临界区，不是 durable producer 证据。

flight claim 原子执行：无 owner 可认领，同 RequestId 可刷新，异 RequestId 返回 typed conflict 并保留原 owner。正常启动、retry、crash recovery 共用此规则。

load 时，上一 runtime 遗留且本进程无同 RequestId live flight 的 open request，须结算为 BloggerRequestAbandoned，reason 为 `stale-open-at-load`，遵循 crash-reconciliation-020。

terminal/idle callback 在 commit、repair 或 refresh 前，须以 assistant parentID 对应的 physical prompt durable evidence 证明自己属于当前 open RequestId。已被接替的回调只能 no-op，不消耗新请求预算、不 abandon/release 新 owner，也不借 Main refresh 重新认领 flight。

## [025] fatal 保留 exact settlement，经注入执行

flight 冲突、semantic cut 与压缩不变量故障须携带确切 BloggerSession、RequestId 和 durable settlement 形成 typed incident；被接替的回调无权 fatal。runtime 使用构造时必需注入的 fatal capability，不直接引用物理实现。同一 incident 只报告、终止一次，fatal 不改 flight 或 durable projection。

协议修复预算耗尽只终止所属 attempt：消费已结算结果并停止物理运行，不继续发原消息、不杀其它会话。缺失 provider identity 的外部终态只关闭确切请求并停止 attempt，不编造 run。需要放弃请求的不变量分支先 durable abandon、exact release，再熔断。

## [026] 修复结算失败共享收束

repair episode 的 durable abandon 失败后，所有已接收未完成、已排队及后续新旧观察者共享同一失败。不通知成功终态、不释放 exact flight、不重开预算；保留失败 episode 注册，防止同请求重新启动。

## [027] owner 构造请求，恢复先解码再验证

Blogger Main/Squash 请求对外只读，由 owner 的同一验证构造器创建完整值或返回 typed rejection；live 派生与恢复都不得绕过。Main 的 ingest 严格推进、DeltaDigest 等于 TOML 的 SHA256；epoch 合法；Squash 的覆盖 frame 数至少为一且等于 digest 列表长度。

RequestId 在 materialization 时按 canonical hash 计算并冻结，构造与恢复只携带，不从 blob 重算。恢复先解码再验证，分别报告 blob 不可读、损坏、请求种类不支持、条目无法解码、违反不变量，不合并成 None 或零默认值。旧请求保留冻结的 epoch，提交仍核对 staged coverage 与 live projection，不因恢复取得当前提交权。

## [028] K 窗口公式与同回合多提交

设成功提交按顺序为 `A1..AN`，`Bi` 为包含 `Ai` 的完整 semantic turn 起始边界（canonical XTrace generation + stable Host identity）。`K` 在 owner 开启时冻结，必须为正整数，默认 2，含当前活跃阶段在内。

```text
N = 0：不给新 cutoff，沿用当前已提交前缀与原始尾部。
N > 0：j = max(1, N − K + 1)
desired cutoff exclusive = Bj
```

保留调用所属整回合，绝不在 call/result 中间切断。`A1` 之前的探索属于前导历史 P0；第一份阶段快照出现后，它可在 coverage 充分时被折叠，真实 Opening 仍永久保留。同一 semantic turn 内多个顺序提交的阶段序号各自增加，但物理边界可以相等；不得删除半个回合，也不得因两次调用共享边界而拒绝合法调用。

## [029] coverage 落后不丢 raw，frame 不跨界冒用，紧急 Probe 是明示例外

actual cutoff 须满足当前 generation、完整 semantic turn、连续 PrefixCoverage、可精确截断的已冻结 Blogger/LWR 材料及 Opening floor；不得回退已提交 cutoff，或越过当前请求正在回答的最新语义回合。请求边界取 canonical XTrace 当前 generation 的最新语义回合，不取最后一条 `role=user` 消息。

Blogger 落后时仍可提交画板、投影 todos、退休有新载体的成功画板结果；未覆盖的普通历史原样保留，RawGap 不算前缀覆盖。frame 跨过 desired 边界时，须有可验证的完整材料子集，否则不前移。

真实 WorkMain 失败后，Probe 可凭完整 coverage 与合法语义边界越过正常 K 窗口，但必须明确记录为 Probe 冷边界；这不属于常规窗口策略。
