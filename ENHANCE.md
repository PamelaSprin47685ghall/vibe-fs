# ENHANCE — 重构待办清单(剩余)

> 原始 76 点微观改造清单。已完成 11 点,评估保留 8 点,剩余 57 点待办。
> 基线:`pnpm build && pnpm test` → 913 passed, 0 failed。
> 规约:`open Fable.Core.JsInterop` / `?()` / `Dyn.get` 在 `Kernel/` 内为 Bug;异步货币唯一为 `JS.Promise<'T>`(见 AGENTS.md)。

## 进度概览

### 已完成重构(11 点)

| 点 | 文件 | 改造 |
|---|---|---|
| 21-22 | Kernel/Config.fs | `canUseCanonical` 的 if-elif 链 → `match agent, tool with` 有序真值表;`toolContainsAny` 内联为 `toolMatches` guard |
| 24-25 | Kernel/Executor.fs | `scan` 词法重构:扁平 `classifyToken` + 4 分派 + `Cursor` DU(RED 先补 `stripLexer'` 35 检查锁定行为) |
| 29 | Kernel/Fuzzy.fs | `relativePath` 递归 `commonPrefix` → `List.zip`+`takeWhile`+`length`+`skip` |
| 31 | Kernel/Fuzzy.fs | `buildGrepOutput` `Option.toList`+`@` → `List.choose id` 序列推导式 |
| 33 | Kernel/MagicProjection.fs | `todoSegmentEndIndexesFor` 三元组状态机 → `splitOn` + last-anchor-per-segment |
| 35,37 | Kernel/ReviewSession.fs | `transition` 笛卡尔积展平 + `updateSession`/`transitionSession` 统一原语 |
| 70-71 | Shell/OllamaClient.fs | `postInitNoSignal`/`postInitWithSignal` 合一为 `postInit` |

### 评估保留现状(8 点)

| 点 | 文件 | 理由 |
|---|---|---|
| 23 | Kernel/Executor.fs | `isWhitespace`/`isDigit`/`isLetter`/`skipWhile`/`takeWhile` 是 `parsePipe` 内部正确的解析原语,内联反损可读性 |
| 26 | Kernel/Executor.fs | `shouldAppendReadOnlyWarning` 已是干净 `Set` 查找;"工具元数据携带只读"属架构级改动,非局部重构 |
| 27 | Kernel/Executor.fs | `describeResultTag` 已对 4 个 `ExecuteResult` 分支穷尽,无缺模式,无需 `_` |
| 32 | Kernel/MagicCore.fs | `breaksTodoBurstFor` 是 1 行 OR;套活跃模式属过度抽象 |
| 34 | Kernel/MagicProjection.fs | `collectUserText` 已是 clamp+slice+`choose` 清晰形式,`Seq.skip`/`take` 不更优 |
| 49,50 | Opencode/TitleFetchGuard.fs | 原地涂改的是局部 parse 结果(非调用方数据);`init.body <-` 是不可避免的边界效果;强行纯映射属过早抽象 |
| 69 | Shell/FileSys.fs | `absorb`/`Budget` 已写得好,原清单即标注"保持" |

### 本轮新增完成(14 点,基线 922 passed → 922 passed)

