我把附件里的仓库结构、Kolmogorov 原文、现有架构门禁、F# 编译顺序，以及超大/超小文件做了一轮盘点。先给结论：

**这次不要做“按行数切文件”。要做“按知识主权重新装箱”，顺带把文件尺寸压回人能一眼掌握的范围。**

仓库自己的规则已经把这一点说得很清楚：文件数百行、函数数十行往往意味着样板、框架礼仪或错误抽象开始吞掉本质复杂度；但“压缩不是合并”，独立生命周期的概念即使代码相似也应该分居。 `god-module` 规则更明确：**Size is a symptom; mixed reasons to change are the disease**，不能因为大就切，而应按 invariant / lifecycle / side-effect boundary 分主权。

下面这份可以直接当施工手册。

---

# 一、先定这次重构的判定标准

我建议把 Kolmogorov 文件治理落成下面这套**工程阈值**。注意：这是我根据仓库现有规则给出的施工标准，不是说仓库正式规范已经全局规定“200 行”。

仓库目前 E2E support 已经多次明确采用 **200 行文件预算**，并有 50 行、60 行函数预算；个别 server 文件有 300 行例外。   但当前 `architecture.mjs` 只检查 source root、`.fsproj` 完整性和依赖方向等，并**没有全仓行数门禁**。

所以不要一上来加：

> 所有文件必须 ≤200 行，否则 CI 红。

这会逼出最坏的“为了数字拆文件”。

建议使用以下判定：

| 尺寸        | 处理                            |
| --------- | ----------------------------- |
| ≤15 行     | 强制人工复核：删除？并回 owner？还是合法 seam？ |
| 16–30 行   | 复核，不自动合并                      |
| 30–80 行   | 完全正常                          |
| 80–200 行  | 理想区                           |
| 200–300 行 | 黄色：检查是否存在第二理由变化               |
| >300 行    | 必须给出“为何仍属于一个 owner”的证明，否则拆    |
| >500 行    | 默认进入治理清单                      |
| >1000 行   | P0，除非是生成数据/纯声明表               |
| 函数 >50 行  | 复核                            |
| 函数 >60 行  | 默认拆成规则/步骤，除非一个不可分割算法          |

**没有最小文件行数门禁。**

这是极重要的一点。`Ports.fs`、某个 DU、一个 public package entry、compile-order seam，完全可能只有十几行，而且非常健康。

---

# 二、当前仓库到底有多严重

我直接对 Repomix 中每个 `<file>` 内容做了计行统计。

生产 F#：

* 351 个 `.fs`
* 中位数约 **118 行**
* P75 约 **232 行**
* **105 个 >200 行**
* **58 个 >300 行**
* **16 个 >500 行**
* 最大 **1988 行**

测试 JS/MJS：

* 354 个文件
* 中位数约 **171 行**
* **158 个 >200 行**
* **80 个 >300 行**
* 最大文件达到 **6181 行**

所以你的判断是对的，但问题不是“整个仓库都烂”。实际上**中位数相当正常，主要是长尾特别肥，同时又存在少量 7～25 行的历史残片和兼容 seam。**

真正应该集中火力的是长尾。

## 生产代码最需要处理的一批

| 文件                                            | 当前约行数 | 建议                        |
| --------------------------------------------- | ----: | ------------------------- |
| `Session/EnforcerHost.fs`                     |  1988 | P0 拆                      |
| `Domain/JsTools.fs`                           |  1149 | P0 拆，且风险较低                |
| `Journal/Fold.fs`                             |  1139 | P0 按 fact family 拆        |
| `Kernel/Fact.fs`                              |   928 | **先别急着拆**                 |
| `Domain/ProjectionAlgebra.fs`                 |   890 | P0 拆                      |
| `Plugin/SpikePlugin.fs`                       |   884 | P1，瘦身 composition root    |
| `Codec/Projection.fs`                         |   699 | P1 拆 decoder/capture/edit |
| `Infrastructure/JsToolsFs.fs`                 |   633 | P1 按技术 invariant 拆        |
| `Session/HostForkRuntime.fs`                  |   630 | P1                        |
| `Host/ManagerNarrativeTransform.fs`           |   613 | P1                        |
| `Session/SyncDelegateRuntime.fs`              |   600 | P1                        |
| `Domain/PromptAuthority.fs`                   |   565 | P2，高协议敏感                  |
| `Application/Reconciliation/XTraceCapture.fs` |   533 | P2                        |
| `Orchestration/Host.fs`                       |   531 | P2                        |
| `Host/PluginRuntimeScope.fs`                  |   530 | **P0，主权混杂很明显**            |

下面逐个说怎么动。

---

# 三、P0-1：先拆 `EnforcerHost.fs`

这是第一优先级。

文件开头自己已经声明，它处理 continuation transform、blog canonicalization、commit、parking、fresh material resume、provider projection 等一整条链。

1988 行不是最大的毛病。毛病是这里同时存在：

