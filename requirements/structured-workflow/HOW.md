# structured-workflow — HOW

`WHAT.md` 是唯一 normative 合同。本文只记录当前实现入口与迁移方法。

## 1. 当前结构入口

- `scripts/checks/subsystems.json`：迁移期唯一 legacy-owner → subsystem 映射。它只帮助尚未显式迁移的 shard 解析 subsystem，不定义 consumer ACL、exposure 或业务 law。
- `scripts/lib/compile-shards.mjs`：纯构建层。读取 fsproj、source、sibling `.fsi`、ProjectReference 与 aggregate union；不知道业务 owner/slice/exposure。
- `scripts/checks/subsystems.mjs`：唯一 subsystem 结构 release gate。验证 source 唯一归属、compile-shard DAG、aggregate 等价与显式 runtime-platform shard 的依赖倒置，并报告 subsystem SCC。
- `scripts/check.mjs`：只运行 `subsystems.mjs` 作为 source/subsystem/shard 的架构权威；旧 semantic-owner/owner-contract/owner-project gate 不再进入 release verdict。

旧 `WanxiangshuSemanticOwner`、`WanxiangshuOwnerLocality` 与 `WanxiangshuOwnerLocalityKind` 暂留在 fsproj，供未迁工具和历史测试读取。新 shard 同时声明 `WanxiangshuSubsystem`、`WanxiangshuCompileShard`；迁移结束后可机械删除旧字段，但删除本身不是架构收益。

## 2. 拆 compile shard 的固定方法

先按知识而不是文件大小分 cohort：

1. 列出当前 shard 的公开类型、纯 decision、effect/factory、runtime registry、codec 与测试 surface。
2. 按真实 consumer 需要分组；两组若 reason-to-change 不同或 audience 长期不同，拆 shard。
3. 最底层通用 primitive 必须无领域依赖；若需要领域 identity，说明它不是 platform primitive。
4. consumer 直接引用最窄 shard。禁止保留 umbrella reference 作为“保险”；编译失败用于发现遗漏依赖。
5. `.fs` 与 sibling `.fsi` 同迁，aggregate 顺序不变；full build 输入并集不变。
6. 用定向真实 Fable compile 验证 ProjectReference closure，而不是 aggregate 成功或源码 grep 冒充编译证明。

第一批样本：

```text
dispatch/identity            <- Foundation/Identity
chat-execution/outcome       <- Foundation/Outcome + OutcomeSurface
dispatch/runtime-nudge       <- Interaction/Dispatch/Nudge
dispatch/prompt-metadata     <- MetadataCodec
runtime-platform/canonical   <- CanonicalJson + surface

runtime-platform/task-result <- TaskResult + FsToolkit compatibility
runtime-platform/parallel    <- bounded Parallel + surface
runtime-platform/async       <- AsyncSupport
delegation/fission-facts     <- Execution/Fission/Facts
```

`runtime-platform/*` 明确不依赖 domain subsystem。Fission facts 因为携带业务 identity，归 Delegation 而不是为了复用留在 TaskResult 大包。

## 3. subsystem 粒度与后续收敛

调整 subsystem 只看 semantic cohesion、历史 co-change、dependency direction、rewrite independence 与 boundary tax。compile shard 数量不参与 subsystem 数量裁决；下面的观测数量不是 quota。

Subsystem SCC 是首要结构债：双向依赖必须通过移动知识所有权、提取窄纯 contract、capability injection 或删除重复 decision 消解。严禁以 facade、service locator、event bus、shared DTO bucket 隐藏环。

后续优先继续处理 reverse closure 最大的公共重力井：`foundation-roles`、剩余 Identity cohorts、provider projection model，以及仍同时承载多种 reason-to-change 的 Host/Delegation/Persistence shard。

### 3.1 观测基线

2026-09-08，在上游提交 `58aa3d934` 运行 `node scripts/checks/subsystems.mjs`：26 subsystem、199 compile shards、702 production sources、1919 ProjectReference，shard DAG，最大 subsystem SCC 为 23 个节点。口径是 fsproj 声明及其聚合图，不是源码实际调用图或编译耗时测量。后续报告附对应代码版本和工作区差异，不把本次快照写成固定测试期望。

### 3.2 GAP-033 当前缺口与关闭证据

本节是 STRUCTURED-WORKFLOW-011 至 016 的实现与证明缺口记录，不新增产品条款，也不构成整表开工授权。仅处理用户任务明确覆盖的部分；相关 Worker 应显式读取本节，不依赖路径触发的自动接地。聚合状态见 `requirements/GAP.md` 的 GAP-033，保持 PARTIAL；单项修复不代表整体已可独立替换。

第 3.1 节保留历史测量口径；以下逐项记录当前实现和仍缺的证据，不用历史数量约束后续合法变化。

1. **知识依赖尚未收敛。** subsystem 聚合图仍有 SCC，公共重力井仍待拆解。按真实 consumer 收窄 contract／port，记录被切断的具体知识依赖与前后闭包；对受影响 shard 和 consumer 提供真实 focused Fable 编译及相关行为证明。SCC 数字下降不能独自关闭整体缺口。
2. **结构快照已替换为性质反例。** `subsystem-boundaries.test.mjs` 与 `owner-project-boundaries.test.mjs` 不再限制 subsystem 数量范围或要求 shard 多于 subsystem。前者通过真实临时 fsproj 验证合法增长、缺失／重复 source 归属、sibling 签名与 aggregate 输入完整性；`A1(alpha) → B1(beta) → A2(alpha)` 是合法 shard DAG，但必须准确报告 subsystem SCC，单向对照则无 SCC。测试标题与第 5 节的精确证明边保持不变。
3. **平台解析路径已有正反例，知识归属审计仍未完成。** `scripts/checks/subsystems.mjs` 已按解析后的 `subsystem` 检查全部平台 shard；`subsystem-boundaries.test.mjs` 对 explicit 与 legacy 映射路径分别证明平台依赖合法、域外依赖拒绝。它不证明所有标成平台的源码都没有领域知识，也不替代真实 focused compile；不得据此宣布整个平台隔离已完成。
4. **authority 源码身份已与 WHAT 包归属分离。** `authority-boundary.mjs` 通过 `buildSubsystemInventory` 取得唯一源码 subsystem；manifest 的 declaration、method、issuer `owner` 全部按实际文件归属迁移，`whatOwners` 继续指向 requirement package。legacy 字段只参与既有映射解析，不再作为第二个可接受身份；`scanRepo` 使用传入 repository root。`requirements/capability-enforcement/tests/authority-boundary.test.mjs::WHAT[ENF-014] explicit and legacy-mapped subsystems resolve through the real repository path` 用真实临时工程证明无 legacy 字段与 legacy 映射的合法路径，并拒绝旧 alias、错误源归属及错误 WHAT 包归属。能力发行、一次性消费和持久化检查没有放宽。

2026-09-09，在 merge commit `2c6088fa8` 上叠加本批修改：相关结构、impact 与 authority 测试 40/40 通过；补全实际 consumer 后，subsystem gate 报 26 subsystem、207 shard、702 source、1874 references、最大 SCC 20。相比本批中途的 18，SCC 增长来自显式恢复 Host→Casebook 等已存在的代码依赖，不能把先前漏报当作隔离成果，也不能宣布本 GAP 已关闭。authority cutover 的临时对照使用同一迁移后 manifest，旧 gate 得到 59 个错误，新 gate 零错误；临时模块已删除。