| 点 | 文件 | 改造 |
|---|---|---|
| 9 | Kernel/Message.fs | `readAssistantText` 深层 `isNullish` 嵌套 → 抽出 `tryAssistantContent`/`tryPartText` 两个扁平 guard,主函数退化为 `Array.choose tryAssistantContent >> collect >> choose tryPartText` |
| 18-20 | Kernel/SubagentIntents.fs | 引入 applicative `<*>` 组合子;`parseCoderIntent`/`parseInvestigatorIntent`/`decodeCoderTarget` 从 `Result.bind` 金字塔坍缩为 `Ok (fun o b t dnt -> …) <*> objective <*> background <*> targets <*> Ok doNotTouch` 的声明式绑定 |
| 28 | Kernel/Fuzzy.fs | `normalizeSegments` 内部 `rec fold` → `(segments, []) \|\|> List.fold` + `List.rev`,消除手写累加器递归 |
| 30 | Kernel/Fuzzy.fs | `normalizeTrimmed` 多重 `StartsWith/EndsWith` → `(｜RecursiveDirGlob｜_｜)` active pattern + 模式匹配 |
| 36 | Kernel/ReviewSession.fs | `applyCommand` 不再用 `nextState = session.state` 属性比较判变,改用 `transition` 自身返回的事件日志(`ReviewEvent option`)——`None` 事件 ⟺ 无变化 |
| 38-39 | Kernel/TreeSitterKernel.fs | `extractFilePaths` 拆为纯函数 `pathsFromPatchText (patchText: string)` + 仅负责取 key 的 `extractFilePaths`,patch 正则解析与 args 取值彻底分居 |
| 40-41 | Kernel/NudgeState.fs | `NudgeHostEvent` 的布尔盲区消除:`MessageUpdated(isAbort,isCompleted)`/`MessagePartUpdated(partType,isAbort,isAbortState)`/`*Failed(isAbort)`/`SessionError(isAbort)` → 携带有限构造的 `MessageOutcome`(UpdateAborted/UpdateCompletedAssistant/UpdateNoChange)/`PartOutcome`/`StepFailOutcome`/`ToolFailOutcome`/`SessionErrorOutcome`;非法布尔组合在源码层无法构造。同步删 5 个因此失去作用的 handler |
| 42 | Kernel/NudgeState.fs | `handleSessionNextPrompted` 内联进 `handleEvent` 顶级匹配,所有状态迁移收敛在同一闭环 |
| 52 | Opencode/WikiRuntime.fs | `directWriteTurns : Dictionary<string, ResizeArray<string> * bool>` 裸元组 → 单一职责记录 `DirectWriteTurn = { rwSummaries; dirty }` |
| 66 | Opencode/Tools.fs | 删除 `entry`/`mergeObjects` 两段样板,`createTools` 直接用单个 `createObj` 推导式 |

### 第二轮新增完成(9 点,基线 922 → 925 passed,零回归)

| 点 | 文件 | 改造 |
|---|---|---|
| 8 | Kernel/Message.fs | `stripSyntheticMessages` 字符串前缀嗅探 → `decodeMessage` + `Source` DU 过滤(`m.source = Native`) |
| 11 | Kernel/Message.fs | `setPartOutput` 的 clone+withKey 金字塔 → `decodePart` + `withToolOutput`(类型 match + 边界 encode) |
| 1/2 基础 | Kernel/Messaging.fs(新) | P2 锁眼基础设施:强类型 `Entry`/`Message`/`MessageInfo`/`Part`/`Role`/`Source` 树 + `decode*` 边界解码 + `withParts`/`withToolOutput` encode。消费者迁移路径已通 |
| 46-48 | Opencode/HookExecute.fs | 删 `Dyn.deleteKey` 原地涂改;新增 `copyObjExcept` 构建剥除字段的新 args 对象赋 `output.args`,原始引用永不 mutate。2 个 in-place 测试断言同步改为验证新对象 |
| 51 | Opencode/WikiRuntime.fs | 删除 `WikiActor` 类(33-48 行),`writeQueues` + `postWorkspace`/`runWorkspace` 直接放 runtime |
| 52 | Opencode/WikiRuntime.fs | 5 个散落容器(sessionSnapshots/jobContexts/bookkeeperLaunches/directWriteTurns/scheduledMaintenance)→ 单一不可变 `WikiState` record + `mutable state` 单元;`directWriteTurns` 改 `string list` |
| 53 | Opencode/WikiRuntime.fs | 纯转移函数 reducer(`registerJob`/`removeJob`/`cacheSnapshot`/`markRwTool`/`consumeDirtyTurn`/`recordLaunchOnce`/`drainLaunches`)+ 按 workspace 写队列串行化;IO 不在状态转移中偷跑 |
| 65 | Opencode/PluginCore.fs | `commandExecuteBefore` 不再 `clearArray` 清空宿主原 parts 数组 + `pushPart`;构建新 `ResizeArray` 一次性 `setKey output "parts"` |