* Tip guidance
* Blog tool-call decode
* Cycle validation
* Commit
* Squash
* Journal frame loading
* Recovery
* Repair
* XTrace/context reconstruction
* Continuation orchestration
* parking outcome

这些不是一个函数的内部步骤，而是**不同知识所有者**。

## 建议最终结构

```text
Session/
  EnforcerContinuation.fs
  EnforcerTipGuidance.fs
  EnforcerCycleDecode.fs
  EnforcerCycleCommit.fs
  EnforcerFrameRecovery.fs
  EnforcerRepair.fs
  EnforcerHost.fs
```

职责严格限定如下。

### `EnforcerTipGuidance.fs`

移动：

* `TipGuidance`
* `tipIdentityText`
* `tipFullText`
* owner Main session resolution
* latest tip lookup
* full-delivery detection
* full-delivery recording
* `resolveTipGuidance`
* `latestTipGuidance`

这个模块只回答：

> 当前 Main 应该看到哪一个 tip 文本？

不要让它知道 continuation parking、blog commit、repair。

---

### `EnforcerCycleDecode.fs`

移动：

* `blogCallFromPart`
* assistant step 查找
* blog calls 收集
* canonical merge
* part ordinal 排序
* cycle input validation

它应该尽可能是纯函数。

输入：

```text
Host-visible assistant snapshot
```

输出：

```text
ValidatedCycle | CycleDecodeError
```

不要在这里 append journal。

---

### `EnforcerCycleCommit.fs`

移动：

* append failure classification
* ordinary observation commit
* squash commit
* commit outcome

只拥有：

> 一个已经合法化的 cycle 如何成为 durable fact？

---

### `EnforcerFrameRecovery.fs`

移动：

* journal frame load
* resolved frame
* request-context rebuild
* XTrace/context 恢复
* recovery stage 判断

只拥有：

> Durable history 如何恢复成 Enforcer continuation 所需事实？

---

### `EnforcerRepair.fs`

移动：

* incomplete/aborted blog tool 判断
* repair key
* repair instruction
* repair context

只拥有 protocol repair。

---

### 最后的 `EnforcerHost.fs`

最终最好只剩：

```fsharp
let handleContinuation ... =
    decode
    |> recover
    |> decide
    |> commit
    |> parkOrProject
```

以及少数边界 outcome。

目标不是“200 行刚好”，而是打开文件时一眼能看完**主控制链**。

建议最终 150～300 行。

---

# 四、P0-2：`PluginRuntimeScope.fs` 必须按生命周期拆

这个文件比某些 800 行文件更值得优先处理。

它的开头说自己是：

> Explicit lifetime root for one plugin instance.

这本身合理。问题是它里面实际同时保存了：

* tool runtime
* subscription
* shared terminal
* parked transforms
* pending blogger offers
* drain windows
* compaction probe
* family recovery
* loop sensor
* satellite runtime
* SyncDelegate runtime
* Strength runtime
* Strength predictor
* Strength fuse
* SessionDirectories
* OwnedSessions
* UserMessageBindings
* SessionParents
* Companions
* recovery arming
* attempt plans
* quiescence
* join interrupts

这些内容在同一个类中直接可见。 后半段又直接实现 Blogger parking/flight/pending-offer/drain-window 生命周期。

这正是 `god-module` 定义里的“多个独立生命周期被便利性 colocate”。

## 建议结构

```text
Host/
  PluginSessionScope.fs
  PluginBloggerScope.fs
  PluginRecoveryScope.fs
  PluginStrengthScope.fs
  PluginRuntimeScope.fs
```

### `PluginSessionScope`

拥有：

* `SessionDirectories`
* `OwnedSessions`
* `SessionParents`
* `UserMessageBindings`
* `Companions`
* session cleanup
* quiescence
* join interrupts

### `PluginBloggerScope`

拥有：

* `parked`
* `pendingOffer`
* `drainWindows`
* `IParkedTransformHost`

### `PluginRecoveryScope`

拥有：

* `familyRecoveryPorts`
* `RecoveryArming`
* `AttemptPlans`
* recovery single-flight

### `PluginStrengthScope`

拥有：

* `StrengthRuntime`
* predictor state
* recent primary
* pending first/second
* fuse
* replica runtime attachment
* managed inventory中确实只为 Strength 服务的部分

### `PluginRuntimeScope`

最后只负责：

* 聚合上述 owners；
* plugin-global terminal/subscription/tool-runtime 等真正 root-level resource；
* 统一 `Dispose()`。

理想形态：

```fsharp
type PluginRuntimeScope(...) =
    let sessions = PluginSessionScope(...)
    let blogger = PluginBloggerScope(...)
    let recovery = PluginRecoveryScope(...)
    let strength = PluginStrengthScope(...)

    member _.Sessions = sessions
    ...
    member _.Dispose() =
        ...
```

这叫 **composition of owners**。

不要改成：

```text
PluginRuntimeScopeHelpers.fs
PluginRuntimeScopeUtils.fs
PluginRuntimeScopeMisc.fs
```

那只是把 god-module 的尸块撒到四个文件里。

