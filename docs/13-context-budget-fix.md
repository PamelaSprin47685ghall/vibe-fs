# todowrite emergency 过度触发：审计与实现决策

> 本文记录预算修复的根因、决策和回归要求。实现事实以 `src/Kernel/ContextBudget.fs`、`src/Runtime/MessageTransform/`、`src/Runtime/Execution/` 与测试为准；本文中的“修改”清单是决策记录，不表示当前仍未实施。

## 已验证根因

- `F` 的代数移项没错；错在状态契约。推导要求 `P` 与 `R` 共享同一折叠周期原点，但文档与实现同时采用互斥语义：
  - `docs/13-context-budget.md` 声称“每次成功 todowrite 都是 phase boundary，重建 `P`”；
  - 同一文档的动态阈值与手动模拟又固定 `P`，令 `R=0→1→2`。
  - 若 todo 后真的开新 phase，则 phase-relative `R` 必回 0；若 `R` 要增长，则 `P` 不能随前两次 todo 重置。第一、二轮重复触发正是该矛盾的确定性结果。
- 生产链进一步混用了三套坐标：
  - `ContextBudgetPhase.rebuildPhaseState`：任意 `backlog <> LastBacklog` 都重建 `P`，并把 `NudgeTrack/NudgeCount/SignalTodoOrdinal` 清零；
  - `Kernel.ContextBudget.classifyPressure`：忽略 `phaseStartTodoOrdinal`，把投影后 completed todo 绝对数量 Clamp 为 `R`；
  - `NudgeInject`：phase reset 后同一轮立即重新分类；只要混合后的 `F` 仍真，必走新的 `injectFirstSignalNudge`。
- `P` 与 `a` 也不属于同一候选：真实 outbound 用 `FoldAfterSecond`，phase baseline 用 `FoldAfterFirst`；`ContextBudgetEngine` 传入的 encoder 还忽略 `stableMessages`，实际返回 `plan.RawArray`。文档宣称的 stable projection baseline 没有实现。
- `R` 不是文档定义的“未折叠 raw todo 数”。折叠后首个 todo ToolPart 仍作为 projection carrier 保留且状态仍为 Completed；当前数值主要靠 Clamp 偶然得到 2。
- “引入 `R` 后完美防范重复”本身也是伪命题。即使坐标修正，离散观测若一次 overshoot 多个阈值，更新进度后的 `F` 仍可为真。正确承诺只能是：先消费 todo acknowledgement、推进进度，再用同一 snapshot 分类一次；标准增量轨迹落回安全区，不重复提示；真正跨越新阈值时允许同一 emergency chain 执行 catch-up nudge 或 compact，不能靠静默一轮牺牲安全。

## OpenCode 宿主协作裁决

- OpenCode 在同一用户 prompt 内执行 `messages.transform → LLM → tool → messages.transform` 连续 provider step。成功 `todowrite` 后无需新用户消息，下一次 transform 立即发生；此时 backlog event、completed ToolPart、上一 step usage 均已提交，旧 synthetic nudge 因不落库而消失。故首两次 todo 后的重复 emergency 不是 DB race，而是当前 reset/reclassify 逻辑被宿主确定性放大。
- 正常单 session loop 中，`tool.execute.after`、completed ToolPart、step-finish usage 均在下一 transform 前 await/事务提交；实现与测试不得用“偶发不可见”解释主路径。仅崩溃恢复/未来重入可出现 backlog 已前进而 anchor 未可见，届时必须保持 cycle/signal不变。
- `experimental.chat.messages.transform` 的 input 固定为 `{}`；宿主随后继续使用局部 `msgs` 引用。仅原地修改数组有效。本项目 `replaceArrayInPlace` 当前正确，修复不得退化为替换 `output.messages`。
- usage observation 天生指向上一 completed provider step：
  - 只有它与上一 `PendingOutbound` 的 bytes/assistant ID 匹配时才能形成 calibration；`PendingOutbound=None` 时直接把旧 usage 当当前 candidate 是确定坐标错误。
  - OpenCode 把 prompt tokens 拆成互斥桶，当前上下文输入应为 `tokens.input + tokens.cache.read + tokens.cache.write`；现实现漏 `cache.write`。
  - 当前零 token assistant 与 compaction summary assistant 必须跳过，不能参与普通回合 calibration。