### 第三轮新增完成(基线 925 passed → 925 passed,零回归)

| 点 | 文件 | 改造 |
|---|---|---|
| 47-48 | Opencode/HookExecute.fs | 删除 `restoreMimocodeTaskArgsAfterExecute`。调研证实 setKey 写回 input.args 无任何生产消费者(backlog replay 经 MagicSessionStore 按 callID 取,不读 input.args),且其 takeReport 反而偷走了 BacklogInputForPart 所需的 report —— 删除既消除原地涂改又修复潜在 bug。capture 保留(before-hook 存 store 供 backlog)。2 个 round-trip 测试改为验证 capture store 保留供 replay |
| 53 | Opencode/WikiRuntime.fs | P53 真正落地:`WikiCommand` DU(8 case)+ 纯 `reducer` 函数(WikiState → WikiCommand → WikiState,无 IO);新增 `commandQueue : SerialQueue` 串行所有异步方法(EnsureSessionSnapshot/Submit/StartMaintenanceIfDue/WaitForBackgroundJobsForTesting/launchBackgroundSession)的状态+IO;同步方法同 tick 经 reducer;状态更新不再在 JS.Promise 悬挂中裸跑 |

### 第四轮新增完成(基线 926 passed → 926 passed,零回归)

| 点 | 文件 | 改造 |
|---|---|---|
| 1/2 全链 | Kernel/Messaging.fs + Message.fs + MagicProjection.fs + MagicCore.fs + MagicTodo.fs + CapsFormat.fs + Opencode/MessagingCodec.fs(新) + CapsCodec.fs(新) + HookTransform.fs + SessionIo.fs + MagicTests.fs + KernelTests.fs | P1/P2 全链落地:Kernel 消息链彻底去 Dyn。Messaging.fs 纯类型+纯逻辑(零 open Dyn/JsInterop);decode/encode 集中 Opencode/MessagingCodec.fs(唯一 FFI 边界);Message.fs 删全部 Dyn 访问器(只留纯常量);MagicProjection 改 Message list→Message list(纯,删 open Dyn,findFoldRange/burst 算法逐行保持);MagicCore/MagicTodo 改 Part 模式匹配;CapsFormat 拆为纯函数(Kernel)+ CapsCodec(Opencode 构造边界);MagicTodo(Kernel) reportOf 注入消除 Dyn;HookTransform/SessionIo 边界 decode/encode;MagicTests/KernelTests 强类型。encodePart/encodeMessage 保持未改 part/message 引用(dedup/caps "preserves original" 契约) |
| 48 | Opencode/HookSchema.fs + HookExecute.fs | setUiLabel 改为返回带 _ui 的新 args(Dyn.withKey 浅拷贝),不再原地 setKey mutate 入参 args;toolExecuteBeforeFor 赋 output.args 新对象 |
| 72(部分) | Opencode/PluginCore.fs | 删除死代码 clearArray/pushPart/ensureParts |

### 评估保留 / 后续专项(第四轮复核)

下列点经评估后判定「保留现状」或「属架构级多日改造,单次安全重构不可行」,理由附后。判定遵循铁律:不为追求数量把读者注意力引向新的偶然复杂度。