---

# 五、P0-3：`Journal/Fold.fs` 是最适合“按主权拆”的实例

这是很明确的候选。

它自己声称：

> each bounded projection owns its own fold algorithm; this module only routes facts and decides which refusals are fatal.



但实际文件已经 1100 多行，里面包含：

* fallback outcome policy
* verdict policy
* association policy
* handle policy
* blog fold rejection policy
* prefix policy
* 各种 projection update helper
* 大型 fact dispatch

也就是说：**文件的注释描述的是正确目标架构，但物理实现还没有压缩到那个架构。**

## 最终建议

```text
Journal/
  PromptFactFold.fs
  FallbackFactFold.fs
  ReviewFactFold.fs
  ExecutionFactFold.fs
  CompanionFactFold.fs
  ContextFactFold.fs
  OrchestratorFactFold.fs
  HostFactFold.fs
  ManagerLifecycleFactFold.fs
  Fold.fs
```

每个 family 模块形状尽可能统一：

```fsharp
module FallbackFactFold =
    let fold projection fact =
        ...
```

`Fold.fs` 最终只保留：

```fsharp
match fact with
| AgentFact.Prompt x -> PromptFactFold.fold ...
| AgentFact.Fallback x -> FallbackFactFold.fold ...
| AgentFact.Review x -> ReviewFactFold.fold ...
...
```

## 一个特别重要的禁区

不要造：

```text
FoldHelpers.fs
FoldCommon.fs
FoldUtils.fs
```

除非里面真有一个可命名代数。

仓库的 generic-helper 规则本来就要求每个模块具有能**拒绝不相关函数**的 membership rule。

例如：

* fallback 的 “AlreadyObserved 是 idempotent replay” → 放 `FallbackFactFold`
* blog stale frame epoch 的拒绝语义 → 放 `Companion/Blog` fold
* handle retirement → 放 execution fold

不要为了去重把不同语义的 `Result` handling 合成万能 helper。

---

# 六、`Fact.fs`：很大，但我建议暂缓

`Kernel/Fact.fs` 928 行。

乍看应该马上拆，实际上这是一个**高风险假阳性**。

文件已经明确解释了现有设计：

> Durable agent facts by bounded context
> each family owns its cases and fold branch



也就是说，这里的“大”很大程度上来自：

**大量代数数据类型 vocabulary。**

这和一个 900 行 service 完全不是同一种病。

而且 `Fact` 是 durable wire/journal vocabulary，随意物理拆分很容易牵连：

* Fable emitted name
* codec
* test bridge
* compile order
* serialized case identity

因此顺序应该是：

**先拆 `Fold.fs` → 看 `Fact.fs` 阅读压力是否自然下降 → 最后才决定要不要拆 vocabulary。**

如果 Fold 拆完后，`Fact.fs` 只是“一整本 durable vocabulary 字典”，我甚至接受它作为明确例外。

这正符合：

> Do not split merely because a cohesive module is large.



---

# 七、P0-4：`Domain/JsTools.fs` 是最安全的大拆之一

这个文件 1149 行，但比 `Fact.fs` 容易动很多。

目前同一个物理文件已经包含独立 namespace-level 概念，例如：

* `JsCapability`
* `JsCapabilityFragment`
* `JsFragmentRegistry`
* `JsCanonicalDescription`
* 后续 Js surface/generator
* failure
* anchors
* staged transaction

从开头就能看到 capability、registry、description 已经是三个清晰概念。 

所以这里优先做**纯物理搬家，不改公开 symbol 名**。

建议：

```text
Domain/
  JsCapability.fs
  JsDescription.fs
  JsSurface.fs
  JsFailure.fs
  JsAnchor.fs
  JsTransaction.fs
```

这里尽量不要建立新的 `Domain/Js/` 目录，第一轮先减少 churn。

## 推荐搬法

### `JsCapability.fs`

放：

* `JsCapability`
* `JsCapability.ofToolPermission`
* `JsCapabilityFragment`
* `JsFragmentRegistry`

### `JsDescription.fs`

放：

* `JsExample`
* `JsCanonicalDescription`

### `JsSurface.fs`

放：

* public generated surface types
* generator

### `JsFailure.fs`

只放 typed failure vocabulary。

### `JsAnchor.fs`

放：

* anchor declaration
* match/range rules
* anchor validation

### `JsTransaction.fs`

放：

* staged mutation
* transaction
* transaction IDs/facts（如果确实属于同一 transaction invariant）

这批改动很适合作为第一批“练兵”，因为几乎可以做到：

**只移动定义 + 调整 `.fsproj`，调用点基本不变。**

---

# 八、P0-5：`ProjectionAlgebra.fs` 按“类型 → 规划 → 渲染”拆

这里的规范注释已经直接告诉你边界了：

* feature module 只能声明 `ProjectionIntent`
* Planner 排序和冲突检查
* Renderer 统一渲染



但现在这些东西塞在一个 890 行文件。

它天然应该变成：