- OpenCode compaction 也调用同一个 messages transform，input 同为 `{}`，随后以 `tools={}` 发 summary LLM。当前 binding 无法识别该 invocation，会污染普通 `ContextBudgetEntry/PendingOutbound/calibration`，且可能向无工具 summary 请求注入强制 todowrite 文本。
- 模型窗口解析违反锁定 SDK 1.17.4：`session.get` 的 `Session` 无 model/limit；完整模型目录来自 `provider.list`，limit 为 `{context, output}`。当前路径常因错误假设/错误 flat RPC 参数退化到 8192，且 raw context 未扣宿主 output reserve。
- OpenCode TextPart 在 decode→encode 中被压成 `{type,text}`，丢失 `ignored/metadata/synthetic/id/...`；`ignored=true` 文本会被重新送入模型，直接改变真实 outbound 与预算。该 codec 边界必须与预算修复同批收敛。
- `tool.execute.before` replacement 也不被目标宿主消费，但当前核心变换已原地作用于原 args；它不造成本次重复 emergency，列为独立 P1 host-contract cleanup，避免混入预算状态机 diff。

## 设计决策：以真实投影周期取代模糊 phase

建立单一 `ProjectionBudgetCycle`，所有量来自同一份 `FoldAfterSecond` 输入快照：

- `baselineTokens`：最近一次真实投影前沿推进后的普通 outbound token 估算；
- `baselineTodoOrdinal`：该 baseline 对应的 completed todo 绝对序号；
- `foldFrontierOrdinal`：已折叠到的 todo 前沿，用于判断投影是否真的缩减；
- `completedSegments`：baseline 后已完成且尚未触发下一次折叠的工作段数；
- `remainingTodoWritesUntilFold`：从当前投影形态到下一次真实缩减仍需的 todo 数。

不再把 backlog 内容变化、todo acknowledgement、投影缩减、nudge retry 合并成一个 reset。

将触发式推广为两个独立进度量。令 `Q=completedSegments`、`M=remainingTodoWritesUntilFold`：

$$b_{eff}-a \ge M\frac{a-P}{Q+1}$$

触发条件：

$$\boxed{(Q+M+1)a \ge (Q+1)b_{eff}+MP}$$

首次 `FoldAfterSecond` 周期中，`Q=R`、`M=N-R`，退化为现有公式；真实折叠后，因消息已保留两个 raw anchors，下一次缩减只需一个 todo，应重建为 `Q=0,M=1`，而不是继续伪装成固定 `N=3,R=2`。

## 实施步骤

### 1. 让 backlog 投影产出预算元数据

修改 `src/Runtime/Execution/BacklogProjection.fs`：

- 将仅返回 `Message list` 的投影结果扩展为强类型结果，例如 `BacklogProjectionResult`：`Messages`、`TotalTodoOrdinal`、`FoldFrontierOrdinal`、`RemainingTodoWritesUntilFold`、`DidAdvanceFoldFrontier`。
- 元数据从投影前的 canonical messages 与同一次 fold range 计算；projection carrier 不算 raw anchor，预算层不得再次扫描投影后的 ToolPart 猜进度。
- 明确 `FoldAfterSecond` 的序列：0/1/2 anchors 时距首次缩减为 3/2/1；达到 3 后 fold frontier 前进；稳态每新增一个 anchor，frontier 再前进一次。

修改 `src/Runtime/MessageTransform/Plan.fs`、`Pipeline.fs`，把同一投影结果及元数据传给 ContextBudget；禁止 backlog revision 与 message ordinal 分别取样。