随后恢复 `ToolRuntimeScope → change-integration.git-integrationgate` 已被动态加载隐藏的实际依赖，静态构造 Host 并保留 callback 与 snapshot 类型。scope shard 的 942-source focused Fable compile 通过；subsystem gate 更新为 1875 references，其余上述计数不变，shard 图仍无环。这修复了真实 Long Stroke 首次 publication 的异步调用错误，不代表 subsystem 知识耦合已经消除。

在 `40692d3dc` 上继续迁移 `ToolRuntimeScope` 的 Relay 查询：两个 `emitJsExpr` 布局探针改用既有 `Fold.view` public contract，退休 fence 与 `.fsi` 改为 `IncumbencyId`，并显式引用 `relay-incumbency.mission-relay-core`。真实 `SuicideTool` consumer 连同 scope 的 948-source focused Fable compile 通过，结构／impact 测试 24/24、既有退休／投影／Manager plugin 测试 26/26 通过。当前声明图为 1876 references，其余上述计数不变；本批切断的是对 Relay 内部 JS 表示的知识依赖，不宣称编译闭包或 SCC 缩小。未新增依赖编译器内部表示的测试，也不把已有 pure fence 测试描述为 scope fence 全分支证明；GAP-033 仍为 PARTIAL。

资源校验接缝恢复静态 `EnforcerCatalog.validate` 后，`Wanxiangshu.Owner.cognitive-environment.resources-promptsurface.fsproj` 的真实 focused Fable compile 通过（90 source）；新产物中英文各装载 120 条，移走 scratch 校验器模块时新 Node 进程以 `ERR_MODULE_NOT_FOUND` 失败，随后恢复并清理临时资源链接。随后修复 `enforcer-codec` consumer 的真实闭包：删除无用 namespace 引入，恢复纯 `EnforcementProjection` 静态依赖与 root-workspace contract；将 `SessionNudge`／repair port 从 ingress 大分片移到 `dispatch/session-nudge`，使 Companion 无需反向引用包含自身的 ingress 闭包。原 516-source 编译失败已在 522-source focused Fable compile 中通过；源码总数与 aggregate 顺序不变，没有用动态加载回捞依赖。合入的格式问题由正式 Fantomas 工具修正，整体验收仍使用 `npm run format-build-test`，局部编译不代替该入口。

独立编译原 ingress consumer 又暴露了被 enforcer-codec 根项目遮住的 Companion→repair 依赖。`enforcer/repair` 现单独编译既有 codec、cycle model/decode、repair 和 Blogger probe，Companion 与 enforcer-codec 均静态引用它，不再依赖偶然被聚合根纳入的源码；Host signal adapter 也显式引用实际调用的 `ToolResultBound` 窄合同。`Wanxiangshu.Owner.dispatch-protocol.interaction-dispatch-opencode-ingresscodec.fsproj` 的 564-source focused compile 已通过。这些修复恢复真实声明依赖，references 增长是如实表达知识，不是架构退步的自动判据。

`LanguageSurface` 的 Bookkeeper 资源读取仍需要 `PromptResources`，因此恢复其静态资源引用；真实 Host transform 从 bootstrap 分片移到 `host/provider-system-transform`，由语言验证 Surface 与 bootstrap 共用，公开 API 和源码路径不变。bootstrap 对 `BookkeeperRuntime`／`CasebookLifecycle` 的实际调用分别通过现有 casebook-model／casebook-bookkeeper 分片声明，不再依靠 aggregate 偶然补齐。语言 Surface 与原 bootstrap consumer 各使用自己的 focused compile 入口验证，不能仅用全量构建证明这两条闭包。

旧 M6 计划仅用于追溯退役原因；其 extractor、worksheet、ACL 与 snapshot 清单不形成关闭本节缺口的条件。ENF-015、ENF-016 等独立产品证明缺口仍由所属包 HOW 记录，不因 GAP-031 路线退役或本节记录完成而关闭。

在 `9b56d8ce2` 上继续恢复两条 typed 边界：Coder 静态调用既有 WarmStart 合同，保留查询级 fail-open 而去掉 adapter 的动态加载与 catch-all；Blogger 将唯一 context 构造及其两个纯 helper 收回 MainContext，删除旧 Enforcer Host 与 Recovery 中已无消费者的副本。`coverageBirth` Surface 改用真实 trace fold 和出生门，无法映射的正序列反例旧败新胜；相关出生门、WarmStart 和 JoinGuard 等测试 15/15 通过，不将出生门证据冒充 writer 提交前校验证据。

本批 Coder focused compile 又暴露 `JoinGuardSurface → DispatchSurface` 的漏报依赖。证明 adapter 从运行时分片移入独立 `delegation/join-guard-surface`，显式引用原运行时和 DispatchSurface 所属分片，未复制 Host port adapter，aggregate 源码顺序不变。Coder、JoinGuard Surface、Enforcer continuation 的真实 focused Fable compile 分别通过 982、972、958-source 闭包；声明图为 26 subsystem、208 shard、701 production source、1879 references，shard DAG，最大 subsystem SCC 为 21。SCC 增长如实反映恢复的依赖，不是闭包收敛成果；GAP-033 保持 PARTIAL。

在 `11a221797` 上继续恢复 Fork → WarmStart 与 Prefix → WorkRecord 两条实际依赖。Fork 直接调用既有 typed append 合同，不再解码编译器 union 或吞掉 adapter 异常；Prefix 静态调用唯一纯 materializer，删除动态加载与备用 Chronicle 渲染器。两个真实 consumer shard 的 focused Fable compile 分别通过 980、952-source 闭包，相关行为测试 50/50 通过；声明图为 26 subsystem、208 shard、701 production source、1881 references，shard DAG，最大 subsystem SCC 21。新增引用公开原来隐藏的知识依赖，并不代表 SCC 已缩小；GAP-033 仍为 PARTIAL。删除只能匹配源码注释的 Opening 伪证明，保留 canonical renderer 与真实 writeback 行为证明；完整 frozen 物化及 Fork adapter fault／取消的直接证明限制分别记录于所属 HOW。

在 `21033d90c` 上恢复 Host 四个 consumer 对 `SessionExecutionBinding` 的 typed 调用，以及 SyncDelegate Surface 对真实 Inspector tool 的 typed 构造／调用。移除动态加载、手写 union、缺失模块时的 no-op 和虚假零值；provider 合同与公开 Surface 签名不变。真实 consumer 的影响集合经既有 `compile-impact` 求并集，focused Fable 编译通过 1068 parsed sources／1030 compile items；首次编译暴露并清除了 `JoinSurface` 未使用的旧 Manager namespace 引入，没有用额外引用掩盖问题。新全量产物上的绑定、chat.params、pre-provider settlement、Inspector finalize 与 SyncDelegate lifecycle 测试 38/38 通过。声明图为 26 subsystem、208 shard、701 production source、1885 references，shard DAG，最大 subsystem SCC 21；这些新增引用公开实际知识依赖，不代表 SCC 收敛或 GAP-033 完成。