```text
Domain/
  ProjectionIntent.fs
  ProjectionPlanner.fs
  ProjectionRenderer.fs
```

或者如果 snapshot/type 很多：

```text
ProjectionTypes.fs
ProjectionIntent.fs
ProjectionPlanner.fs
ProjectionRenderer.fs
```

职责：

```text
Types
  ↓
Intent factories
  ↓
Planner: Snapshot × Intent list → Plan
  ↓
Renderer: Plan → ProviderSemanticProjection
```

**Planner 不碰 Host obj。Renderer 不决定业务冲突。**

做到这里以后，原 `ProjectionAlgebra.fs` 应该直接消失，而不是留下一个 re-export facade。

因为仓库明确反对“门面把内部混乱藏起来”。

---

# 九、P1：`SpikePlugin.fs` 不应该“拆散”，应该“瘦成装配图”

`SpikePlugin.fs` 的 `initSpikePlugin` 一开始就同时做：

* resources
* port
* journal
* runtime scope
* host
* git tree
* EventStore/Strength durability
* causal wait
* casebook
* signal bootstrap
* SyncDelegate
* Strength replica
* recovery
* transform/hook wiring



这是 composition root，所以**拥有很多依赖本身并不是罪**。

真正的问题是装配细节太多，导致 884 行。

最终应像：

```fsharp
let initSpikePlugin input =
    task {
        let bootstrap = PluginBoot.create input
        let! host = PluginHostWiring.create bootstrap
        PluginSessionWiring.attach host
        PluginRecoveryWiring.attach host
        PluginStrengthWiring.attach host
        PluginTransforms.attach host
        return PluginHooks.create host
    }
```

但千万注意：

这些 `*Wiring` 模块只负责各自 owner 的装配。

不要把真正 lifecycle policy 搬进它们。

**`SpikePlugin.fs` 仍然必须保留全局初始化顺序的唯一权威。**

目标约 100～200 行。

---

# 十、P1：`Codec/Projection.fs`

这个文件的合理主权其实非常清楚：

> Host raw object → ProviderWireProjection adapter boundary.

而且它明确强调动态属性访问只能在这里。

所以不要把动态 Host parsing 扩散到普通 Host 模块。

但 699 行仍然可以拆成三个**边界子能力**：

```text
Codec/
  ProviderWireDecode.fs
  ProviderWireCapture.fs
  ProjectionMessageEdit.fs
```

### Decode

拥有：

```text
raw Host object → WirePart / WireMessage
```

包括各种：

* tool
* tool-result
* assembled tool
* file
* reasoning
* text

### Capture

拥有：

```text
Wire + Host stable address
```

例如 `HostToolPartId`。

### MessageEdit

如果文件后半存在 Host raw message patch / rendered projection apply，就单独放。

最后调用点从：

```fsharp
Projection.decodeMessage
```

迁成：

```fsharp
ProviderWireDecode.message
```

**别留下永久 `Projection.fs` 转发全部旧 API。**

可以在一次 commit 内完成所有调用点迁移，然后删除旧模块。

---

# 十一、P1：`JsToolsFs.fs`

它的文件注释自己列了四类工作：

* strict UTF-8
* glob
* anchor matching
* staging / commit / rollback



这些虽然都属于 filesystem adapter，但有明显独立算法和失败语义。

建议：

```text
Infrastructure/
  JsUtf8Fs.fs
  JsGlobFs.fs
  JsAnchorFs.fs
  JsMutationFs.fs
```

外层 workflow 根据需要依赖它们。

注意，不建议：

```text
JsToolsFs1.fs
JsToolsFs2.fs
```

文件名必须回答：

> 为什么这些函数必须一起变化？

---

# 十二、P1：`HostForkRuntime.fs`

这个文件自己已经写着：

> Fork / Reuse / Pty operations live in extension files (semantic split).



这说明方向已经对了，只是主文件还剩 630 行。

建议继续把它压成：

**Runtime = state/resource spine**

保留：

* child dictionary
* pending runs
* pty ownership
* abort token
* dependency ports
* internal resource access

迁出：

* join workflow → `HostForkJoin.fs` / 已有 `JoinDrain.fs`
* run terminal workflow → 已有 `HostForkRunLifecycle.fs`
* PTY 行为 → 已有 `HostForkPty.fs`
* restart → 已有 `HostForkRestart.fs`
* child dispatch → 已有 `HostForkChildDispatch.fs`

主类不要成为“所有 extension 都通过 internal getter 戳它内部”的 mutable bag。

每次迁一个 operation 时顺便问：

> 这个 extension 真正需要哪三个能力？

如果一个 extension 需要 15 个 internal member，说明边界还没成立。

---

# 十三、P1：`SyncDelegateRuntime.fs`

目前它一个类同时拥有：

* call indexes
* pending completion text
* deleted inspector cache
* in-flight scope
* tool policy
* wait diagnostic description
* invoke
* return
* completion
* lifecycle cleanup

从文件开头已经能看到多个 process-local registries 与 CE 等待语义混在一起。

建议：