| 点 | 判定 | 理由 |
|---|---|---|
| 1, 2 | 已完成 | 见第四轮表:Kernel 消息全链去 Dyn(Messaging/Message/MagicProjection/MagicCore/MagicTodo/CapsFormat 纯,decode/encode 集中 MessagingCodec/CapsCodec) |
| 3-8, 10-17 | 已完成 | 见第四轮表:P1/P2 全链后,readAssistantText/flatten/setPartOutput/stripSynthetic 等均消费强类型 Message/Part,记录复制({ part with … })已可表达 |
| 23, 26, 27, 32, 34, 49, 50, 69 | 保留 | 原 ENHANCE.md 已列入「评估保留」,本轮复核结论不变 |
| 43 | 保留 | `decodeTodos`/`decodeLastAssistant` 仍接收 `obj array`,需 P1/P2 解码器才能转 `SessionSnapshot` 强类型 |
| 44, 45 | 保留 | `TypedIteratorStore` 已是强类型双桶(见 `FuzzyTests.iteratorStoreStronglyTyped` 锁定的契约);`mutable counter` 是生成唯一 opaque id 的必要手段,改 Actor 会让 `storeIterator`/`consumeIterator` 退化为异步,破坏 `FuzzyTests.iteratorRoundTrip` 的同步契约 |
| 46-48 | 已完成 | 见第二轮表(copyObjExcept 构建新 args 赋 output.args)+ 第三轮表(删 restore:setKey 无消费者)。before-hook 不再原地 mutate,after-hook 不再 setKey |
| 51, 53 | 已完成 | 见第二轮表(删 WikiActor + 单一不可变 WikiState)+ 第三轮表(WikiCommand DU + 纯 reducer + commandQueue 串行,IO 与状态分离) |
| 54-56 | 保留 | `ToolSpec`/`Params` 已在 `Kernel/ToolCatalog` 单一 SSOT 集中(`toolCatalogCentralized` 测试锁定);`Params.X` 命名访问是合理的便利层,P56 的反射/源生成在 Fable 下不可行 |
| 57, 58 | 宿主约束 | Zod-like schema 必须经宿主 `tool.schema` 的 JS API(`call1 schema …`)构建,无法用一套 F# 解码器替代宿主声明——桥接层是 FFI 必需 |
| 59, 60 | 保留 | `mergeNamedSettings` 已是 `Option.orElse` fold;`normalizeStr` 是边界 nullish→None 归一,无解码器库时不可消 |
| 61 | 保留 | `createInput` 的条件赋值(`modelString`/`thinkingLevel` 仅非空才写)在匿名记录里无法表达条件缺省,`o?() <-` 是必要 |
| 62 | 保留 | `extractToolContext` 的多键探测是应对宿主键名变体的必要防御 |
| 63-65, 67, 68, 72, 75 | 架构缓行 | 大文件拆分 / FFI 单通道收拢 / 配置强类型化,均是多文件结构性改造,单次安全重构不覆盖 |
| 73 | 已完成 | `applyDrafts` 早就是 `Kernel/Wiki` 的纯函数;`WikiRuntime.buildEntries` 调它,外壳仅经 `appendEntries` 落盘 |
| 76 | 已完成 | `DomainError` DU 已含 `ExecutorExecutableMissing`/`ParseError(ctx,detail)`/`ToolNotPermitted`/`InvalidIntent`/`UpstreamTimeout`/`UpstreamRefused`(`domainErrorsShared` 测试锁定),`UnknownJsError` 仅作最后兜底 |

---

## 剩余待办清单(原始 57 点;本轮完成 14 点,判定保留/缓行见上方表格)

> 以下保留原始清单文本作为每一点的意图存档。每一点的当前状态以「进度概览」的三张表(已完成 11 + 本轮 14 + 评估保留/缓行)为准。

### 一、干掉边界上的黑箱(消灭 `Dyn.fs` 和 `obj`)

**[全库]**

1. 废弃 `Kernel/Dyn.fs`:禁止在 `Kernel/` 目录中导入 `Fable.Core.JsInterop`、`?()` 动态操作符或 `Dyn.get`。
2. 引入 `Thoth.Json` 或同等强类型解码器,将所有的 Mux/Opencode 配置、消息、Hook Payload 在系统入口处硬解码为不可变的 F# 记录和代数数据类型 (DU)。