### 2. 收敛 Kernel 状态与公式

修改 `src/Kernel/ContextBudget.fs`：

- 用 `ProjectionBudgetCycle` 替换当前语义漂移的 `phaseBaseTokens/phaseStartTodoOrdinal` 组合；删除失效字段或令其成为 cycle 内不可分割字段。
- 将 `F/classifyPressure` 改为显式接收 `completedSegments` 与 `remainingTodoWritesUntilFold`，删除绝对 `completedTodoCount` Clamp。
- 保留 `MaxInputTokens <= 0`、80% compact guard、reserve 等现有安全边界，但所有 guard 与公式只读取同一个 decision snapshot。
- 用穷尽 ADT 表达 `NoPressure | RequireTodoWriteEmergency | RequireHostCompact`，避免注入层二次推导领域状态。

### 3. 分离四种 transition

修改 `src/Runtime/MessageTransform/ContextBudgetPhase.fs`、`ContextBudgetStore.fs`、`NudgeInject.fs`、`ContextBudgetEngine.fs`：

1. 冷启动：以当前 `withoutBudgetNudge` 的 canonical token 估算建立 cycle。
2. todo acknowledged：检测到 `TotalTodoOrdinal > SignalTodoOrdinal` 时，仅确认当前 signal、推进 `Q/M`；前两次 todo 不重建 `P`，不新建 episode，不把正常进度误判成全新首次 signal。
3. fold frontier advanced：仅此时重建 baseline/cycle；`P` 直接使用同一 `withoutBudgetNudge` 候选的 `currentTokens`，或使用真正编码该候选所得 bytes 经 calibration 估算。删除 `FoldAfterFirst` stable baseline 与忽略参数的 `plan.RawArray` encoder。
4. backlog 内容变化但没有新可见 todo anchor：只更新 backlog 事实，不改变 cycle、signal、retry；防止 event-log 与消息历史短暂不同步触发新 episode。

同时：

- `NudgeTrack` 携带 cycle/frontier、signal todo ordinal、retry count；todo acknowledgement 与 host stripping retry 分属不同 transition。
- 成功 todo 被首次观察时先完成 acknowledgement transition，再以更新后的 `Q/M` 分类一次：低于新阈值则不注入；仍越界则沿原 emergency chain 标记 `CatchUpAfterAcknowledgement`，不得 reset 成新的 first signal。
- pressure 只计算一次；同一结果同时驱动 store transition、synthetic message 与 `DecisionTrace`，删除 `checkAndInjectNudge` 后再次调用 `classifyPressure` 的分叉。
- `source = Synthetic "context-budget-nudge"` 继续采用替换语义，不无限追加。

### 4. 收敛 OpenCode 宿主边界

修改 `src/Hosts/OpenCode/MessageTransformHook.fs`、`MessageTransformPipeline.fs`、`OpenCodeModelResolution.fs`、`MessagingCodec.fs`，以及 `src/Runtime/Messaging/OpencodeContextBudgetObservation.fs`、`ContextBudgetEngine.fs`：

#### 4.1 将 tool continuation 建模为 acknowledgement，而非新 phase

- 以同一次 canonical message snapshot 中的 completed todowrite ordinal 与 projection metadata识别 `TodoAcknowledged`；正常 OpenCode 路径中 backlog 与 ToolPart 在下一 transform 前均已提交，不引入臆造 race 分支。
- acknowledgement transform 先消费旧 signal、推进 `Q/M`，再对同一 canonical snapshot 分类一次；标准轨迹不追加 nudge，真实 overshoot 则保留原 chain/cap 并显式进入 catch-up，禁止因 backlog reset 伪装成新 episode。
- `PendingOutbound`、signal、fold frontier 均携带 invocation/assistant/message identity，禁止把同一 assistant observation重复绑定到不同 outbound。