```text
Session/
  SyncDelegateCallStore.fs
  SyncDelegateWait.fs
  SyncDelegateWorkflow.fs
  SyncDelegateRuntime.fs
```

### `CallStore`

只拥有状态和原子 index consistency：

```text
owner scope ↔ delegate ↔ call
```

### `Wait`

只定义 wait vocabulary / causal diagnostics。

### `Workflow`

处理：

```text
Acquire → GetOrCreate → Send
→ await Returned
→ await Completion
```

### `Runtime`

成为几个入口的 façade **仅当它真的是外部 runtime boundary**。

这里保留 facade 是合理的，因为 facade 自身就是一个 runtime capability，不是为了掩盖内部垃圾。

---

# 十四、P1：`ManagerNarrativeTransform.fs`

这个 613 行文件里，目前同时存在：

* raw dynamic access
* authority/journal querying
* compaction marker decoding
* user message finding
* prompt metadata decoding
* XTrace lifecycle判断
* suicide evidence
* raw message rewrite
* narrative policy

例如它已经直接重复了 `readField` 一类 Host dynamic access。

这里有个更重要的问题：

**不要只是拆文件，要消灭重复边界解析。**

因为 `Projection` 已经被正式定义为 Host dynamic-property adapter。

理想改造：

```text
Codec 层
  解 raw Host message

Domain / Transform 层
  只收 typed evidence

ManagerNarrativeTransform
  typed evidence → rewrite decision
```

即把：

```fsharp
raw?info?...
```

尽量赶回 Codec。

这比把 613 行机械拆成三个 200 行更符合 Kolmogorov。

---

# 十五、小文件怎么处理：分成“删、合、留”

这是这次最容易做错的部分。

**小 ≠ 坏。**

尤其这个仓库大量使用：

* dependency inversion ports
* compile-order seam
* typed contracts
* public entry point

这些就是应该小。

## 第一类：直接删除

### `EnforcerNudge.fs`

7 行。

### `EnforcerThrottle.fs`

7 行。

两者自己都已经声明：

* compiled tombstone
* `Removed = true`
* zero production call sites



这种东西**不要合到 `EnforcerLegacy.fs`**。

正确操作：

1. 搜调用点；
2. 确认零生产调用；
3. 删除两个文件；
4. 删除 `.fsproj` 中两项 `<Compile Include>`;
5. 运行 architecture/fsproj gate；
6. 运行 unit；
7. 提交。

这才符合“版本控制保存历史，不在源码保存尸体”的仓库纪律。

---

# 十六、另一个高价值删除：`HostPendingRun.sessionDeadRefusal`

这个文件只有 25 行，而且末尾写得非常直白：

> Kept for call-site compatibility; always returns None



这就是优质清理目标。

不要把它保留成：

```fsharp
let sessionDeadRefusal ... = None
```

保姆级做法：

1. 全局搜 `sessionDeadRefusal`；
2. 调用处删除这层判断；
3. 让逻辑直接表达“retry count 不杀死 Logical Run”；
4. 删除此函数；
5. 如果 `PendingHostRun` 类型只服务 `HostForkRuntime` / lifecycle：

   * 把类型和 `completionSource` 挪到真正 runtime-state owner；
6. 若多个 extension 都需要这个类型，则**文件留下来也完全可以**。

重点：

> 先删除 compatibility cruft，再决定这个 20 行文件是否值得合并。

不要反过来。

---

# 十七、这些小文件大多应该保留

例如：

```text
AssemblyInfo.fs
Application/Finality/Ports.fs
Application/Review/Ports.fs
Session/ConfirmedFailurePort.fs
Session/InteractionRepairPort.fs
Journal/ProjectionState.fs
Infrastructure/OpenCode/Plugin/Plugin.fs
Kernel/Temporal.fs
Tools/ToolContext.fs
Host/HostDigest.fs
```

我不建议为了“平均文件行数漂亮”而合。

原因分别是：

* package entry；
* dependency inversion；
* compile-order barrier；
* capability contract；
* central state vocabulary；
* single crypto boundary。

这些文件虽小，但它们用**文件边界本身表达架构**。

合掉反而增加 Kolmogorov description length——以后读者要在大文件里重新识别那条边界。

---

# 十八、`TurnRuntimePreparation.fs`：可以合，但优先级极低

它只有一个动作：

```text
observed session → dispose Executor runtime
```

而且明确属于：

> Physical runtime cleanup before Application turn observation.



如果它只有一个调用者 `HostSignalBootstrap`，并且不存在架构测试需要这个 seam，我会倾向：

**并入 `HostSignalBootstrap` 中拥有该 turn transition 的局部函数。**

但要排在所有大文件之后。

别为了清掉一个 11 行文件花半天。

---

# 十九、测试代码：最大的问题其实是 `tests/unit/support/domain.mjs`

它大约 **6181 行**。

但同样不能直接：

```text
domain1.mjs
domain2.mjs
domain3.mjs
```

因为它的开头有非常重要的架构约束：