**[Kernel/Message.fs]**

3. 删除 `messageInfo`, `messageParts`, `infoId`, `entryMessage` 等所有基于 `Dyn.str` 和 `Dyn.get` 的访问器。
4. 定义完整的领域类型 `Message` 记录,包含 `MessageInfo`。
5. 将 `role` 定义为封闭的 DU `| User | Assistant | ToolResult | System`。
6. 将 `parts` 定义为封闭的 DU 列表 `| TextPart of string | ToolPart of ToolCall | ErrorPart of string`。
7. 删除 `infoIsError` 中的动态布尔值检查,直接由类型 `ErrorPart` 承载错误语义。
8. 删除 `stripSyntheticMessages` 中对前缀的字符串操作,改为在 Message 类型中附带 `Source` DU(`| Native | Synthetic of SyntheticKind`),直接对 DU 分支过滤。
9. 删除 `readAssistantText` 中的深层 `isNullish` 嵌套,改写为扁平的 `List.choose` 结合模式匹配 `| AssistantMessage { Parts = textParts } -> ...`。
10. 删除 `getLatestTodoPhasesFromEntries` 中对于 `entryType` 和 `entryCustomType` 的多重 if-else 分支,利用深层模式匹配 `match entry with | Custom ("user_todo_edit", data) -> ...` 坍缩逻辑。
11. 删除 `setPartOutput` 中 `clone` 和 `withKey` 的黑箱涂改,改为原生记录的不可变复制 `{ part with State = { part.State with Output = newOutput } }`。
12. 删除 `flatten` 中多层的 `isNullish` 防御,改为直接摊平合法且已验证的强类型消息链表。

**[Kernel/MessageDedup.fs]**

13. 删除 `ReadPart` 中的 `obj` 字段 `output` 和 `input`,改用强类型。
14. 删除 `tryDecodeReadPart` 和 `tryDecodeModelReadPart` 中的手写解析,使用统一解码器在入口解析为 `FileReadResult` DU。
15. 删除 `readPartOutputKey` 中对 `typeIs output "string"` 的动态判断,由代数类型保证数据形态。
16. 删除 `deduplicateReadOutputsWithSeenByPath` 中通过 tuple 和 `withKey` 强行复制 JS 对象的样板逻辑。直接 map 强类型实体链表,生成新的链表,最后通过唯一的 Encoder 投射给宿主。
17. 取消内部的多个 fold 中的 `anyChanged` 布尔标记。F# 记录的引用等价性天然可用于检查是否发生重建,无需手工维护追踪。

**[Kernel/SubagentIntents.fs]**

18. 删除大量手动使用 `requireNonEmpty`、`optionalStrArray`、`foldArrayResult` 等组合起来的"伪反序列化器"。
19. 换用 Applicative 组合子(例如 `Decode.object` 配合 `Required.Field`),将每个字段的解析从按行控制流坍缩为一行声明式绑定。
20. 删除 `decodeCoderTarget` 中通过 `typeIs t "object"` 进行的基础防御,交由静态解析器接管。

### 二、逻辑坍缩:把 if-else 迷宫压成模式匹配与规则表

**[Kernel/Fuzzy.fs]**

28. 删除 `normalizeSegments` 中的手动累加 fold,使用 `List.foldBack` 或模式匹配对 `.` 和 `..` 直接归约。
30. 删除 `normalizeTrimmed` 中的多重 `StartsWith` 和 `EndsWith`,用基于 Active Pattern 的正则提取:`| Regex @"^(.*)/\*\*(?:/\*)?$" [dir] -> ...`。

**[Kernel/ReviewSession.fs]**

36. `applyCommand` 中使用了 `session.state = nextState` 做变化判断,可改用 `Result` 类型或者返回变更日志以严格记录事实,替代基于属性比较的差异追踪。