#### 4.2 修正 usage 配对

- `OpencodeContextBudgetObservation` 计算 `input + cache.read + cache.write`，不含 output/reasoning。
- 反向扫描时跳过当前 zero-token assistant，以及 `summary=true` 或 `agent/mode=compaction` 的 assistant。
- fresh observation 仅在存在匹配的 prior `PendingOutbound` 时产生 calibration；`PendingOutbound=None` 时标记该 assistant ID 已观察，但当前 candidate仍按已有 calibration/bytes bootstrap 估算，绝不把上一 step token 冒充当前值。
- 保留最后一份有效 calibration；暂时 API 不可用、重复 assistant ID、无 completed usage 不得触发 cycle/episode reset。

#### 4.3 隔离 compaction invocation

- 复用现有 `FallbackRuntimeStore/FallbackSessionRuntime` 作为唯一 session SSOT；在 compaction 字段组增加瞬时 `CompactionSummaryTransformPending: bool`，禁止另建 RuntimeScope marker map或复制 active compaction ID。
- 将 compaction start收敛为单个 aggregate transition：同时设置 owner/active identity/generation/阶段位与 pending=true；提供同步 `TryConsumeCompactionSummaryTransform(sessionID)` check+clear。
- messages transform先从原始 messages解析 sessionID，在任何 model-limit/usage/plan副作用前消费 pending；命中后局部分类为 `CompactionSummary`并直接 pass-through，保留宿主数组与 part引用，不进入 backlog projection、parallel/CAPS、ContextBudget或普通 transform store。
- `autocontinue`、`session.compacted`、settled/error与 session cleanup幂等 clear未消费 pending；event-log restore固定为 false；两 session聚合互不影响。
- A+B 双保险：bypass compaction transform，同时过滤 summary assistant usage；任一缺失都会污染下一普通 calibration。

#### 4.4 对齐模型窗口

- 从 canonical messages 的最后有效 user model ref 获取 `{providerID, modelID}`；调用锁定 SDK 形状 `provider.list({query:{directory}})` 获取完整 catalog，禁止从 `session.get().data.model.limit` 或 `config.provider` 猜目录。
- 对 SDK 1.17.4 `{context,output}` 计算宿主可用输入：`max(0, context - min(nonzero output, 32000))`；若兼容新版 `limit.input`，按宿主 reserved 规则扣减，不能返回 raw input/context。
- 合并 model key 与 limit 解析为一个强类型结果 `{ModelKey; UsableInputTokens; Source}`；修复所有 OpenCode flat `session.get({sessionID,directory})` 调用，source 精确区分 provider catalog、input-reserved、fallback-8192。
- 仅真正未知 provider/model 时使用 8192；已知模型不得因 RPC shape/错误数据源静默退化。

#### 4.5 保真 OpenCode TextPart

- 在 OpenCode FFI codec 持有 raw part，不把宿主字段塞入 host-agnostic Kernel 类型。
- 原文本未改时直接复用 raw TextPart 引用；文本变更时 shallow-copy raw，仅覆写 `text`；仅 synthetic/new part 才构造新对象。
- 保留 `ignored/metadata/synthetic/id/sessionID/messageID/time` 与未知扩展字段，确保预算 transform 不改变宿主 model projection。

### 5. 修正文档模型

重写 `docs/13-context-budget.md` 的以下部分：