> the ONLY file allowed to know Fable's output shape

并明确解释：

* DU tag
* FSharpMap/FSharpList internals
* emitted module suffixes

都必须隔离在这个边界里。

这条思想完全正确。

错误的只是：

**“anti-corruption boundary = 一个 6181 行物理文件”。**

## 应改成“一个 boundary package/directory”

建议：

```text
tests/unit/support/domain/
  interop.mjs
  identity.mjs
  journal.mjs
  context.mjs
  execution.mjs
  prompt.mjs
  enforcer.mjs
  orchestrator.mjs
  strength.mjs
  persist.mjs

tests/unit/support/domain.mjs
```

其中：

### `interop.mjs`

唯一允许理解：

* Fable DU shape
* `caseOf`
* FSharpMap
* FSharpList
* emitted-module naming mechanics
* curry mechanics

### family adapter

例如：

```text
journal.mjs
```

只描述 Journal 测试 API。

### `domain.mjs`

过渡期可以 re-export family adapters。

这里我认为 facade 是**合法的**，因为它是在维护测试 anti-corruption boundary，不是在掩盖生产内部混乱。

之后可以逐步让 tests 按 family 导入：

```js
import { ... } from '../support/domain/journal.mjs'
```

最后 `domain.mjs` 只留下最基础公共 API，甚至删除。

## 同步加门禁

原来的规则要从：

> 只有 `domain.mjs` 可以碰 Fable output shape

改为：

> 只有 `tests/unit/support/domain/**` 的指定 interop boundary 可以碰 Fable mechanics；普通测试禁止。

否则拆完文件等于拆穿边界。

---

# 二十、超大测试文件按“行为簇”拆，不按 describe 长度拆

当前明显候选还有：

```text
manager-tool-contract.test.mjs       ~1277
enforcer-cycle-protocol.test.mjs     ~1124
schema-cases.mjs                     ~1022
scenario-schema.js                    ~917
scenario-driver.mjs                   ~853
runtime-key-cases.mjs                 ~825
plugin-fixture.mjs                    ~806
projection-algebra.test.mjs           ~800
```

测试文件应按“一个失败在证明什么”拆。

例如：

```text
manager-tool-contract/
  fork-contract.test.mjs
  join-contract.test.mjs
  permission-contract.test.mjs
  finality-contract.test.mjs
```

不是：

```text
manager-tool-contract-part1.test.mjs
manager-tool-contract-part2.test.mjs
```

后一种没有知识边界。

---

# 二十一、不要一口气整理全部 105 个 >200 文件

这是整个施工方案里最重要的风险控制。

仓库自己要求：

* 改前定位 owner；
* 读周边 contract；
* 做最小结构变化；
* 大意图拆成独立可审单元；
* 重构不能停在新旧并存的半路。

所以我建议按 **5 个 Wave** 做。

---

# Wave 0：只建立基线，不重构核心

目标：把“以后不能变差”建立起来。

做四件事。

### 0.1 生成一次人工审查清单

只统计，不修改源文件：

```text
src/**/*.fs
tests/**/*.mjs
tests/**/*.js
scripts/**/*.mjs
```

输出：

```text
path
physical lines
largest function
classification
```

checker 是允许的。

`AGENTS.md` 禁止的是用自动程序**批量改源码**，不是禁止只读门禁。

---

### 0.2 建 `kolmogorov-size` ratchet

**不要马上全库 hard ≤200。**

第一版规则：

```text
现有超标文件不得变更得更大；
新文件默认不得 >200；
原来 ≤200 的文件不得突破 200；
已有 >200 文件采用 current-baseline ratchet。
```

例如：

```text
EnforcerHost.fs baseline 1988
```

修改后：

* 1900 → 可过
* 1990 → 红
* 1500 → 新 baseline 1500

重构逐步压到底。

这种模式与仓库已有的 ownership ratchet 思路非常相称。

---

### 0.3 删除两个 Enforcer tombstone

这是第一笔纯绿收益。

---

### 0.4 清理 `sessionDeadRefusal`

删除 compatibility no-op。

Wave 0 不碰行为。

---

# Wave 1：做“几乎只搬定义”的安全拆分

顺序：

```text
JsTools.fs
→ ProjectionAlgebra.fs
→ tests/unit/support/domain.mjs
```

原因：

这三处边界已经比较成熟。

这一阶段的核心原则：

> **名字不变、namespace 不变、行为不变，只把已经存在的概念放回独立文件。**

这是风险最低的一轮。

---

# Wave 2：处理真正 god-module

顺序建议：

```text
PluginRuntimeScope
→ Fold
→ EnforcerHost
```

为什么 `EnforcerHost` 不排第一？

因为它会使用：

* journal
* projection
* runtime scope

先把下游 owner 变清晰，再拆 orchestration，更容易一次到位。

---

# Wave 3：处理运行时协调大文件

```text
HostForkRuntime
SyncDelegateRuntime
ManagerNarrativeTransform
Codec/Projection
JsToolsFs
SpikePlugin
```