在 `173887ea9` 上恢复 `ToolRegistry` 的 typed 工具注册及 `PluginHooks` 的 Casebook 接线，同时将唯一 persistence 调用方 `PluginHostInterop` 与 registry 签名迁移到既有 `IJsTransactionPersistence`。删除 foreign module loader、备用权限表、静默漏注册及无类型 factory；provider 合同不变。首次真实 focused compile 暴露 `ExecutorTool` 对 `DistillationRuntime`／`Distillation` 的缺失引用，补回实际 provider 声明后，包含签名反向消费者的影响并集通过 1284 parsed sources／1246 compile items（fingerprint `879e5ddd19b7`）。新全量产物上的工具权限、Casebook、JS Host／事务测试 44/44 通过。声明图为 26 subsystem、208 shard、701 production source、1896 references，shard DAG，最大 subsystem SCC 从 21 增至 22；这是如实暴露既有知识依赖，不是隔离收敛。GAP-033 保持 PARTIAL，完整 hook／composition 的局部分支证明限制记录于所属 HOW。

在 `bd2e9563e` 上恢复 `HostSignalBootstrap` 对 `LoopSensor` 的 typed 构造、装配及 reset／observe／drop 消费，复用 owner 的 continuation 资源映射。真实插件回归先复现 managed child 无物理中断，再确认恢复 exactly-once interruption；不再以注释里的 `LoopSensor.create` token 证明接线。focused Fable 并集通过 1144 parsed sources／1106 items（fingerprint `641bd4fec085`），新全量产物上的 bootstrap 与 sensor 行为测试 19/19 通过。声明图为 26 subsystem、208 shard、701 source、1897 references，shard DAG，最大 subsystem SCC 22；新增引用公开真实依赖，GAP-033 仍为 PARTIAL。

该批首轮 ladder 的 3975 个语义测试及全部 integration suites 通过，但唯一 Long Stroke 在 `manager-interrupt.1` 后等待 `coder.3` 超时（保留世界 `/tmp/oc-e2e-yqJFT1`）。保留证据仅证明 Coder 下一步未进入 provider stream，不能证明其进入了 messages.transform，也不能据此断定容量等待。完整 stderr 的阶段观测实验通过（441 journal／2142 SSE），没有同 fence 重入或 capacity waiter；探针已移除，该实验不替代无探针正式验收。

另一保留世界 `/tmp/oc-e2e-CEqj6W` 的 `seal-undeclared` 已通过 frozen blob 的 wire digest 对应及确定性旧败新胜回归定位：成功后仍保留的 retry row 重新触发 prefix candidate 选择。`XWire.mayProbe` 现在读取成功结算后的连续失败计数，复用已有 frozen plan，零失败只投影 committed epoch，不再构造候选。修复与证据边界见 `context-compression/HOW.md`；focused consumer 并集通过 1286 parsed sources／1248 items（fingerprint `e532369f51e2`），相关测试 38/38。未修改 watchdog、event ceilings 或 seal 声明，原 Coder 停顿仍不归因于该 prefix 缺陷。

在 `47e2d4666` 上恢复 RequirementGroundingTransform → PairProgrammingThoughtTransform 与 PromptResources → ProviderResources 的静态 typed 调用，删除缺失模块时的伪造成功、空资源及跳过校验路径。两个 owning shard 显式声明原有知识依赖，公开签名不变；包含真实 PluginHooks、ManagedAgentConfig、Grounding／Language／Prompt Surface consumer 的 focused Fable 并集通过 1288 parsed sources／1250 items（fingerprint `9cb200792b8c`）。相关投影、资源与语言行为检查通过，结构／impact 测试 14/14；规范重放测试不再比较空 synthetic 列表，而是证明冻结终端结果、journal 重开不重新读取文件以及新 digest 仅追加。删除入口 token 伪证明，物理终止与 OS crash 的证明限制见 requirement-grounding/HOW。声明图为 26 subsystem、208 shard、701 source、1899 references，shard DAG，最大 subsystem SCC 22；新增引用公开隐藏依赖，不代表耦合消除，GAP-033 保持 PARTIAL。

在 `9dbdbf2a8` 上恢复 CanonicalIntegrator 的 Casebook／JsTransaction oracle、GroundingCatalog 的 glob 匹配及 RequirementGroundingRepositorySurface 的生成器／工作流静态调用。删除 foreign runtime loader、编译器 union tag 解码及缺失模块 fallback；只扩大既有 `JsGeneratorSurface.typedRole` 的可见性，`typedFor` 仍为 internal，不复制生成器或装配逻辑。接地 Surface 保留原失败词汇，业务注册、cut/reset、提交与观察语义不变。首轮 focused compile 拒绝事务嵌套 union 模式的缺括号，修正后包含签名反向 consumer 与真实持久化／Host／Delegation consumer 的并集通过 1360 parsed sources／1322 items（fingerprint `2444894e6950`）；新全量产物上的 grounding、事务、canonical Current／重放及结构／impact 测试 44/44 通过。生产工作流 smoke 实际拒绝无效程序与程序异常，分别保持 `invalid_program`、`program_failed`；临时探针已删除。声明图为 26 subsystem、208 shard、701 source、1905 references，shard DAG，最大 subsystem SCC 22。新增六条引用公开既有依赖，不代表知识耦合消除；未新增 Casebook／JsTransaction decoder fault 到物理 fatal 的专门证明，GAP-033 保持 PARTIAL。

在 `1aaf1c43e` 上继续恢复 Prefix Wire 与 Fallback Workflow 的 typed 查询。前者通过既有 `StrengthRuntime.TryFindByReplica` 取得 `StrengthReplicaBinding`，直接比较 Role 与 capability Set，删除私有字典路径、擦除字段与 catch-to-None；后者调用唯一 `SessionAssociationProjection`，删除 shadow module 的 Map 扫描与手写 union tag。两个 owning shard 分别显式引用既有 Strength runtime 和 Session association 合同，公开签名不变。包含真实 `PluginTransforms` 与 `OrdinaryTurnWorkflow` consumer 的 focused Fable 并集通过 1284 parsed sources／1246 items（fingerprint `ba15998515e3`）；新全量产物上的 association、Replica registry／transform／工具门禁、XWire decision、Blogger runtime、retry policy 与结构／impact 测试 105/105 通过。声明图为 26 subsystem、208 shard、701 source、1907 references，shard DAG，最大 subsystem SCC 22；两条引用公开原有依赖，不代表 SCC 或耦合已经收敛。未保留只匹配源码 token 的伪回归：上述 owner／decision Surface 测试不等于直接执行 Wire 的 Replica Authority 分支，也不覆盖 Workflow 的 association 材料等待分支；这两处专门 consumer 行为证明仍缺，GAP-033 保持 PARTIAL。

在 `f40d0b63f` 上移除 `ToolHostSurface` 对 `HostSchema` 的 `.value/.fields[0]` 表示探针，改由既有 `ToolHostCodec` 在同一 shard 内发布 internal typed 解包；私有构造器、公开 Surface 签名与工具注册策略不变。原生 literal schema 在旧 Surface 被错误解成字符串，正式回归先以 `schema.parse is not a function` 失败，再随 typed 解包通过；删除迎合二次解包的虚构 schema fixture，用真实 SDK validator 证明接受／拒绝与 optionality。签名影响及其反向 consumer 的 focused Fable 并集通过 1418 parsed sources／1380 items（fingerprint `9557abcf695f`），新全量产物上的 codec、结构与 impact 测试 40/40 通过。声明图仍为 26 subsystem、208 shard、701 source、1907 references，shard DAG，最大 subsystem SCC 22；没有以表示依赖清理宣称 subsystem 已可独立替换，GAP-033 保持 PARTIAL。