- 将“phase”拆成 projection cycle、todo acknowledgement、fold frontier、compact 四个边界。
- 删除“每次 todo 都重建 `P`”与“固定 `P`、`R=0→1→2`”的冲突；删除管线段落残留的“继承旧 `P`”。
- 用 `Q/M` 推导替换模糊的绝对 `R`；说明原 `N=3` 只描述首次折叠距离，稳态下一次 fold 距离为 1。
- 修正 projection carrier/raw anchors 描述；不再声称第三次后 completed ToolPart 计数物理下降为 2。
- 将“完美防范重复”改为可验证保证，并加入 overshoot 例：acknowledgement 先推进进度；标准轨迹不重复提示；若同一 snapshot 已跨越更新后阈值，则沿原 chain 立即 catch-up，而不是重置为新的 first signal。
- 更新手动模拟，逐步列出 `P/Q/M/foldFrontier/NudgeTrack`，使每个状态转换与源码一一对应。
- 更新 OpenCode 章节：明确同一 prompt 内 tool continuation 时序、synthetic 不持久化、acknowledgement 必须先推进进度再分类、标准轨迹不 reinject而真实 overshoot可沿原 chain catch-up、usage 为上一 step且需与 pending配对。
- 将 `currentTokens` 改为 `input + cache.read + cache.write`；将 `maxInputTokens` 来源改为 message model ref → `provider.list` catalog → 宿主 usable input，删除 session model limit 与“无 provider.list 回退”的错误描述。
- 增加 compaction 隔离契约：summary invocation 不进入普通 transform/budget，summary usage 不参与 calibration。

### 6. 建立真实三轮回归链

修改/新增正式测试：

- `tests/ContextBudgetSpecs.fs`
  - 表驱动验证推广公式：首次周期 `(Q,M)=(0,3),(1,2),(2,1)`；fold 后 `(0,1)`。
  - 非零 ordinal/cycle origin；成熟历史不得被绝对 count Clamp 成固定进度。
  - 明确 overshoot 可在多个进度点保持 emergency，防止文档再次承诺不可能性质。
- `tests/ContextBudgetAfterTodoTests.fs`
  - 使用真实 completed `todowrite` ToolPart、真实 backlog 与真实 `applyBacklogProjection`，覆盖 `60000→65000→90000→95000→120000`。
  - 第一次、第二次 todo：断言 baseline/frontier 保持、`Q/M` 推进、同一 transform 无 reinject。
  - 第三次 todo：断言 fold frontier 恰好推进一次、baseline 使用真实 projected outbound 重建、cycle 转为稳态 `Q=0,M=1`。
  - 重写当前 `fiveConsecutiveTodos` 假绿用例：不得只改 `backlogRef`、不得复用无 tool parts 的 messages、不得丢弃 `_resTodo`。
- `tests/BacklogProjectionSpecs.fs`
  - 对 0..5 anchors 验证 projection metadata、carrier/raw 区分、frontier 与 remaining count。
- ContextBudget calibration/engine 测试
  - 构造 RawArray 很大、真实 projected candidate 很小的反例，断言 baseline 跟随后者，锁死 ignored-encoder bug。
  - 覆盖 backlog 已更新但 anchor 尚不可见时不 reset episode。
- `tests/ContextBudgetNoReinjectTests.fs`
  - 区分同一 signal 被宿主剥离后的 retry 与 todo acknowledgement 后的新工作段。
- 检查并注册当前未执行的 `ContextBudgetPipelineNudgeTests`；若与上述链重复则删除，保持单一可执行事实来源。
- OpenCode 集成测试记录每轮起始 call index，只检查本轮新增 LLM calls，禁止扫描全部历史误判 round2 注入。
- OpenCode tool-loop 时序测试
  - 复刻真实顺序：canonical reload → 当前 zero-token assistant 入库 → transform → provider → todowrite after/backlog commit → completed ToolPart → step-finish usage → 下一 transform。
  - `T0 emergency → T1 acknowledgement → T2 acknowledgement → T3 first fold`；T1/T2 请求体不得含 budget marker，后续普通 step 若仍越界才允许 catch-up。
  - 独立 overshoot 轨迹令 acknowledgement 后更新公式仍为 emergency；允许 T1/T2 立即 catch-up，但断言 Episode/chain/cap 不重置、action 不是新的 first signal。
  - todo 失败/拒权/非-todo tool 属 `NoProgressRetry`，与成功 acknowledgement 分开断言。