**[Kernel/TreeSitterKernel.fs]**

38. 删除 `extractFilePaths` 中直接依赖 `args` (obj) 且依赖正则搜索 `patchText` 文本的实现。解析 `patchText` 应该是独立于工具参数抽取的功能。
39. 抽象出一个纯函数用于从强类型的 Patch 对象中解析文件路径。

**[Kernel/Nudge.fs] & [Kernel/NudgeState.fs]**

40. `NudgeHostEvent` 尽管已经是 DU,但其中携带的 `isAbortError: bool`, `isCompletedAssistant: bool` 等布尔盲区,导致凭空造出了非法状态(比如全是 true 的荒谬组合)。
41. 将布尔标记消除,重构事件的 DU Payload 携带具有领域意义的有限构造,例如 `| StepEnded of Result<TerminalFinish, AbortReason>`。
42. 删除 `handleSessionNextPrompted` 及相关处理器中的散落判断,将所有判断合并到 `handleEvent` 的顶级模式匹配中,使得每一种状态迁移都能在同一个闭环内被肉眼追踪。
43. 彻底删除 `decodeTodos` 和 `decodeLastAssistant` 中针对 `obj array` 和散装布尔值的魔改处理。这些必须在系统边缘转化为 `SessionSnapshot` 强类型前完成。

### 三、消灭可变状态与副作用偷渡

**[Shell/FuzzySearch.fs]**

44. 删除 `TypedIteratorStore` 内部使用的 `Dictionary` 和 `mutable counter`。这是一个并发漏洞也是非纯函数。
45. 将迭代器存储改为 Actor(通过现有的 `SerialQueue`)管理隔离的可变性,并对外只暴露异步命令接口。

**[Opencode/HookExecute.fs]**

46. 极端恶劣点:删除 `stripMimocodeTaskArgsForExecute` 中的 `Dyn.deleteKey args "completedWorkReport"`。
47. 删除 `rewriteMimocodeApplyPatchArgsForExecute` 和 `restoreMimocodeTaskArgsAfterExecute` 中反复依靠 `setKey` / `deleteKey` 原地涂改入参的操作。
48. 修改挂钩设计:Hook 必须接收 `InputRecord`,并返回 `Result<InputRecord, Error>`。如果需要剥除某些任务参数,应该是创建并传递一个剥除了相关字段的新 Record 给下家,而不是对宿主传入的引用原地擦除再还原。这是"副作用不在函数体偷跑"铁律的核心。

**[Opencode/WikiRuntime.fs]**

51. 删除 `WikiActor` 中对 `Dictionary<string, SerialQueue>` 的封装。
52. 删除 `sessionSnapshots`,`jobContexts`,`bookkeeperLaunches` 等混杂在同一个 class 里的 Dictionary,重构为单一职责、不可变数据的 `WikiState`。
53. 把处理队列和存储分离:外壳层负责维持 `Agent<Message>` 的生命周期,内核的 reducer 纯函数接受命令并返回下一状态和事件集。不再让 `JS.Promise` 在业务状态更新的过程中悬挂。

### 四、工具配置、路由与层级清理

**[Kernel/ToolCatalog.fs]**

54. 删除针对不同工具参数分别使用 `Map.ofList` 配置的 `paramDocs` 字典。
55. 删除 `Params` 模块下长达数十行的 `let coderIntents = doc "coder" "intents"`。这属于纯手工样板代码。
56. 将 ToolSpec 从字典定义改为强类型的接口/记录结构,用 F# 的特性自动反射或利用源生成宏提取参数说明。让元数据紧贴类型。

**[Opencode/ToolSchema.fs]**

57. 删除通过 `call1 schema ...` 大量封装的 Zod Schema 桥接层。
58. 这是 "两段像,只说明此刻长得像" 的反面教材。既然已经有领域记录类型,应当直接在入口处用一套解码器完成 Zod 级别的数据检查,而向宿主声明 Schema 可以由该记录统一生成,不再人工维护两套参数结构(Catalog 里一套,Zod 里一套)。