在 `8cddee173` 上将 CompletionMailbox、Change VerdictMailbox 与 HostForkJoin 的 journal／fission 竞争结果改为 typed `Choice`，删除手写 `{ kind, reason }` 协议；不新增调度运行时，不改变单次 `.then`、注册顺序、drain-first 或中断语义。首轮格式检查拒绝未括号化的 `let!` 类型模式，修正后真实 focused Fable 并集通过 950 parsed sources／912 items（fingerprint `21025f5f1239`），新全量产物上的 verdict／join／causal wait 测试 35/35 通过。新增 Change Surface 直接观察真实 VerdictMailbox，证明竞争优先级、旧 waiter 移除及有界 FIFO；公开 mailbox／join 生产签名不变，仅扩充既有证明 Surface。无 journal join probe 不证明 journal／fission 全分支，具体证据边界见 delegation/HOW。声明图仍为 26 subsystem、208 shard、701 source、1907 references，shard DAG，最大 subsystem SCC 22；本批只是类型安全收敛，不是 subsystem 知识依赖减少，GAP-033 保持 PARTIAL。

在 `d087240ac` 上将现有 `Host/Digest.fs/.fsi` 从混有 OpenCode 类型的 host-digest 分片抽到零 ProjectReference 的 `runtime-platform/digest`，公开摘要 API 与 aggregate 顺序不变。原 Host 分片保留全部消息／事件合同并引用原语；Casebook 和 Sphinx 直接声明窄原语引用，删除各自重复的字符串 crypto 实现及 Sphinx 旧导出的全部调用方，保留 canonical JSON、身份输入与 null 语义。二进制 SHA-1／SHA-256 不属于本次统一范围。focused Fable 的签名反向 consumer 并集通过 1402 parsed sources／1364 items（fingerprint `dbdfc4eb9b9d`）；新全量产物上的捕获／重放、Sphinx 身份／承诺／golden 与结构测试 41/41 通过，真实 Capture Surface smoke 另覆盖空串、Unicode、CRLF、NUL、孤立 surrogate 与 null。删除只执行测试内 crypto、却误称 HOST-BOUNDARY-019 生产证明的用例；该删除不宣称补齐 Host canary 缺口。声明图为 26 subsystem、209 shard、701 source、1910 references，shard DAG，最大 subsystem SCC 22。新增原语确实不包含 Host 类型或领域知识，但原宽 Host 消费者未迁移，不宣称其闭包缩小或 GAP-033 完成。

在 `686f3a9c4` 上将 Institutional Learning 事实／Enhancer 分片的摘要引用从宽 Host 分片迁到既有 `runtime-platform/digest`。完整分片只需要 Identity、Enforcer catalog 与字符串摘要；不修改源码、签名或 aggregate，不以动态加载补回 Host。正式 planner 的 forward closure 从 7 项目／38 个 `.fs/.fsi` 输入降至 4 项目／16 个输入，移除 OpenCode Message／OpencodeTypes／EventContract 及其 Outcome／Roles 传递输入；真实 focused Fable 编译通过 54 parsed sources（fingerprint `a731717f814a`）。声明图仍为 26 subsystem、209 shard、701 source、1910 references，shard DAG，最大 subsystem SCC 22。此次证明的是这一 consumer 的闭包收窄，不代表其余宽 Host consumer 已迁移或 GAP-033 完成。

在 `be3054fab` 上并行核对 Grounding model 与 Relay workspace snapshot 的真实源码需求，分别将宽 Host 摘要引用替换为既有 `runtime-platform/digest`，无源码、签名或 aggregate 修改。按声明 ProjectReference 递归闭包计数，Grounding 从 8 项目／60 个 `.fs/.fsi` 输入降至 6／50，Relay snapshot 从 8／38 降至 4／14；前者仍保留 JS capability 所需的 Roles／OfficeCapability，后者仍保留 GitSubject 与 Relay core。两分片独立 Fable 编译分别通过 88、52 parsed sources（`91e59f8a1e03`、`068168b18cec`）；真实 Grounding Runtime consumer 通过 818／780，四条 Relay capture consumer 的并集通过 1284／1246（parsed sources／compile items；`9956ed957c82`、`6d4bccc9b9ff`）。编译按共享产物纪律串行，新隔离产物另实际执行规范材料与 Git snapshot 摘要 smoke，范围见所属 HOW。不将声明闭包计数当作耗时实验，也不以局部 consumer 收窄关闭 GAP-033。

在 `bd99d71e7` 上并行核对 Change 事实分片和 Host message codec。仅前者的宽 Host 引用可替换为既有 `runtime-platform/digest`：六个输入中只有 `RuntimePath` 需要字符串摘要，声明递归闭包从 29 项目／158 个 `.fs/.fsi` 输入降至 27／148；独立 Fable 编译通过 186 parsed sources（`7711a827c1d3`），三个既有签名的保守反向消费者并集通过 1432／1394（parsed sources／compile items，`6f04afa1af0a`）。新隔离 RuntimePath 产物的真实 Git 与非 Git 摘要 smoke 范围见 change-integration/HOW。Host message codec 实际依赖宽分片拥有的 `MessagePart`，不使用摘要，因此保留声明依赖，不按项目名批量替换。源码、签名和 aggregate 均不变；本批不证明全局 SCC 收敛或 GAP-033 完成。

在 `32f66d952` 上并行核对 Provider wire decoder 与 Git hook 的全部源码／签名，分别将宽 Host 引用收窄到既有 `runtime-platform/digest`。两者只需媒体 URL 摘要与 common-dir socket key，不消费 OpenCode 消息／事件类型；保留其余真实引用，源码、签名与 aggregate 均不变。声明递归闭包分别从 10 项目／48 个 `.fs/.fsi` 输入降至 7／26、从 51／250 降至 49／240；独立 Fable 编译分别通过 64、278 parsed sources（`3b3604da2356`、`f39c6d4c6b7b`）。先验证 decoder 的反向 consumer 再修改 hook 引用，最终两分片签名反向消费者的 flat 并集通过 1418 parsed sources／1380 items（`91bced9bf768`），包含真实 decoder 与 HostSignalBootstrap 消费路径。新隔离产物的媒体投影和临时 Git hook 安装 smoke 范围分别记录于 provider-projection/HOW 与 durable-convergence/HOW。上述计数不代表编译耗时改善或全局 SCC 收敛，GAP-033 仍为 PARTIAL。

在 `e5794c8f4` 上并行审查 Companion、Delegation recovery 与 shared-state 三个分片。前两者分别实际消费 `IEventObservationPort`／终端事件词汇和 `MessagePart.Text`，保留宽 Host 引用；三处机械替换摘要引用均不会缩小声明闭包，不作为隔离成果。shared-state 中的 OpenCode `GitTree.create`／`GitTreePort` 已无调用方，因此删除退役适配器、类型及当前编译／适用入口，保留 EventStore 同名模块和 Relay snapshot。

该批首次 shared-state 独立编译暴露 `IJoinAttemptRegistry`／构造器漏报依赖及失效 namespace 引入。将既有 registry 的 sibling 源码移入窄 `delegation/join-attempt-registry` 分片，原 recovery 与 shared-state 显式消费它；只依赖 Identity、causal-wait contract、AsyncSupport，不以 recovery umbrella 或动态加载补回类型。registry 与 repaired shared-state 独立 Fable 编译分别通过 46、578 parsed sources（`5612752d421a`、`cf8198d664c9`），GitSubject／registry／session scope／recovery scope 签名反向消费者并集通过 1430 parsed sources／1392 items（`f31b44cd590f`）。新隔离 shared-state smoke 范围见 host-boundary/HOW。