- `tests/ContextBudgetEstimateTests.fs` / `ContextBudgetRealApiSpecs.fs`
  - `input=1000, cache.read=500, cache.write=250` 必得 1750，不含 output。
  - current zero assistant被跳过；compaction summary被跳过；仅 summary 时返回 None。
  - `PendingOutbound=None + previous usage=120000` 不得把当前小 candidate 标为 Observed或触发 nudge；旧 assistant ID只消费一次。
- OpenCode model resolver 测试
  - hook input严格 `{}`，从 messages 获取 session/model；合法 SDK `Session` 不含 model/limit。
  - `provider.list` 返回 `{context=128000,output=16000}` 时 usable=112000；已知模型禁止退化8192；未知模型保留带 source 的8192。
  - 所有 RPC mock严格拒绝 flat `{sessionID,directory}` 参数。
- `tests/OpencodeCompactionTransformIsolationTests.fs`
  - compacting hook按 session arm一次；下一 transform pass-through且零 budget I/O/store mutation/nudge；第二次恢复 NormalTurn。
  - 两 session并发隔离，未消费 marker在失败/autocontinue/session cleanup后清除。
  - summary 后普通 calibration仍绑定 compaction 前的普通 pending/assistant，不绑定 summary bytes/usage。
- OpenCode codec测试
  - 含完整 TextPart字段的 native message decode→encode 无修改时 message/part引用恒等；改 text时仅 text变化。
  - `ignored=true` 大文本经过完整 budget transform后仍 ignored，metadata/synthetic/IDs/time不丢。
- 修正 `integration/opencode-plugin-contract-harness.js` 的 messages-transform characterization：caller保留局部数组并忽略 replacement，只认原地 mutation。`tool.execute.before` replacement契约另开P1，不混入本批实现。
- 重写 `e2e/opencode/specs/p0-canary-tests-advanced.js`/对应 behavior manifest：删除“todo result后立即出现nudge”的反向期望与 synthetic `done` 旁路；每轮用 provider-call watermark 检查新增请求。

## 验收标准

- 首次 fold 前，正常增量轨迹严格得到 40%→60%→80% 阈值；OpenCode 第一、第二次 acknowledgement 在未越过更新阈值时不注入。overshoot 仍可立即 catch-up，但必须沿原 chain/cap，不能重置为新 episode。
- `P/Q/M` 永远来自同一 projection cycle；不存在 `FoldAfterFirst` baseline + `FoldAfterSecond` outbound、cleaned ordinal + projected count 的混搭。
- 只有真实 fold frontier 前进才重建 baseline；单纯 backlog 文本变化不重置 emergency episode。
- fresh usage 只校准其对应的 prior normal `PendingOutbound`；无 pending、重复 assistant ID、zero assistant、compaction summary 均不能冒充当前 candidate。
- 已知 OpenCode provider/model 解析出与宿主一致的 usable input，不能静默落 8192；prompt usage 包含 cache.write。
- compaction transform 完全 pass-through，前后普通 ContextBudget store 逐字段不变，summary 请求无 todowrite emergency，summary usage 不进入普通 calibration。
- OpenCode 原生消息经过 transform 后保留数组引用与 TextPart 宿主字段；`ignored=true` 不得被重新送入模型。
- 高占用 overshoot 行为被测试和文档显式定义：acknowledgement 后更新公式仍不安全时可立即沿原 chain catch-up；后续普通回合再次越界亦可继续；达到 compact guard 时交宿主 compact。
- 不修改 `../opencode`；实现落在本仓库，binding 改动仅限必要宿主契约边界。
- 验证顺序：完成静态核对后运行 `npm run build-and-test`；随后运行 `npm run test:e2e:opencode:p0`，其真实启动 `opencode serve`。若失败，使用已构建产物与精确 selector 定位，修复后重跑失败入口及最终完整入口；全部通过前不得宣称完成。