这里会出现更多 call-site migration，所以要一文件一 owner、一 commit。

---

# Wave 4：再看高语义密度文件

最后重新审视：

```text
Fact.fs
PromptAuthority.fs
Identity.fs
MagicTodo.fs
```

此时问的不是：

> 它还有 500 行吗？

而是：

> 打开这个文件，是否仍需要同时记住两个独立世界？

如果答案是否，就允许它大。

---

# Wave 5：收尾剩余 300+ 文件

再扫全仓。

此时大部分剩余应该属于两类：

1. 真正 cohesive、可以批准 exception；
2. 二级长尾，可以按已成熟的方法继续拆。

---

# 二十二、每拆一个 F# 文件，都必须按这个固定动作执行

这个项目的 `.fsproj` 关闭了默认 compile items：

```xml
<EnableDefaultCompileItems>false</EnableDefaultCompileItems>
```

而且所有 `.fs` 文件显式按顺序列出。

架构门禁还会检查：

* 文件在磁盘但未声明；
* fsproj 声明但文件不存在；
* 重复声明。

所以 F# 文件拆分**绝对不能**只“新建文件然后 build 看看”。

固定手顺：

```text
1. 确定新文件依赖谁。
2. 确定谁依赖新文件。
3. 在 Wanxiangshu.fsproj 中找到这两个点之间。
4. 新文件创建与 Compile Include 在同一个小改动完成。
5. 原文件移动定义。
6. format。
7. build。
```

F# 编译顺序要视为**显式 dependency DAG 的物理表示**。

---

# 二十三、一个文件具体怎么拆：标准动作模板

以后每个大文件都按下面机械流程。

### 第一步：不要先写新文件名

先在纸上列：

```text
定义
状态
纯规则
I/O
生命周期
编码/解码
协调
```

---

### 第二步：给每个函数回答一句

> 谁拥有这条知识？

不是：

> 它和谁长得像？

---

### 第三步：按 reason-to-change 聚类

例如：

```text
A、B、C 都会因为 journal replay 语义变化而变
→ 一个 owner

D、E 会因为 Host raw payload 改版而变
→ Codec owner

F 会因为 UI 文案变而变
→ 不得和前两组一起
```

---

### 第四步：给每组写 exclusion rule

好的：

> `ProviderWireDecode` 只把 Host raw message 解成 typed wire projection，不执行 domain policy。

坏的：

> `ProjectionHelpers` 放 projection 相关的帮助函数。

如果一句话不能拒绝未来函数，这个模块名就不合格。

---

### 第五步：先迁最叶子的 owner

先纯函数。

再 adapter。

最后 orchestration。

不要从顶层函数往下硬切。

---

### 第六步：一次只转移一个所有权

例如：

```text
commit 1: Extract EnforcerTipGuidance
commit 2: Extract EnforcerCycleDecode
commit 3: Extract EnforcerCycleCommit
...
```

不要：

```text
refactor enforcer architecture
```

一个 3000 行 diff。

---

### 第七步：转完立刻删旧路径

禁止：

```text
OldEnforcer.commit
NewEnforcerCommit.commit
```

同时活两周。

仓库自己明确要求所有权迁移完成后删旧路径。

---

# 二十四、验证阶梯也固定下来

项目 `package.json` 已经提供：

* `format:check`
* `lint`
* `build`
* unit
* integration
* e2e
* `check`
* `check:release`

其中：

```text
npm run check
= lint
→ build
→ unit
→ integration
```

`check:release` 再增加 warmup、E2E、package 和 pack dry-run。

每个小 commit 推荐：

```bash
npm run format:check
npm run build
# 当前 owner 对应 focused test
npm test
```

一个 Wave 完成：

```bash
npm run check
```

涉及这些区域时额外跑 E2E：

```text
SpikePlugin
HostSignalBootstrap
EnforcerHost
HostForkRuntime
SyncDelegateRuntime
Projection Host boundary
```

整个重构完成：

```bash
npm run check:release
```

仓库本身也要求验证按阶梯推进，而不是跳级。

---

# 二十五、这次最好专门增加一个结构 proof

我建议最终新增一个只读门禁，例如：

```text
scripts/checks/kolmogorov-size.mjs
```

但它只做**结构报警**，不假装“行数 = 架构正确”。

建议检查四件事：

```text
1. 新普通源码文件 >200 → fail
2. grandfathered 大文件超过 ratchet baseline → fail
3. ordinary function >60 → report/fail
4. <=15 行普通 implementation → warning，仅检查，不自动 fail
```

同时有显式例外类型：

```text
ports
entry points
pure vocabulary
generated/large declarative tables
compile-order seams
```

并且 exception 必须写：

```text
path
owner
reason
```

不要写：

```text
Fact.fs: too hard to split
```

应该写：

```text
Fact.fs:
durable fact vocabulary;
single serialized algebra;
physical size does not mix lifecycle/effect ownership.
```

这样例外也是建筑说明，而不是逃生门。

---

# 二十六、我建议最终追求的仓库形态

不是：