registry 声明闭包为 4 项目／8 输入；shared-state 从基准 87／538 变为 89／540，增长来自补齐真实 registry／wait contract，而非耦合收敛。结构 gate 为 26 subsystem、210 shard、700 source、1913 references，shard DAG，最大 SCC 22；相关 Host／Delegation／subsystem 结构测试 8/8 通过。删除死适配器与恢复可编译边界不代表全局 SCC 缩小或 subsystem 已可独立替换，GAP-033 保持 PARTIAL。

在 `8e9e6d9f1` 上将既有 `OpenCode/Host/Message.fs/.fsi` 从宽 Host 分片移入零 ProjectReference 的 `host/host-message-contract`，HostMessageCodec 直接引用它，宽 Host 分片仍引用唯一消息定义。未改消息 union、decoder 实现、公开签名或 aggregate 顺序。按同一 production compile-shard inventory 递归计数，codec 声明闭包从 7 项目／32 输入降至 3／8，移除 OpencodeTypes、EventContract、Digest 及其传递输入；消息合同自身为 1／2。首轮 subsystem gate 拒绝将 requirement package 名 `host-boundary` 当作 subsystem，修正为现行映射的 `host`，未新增 alias 或放宽 gate。

`host-boundary/tests/m6-slice-boundary.test.mjs` 的既有 codec audience 证明新增实际 transitive source closure 排除：临时恢复宽引用时以 `message codec must not acquire .../OpencodeTypes.fs` 失败，恢复窄引用后通过。codec 独立 Fable 编译通过 46 parsed sources；Message 与 HostMessageCodec 签名的真实反向消费者并集通过 1426 parsed sources／1388 items。新消费者产物的 `ProviderProjectionSurface.decodeHostParts` smoke 验证 Unicode／CRLF text、reasoning、pending/completed/error tool、canonical args、activity normalization 与未知／空白分段丢弃，不读取 Fable union 布局。它不替代真实 Host 时序 canary。

同批并行审查保留 SyncDelegate runtime 的 `TerminalCompletionListener`／`TerminalStop`／`TerminalOutcome` 依赖，以及 recovery model 的 `ReconciledTurn.Parts`／`OpencodeModel` 和 `QuiescencePermit` 合同；它们不能机械替换为摘要或消息合同。仅 codec 的无关知识闭包已切断，未宣称全局 SCC 缩小或 subsystem 已可独立替换，GAP-033 保持 PARTIAL。

在 `cd6ef0ded` 上将既有 `OpenCode/Codec/OpencodeTypes.fs/.fsi` 独立到零引用的 `host/host-opencode-types`，宽 Host 分片引用唯一 SDK 类型定义，`OpenCodeContract` 与 `strength-policy` 直接消费窄分片。两 consumer 只使用 `OpencodeModel`，不修改源码、签名或 aggregate 顺序。声明递归闭包分别从 7 项目／28 输入降至 5／22、55／338 降至 54／334；policy 仍保留其余真实域依赖及传递摘要原语，不宣称整个 policy 已纯化。

先验证端口隔离编译及实际反向 consumer，再迁移 policy。最终端口、policy 独立 Fable 编译分别通过 60、372 parsed sources（`49421f904449`、`0b1e1c80861a`）；三组 SDK／端口／routing 签名反向 consumer 的 flat 并集通过 1426 parsed sources／1388 items（`dde50e2f81ac`）。既有 HOST-BOUNDARY-026 闭包测试在端口旧引用和 policy 宽引用 mutation 下分别因 EventContract 被编入而失败，窄引用下通过。新产物的公开 SDK prompt projection smoke 范围见 execution-model-routing/HOW。

同批完整审查 guidance-tip：唯一直接宽 Host 符号是 `stableCallId` 的 `HostDigest.sha256Hex`，但 production inventory 实测其余引用仍保留宽闭包。基准 112 项目／770 输入，在 SDK 分片迁移后为 113／770，假设将 guidance 引用替换为 digest 仍为 113／770；因此本批保留，不将直接符号收窄误报为闭包收益。结构 gate 为 26 subsystem、212 shard、700 source、1915 references，shard DAG，最大 SCC 22；Host／subsystem 相关检查 13/13 通过。GAP-033 仍为 PARTIAL，局部闭包下降不证明全局 SCC 收敛或 subsystem 已可独立替换。

在 `26bfed9d0` 上继续审查终端事件 audience。既有 `host-digest` 分片只保留 EventContract 的 identity/outcome 引用，不再转接 SDK types、MessagePart 或 digest；SessionSnapshot 显式引用唯一消息合同。diagnostics 与 message visibility 没有终端知识，移除其引用。声明递归闭包（项目／输入）分别从 terminal 7／26、diagnostics 14／62、visibility 11／36、SyncDelegate 17／66、session contract 9／34 收到 4／20、10／54、5／12、14／60、8／32。

同批 signal adapter 的独立 Fable 编译拒绝既有漏报的 ExecutionFailure、ChatExecutionTerminalDisposition 与 RuntimePath。先补齐实际 provider 引用并独立编译通过，再继续合同收窄；只删除 adapter 与 snapshot sibling 中无用途的 ingress namespace 引入，没有改变业务或公开签名。修复后的 adapter 闭包从漏报的 12／60 增至 35／198，不能把旧缺失输入当作隔离成果；RuntimePath 仍需要 digest。六个相关分片最终独立 Fable 编译分别通过 58、92、50、98、70、236 parsed sources；EventContract／SessionSnapshot／ReliabilityDiagnostics／MessageVisibility 签名反向消费者合并为一次 flat compile，通过 1426 parsed sources／1388 items（`0e6946de9286`）。初次 snapshot 独立编译拒绝失效 namespace，删除无用 open 后通过，没有把 namespace 名重新变成宽引用。

既有 HOST-BOUNDARY-026 回归新增真实闭包性质：三个独立的 terminal 宽引用 mutant 与恢复 adapter 漏依赖的 mutant 均退出 1。新产物的 terminal replay／authority／dispose、diagnostic optional effect 与 visibility signal／deadline smoke 范围见 host-boundary/HOW。结构 gate 为 26 subsystem、212 shard、700 source、1914 references，shard DAG，最大 SCC 22；GAP-033 保持 PARTIAL，不以局部减少抵消漏依赖修复，也不宣称全局 SCC 收敛或编译耗时下降。

在 `26b439645` 上将既有 ToolHostCodec／ToolHostSurface 四个源码与签名输入转入显式 `host/host-tool-adapter` 分片，公开类型、函数、物理实现与 aggregate 顺序不变。完整审查十五个直接 consumer：十三个只直接使用工具知识，改引工具适配器；bootstrap 同时使用 HostIngressCodec 与信号／终端实现，显式引用两侧；SessionExecutionBinding 只使用 ExactProviderStartObservation，保留信号引用。所有其他工具符号消费者的传递闭包仍有唯一 provider，没有复制实现或运行时回捞。