**[Mux/AiSettings.fs]**

59. 删除 `normalizeStr`, `modelFromEntry`, `thinkingFromEntry` 这种手动防卫代码。
60. 将嵌套配置树的解析映射给一套 JSON 解码组合子,将 fallback 和继承逻辑编写为对于强类型配置树的扁平合并操作 `Option.orElse`,消灭一切中间状态。

**[Mux/Delegate.fs] & [Opencode/SessionIo.fs]**

61. 删除 `createInput` 中手动为 `o?("kind") <- "agent"` 等属性的多次赋值。直接返回 F# 匿名记录 `{| kind = "agent"; prompt = ... |}`。
62. 删除 `extractToolContext` 里在 `firstString` 数组中找 `["directory";"cwd";"workspaceDir"]` 的猜测逻辑。应该使用标准化的解析器直接锁定合约规定的键。

**[Opencode/PluginCore.fs]**

63. 删除 `withRoleDefaultsFor` 和 `applyAgentConfigFor` 中复杂的配置字典深拷贝和回填(`mergeObj`, `setKey`)。
64. 把配置定义为一个封闭类型:具有继承关系的强类型 `AgentConfig`,直接使用 F# 的解构与默认值赋予构建完全合并的副本。
65. 删除 `commandExecuteBefore` 中的 `pushPart parts` 副作用:它清空了原来的数组然后 push 数据进去。应该返回一个命令(例如 `ReplaceParts [ ... ]`)交给系统的外侧来应用到 JS 原型上,保持拦截器为纯函数。

**[Opencode/Tools.fs]**

66. 删除 `mergeObjects`。
67. 删除这里对于 `Dyn.get args` 的第二轮解构(例如 `optInt args "numResults"`)。此时,工具参数应当在经过 Hook 时已经被反序列化为特定的 Intent 类型。这里只需做纯业务调度:`runSubagent(intent.numResults, ...)`。

**[Shell/FileSys.fs] & [Shell/WorkspaceFiles.fs]**

68. 将散乱在外层 Promise 流中的文件读写、路径合并逻辑压入极简的外壳模块。

### 五、具体改造实施准则摘要

72. **单文件超200行死**:诸如 `PluginCore.fs`、`Tools.fs`、`SubagentTools.fs` 由于混杂了 Schema 映射、路由定义和环境依赖,显得臃肿不堪。需将其分解为:`DomainTypes` (纯数据), `SchemaEncoder` (仅映射), `CommandHandlers` (纯业务状态更新), `HostAdapter` (只有 IO 副作用)。
73. **纯函数内核与外壳切分**:`WikiRuntime` 中的 `Submit`,需要提取成纯业务函数 `applyDrafts` (已存在),只留外壳通过 `fsPromises` 去应用 `AppendEntries`,彻底把 I/O 剔除出状态类。
74. **消灭万能分支**:查找所有的 `tryGet` 或者 `Dyn.isNullish` 然后赋兜底值(fallback),要求重新设计类型系统,如果某个属性缺失是合法的,直接标记为 `Option`,如果在该上下文中是非法的,则定义无该属性的新 DU 并在上下文之间设海关拒绝其流入,决不允许在底层随意赋空字符串。
75. **移除废弃接口样板**:所有形如 `System.Func<obj, obj, JS.Promise<obj>>` 的硬包装,必须收拢到唯一的一个底层 FFI 通道文件中。业务逻辑代码(如 `Hooks`, `Tools` 绑定)绝对不出现任何 JS 互操作相关关键字。
76. **消灭日志/异常的重定向**:类似 `UnknownJsError ex.Message` 这种泛化异常掩盖了失败的确切原因。将其分解为具体的 `IOFailure of Path * Reason`,使调用方通过模式匹配能对其进行精确响应或回放。