```text
所有文件 199 行。
```

而是：

```text
Domain
  多数 50–180 行
  大 vocabulary 可 300+

Journal
  每个 fold family 80–200 行
  Fold dispatcher 100–180 行

Session
  runtime state owner 100–250 行
  每条 lifecycle workflow 80–200 行

Infrastructure
  一个 external failure law 一个 adapter
  100–250 行

Plugin
  composition root ~150 行
  wiring owner 各自独立

Tests
  behavior file 100–300 行
  support adapter ~100–250 行
  不再存在 6000 行测试万能入口
```

这会比“严格每个文件 200 行”健康得多。

---

# 二十七、施工优先级，我会这样排

如果让我实际在这个仓库上动手，我会严格按这个次序：

| 顺位 | 工作                                | 风险  | 收益   |
| -: | --------------------------------- | --- | ---- |
|  1 | 删除 `EnforcerNudge/Throttle`       | 极低  | 中    |
|  2 | 清 `HostPendingRun` compatibility  | 极低  | 小    |
|  3 | 建 size ratchet                    | 低   | 高    |
|  4 | 拆 `JsTools.fs`                    | 低   | 很高   |
|  5 | 拆 `ProjectionAlgebra.fs`          | 低～中 | 很高   |
|  6 | 拆 `tests/unit/support/domain.mjs` | 中   | 极高   |
|  7 | 拆 `PluginRuntimeScope.fs`         | 中   | 极高   |
|  8 | 拆 `Fold.fs`                       | 中   | 极高   |
|  9 | 拆 `EnforcerHost.fs`               | 中～高 | 极高   |
| 10 | 收 `HostForkRuntime`               | 中   | 高    |
| 11 | 收 `SyncDelegateRuntime`           | 中   | 高    |
| 12 | 收 `ManagerNarrativeTransform`     | 中   | 高    |
| 13 | 收 `Projection.fs`                 | 中   | 高    |
| 14 | 收 `JsToolsFs.fs`                  | 中   | 高    |
| 15 | 瘦 `SpikePlugin.fs`                | 高   | 极高   |
| 16 | 重新裁决 `Fact.fs`                    | 高   | 未必需要 |
| 17 | 再扫全部 >300                         | 低～中 | 高    |

---

# 最后给一个最关键的验收问题

这次重构结束后，不要问：

> “还有多少 >200 行文件？”

先问这四个问题：

**任意一个文件，我能不能用一句话说清楚什么属于它、什么绝不属于它？**

**任意一个修改理由，我能不能基本预测应该改哪一个 owner，而不是全局 grep 后选五个文件？**

**打开一个 workflow 文件时，我看到的是业务/生命周期规则，还是 codec、dictionary、retry、storage、formatting 一起扑面而来？**

**那些十几行文件，是清晰的 Port / vocabulary / entry seam，还是 tombstone / compatibility / forwarding ceremony？**

四个答案都健康以后，行数自然会大幅回落。

这才真正符合这个仓库写下的 Kolmogorov 目标——**把不可消除复杂度压成不可再短的充分描述，而不是把 1000 行切成五张 200 行的纸。** 

---

## Active work

> 本文件现为变更工作记录，不是当前产品规范。Proposal 原文已冻结。
> 当前用户已明确启动本 Proposal（active-goal 指令）。

### 启动

- 启动时间：按仓库生命周期合同（`changes/README.md`），由用户指令启动。
- 范围：第 1707–1725 行施工优先级表全部 17 顺位，按 Wave 0–5 顺序执行。
- 方法约束：每次只转移一个所有权；一 commit 一 owner；迁移完成立即删除旧路径；不建立
  facade/re-export 中转；`Wanxiangshu.fsproj` 编译顺序视为显式依赖 DAG。

### Remaining work（关闭条件）

- [x] Wave 0：删除 `EnforcerNudge/Throttle`、清 `sessionDeadRefusal`、建 `kolmogorov-size` ratchet
- [x] Wave 1：拆 `JsTools.fs`、`ProjectionAlgebra.fs`、`tests/unit/support/domain.mjs`
- [x] Wave 2：拆 `PluginRuntimeScope.fs`（四 owner）、`Journal/Fold.fs`（8 family 模块）、`EnforcerHost.fs`（五 owner）
- [ ] Wave 3：收 `HostForkRuntime`、`SyncDelegateRuntime`、`ManagerNarrativeTransform`、`Codec/Projection`、`JsToolsFs.fs`、`SpikePlugin.fs`
- [ ] Wave 4/5：重审 `Fact.fs` 等 + 再扫全部 >300 行文件

### Completion criteria

- 每个大文件拆分后：一个文件一句话能说清属于它/绝不属于它的知识；调用点全部迁移；旧文件删除。
- `kolmogorov-size` 门禁纳入 `scripts/check.mjs`，`npm run check` 全绿。
- 全仓 `npm run check:release` 通过（E2E 涉及 EnforcerHost/HostForkRuntime/SyncDelegateRuntime/SpikePlugin 等）。