声明递归闭包口径为项目／`.fs/.fsi` 输入。工具 audience 从原宽 adapter 的 35／198 收为 5／26；signal adapter 自身从 35／198 收为 33／186。Attention fold 59／356 → 56／332，Concern fold 58／352 → 55／328，InstitutionalLearning fold 61／362 → 59／340，filemutationtools 99／616 → 97／594，repository-programming runtime 100／628 → 98／606，BookkeeperTool 101／636 → 99／614，FetchTool 99／622 → 97／600；这七条路径不再编入 HostSignal、HostEventCodec、Events 与 SharedTerminalBus。其余直接迁移路径仍经真实依赖保留信号闭包：executor tools 143／986 → 144／986，action runtime 146／1004 → 147／1004，PTY tools 141／968 → 142／968，review 141／964 → 142／964，retirement 141／962 → 142／962，plugin composition 190／1244 → 191／1244，bootstrap 166／1118 → 167／1118。后七项只完成正确 provider cutover，不计源码闭包收益。

tool adapter、signal adapter、Attention consumer 与 repository-programming runtime 分别独立 Fable 编译通过 64、224、370、644 parsed sources；ToolHostCodec／HostEventCodec 签名的真实反向消费者合并为一次 flat compile，通过 1416 parsed sources／1378 items（`bafa41b8e0a7`）。新编译边界回归先在旧混装闭包失败，再分别拒绝工具、consumer 与信号侧重新耦合的三个引用 mutant。新产物的 SDK schema、exact identity、abort disposal、结果尾部界限与异常 smoke 范围见 host-boundary/HOW。结构 gate 为 26 subsystem、213 shard、700 source、1917 references，shard DAG，最大 SCC 22；局部闭包变小不等于全局 SCC 收敛、subsystem 可替换性已证明或实际编译耗时下降，GAP-033 保持 PARTIAL。

在 `8903c729c` 上核对 Host 现行规范时，发现 HOST-BOUNDARY-026 仍将摘要、消息、SDK 类型和工具注册写入旧宽 Host 边界，并将普通业务契约限制为两个旧合同。用户于 2026-09-10 明确批准同步合同；本批仅修订 WHAT/HOW 与导航，保留物理能力隔离及既有证明，不改变实现或以此关闭 GAP-033。另确认 `host-session-contract-closure.test.mjs` 仍依赖 legacy locality/kind、100/185 数量断言及 legacy owner 文件选择；这些检查尚未完全迁到 subsystem inventory，显式 Host 分片的归属仍由全仓 subsystem gate 证明。后续迁移必须保留真实闭包排除、必要 provider 与唯一归属反例，不能恢复旧 ACL 或仅删除测试取得绿色。

在 `a0a710fb2` 上完成上述 Host 验证迁移：闭包测试直接消费既有 compile-shard/subsystem inventory，不再维护 XML parser、legacy kind、数量预算或 legacy owner 文件集合。保留真实 provider 与 composition 引用保护；会话合同排除工具注册、信号订阅和终端总线，新增 Host 分片与摘要原语按实际 subsystem 核对。显式元数据替换旧声明的正例通过；会话误引工具、会话归属错误及显式工具分片归属错误的三个真实工程反例均被拒绝，原工程全部恢复。全仓唯一来源、签名与 aggregate 完整性仍由同一库存机制及既有结构反例承接。本批不改生产编译输入，不宣称新增编译隔离或全局 SCC 收敛，GAP-033 保持 PARTIAL。

在 `46a1c334c` 上将 `owner-project-boundaries.test.mjs` 的生产工程读取统一到既有 `checkSubsystems` 返回的库存，删除重复工程扫描、Compile／ProjectReference XML parser 与 GitGateway 的旧 locality 标签要求。GitGateway 归属核对现行 `change` subsystem，而不是 legacy owner `change-integration`；源码配对、必要／禁止引用和 NodeFs 物理边界仍受原测试保护。只删除库存已经覆盖的非空工程断言及无合同依据的项目数量下限；临时 planner 工程的 XML、顺序、缓存与失败生命周期证明不变，原测试标题及证明注册不变。恢复原字节的实验中，GitGateway 仅保留显式 subsystem／shard 元数据通过，错误归属到 persistence 被拒绝，FileMutationTools 缺失 NodeFs provider 被拒绝。相关 boundary／subsystem／impact 测试 24/24 通过；本批不修改生产工程，不以静态证明迁移宣称新 focused Fable 隔离或业务等价，GAP-033 保持 PARTIAL。

在 `f3479bdf6` 上将 `owner-impact-compile.test.mjs` 的六个临时工程改为显式 subsystem／compile-shard 元数据，去掉夹具的旧 owner／locality／kind 参数。全部分片同属 fixture subsystem，原有精确 source／root 集合断言仍要求实现变更不扩入反向 consumer、签名变更包含全部反向 consumer；合并编译、full fallback、缓存与 CLI 证明及其标题／注册不变。相关测试 24/24 通过；临时将实现变更扩成 reverse closure、将签名变更缩成 owning shard 的两个 planner mutant 均被既有断言拒绝，planner 原字节恢复。本批仅更新证明输入，没有改变生产 planner、工程或公开合同，不是新的真实 focused Fable 编译或知识依赖收敛证据，GAP-033 保持 PARTIAL。

在本次推进中，将 `event-store-compile-boundary.test.mjs` 与 `ctx-capacity-observation-forbidden.test.mjs` 统一迁移至 compile-shard／subsystem inventory。删除自建 readdirSync/XML regex 解析与 legacy kind、legacy owner 属性依赖；按 persistence／strength subsystem 与显式 compile shard 验证闭包排除、生产源码预算及禁止容量词汇。相关边界测试通过；非契约分片侵入契约闭包与错误 subsystem 的两个反例均被拒绝。同时，完成 `causal-wait/tests/m6-slice-boundary.test.mjs`、`time-capability/tests/m6-slice-boundary.test.mjs` 与 `host-boundary/tests/m6-slice-boundary.test.mjs` 向标准 `readCompileShardInventory` + `buildSubsystemInventory` 的迁移，移除了对旧 `readCompileShardInventoryV1` helper 及其 legacy locality/kind 属性的依赖，并彻底删除了该已退役 helper。随后推进 participant、sphinx、process 与 verification 四个子系统全部剩余分片声明显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使这四个子系统（含 `foundation-roles` 重力井）全部达成 100% 显式归属。进一步将 change（5分片）、strength（5分片）、resources（2分片）、output（2分片）与 repository-investigation（2分片）共计 15 个分片全部声明显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使这五个子系统达成 100% 显式归属。随后推进 repository-programming（3分片）、requirements（3分片）、dispatch（4分片）、knowledge（4分片）与 work（5分片）共计 19 个分片全部补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使这五个子系统同样达成 100% 显式归属。进一步推进 authority（7分片）、relay（8分片）、chat-execution（6分片）与 enforcer（7分片）共计 28 个分片全部补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使这四个子系统达成 100% 显式归属。紧接着推进 interaction（10分片）与 context（13分片）共计 23 个分片全部补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使 interaction 与 context 两个子系统同样达成 100% 显式归属。随后进一步推进 session-lifecycle（19分片）与 delegation（16分片）共计 35 个分片全部补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使 session-lifecycle 与 delegation 两个子系统达成 100% 显式归属。全仓显式分片数量提升至 162/213（占比 76%），全仓 26 个子系统中已有 23 个达成全显式归属。随后进一步推进 persistence（17分片）、provider（17分片）与 host（17分片）最后 51 个分片全部补齐显式 `<WanxiangshuSubsystem>` 与 `<WanxiangshuCompileShard>` 元数据，使全仓全部 26 个子系统、213 个编译分片达成 100% 显式归属（213/213，0 legacy-mapped 遗留）。随后审查 interaction 与 process 间的编译引用，发现 `opencode-tools-executortoolsurface` 仅拥有 ChronicleTool、BashHoneypotTool 与 AssumeTool 三个工具实现，源码及签名并未消费 PtyTool 或 ExecutorTool，但多声明了对 `process/opencode-tools-ptytool` 的引用。删除该无用引用后，该分片的前向闭包从 144 个项目缩减为 141 个项目；`opencode-tools-executortoolsurface` 独立 Fable 编译（1012 parsed sources）以及其消费者 `tool-runtime-surface` 独立 Fable 编译（1042 parsed sources）均通过。接着审查 persistence 与 change 间的编译引用，发现 `persistence/git-hook-sync`（`Wanxiangshu.Owner.durable-convergence.git-hook-sync.fsproj`）的实现与签名源码仅消费 GitGateway、EventStoreProcessEventLog 与 EventStoreSyncRuntime 等分片，并未消费 `change/change-fact`（ChangeFact、Projection、RuntimePath），但仍保留该跨 subsystem 引用。删除该无用引用后，`git-hook-sync` 独立 Fable 编译（278 parsed sources）及其真实消费者 `opencode-host-hostsignalbootstrap`（1156 parsed sources）与 `plugin-composition`（1282 parsed sources）均通过独立编译。全仓 ProjectReference 引用数降至 1915，相关结构与边界测试全部通过，全量编译与阶梯验收通过，GAP-033 保持 PARTIAL。

随后进一步系统审查跨 subsystem 的 ProjectReference 依赖，严格依据源码符号使用情况剔除冗余依赖：
1. `interaction/opencode-tools-executortoolsurface`（`Wanxiangshu.Owner.action-affordance.opencode-tools-executortoolsurface.fsproj`）：确认其三个工具源码（ChronicleTool、BashHoneypotTool、AssumeTool）均不消费 `host/host-session-contract`（QuiescencePermit、DegenerationKind、ILoopSensor 等），移除该跨 subsystem 引用。经独立 Fable 编译（1012 parsed sources）及其消费者 `action-affordance.runtime`（1042 parsed sources）编译验证通过。
2. `enforcer/enforcer-institutionallearning-fold`（`Wanxiangshu.Owner.institutional-learning.enforcer-institutionallearning-fold.fsproj`）：源码不使用 `host/host-signal-contract`（TerminalStop、TerminalOutcome、IEventObservationPort）与 `runtime-platform/bounded-parallel`，移除这两个冗余 ProjectReference。经独立 Fable 编译（372 parsed sources）及其直接消费者 `strength-persistence-durabilityport`（618 parsed sources）和 `opencode-host-hostsignalbootstrap`（1156 parsed sources）编译验证通过。
3. `knowledge/repository-knowledge-casebook-bookkeeper`（`Wanxiangshu.Owner.knowledge-reuse.repository-knowledge-casebook-bookkeeper.fsproj`）：源码不使用 `participant/foundation-roles`（Role/Roles）与 `host/host-session-contract`，移除这两个冗余 ProjectReference。经独立 Fable 编译（774 parsed sources）及其消费者 `repository-knowledge-casebook-lifecyclesurface`（780 parsed sources）与 `knowledge-reuse.runtime`（790 parsed sources）编译验证通过。

全仓 ProjectReference 引用总数进一步从 1915 降至 1910。全套静态架构与规范检查 `node scripts/check.mjs`、子系统门禁 `node scripts/checks/subsystems.mjs` 以及结构工作流测试全部保持绿色通过，GAP-033 保持 PARTIAL。

紧接着进一步深度审查 enforcer 子系统内部编译分片的跨 subsystem 冗余依赖：
1. `enforcer/enforcer-codec`（`Wanxiangshu.Owner.behavior-diagnosis.enforcer-codec.fsproj`）：经对 ChronicleExecution、Surface、Commit、Recovery、RepairSurface 与 ObservationSurface 的源码及签名完整审查，确认其未消费 `chat-execution/outcome`、`host/host-digest`、`authority/interaction-authority-fact`、`authority/interaction-authority-identityseed`、`authority/interaction-authority-ledger` 以及 `context/context-trace-cursor`。移除这 6 条冗余引用后，独立 Fable 聚焦编译（542 parsed sources）以及其消费者 `opencode-tools-executortoolsurface`（1012 parsed sources）与 `enforcer-continuation`（1010 parsed sources）均编译通过。
2. `enforcer/enforcer-continuation`（`Wanxiangshu.Owner.behavior-diagnosis.enforcer-continuation.fsproj`）：确认其源码与签名不使用 `persistence/composition-durable-projection`（CompositionDurableProjection 等），移除该跨 subsystem 引用。独立 Fable 编译（1010 parsed sources）通过。
3. `enforcer/repair`（`Wanxiangshu.Owner.behavior-diagnosis.enforcer-repair.fsproj`）：确认其源码与签名不使用 `host/host-digest`（HostDigest），移除该跨 subsystem 引用。独立 Fable 编译（410 parsed sources）以及消费者 `context-companion-companionfactfold`（530 parsed sources）编译通过。

全仓 ProjectReference 引用总数从 1910 降至 1902（减少 8 条冗余跨 subsystem 依赖）。全套静态架构检查 `node scripts/check.mjs` 与子系统门禁 `node scripts/checks/subsystems.mjs` 均保持绿色通过，GAP-033 保持 PARTIAL。

继续推进全仓真实跨 subsystem 知识依赖审查与裁剪，对 7 个编译分片中无符号消费的跨 subsystem 引用进行剪枝：
1. `resources/resources-promptsurface`（`Wanxiangshu.Owner.cognitive-environment.resources-promptsurface.fsproj`）：移除对 `runtime-platform/llm-facing` 的无用引用；
2. `context/context-companion-companionfactfold`（`Wanxiangshu.Owner.context-compression.context-companion-companionfactfold.fsproj`）：移除对 `chat-execution/outcome` 的无用引用；
3. `context/context-compression-runtime-surface`（`Wanxiangshu.Owner.context-compression.runtime.fsproj`）：移除对 `chat-execution/outcome` 的无用引用；
4. `delegation/delegation-fold`（`Wanxiangshu.Owner.delegation.execution-delegation-handle-surface.fsproj`）：移除对 `context/context-trace-cursor` 的无用引用；
5. `delegation/delegation-ledger`（`Wanxiangshu.Owner.delegation.execution-delegation-ledger.fsproj`）：移除对 `persistence/composition-durable-fact` 的无用引用；
6. `persistence/strength-persistence-durabilityport`（`Wanxiangshu.Owner.durable-events.strength-persistence-durabilityport.fsproj`）：移除对 `context/context-prefix-candidate` 的无用引用；
7. `provider/strength-policy`（`Wanxiangshu.Owner.execution-model-routing.strength-policy.fsproj`）：移除对 `chat-execution/outcome` 的无用引用。

上述 7 个分片及代表性消费分片（如 `delegation-ledger`、`delegation-recovery-runtime`、`enforcer-guidance-tip`、`persistence-eventstore-canonicalintegrator`、`opencode-host-modelroutingsurface`）均通过独立 Fable 聚焦编译验证。全仓 ProjectReference 引用总数从 1902 降至 1895（净减 7 条跨 subsystem 依赖），子系统门禁与架构检查全绿。

在此基础上，持续深入审查 22-subsystem SCC 内的跨 subsystem 知识依赖，剔除 6 条无真实符号消费的冗余引用，并补齐 2 处真实编译消费声明：
1. `delegation/execution-delegation-hostturnobservedsurface`（`Wanxiangshu.Owner.delegation.execution-delegation-hostturnobservedsurface.fsproj`）：移除对 `provider/opencode-host-opencodeport` 的冗余引用；
2. `host/host-diagnostics-runtime`（`Wanxiangshu.Owner.host-boundary.host-diagnostics-runtime.fsproj`）：移除对 `chat-execution/outcome` 的冗余引用；
3. `knowledge/repository-knowledge-casebook-lifecyclesurface`（`Wanxiangshu.Owner.knowledge-reuse.repository-knowledge-casebook-lifecyclesurface.fsproj`）：移除对 `host/host-signal-contract` 的冗余引用；
4. `work/mission-obligation-todo-magictodosemanticsurface`（`Wanxiangshu.Owner.obligation-ledger.mission-obligation-todo-magictodosemanticsurface.fsproj`）：移除对 `chat-execution/outcome` 的冗余引用；
5. `process/opencode-tools-ptytool`（`Wanxiangshu.Owner.process-execution.opencode-tools-ptytool.fsproj`）：移除对 `chat-execution/outcome` 的冗余引用；
6. `provider/participant-provider-attempt-planner`（`Wanxiangshu.Owner.provider-attempt-recovery.participant-provider-attempt-planner.fsproj`）：移除对 `chat-execution/outcome` 的冗余引用；
7. 显式补齐 `fetchtool` 对 `casebook-bookkeeper` 及 `sessionexecutionbinding` 对 `opencode-tools-managedagent` 的真实类型声明依赖，并清理 `ManagedAgentConfig.fs` 中未使用的 `Manager` 命名空间 open。

上述分片及其代表性消费分片（如 `join-guard-surface`、`plugin-composition`、`action-affordance.runtime`、`opencode-host-managedagentconfig`、`enforcer-repair`、`sessionexecutionbinding`）均通过独立 Fable 聚焦编译验证。全仓 ProjectReference 引用总数降至 1890，结构与架构门禁全绿通过。

### 3.3 语义词汇与证明义务注册

此表保留既有业务词汇 proof edge。第二列中的旧模块身份只用于定位已有源码，不恢复 owner 作为治理粒度。

| 词汇 | subsystem / legacy module / path | WHAT law | 允许的 trace relation | executable proof |
|---|---|---|---|---|
| `ManagerWorkflow.observe` | relay / Mission.Manager / Mission/Manager/Workflow.fs | `STRUCTURED-WORKFLOW-007` | one admission → one settled/no-effect outcome | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ManagerWorkflow.observeIdle` | relay / Mission.Manager / Mission/Manager/Workflow.fs | `STRUCTURED-WORKFLOW-007` | one idle observation → at most one encouragement | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `FallbackLedger.recordAuthorizedFailure` | provider / Participant.Provider / Participant/Provider/Attempt/Fallback/Ledger.fs | `STRUCTURED-WORKFLOW-007` | one policy licence + duplicate observation → one durable cursor advance | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| `ProviderRecoveryWorkflow.continueAfterConfirmedFailure` | provider / Participant.Provider / Participant/Provider/Attempt/Fallback/Workflow.fs | `STRUCTURED-WORKFLOW-008` | `R_fallback`: confirmed failure → bounded ordinary CE re-entry | `requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |
| `OrchestratorProgram.run` | change / Change / Change/Program.fs | `STRUCTURED-WORKFLOW-008` | `R_publish`: finite retry → one accepted or typed failed result | `requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative` |

### 3.3.1 词汇约束

这些 proof 约束业务 trace；subsystem 迁移不得通过改变测试标题、降低 multiplicity 或放宽 failure/cancel 语义取得绿色。

## 4. 编译与验证

日常结构验证：

```sh
node scripts/checks/subsystems.mjs
node --test requirements/structured-workflow/tests/subsystem-boundaries.test.mjs
```

涉及 shard ProjectReference 或 `.fsi` 时，先跑对应定向 compiler-boundary/impact tests，再运行：

```sh
node scripts/check.mjs
node scripts/build.mjs
```

禁止 `dotnet build`。aggregate full build 只证明最终完整输入可编译；缺失 ProjectReference 必须由定向 shard compile/canary 发现。

## 5. proof map

| WHAT | 当前 proof |
|---|---|
| STRUCTURED-WORKFLOW-001 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-001] FLOW_001_direct_task_workflow_is_allowed` |
| STRUCTURED-WORKFLOW-002 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-002] FLOW_006_second_runtime_patterns_are_rejected` |
| STRUCTURED-WORKFLOW-003 | `requirements/structured-workflow/tests/workflow-surface.test.mjs::WHAT[STRUCTURED-WORKFLOW-003] SW_002_workflow_modules_export_no_program_counter_shaped_names` |
| STRUCTURED-WORKFLOW-004 | `requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs::WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_nested_match_is_RED_at_the_inner_decision` |
| STRUCTURED-WORKFLOW-005 | `requirements/structured-workflow/tests/dsl-ownership.test.mjs::WHAT[STRUCTURED-WORKFLOW-005] DSL_OWNERSHIP_negative_mutable_goes_red` |
| STRUCTURED-WORKFLOW-006 | `requirements/structured-workflow/tests/direct-ce-contract.test.mjs::WHAT[STRUCTURED-WORKFLOW-006] FLOW_017_composition_keeps_domain_results_and_rejects_child_program_counters` |
| STRUCTURED-WORKFLOW-007 | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-007] every vocabulary binds owner_law_relation_and_executable_proof` |
| STRUCTURED-WORKFLOW-008 | `requirements/structured-workflow/tests/semantic-vocabulary.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary` |
| STRUCTURED-WORKFLOW-009 | `requirements/structured-workflow/tests/reconcile-program.test.mjs::WHAT[STRUCTURED-WORKFLOW-009] operator abort is a control-plane wake, never a business outcome` |
| STRUCTURED-WORKFLOW-010 | `requirements/structured-workflow/tests/parallel.test.mjs::WHAT[STRUCTURED-WORKFLOW-010] ARCH_009_results_follow_input_order_not_completion_order` |
| STRUCTURED-WORKFLOW-011 | `requirements/structured-workflow/tests/subsystem-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] subsystem is the only semantic governance identity`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] subsystem ownership and compile-shard graph are complete and acyclic` |
| STRUCTURED-WORKFLOW-012 | `requirements/structured-workflow/tests/owner-impact-compile.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] implementation changes exclude reverse consumers`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-012] flat Fable projection planner produces exact closure and canonical aggregate order` |
| STRUCTURED-WORKFLOW-013 | `requirements/structured-workflow/tests/subsystem-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-013] reusable platform shards depend on no domain subsystem`；`requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-013] GitGateway exposes a narrow dependency-inverted compiler boundary` |
| STRUCTURED-WORKFLOW-014 | `requirements/structured-workflow/tests/owner-project-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-014] NodeFs physical port and tool contracts have isolated compiler boundaries` |
| STRUCTURED-WORKFLOW-015 | `requirements/structured-workflow/tests/generated-module-relation.test.mjs::WHAT[STRUCTURED-WORKFLOW-015] generated artifact binds tracked inputs bytes lineage traversal and import` |
| STRUCTURED-WORKFLOW-016 | `requirements/structured-workflow/tests/subsystem-boundaries.test.mjs::WHAT[STRUCTURED-WORKFLOW-016] release architecture has one subsystem authority` |
