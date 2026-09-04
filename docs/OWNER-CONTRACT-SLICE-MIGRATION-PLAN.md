# Owner、Locality 与 Contract Slice 迁移方案

日期：2026-09-03

状态：M6.0–M6.3a已完成；2026-09-04独立复核发现的pure-oracle同源自证、test-only mirror、fail-open collection/reference/input closure与Host raw membrane反例已由R0永久测试关闭。旧v1 gate仍是唯一release authority；下一节点为M6.3b production extractor、fresh全集worksheet与旧gate下可独立绿色的contract/port split。全部live locality的terminal classification/adjudication、完整capability census与全量slice manifest完成后才可进入M6.3c/M6.4。`deriveAdjudicationCandidates`的key universe固定为fresh owner-project graph的全部locality；当前92个composition provider只是带`CompositionProvider` reason的pre-cutover子集，不是永久gate数量。

适用背景：Fable owner-project 编译边界、published contract 授权与 semantic owner 重整

## 简略介绍

当前 contract manifest 声称能够实施 exact symbol × exact consumer owner 授权；实际 gate 会把一个 consumer owner 的授权扩张到该 owner 的全部 project，Fable 又会合并 ProjectReference 的传递源码闭包。因此，manifest 的精度高于编译边界真正能兑现的精度。

本方案改用三层模型：

- owner 管 vocabulary、invariant 与业务决策责任；数量可在语义一致时适度减少。
- locality/project 管真实编译边界；数量适度增加，以缩小依赖闭包与增量编译范围。
- contract slice 管一组共同演化、共同授权的公开能力；provider locality 中全部 sibling `.fsi` exports 的并集是唯一公开符号清单。

授权绑定稳定的 locality，不绑定 owner 名称。每条跨 locality 依赖都必须经过 slice grant；owner 是否相同不参与授权判定。Fable 的真实有效 audience 按 ProjectReference 反向可达闭包计算；轻量 compiler-resolved analyzer 证明实际源码依赖没有逃出该闭包。高风险 effect implementation 只能由 composition 到达，普通 consumer 只依赖 port/capability。

当前 fresh census 为 49 个 owner、178 个 locality、711 个 production source、1,853 条 ProjectReference、784 份旧 contract record、4,421 条 actual cross-locality source edge；尚有 1 条 missing closure edge。数字随施工变化，只作计划快照。190–210 个 project、39–44 个 owner、增量影响下降 25%只作规划导向，不构成正确性定义。模拟显示，优先增加约 30 个高价值 slice，能消除理想化 project-level 模型中约 50.8% 的额外暴露；继续增加至 50 个的收益仅升至约 55.4%，边际收益明显下降。

这不是试点方案。迁移可以分批提交，但全部批次使用同一终态 schema；禁止长期并存新旧授权模型、兼容 facade 或临时 baseline。

## 1. 决策摘要

采用以下终态：

> owner 管语义；locality/project 管编译；contract slice 管能力授权；effective audience 服从 Fable 真实传递闭包。

不追求 owner 数等于 project 数。“一致”指五项事实一致：

1. 每个 production source 恰有一个 semantic owner。
2. 每个 source 恰由一个 locality 编译。
3. 每个跨 locality dependency 恰由一个 contract slice 授权。
4. compiler-resolved source dependency 是 ProjectReference closure 的子集。
5. manifest 所述允许 audience 与从真实图推导的实际 audience 一致。

老板已裁决授权最小单位为 `consumer locality → provider slice`，不再承诺 per-symbol/per-owner isolation。若未来重新要求 exact symbol × exact consumer 隔离，则必须扩展为保留 symbol identity 的 compiler-resolved analyzer，或改变构建方式以获得真正 assembly isolation。本方案新增的轻量 analyzer 只把 compiler-resolved declaration use 归约为 locality dependency，不恢复旧 symbol ACL、snapshot、delta 或 cache 管线。

## 2. 当前问题

### 2.1 Owner-level 授权扩张

当前 `allowedForeignReferences` 的逻辑为：

1. contract 声明 consumer owner。
2. gate 找到该 owner 的全部 project。
3. 全部 project 获得引用整个 provider project 的资格。

因此，一条局部 contract 会变成 owner-wide project authorization。owner 一旦合并，授权面还会继续扩大。

### 2.2 ProjectReference 不是传递防火墙

现有 compiler canary 已证明：

- Fable 会 source-merge ProjectReference closure。
- `internal` top-level module 不能形成跨 project 防火墙。
- `DisableTransitiveProjectReferences=true` 不能阻止 Fable 传递源码可见。
- module-local private binding 与 `.fsi` 未公开 symbol 才能形成真实隐藏。

真实可见关系为：

```text
provider ∈ transitiveClosure(consumer)
→ consumer 可以看见 provider 的 public .fsi surface
```

因此，只校验直接 ProjectReference 仍然会漏掉传递可见性。

### 2.3 Symbol manifest 与 `.fsi` 曾经冲突

GitGateway 是已经修复的局部案例。旧结构中：

- manifest 只授权 `converge` 与 `createDefaultRunner` 给 durable-convergence。
- 当时的 `Gateway.fsi` 同时公开 `SyncActiveEnv` 与 `discoverRemote`。
- consumer 进入 provider 的 Fable closure 后，真实可见性服从 `.fsi`，不服从 JSON symbol 数组。

当前工作树已经把 Gateway 抽成单 source `git-gateway` locality，删除 `SyncActiveEnv`，从 `.fsi` 隐藏 `discoverRemote`，并补入签名必需的 `GitGatewayRunner`。这证明 slice cut 能修正一个局部 sibling leak，但不能证明全仓 actual source dependency 服从 ProjectReference graph。

结论：`symbols`、`symbol_roots` 不能继续充当 consumer ACL。为避免双事实源，终态以 provider locality 全部 sibling `.fsi` exports 的并集为唯一 export inventory；manifest 只记录 locality-slice grant、WHAT laws 与 evidence relation。

### 2.4 Flattened aggregate 隐藏了未声明源码边

clean release 编译完整 flattened aggregate；它包含全部 production source。某个 consumer 即使没有通过 ProjectReference closure 到达 provider，仍可能在 aggregate 中成功引用 provider 的公开 symbol。

当前 production `owner-contracts` 传入空 `symbolUses`，因此无法看到这种真实源码依赖。project DAG 只证明声明图合法，不能证明源码服从声明图。

必须永久证明：

```text
actual compiler-resolved cross-locality source edges
⊆ declared ProjectReference transitive closure
```

本方案选择轻量 compiler-resolved locality dependency analyzer：读取编译器解析后的 declaration use，semantic edge 只保留 consumer/provider source + locality；symbol identity 与 line/column 仅进入本次 scan 的 ephemeral diagnostic side channel，scratch 随 invocation 删除。它不承担 per-symbol ACL，不保存 snapshot，不做 delta/cache 复用。

固定反例：consumer 引用 aggregate 中存在的公开 provider symbol，但 provider 不在 consumer 的 ProjectReference closure；aggregate compile 可以绿色，architecture gate 必须红色。

### 2.5 Same-owner 豁免会让 owner merge 绕过授权

若 gate 只检查 foreign edge，则两个 owner 合并后，原 foreign edge 会变成 same-owner edge并逃过 slice authorization。这与“owner merge 不扩权”直接矛盾。

终态规则：

```text
consumer locality != provider locality
→ dependency 必须满足 provider slice policy
```

owner identity 不参与授权判断。`private` locality 禁止任何其他 locality 引用；同 owner 内需要共享时也必须发布明确 slice。

## 3. 分析快照

以下数据由 2026-09-03 的 `codex/verification-closure-v3` 当前工作树重新计算。执行迁移时，正式 analyzer 必须每次生成 live census；数字只用于方案取舍，不进入 manifest，不作为永久 baseline。

| 指标 | 当前值 |
|---|---:|
| semantic owner | 49 |
| owner locality/project | 178 |
| production source | 711 |
| published contract 记录 | 784 |
| ProjectReference | 1,853 |
| 跨 owner ProjectReference | 1,614 |
| same-owner ProjectReference | 239 |
| actual cross-locality source edge | 4,421 |
| missing closure edge | 1 |
| 指向 contract kind 的 direct reference | 921 |
| 指向 composition kind 的 direct reference | 797 |
| 指向 adapter kind 的 direct reference | 54 |
| 指向 runtime kind 的 direct reference | 81 |

797 条指向 composition kind 的 direct reference说明 composition 标签被大量当作公共 provider使用；其中 consumer-kind矩阵与 capability facts必须逐项判断它们是 kind错标、公开 API与wiring混装，还是依赖方向错误。reference总数只用于 census，不产生授权。

### 3.1 两个极端

以下 project-count 模拟使用较早的 170-locality census，保留它只为展示粒度曲线。当前 live census 已是 178；正式执行必须由 analyzer 重算，不能直接套用表中总数。

若按每条 contract 的 exact consumer cohort 拆 project：

- project 约从 170 增至 640。
- 大量同一 source 仍需按 symbol 物理拆分。
- 工程数、manifest 与依赖边成本不可接受。

若按 source module 的 audience 拆 project：

- project 约增至 376。
- 相比 symbol ACL 仍有约 23% 的放宽来自同一 source 内不同 symbol cohort。
- 成本仍然过高。

结论：exact cohort 与 source file 都不应直接成为 project 粒度。slice 应围绕能力、authority class、依赖闭包与演化生命周期建立。

### 3.2 适量高价值拆分模拟

模拟将每个 project 内 contract surface 看作一个共同 audience，并优先对额外暴露收益最高的 project 做一次二分。结果：

| 新增 project | 总 project | 消除 project 内额外暴露 |
|---:|---:|---:|
| 10 | 180 | 32.0% |
| 20 | 190 | 44.9% |
| 30 | 200 | 50.8% |
| 40 | 210 | 53.7% |
| 50 | 220 | 55.4% |

30 个之后收益明显递减。规划导向：

- 目标区间：190–210。
- 默认中心：约 200。
- 220 可作为成本复核点，不是 correctness 上限。
- effect/bounded 违规必须归零；必要时可突破数量区间，但必须记录原因。
- 不缩小 authority、不缩小依赖闭包的拆分不得保留。

该模拟只衡量 slice 内部混装，不把 Fable 传递闭包误当作已经解决。

### 3.3 Fable 传递闭包估算

按当前 ProjectReference DAG 的反向可达闭包估算：

| 指标 | 数量 |
|---|---:|
| manifest 声明的 owner-symbol-consumer 关系 | 22,369 |
| 编译闭包可能到达的 owner-symbol 关系 | 129,433 |
| 可达但未出现在 exact consumer 声明中的关系 | 107,088 |
| 声明与物理可达的一致率 | 约 17.3% |

这是结构性可达估算，不等于真实调用或漏洞数量；它证明现有 manifest 的宣称精度远高于 Fable 编译边界能兑现的精度。

### 3.4 GitGateway 的已实现效果

GitGateway 已从历史 `git-integrationgate` 混合 project 迁入单 source `git-gateway` locality。当前图实测：

- 只有 `git-hook-sync` 一个 direct consumer。
- reverse closure 为 4 个 project、22 个 source。
- `.fsi` 只公开 `GitGatewayRunner`、`converge`、`createDefaultRunner`。
- owner-private `discoverRemote` 不再公开。

迁移前历史结构的 reverse closure 约为 32 个 project、161 个 source。该结果验证了 project split 可以同时缩小 sibling exposure 与增量编译闭包；它仍不能替代全仓 compiler-resolved source-edge analyzer。

## 4. 终态模型

### 4.1 Semantic owner

owner 只回答：

> 谁拥有这个 vocabulary、invariant、failure algebra 与业务决策？

一个 owner 可以拥有多个 requirement package 与多个 locality。owner 不再充当源码 ACL。

owner 合并必须同时满足：

1. vocabulary 相同。
2. invariant 由同一决策点维护。
3. failure algebra 与权限语境相同。
4. 业务变更生命周期相同。
5. 合并后仍能指出唯一 decision owner。

引用密度高、名称相似、经常一起修改，只能产生候选，不能单独证明应当合并。

### 4.2 Locality/project

locality 是稳定、全局唯一的编译身份。当前快照中 178 个 locality 已全局唯一，可直接作为授权主键。

owner 合并只改变 owner metadata，不改变 locality identity 或 ProjectReference graph：

```text
merge(ownerA, ownerB)
→ allowed locality edges 不变
```

若 project 文件名因旧 owner 名称而变得误导，应在对应 owner merge commit 中重命名并由编译器暴露全部遗漏；不得留下长期别名。

任何两个不同 locality 之间的 dependency 都受同一规则约束，包括 same-owner dependency。owner 合并只扩大语义治理范围，不扩大源码访问权限。

### 4.3 Compiler-resolved locality dependency

新增一个轻量 analyzer，输入：

- aggregate 的真实 production compile set。
- compiler-resolved declaration uses。
- source → locality 唯一映射。
- locality ProjectReference DAG。

输出去重后的 locality edge：

```text
{
  consumer_source,
  consumer_locality,
  provider_source,
  provider_locality
}
```

FCS symbol identity 与 line/column 只允许作为当前 scan 的 ephemeral diagnostic payload，用于定位已经映射出的 source-pair edge。edge identity、dedupe、authorization、census、property comparison 与 normalized projection 只使用 consumer/provider source + locality；manifest、snapshot、cache、baseline、adjudication record 均不得保存或消费 symbol/location。scan scratch 在本次 invocation 结束时删除，不恢复 per-symbol ACL。

必须验证：

```text
∀ actual edge C → P where C != P:
P ∈ projectReferenceClosure(C)
```

当前 analyzer 只有 fixture-project scan 与 fresh full-production scan，不存在 changed-locality production lane。pre-cutover live report 与 M6.4 release sink 都扫描完整 production compile set；禁止复用旧 FCS snapshot、delta、mtime cache 或人工 baseline。若未来有性能证据要求局部诊断，必须另立 node，定义 changed source/locality 输入与受影响 consumer/provider closure，并证明局部结果是 full scan 的保守投影；局部结果不得取得 release authority。

### 4.4 Locality capability facts

源码能力必须形成唯一、规范化、零遗漏的 production fact set。提取边界读取完整 production compile set 的 F# source、全部 sibling `.fsi`、FCS symbol use、Fable interop 与 exact generated runtime artifact；owner、locality kind、目标 exposure、grant、relation、旧 allowlist 与 authority annotation 全部只是待验证的 normative claim，不能增加、删除或降格 observation。

实现固定为两条production pure pipeline。capability pipeline由`enumerateCapabilityObservationsV1`枚举`C(W)`、`classifyCapabilityObservationV1`对单条observation作总函数分类、`extractObservedCapabilityFactsV1`验证完整partition、`validateCapabilityPolicyV1`把canonical facts与normative claims合取；JavaScript coverage pipeline由`enumerateJavaScriptAstNodesV1`与`visitJavaScriptNodeV1`证明`J(W)`全遍历。现有scanner只能贡献parsed primitive；任何按文件allowlist跳过扫描、只输出diagnostics或把annotation当exemption的路径都不能成为fact/coverage owner。

observation schema 固定为 version 1 的封闭代数：

```text
ObservationSiteV1 = {
  locality_id, source_path, semantic_declaration_anchor,
  same_anchor_occurrence_ordinal
}

RawCapabilityObservationV1 =
  | FSharpNode of node_kind * semantic_identity * ObservationSiteV1
  | FcsExternalSymbolUse of assembly * fully_qualified_symbol * ObservationSiteV1
  | FableImport of module_specifier * selector * generated_artifact_id option * ObservationSiteV1
  | FableEmit of expression * javascript_traversal_id option * ObservationSiteV1
  | EmitJsExpr of expression * javascript_traversal_id option * ObservationSiteV1
  | PublicSignatureExport of export_kind * declaration_identity * ObservationSiteV1
  | JavaScriptCapability of javascript_source_kind * source_id * generated_artifact_id option * JsCapabilityObservationV1 * ObservationSiteV1

JsCapabilityObservationV1 = {
  kind: StaticImport | DynamicImport | FreeGlobal | MemberRead | MemberWrite |
        Call | Construct | MutableBinding | Update,
  root, member_path,
  binding_provenance: Local | Imported | Free | Unresolved
}

GeneratedLinkageV1 = {
  import_specifier, package_import_target,
  generator_path, generator_entry,
  input_selector_path, input_selector_entry,
  build_path, build_entry
}

JavaScriptTraversalCoverageV1 = {
  id,
  source_kind: "fable-emit" | "emit-js-expr" | "generated-artifact",
  source_id,
  ast_node_count,
  visited_node_count,
  no_capability_node_count,
  capability_emitting_node_count,
  unknown_node_count,
  ast_node_set_digest,
  visit_partition_digest
}
```

`JavaScriptCapability.javascript_source_kind`只允许`fable-emit | emit-js-expr | generated-artifact`。`generated_artifact_id`当且仅当source kind为`generated-artifact`时非null并精确等于`source_id`；其他两类必须为null。visitor只产出closed `JsCapabilityObservationV1`，traversal validator依据显式source context包装成上述canonical raw observation；禁止把Emit source ID塞进artifact字段或另造第二JS fact case。
`binding_provenance`必须由同一AST scope resolver按node identity提供；visitor不得按`console/process/require`等root字符串猜free global。需要binding却缺resolver结果时只能产生`Unresolved`并分类为Unknown；只有显式`Local`且无独立capability语义的调用可成为NoCapability。
`PublicSignatureExport.export_kind`只允许`pure-type | pure-value | pure-function | capability-type`；前三类映射`PureRepresentation`，后一类映射`CapabilityTypeOnly`。其他值必须进入Unknown，不得由否定判断默认成pure。

每条 raw observation 恰有一个 disposition；`Classified` 内是多轴、多标签集合，不把同一 occurrence 强塞进互斥 physical case：

```text
CapabilityDispositionV1 =
  | Irrelevant of closed_rule_id
  | Classified of CapabilityLabelsV1
  | Unknown of UnknownClassV1 * syntax_kind * raw_identity

CapabilityLabelsV1 = {
  runtimes: RuntimeV1 set,
  authorities: AuthorityV1 set,
  mutable_resources: MutableResourceClassV1 set,
  semantic_classes: SemanticCapabilityClassV1 set
}

RuntimeV1 = FSharp | Node | Bun | Browser | GeneratedJavaScript | ExternalPackage
AuthorityV1 = Console | ProcessControl | Environment | FileSystem | Network | Clock | Randomness | Timer | Git | Provider | Host
MutableResourceClassV1 = TopLevelMutable | Registry | Waiter | TaskCompletionSource | RuntimeCell
SemanticCapabilityClassV1 = PureRepresentation | CapabilityTypeOnly | CapabilityValue | CapabilityFactory | EffectConstructor
UnknownClassV1 = UnsupportedAst | UnparsedInterop | DynamicTarget | UnclassifiedExternalSymbol | UnclassifiedCapability | IncompleteGeneratedLinkage

CanonicalCapabilityFactV1 = {
  observation_id,
  fact_id,
  observation: RawCapabilityObservationV1,
  disposition: CapabilityDispositionV1
}
```

DU 的 canonical JSON encoding 统一为 exact `{case, payload}`；`case` 使用上述 kebab-case 名称，`payload` 只允许该 case 声明的字段，unit payload 是 `{}`，option 只编码为 object 或 `null`。set 在 projection 阶段变为按 4.5 canonical text comparator 排序的 unique array。未知 case/key、空 `Classified`、重复 label 或非法 option 一律 RED。

capability observation universe与JavaScript AST traversal universe必须分开：

```text
C(W) =
  全部 owner-project production .fs 的每个可执行 F# AST node
  ∪ 全部 compiler-resolved external FCS symbol-use occurrence
  ∪ 全部 Fable Import/Emit attribute 与 emitJsExpr occurrence
  ∪ 全部 sibling .fsi public export
  ∪ full JavaScript visitor从Emit/emitJsExpr/generated artifact
    产出的全部 JsCapabilityObservationV1

J(W) =
  每个 Fable Emit/emitJsExpr parse unit的全部JavaScript AST node
  ∪ 每个 production package import可达generated runtime artifact的
    全部JavaScript AST node
```

`C(W)`的每条observation恰有一个capability disposition；普通JavaScript declaration、literal、纯operator等不伪造capability fact，只进入`J(W)` traversal coverage。`FableImport`若独立解析到repository-generated artifact，只引用该artifact的stable ID；linkage与digest只存在于4.5 `GeneratedArtifactRowV1`，不得再复制进fact或产生竞争的`GeneratedModuleImport`。Node只是runtime维度，不自动等于physical authority：例如`node:path/posix`的closed纯调用可以是`Node + PureRepresentation`，platform-default`node:path`另按ambient-platform规则判定；`node:fs`必须同时标记`Node + FileSystem`，`node:child_process`必须同时标记`Node + ProcessControl`。一个multiline Emit可同时标记`Environment + ProcessControl`；多标签必须全部保留。generated provenance绝不删除artifact的runtime/authority/mutable/class labels。

partition 铁律：

```text
observationKeys = keys(C(W))
irrelevantKeys ∪ classifiedKeys ∪ unknownKeys = observationKeys
irrelevantKeys、classifiedKeys、unknownKeys 两两不交
∀ observationKey ∈ C(W)，恰有一个 disposition
```

Emit/emitJsExpr的`source_id`哈希`{raw expression,ObservationSiteV1}`，generated artifact直接使用`artifact.id`；`JavaScriptTraversalCoverageV1.id`哈希`{source_kind,source_id}`，不含coverage结果。`enumerateJavaScriptAstNodesV1`以generic AST child traversal产生`J(W)`的stable`node_id = source_id + AST child-index path`；`visitJavaScriptNodeV1`是独立closed semantic visitor，每个node精确产生`NoCapabilityObservation | EmittedCapabilityObservations(nonempty observation IDs) | UnknownNodeType(node_type)`之一。`validateJavaScriptTraversalV1`的正式输入是`raw AST + scope resolver + visits + canonical facts`；它内部调用enumerator取得唯一node universe，不接受caller node rows。ephemeral node rows按`node_id`排序后生成`ast_node_set_digest`与`visit_partition_digest`；canonical world只保存`JavaScriptTraversalCoverageV1`，不复制普通node。每行必须满足`ast_node_count > 0`且`visited_node_count = ast_node_count = no_capability_node_count + capability_emitting_node_count + unknown_node_count`，node key与visit key集合精确相等且各自unique；全部`EmittedCapabilityObservations` ID的union还必须精确等于同source unit进入`C(W)`的`JsCapabilityObservationV1` ID集合，禁止orphan/missing/跨unit fact。missing、duplicate、unknown分别得到`javascript-ast-node-unvisited`、`javascript-ast-node-duplicate-visit`、`javascript-ast-node-unknown`；`unknown_node_count`必须为0才可cutover。同步删除node、visit与fact仍会由raw AST重建出的missing node精确RED；canonical world拒绝零node coverage。

AST/FCS/JS parser diagnostic、未支持F# node、动态target、无法解析alias/FQN、缺generated artifact/linkage均产生`Unknown`或顶层`capability-extraction-incomplete`，禁止静默跳过。F# visitor必须穷举AST union，不得wildcard忽略；JS的未知node type由上述traversal coverage fail-closed，不要求`JsCapabilityObservationV1`伪装声明/字面量/运算符。integration fixture固定完整capability partition与AST traversal计数/digest；mutation删除一个capability disposition必须精确得到`capability-observation-missing`，复制一个得到`capability-observation-duplicate`，fact ID同而canonical payload不同得到`capability-fact-id-collision`；另独立mutation node visit partition的missing/duplicate/unknown，禁止用一种RED代替另一种。

F#调用按FCS-resolved fully-qualified symbol分类，source alias与open不另走字符串猜测。`FableEmit/EmitJsExpr`先把参数hole替换为不可执行sentinel，再用仓库既有Acorn解析expression/module fragment并执行上述structural enumerator+semantic visitor；无法唯一解析的template进入`UnparsedInterop`，禁止regex命中一部分后当作完整分类。fixed fixture必须让同一physical call以FQN、alias、open、单行Emit、多行Emit五种表示得到相同authority labels。
`.fsi` export kind由production signature extractor映射到closed vocabulary；未知compiler declaration shape只能成为Unknown，禁止以“不是capability-type”反推pure。

分类表至少固定以下 exact 规则；相邻纯反例必须同测：

- `Date.now`、parameterless `new Date()`、`DateTime.Now/UtcNow`、`DateTimeOffset.UtcNow`、`performance.now` → `Clock`；`Date.parse`、`new Date(epoch)`不产生`Clock`，仍分别按返回值与mutability closed rule分类。
- `Math.random`、`Guid.NewGuid`、`System.Random`、random UUID API → `Randomness`。
- `setTimeout/clearTimeout/setInterval/clearInterval`、`Task.Delay`、timer API → `Timer`。
- `console.*`、`System.Console` → `Console`。
- `process.env`、`Environment.GetEnvironmentVariable` 与 ambient cwd/platform读取 → `Environment`。
- `process.kill/exit/pid`、child-process/spawn → `ProcessControl`。
- `node:fs`、`fs`、`System.IO` → `FileSystem`；`fetch/http/https/net/socket` → `Network`。
- Git/provider/Host API 分别累积 `Git/Provider/Host`；其他 Node/Bun/browser global 或 external package symbol若无 closed rule则 Unknown，不默认 pure。

`PureRepresentation` 只能由 closed pure rule产生。判定 precedence 只决定 verdict，不丢标签：`Unknown > any authority/mutable/value/factory/effect-constructor > pure/type-only`。physical、mutable、capability value/factory/effect constructor 不得被 metadata 或 generated proof降格；Unknown 阻断 adjudication/cutover。

`observation_id` 哈希去掉 line/column/diagnostic 后的完整 raw observation；`fact_id` 哈希 `{observation_id, disposition}`。两者都使用 4.5 的唯一 canonical encoder与 domain prefix。`same_anchor_occurrence_ordinal` 按同一 `(source_path,semantic_declaration_anchor,raw case,完整 raw payload)` 的 compiler traversal 顺序编号；两处相同 occurrence 保持不同 ID，重复提取同一 occurrence才去重。fixture必须覆盖不同 payload不碰撞、同payload双 occurrence、alias/FQN、`.fs`/`.fsi`、Import/Emit/emitJsExpr和generated JS。

generated artifact必须先建立4.5唯一`GeneratedArtifactRowV1`，再解析实际输出JavaScript；static/dynamic import、free global、member read/write、call、construct、mutable binding与update只产生引用`generated_artifact_id`的capability facts，普通AST node只进入traversal coverage。生成过程deterministic只证明bytes可复现；artifact含`Date.now()`、`fetch()`、mutable global、未知动态调用或未知AST node仍分别产生authority/mutable/Unknown fact或coverage violation，并使contract/generated policy RED。

actual capability facts 不得复制进 manifest、baseline 或 adjudication record。slice validator、effect purity census、generated-module validator、physical-port/adapter/composition checks 与 fast-check 只消费同一 `CanonicalCapabilityFactV1` set；不得重扫源码、解析其他 gate 的诊断字符串，或以 locality kind metadata代替源码事实。

### 4.5 Canonical world、adjudication candidate 与 freshness

production pure `buildCanonicalWorldV1` 产生唯一 adjudication 输入：

```text
CanonicalWorldV1 = {
  schema_version: 1,
  fact_schema_version: 1,
  observed: {
    localities: LocalityRowV1[],
    project_references: ProjectReferenceRowV1[],
    actual_source_edges: SourceEdgeRowV1[],
    generated_artifacts: GeneratedArtifactRowV1[],
    javascript_traversals: JavaScriptTraversalCoverageV1[],
    capability_extraction: ExtractionCoverageV1,
    capability_facts: CanonicalCapabilityFactV1[]
  },
  normative: {
    authorization_schema_version: 2,
    slices: SliceAuthorizationProjectionV2[],
    capability_relations: CapabilityRelationProjectionV2[],
    generated_module_relations: GeneratedModuleRelationProjectionV2[]
  }
}

LocalityRowV1 = {
  id, owner,
  kind: "contract" | "runtime" | "adapter" | "composition",
  project_path,
  sources: SourcePairRowV1[]
}

SourcePairRowV1 = {
  implementation_path, implementation_digest,
  signature_path, signature_digest
}

ProjectReferenceRowV1 = { consumer_locality, provider_locality }
SourceEdgeRowV1 = {
  consumer_locality, consumer_source,
  provider_locality, provider_source
}

GeneratedArtifactRowV1 = {
  id,
  artifact_path,
  artifact_digest,
  selected_inputs_digest,
  linkage: GeneratedLinkageV1,
  javascript_traversal_id
}

ExtractionCoverageV1 = {
  capability_observation_count,
  irrelevant_count, classified_count, unknown_count,
  capability_observation_digest,
  disposition_digest
}

SemanticEvidenceProjectionV2 = {
  path, title, what_id, surface_module
}

SliceAuthorizationProjectionV2 =
  | {
      id, owner, provider_locality,
      classification: {kind:"contract", exposure:"shared"},
      allowed_direct_consumers: string[],
      laws: string[],
      semantic_evidence: SemanticEvidenceProjectionV2[]
    }
  | {
      id, owner, provider_locality,
      classification: {kind:"contract", exposure:"bounded"},
      allowed_direct_consumers: string[],
      allowed_effective_consumers: string[],
      laws: string[],
      semantic_evidence: SemanticEvidenceProjectionV2[]
    }
  | {
      id, owner, provider_locality,
      classification: {kind:"runtime", exposure:"effect"} |
                      {kind:"adapter", exposure:"effect"} |
                      {kind:"composition"},
      allowed_direct_consumers: string[],
      laws: string[],
      semantic_evidence: SemanticEvidenceProjectionV2[]
    }

CapabilityRelationProjectionV2 = {
  id,
  kind: "physical-port" | "adapter" | "composition-wiring",
  consumer_locality, provider_slice,
  consumer_module, provider_surface_module,
  laws: string[],
  semantic_evidence: SemanticEvidenceProjectionV2[]
}

GeneratedModuleRelationProjectionV2 = {
  id,
  kind: "compile-contract-support",
  consumer_locality, import_specifier, generated_owner,
  package_import_target,
  generator: {path, entry},
  build_invocation: {path, entry},
  input_selector: {path, entry},
  runtime_surface_module,
  laws: string[],
  determinism_proof: {path, title, what_id}
}
```

所有object都是exact keys。所有`*_digest`采用`sha256:<64 lowercase hex>`；source/artifact digest覆盖原始文件bytes。每个implementation必须有同locality sibling signature；source/path/locality/owner均为非空canonical string。`ExtractionCoverageV1`必须满足三类count之和等于`capability_observation_count == capability_facts.length`；`capability_observation_digest`覆盖排序后的全部raw observation，`disposition_digest`覆盖排序后的全部canonical fact。Unknown count非零即阻断。

`GeneratedArtifactRowV1.id := "generated-artifact/v1:" + SHA256(encodeCanonicalJsonV1({artifact_path,linkage}))`，内容变化不换identity，由两个digest显式反映。exact output bytes只作为`artifact_digest`的哈希输入，不复制进canonical world。input selector只返回filesystem path array；selector boundary拒绝root外路径并规范为canonical repository-relative path。repository reader为每个path提供exact bytes并投影`{path,blob_digest}`，其中`blob_digest`是raw bytes的SHA-256；rows按path排序、unique后的canonical encoding产生`selected_inputs_digest`。M6.3c reader必须把这些path绑定stage-0 blob；post-cutover integration reader使用本次fresh working-tree/HEAD输入，不读取formal snapshot。linkage只在该row保存一次；`FableImport`与`JavaScriptCapability[source_kind=generated-artifact]` fact只保存artifact ID。每个artifact精确引用一个`source_kind = "generated-artifact"`的traversal row，且该row的`source_id = artifact.id`；traversal row missing/stale/duplicate/source mismatch分别得到`javascript-traversal-missing`、`javascript-traversal-stale`、`javascript-traversal-duplicate`、`javascript-traversal-source-mismatch`。Fable Emit/emitJsExpr解析成功时引用对应traversal ID；解析失败时为`null`且该observation只能得到`Unknown(UnparsedInterop,...)`。

normative projection直接来自已经通过schema v2 shape validator的manifest：slice、capability relation、generated relation分别使用第5节exact row去掉`justification`后的全部字段；classification、grant、law与evidence保留在所属row。不得另建semantic-owner、law/evidence或generated-linkage副本：locality owner/kind在`LocalityRowV1`，observed generated linkage与artifact/input digests只在`GeneratedArtifactRowV1`，capability facts只引用其ID，normative generated claim只在generated relation。slice/relation中的owner必须与对应locality row交叉验证。

collection identity与排序固定：

- localities：`id`；同 locality sources：`implementation_path`。
- ProjectReference：`(consumer_locality,provider_locality)`。
- source edge：`(consumer_locality,consumer_source,provider_locality,provider_source)`。
- generated artifact：`id`；JavaScript traversal：`id`。
- capability fact：`(observation_id,fact_id)`。
- slice、capability relation、generated relation：各自`id`。
- law：WHAT ID；evidence：`(what_id,path,title,surface_module)`；record proof另按其三元组。

所有 tuple component 使用下述 canonical text comparator。raw compiler的多次symbol use映射到同一source-edge tuple时只投影一条edge；完全相同的observation/fact重复提取只投影一次。locality/source ownership重复、同ID不同payload、manifest grant/law/evidence/relation重复均直接RED，不以dedupe修复非法输入。

closure约定固定：`forwardProjectClosure(C)`是reflexive closure，包含`C`本身；authorization只检查`C != P`的cross-locality edge。`actual_effective_consumers(P)`是reverse reflexive closure删除`P`后的集合。direct/effective query只输出locality ID unique array；source edge仍保留source坐标。

唯一字节序列化入口为：

```text
serializeCanonicalWorldV1(world) =
  encodeCanonicalJsonV1(canonicalWorldProjectionV1(world))
```

`canonicalWorldProjectionV1`只接受上述closed rows并完成unordered collection排序；真正有序的业务sequence保持输入顺序。`encodeCanonicalJsonV1`绝不重排array；object key按Unicode code point序列升序，禁止`localeCompare`。允许值只有`null/bool/string/non-negative safe integer/dense array/plain object`；拒绝`undefined/NaN/Infinity/-0/fraction/bigint/function/symbol/sparse array/non-plain object`。string不做Unicode normalization，拒绝unpaired surrogate；引号、反斜杠与U+0000–001F使用唯一JSON escape，其余scalar直接UTF-8。输出无whitespace、BOM或尾随LF。path必须先规范为无绝对前缀、反斜杠、`.`、`..`与空segment的POSIX repository-relative string。

现有 analyzer/policy/query 内所有影响identity/digest的`.sort(localeCompare)`必须改用同一`compareCanonicalTextV1`。golden fixture固定exact bytes+digest，并覆盖输入permutation、numeric-looking key、non-BMP key、组合字符不归一化、重复row、非法number/surrogate/path；world、query、fact ID、index projection与fast-check全部调用同一个encoder，禁止测试镜像实现。

`canonical_world_digest := "sha256:" + SHA256(UTF8("canonical-world/v1\u0000") ++ serializeCanonicalWorldV1(world))`。在M6.3c/M6.4 cutover audit期间，授权字段、owner/kind、source/signature bytes、law/evidence与relation任一变化都使未冻结snapshot失效。M6.4提交后snapshot生命周期按下文冻结，不再与后续live world比较。

candidate 不再采用可漏项的选择性筛选。每个 live locality 都需要 terminal classification，因此：

```text
deriveAdjudicationCandidates(world) :=
  canonicalSortV1(world.observed.localities.map(row => row.id)).map(locality_id => ({
    locality_id,
    reasons: canonicalSortV1({
      TerminalClassificationRequired,
      ReferencedProvider
        if exists C: (C, locality_id) in ProjectReference or actual-source edges,
      CompositionProvider
        if declaredKind(locality_id) = composition and ReferencedProvider,
      CapabilityBearing
        if exists capability fact owned by locality_id whose disposition is not Irrelevant,
      KindCapabilityMismatch
        if validateDeclaredKindV1(observedFacts(locality_id), declaredKind(locality_id)) is RED,
      RelationEndpoint(kind, role, relation_id)
        for each locality endpoint named by each staged RelationKindV1 at locality_id,
      MissingClosureEndpoint
        if locality_id is either endpoint of an actual edge outside ProjectReference closure
    })
  }))
```

`TerminalClassificationRequired` 使 candidate key universe 精确等于 live locality ID 集合；其他 predicate 只增加 review reason，不能过滤 locality。relation 指向不存在的 locality 在 world validation 阶段直接 RED。production function 输出 stable locality ID/reason；fixture 必须精确覆盖 locality add/delete、one-to-many split、kind/capability mismatch、每个 `RelationKindV1` 的全部 locality endpoint、composition provider 与 missing-closure 两端，并断言 key/reason 的唯一预期变化。

canonical relation endpoint 与 target classification 同样使用 closed algebra：

```text
RelationKindV1 =
  | SliceSemanticEvidence of provider_locality
  | PhysicalPort of consumer_locality * provider_locality
  | PhysicalAdapter of consumer_locality * provider_locality
  | CompositionWiring of consumer_locality * provider_locality
  | GeneratedModule of consumer_locality

TerminalClassificationV1 =
  | Private
  | ContractShared
  | ContractBounded
  | RuntimeEffect
  | AdapterEffect
  | CompositionTerminal
```

`generated_owner` 是 semantic owner，不是 locality endpoint。generated relation 只给其 `consumer_locality` 增加 candidate reason；owner existence 与 proof ownership 由 relation validator另验。`TerminalClassificationV1` 的 canonical JSON 仍遵守 `{case,payload}`：case 依次为 `private/contract-shared/contract-bounded/runtime-effect/adapter-effect/composition-terminal`，六种 payload 均为 `{}`。

唯一 terminal classifier 固定为 production pure `classifyTerminalV1(world, locality_id)`：无 provider slice → `Private`；唯一 slice 的 `{kind:"contract",exposure:"shared"}` → `ContractShared`；`contract/bounded` → `ContractBounded`；`runtime/effect` → `RuntimeEffect`；`adapter/effect` → `AdapterEffect`；`composition` → `CompositionTerminal`。零 slice 之外的 missing/duplicate slice、kind/exposure 与 locality metadata 不一致先由 world validator RED，classifier 不猜默认值。record、manifest report、fast-check 与 release gate都调用该函数，禁止复制 switch。

三个 production pure query 的 ID 与 exact result shape 固定为：

```text
SurfaceExportRowV1 = { export_kind, declaration_identity }
SignatureSurfaceRowV1 = {
  signature_path,
  signature_digest,
  exports: SurfaceExportRowV1[]
}

surface/v1:<locality> = {
  signatures: SignatureSurfaceRowV1[]
}

audience/v1:<locality> = {
  direct_project_consumers: string[],
  actual_source_consumers: string[],
  reverse_closure_effective_consumers: string[],
  relation_endpoints: {
    relation_kind: "slice" | "physical-port" | "adapter" |
                   "composition-wiring" | "generated-module",
    role: "provider" | "consumer",
    relation_id: string
  }[],
  missing_closure_violations: SourceEdgeRowV1[]
}

capability/v1:<locality> = {
  facts: CanonicalCapabilityFactV1[],
  generated_artifacts: GeneratedArtifactRowV1[],
  javascript_traversals: JavaScriptTraversalCoverageV1[],
  declared_kind_mismatch: boolean
}
```

surface export从同一canonical `PublicSignatureExport` facts按signature path投影；capability query的artifact集合等于该locality facts引用的全部artifact，traversal集合等于其Emit facts与artifact rows引用的全部traversal，零漏项、零stale row。signature、export、consumer、relation endpoint、source edge、artifact、traversal与fact分别按4.5对应identity排序且unique，重复不同payload RED。`relation_endpoints`中slice row只产生provider endpoint；capability relation产生consumer+provider endpoint；generated relation只产生consumer endpoint。三个result object与nested row均拒绝未知字段。

query digest只调用同一encoder：

```text
queryDigestV1(query_id, result) =
  "sha256:" + SHA256(
    UTF8("canonical-query/v1\u0000") ++
    UTF8(query_id) ++ UTF8("\u0000") ++
    encodeCanonicalJsonV1(result))
```

query result可在 live report 展示，禁止写入record。

`docs/OWNER-CONTRACT-SLICE-ADJUDICATION-WORKSHEET.json`是M6.3b可提交、可反复更新的迁移worksheet；它使用closed migration-only shape，避免接手Agent自行发明字段，但不进入canonical world、manifest或任何release gate，M6.4原子提交必须删除：

```text
{
  schema_version: 1,
  purpose: "m6.3b-migration-only",
  records: {
    locality_id: string,
    status: "undecided" | "decided",
    draft_reason: string | null,
    draft_target_classification: TerminalClassificationV1 | null,
    draft_migration_path: string | null,
    draft_what_ids: string[],
    draft_proofs: {what_id:string,path:string,title:string}[]
  }[]
}
```

worksheet records按locality ID排序且unique；`undecided`要求全部draft字段为`null/[]`，`decided`要求文本、classification、WHAT/proof满足formal record的局部shape。它不含digest或manifest claim，不能自动提升为正式裁决；M6.3c必须针对最终world/manifest重新投影并验真。正式历史只生成一次，路径为`docs/OWNER-CONTRACT-SLICE-ADJUDICATIONS.json`，顶层与nested object全部exact、未知字段RED：

```text
{
  schema_version: 1,
  snapshot_kind: "m6.4-cutover",
  fact_schema_version: 1,
  canonical_world_digest: string,
  cutover_input_index_digest: string,
  records: AdjudicationRecordV1[]
}

AdjudicationRecordV1 = {
  locality_id: string,
  queries: {
    surface: { query_id: string, query_digest: string },
    audience: { query_id: string, query_digest: string },
    capability: { query_id: string, query_digest: string }
  },
  decision: {
    reason: string,
    target_classification: TerminalClassificationV1,
    manifest_claim_ids: {
      provider_slice_id: string | null,
      consumer_capability_relation_ids: string[],
      consumer_generated_module_relation_ids: string[]
    },
    migration_path: string,
    what_ids: string[],
    proofs: { what_id: string, path: string, title: string }[]
  }
}
```

`records[]` 按`locality_id`排序且unique；使用array而非object map，防止JSON parser在验证前吞掉duplicate key。`reason`与`migration_path`是trim后非空、无控制字符的review文本。三个query ID必须精确等于该locality的`surface/v1:*`、`audience/v1:*`、`capability/v1:*`；digest为`sha256:<64 lowercase hex>`。record禁止保存`.fsi` export、consumer、source edge、capability fact、fact ID list、grant payload或query result。

`projectManifestClaimsV1(world, locality_id)` 是唯一claim投影：`provider_slice_id`等于该locality唯一slice ID或`null`；两个consumer relation ID集合分别精确等于manifest中`consumer_locality = locality_id`的capability/generated relation IDs。record中的三项必须与该投影集合相等，不允许漏项、stale ID或duplicate。

`decision.target_classification`必须精确等于`classifyTerminalV1(world, locality_id)`。`what_ids`按canonical comparator排序、unique、非空，并精确等于所有`manifest_claim_ids`所指slice/relation的`laws[]`并集，再加全局架构law`STRUCTURED-WORKFLOW-011`。

全局proof identity固定为`requirements/structured-workflow/tests/locality-slice-adjudication.test.mjs::WHAT[STRUCTURED-WORKFLOW-011] adjudication validator binds a decision to terminal manifest claims`。该永久test只在synthetic canonical fixture上证明pure validator，post-cutover不读取formal snapshot。`expectedRecordProofsV1`等于所有claimed slice/capability relation的`semantic_evidence`投影为`{what_id,path,title}`、claimed generated relation的`determinism_proof`投影，以及该全局proof的stable unique union。record `proofs`必须与该集合精确相等，按`(what_id,path,title)`排序、unique；`set(proofs.what_id) = set(what_ids)`且每个WHAT至少一份proof。每个WHAT必须存在，proof path必须是tracked正式test，title必须与文件中exact active test一致，HOW必须有唯一active edge指向它，callback必须实际消费WHAT owner登记的Surface。manifest law的owner/evidence另按第5节验证；全局架构law不得替代manifest owner evidence。

record validator使用closed codes：`adjudication-record-missing`、`adjudication-record-unexpected`、`adjudication-record-duplicate`、`adjudication-record-locality-mismatch`、`adjudication-fact-schema-mismatch`、`adjudication-world-digest-mismatch`、`adjudication-index-digest-mismatch`、`adjudication-query-digest-mismatch`（坐标含query kind）、`adjudication-target-mismatch`、`adjudication-manifest-claim-missing`、`adjudication-manifest-claim-stale`、`adjudication-proof-missing`、`adjudication-proof-orphan`、`adjudication-proof-invalid`。fixture为每个code提供单目标mutation并断言exact code+coordinates。

M6.3c先完成production/source/`.fsi`/fsproj/target manifest/relation修改，再建立最终staged input。cutover index协议固定：

1. `resolveCutoverInputClosureV1`只按真实语义入口建集合，不按扩展名猜测：owner/aggregate project与其compile/signature/reference/props输入；semantic-owner与v2 manifest；WHAT/HOW/Surface/proof输入；analyzer/build/generator/input-selector entry module及其全部repository-local transitive import；每个input selector实际返回的全部path；package/tool manifest与lock。canonical world构建期间所有repository file read必须经过同一tracking reader；任何实际读取但不在closure的path、dynamic local import或无法解析的selector output立即`cutover-input-closure-incomplete`。
2. stage全部计划进入M6.4提交的tracked文件；worksheet deletion也必须staged。closure中的每个path必须是canonical repository-relative path并精确命中一个stage-0 tracked entry；尤其每条generated relation的generator、build invocation、input selector及selector返回的每个input都不得untracked、ignored、unmerged或只存在working tree，且必须是`100644/100755`regular blob，禁止symlink在读取时越出已绑定bytes。唯一例外是relation明确声明的build output artifact；它必须由`GeneratedArtifactRowV1.artifact_path/artifact_digest`承接，不能反向成为selected input。拒绝任何unstaged tracked change；不使用文件扩展名allowlist判断相关性。
3. `CanonicalInputIndexRowV1 = {path,mode,blob_oid}`。读取`git ls-files --stage -z`的全部stage-0 tracked entry，排除正式snapshot与worksheet后，按path canonical排序；mode只允许`100644/100755`，拒绝symlink、gitlink、duplicate/path非法值。object format只由`git rev-parse --show-object-format`取得；`blob_oid`必须符合该format的lowercase hex长度且对象类型为blob。`cutover_input_index_digest := "sha256:" + SHA256(UTF8("cutover-input-index/v1\u0000") ++ encodeCanonicalJsonV1(rows))`。不把包含snapshot自身的完整tree OID写入snapshot，避免内容依赖自身OID的循环。
4. scan前后都执行`assertWorkingTreeMatchesCutoverIndexV1`：逐个tracked row比较working-tree bytes与index blob，再重建input closure并要求路径、selector outputs与stage-0 binding完全相同；任一变化立即RED。generated artifact若tracked则同样比较；若是唯一允许的build-only output，则重建后exact bytes必须匹配`GeneratedArtifactRowV1.artifact_digest`，其selector输出rows必须匹配`selected_inputs_digest`。
5. fresh full scan只读上述已核对working tree，产生canonical world、candidate、query digest与正式snapshot；写入并stage snapshot，删除并stage worksheet，再次计算排除两文件的index digest并要求不变。正式snapshot的working-tree bytes必须等于其stage-0 blob，validator直接解析这组bytes；worksheet在index与working tree都必须不存在。之后不得再改任何staged input。

M6.4在同一staged state一次性验证：record locality集合精确等于candidate集合；world/query/index digest零失配；target classifier、manifest claim、WHAT/proof全部零失配。通过后随cutover commit冻结snapshot。M6.5/M6.6/M6.7与所有post-cutover release gate都不得再用snapshot对比live world、更新snapshot或从snapshot读取授权；后续split/owner merge按常规WHY→WHAT→HOW→GAP与live executable proof施工。Git中的M6.4文件只作不可变审计史，实际授权永远只来自live manifest+analyzer。

### 4.6 Published slice authorization

每个允许被其他 locality 使用的 provider locality 必须有一个 published slice authorization，本文简称 slice。这里的 slice 是授权记录总称，不等于 `locality kind = contract`；runtime、adapter 与被 terminal wiring 引用的 composition provider 也必须有各自的 slice row。一个 slice 只能拥有：

- 一个 semantic owner。
- 一个 authority class。
- 一组共同演化的 API。
- provider locality 中全部 sibling `.fsi` exports 的并集；每个 production `.fs` 均有同 locality sibling `.fsi`，并按 `.fsi` → `.fs` 顺序编译。
- 一组 exact direct consumer locality。
- 一个由 DAG 推导的 effective audience。

grant 授权该并集的完整 public surface；manifest 不得复制 symbol 清单。同一 slice 内全部 `.fsi` exports 对全部 effective audience 可见。若并集中任一 export 不能与其余 export 共同授权给同一 audience，必须拆 locality/slice；禁止用 JSON 写出无法执行的更细权限。

`private` locality 没有 slice row：除自身 source 外，不允许任何 locality dependency 或 ProjectReference 指向它。

### 4.7 三种 exposure 与机械矩阵

| locality kind | 合法 exposure | 可执行约束 |
|---|---|---|
| contract | shared、bounded | `.fsi` 完整；closure 只能含 contract；不得包含 runtime/effect、Host import、mutable registry 或 effect constructor |
| runtime | effect | capability value/factory 可在此产生；全部实际反向可达 consumer 必须是 composition |
| adapter | effect | physical import 唯一；只实现登记 port；全部实际反向可达 consumer 必须是 composition |
| composition | 无 exposure | terminal wiring；可消费 contract/runtime/adapter；若被引用，只允许 composition consumer 通过 exact composition-wiring relation 到达，禁止充当普通 slice |

具体规则：

- shared：只允许 immutable data、opaque identity、纯函数与无 authority 的 capability type；传递可见是正式语义。
- bounded：内容仍须满足 contract 纯度；`actual_effective_consumers ⊆ allowed_effective_consumers`。
- effect：IO、写入、网络、进程、provider、Git mutation、capability value 与 constructor；只能位于 runtime/adapter。
- capability type 可位于 contract；capability value、factory 与 physical import 只能在 effect/composition 边界产生。

composition 是 locality kind，不是第四种 exposure。它位于依赖图末端，不得重新成为领域 project 的公共 provider。

纯度不由手写`"exposure": "shared"`自证。gate必须消费4.4的canonical capability facts。`Import`/`Emit`与`RuntimeV1.Node/Bun/Browser/ExternalPackage`都只是mechanism/runtime事实，本身不决定RED。拒绝谓词只看：`authorities`非空、`mutable_resources`非空、semantic class含`CapabilityValue/CapabilityFactory/EffectConstructor`，或disposition为`Unknown`。因此`Node + PureRepresentation`可GREEN；带`Host/ProcessControl/Network/FileSystem/Git/Provider`等authority的同一Node import必须RED；纯表示`Emit`与exact deterministic generated-module relation不得误杀。

### 4.8 Port + capability injection

高风险 effect 不直接把 constructor、runner 或 mutable registry 发布给 consumer：

1. shared/bounded contract project 只定义 port type 或函数能力类型。
2. consumer 通过参数获得 capability value。
3. effect implementation 位于独立 runtime locality。
4. composition root 构造 implementation 并注入 consumer。
5. 普通 consumer 的传递 closure 不得包含 implementation project。

这把 Fable 的传递可见性限制在无物理 authority 的 contract 上。

## 5. Manifest 终态

`scripts/checks/published-contracts.json` 原位升级为 closed schema v2。顶层只允许四个 exact key；缺字段、未知字段、旧字段或非 `2` 版本均 RED。下列是 schema skeleton，不是可迁入 live manifest 的 grant 示例：

```json
{
  "schema_version": 2,
  "slices": [],
  "capability_relations": [],
  "generated_module_relations": []
}
```

`slices[]` 只记录被其他 locality 使用的 provider；未出现的 live locality 机械分类为 `private`。slice 的 `classification` 是 exact tagged union：

```text
{ kind: "contract", exposure: "shared" }
{ kind: "contract", exposure: "bounded" }
{ kind: "runtime", exposure: "effect" }
{ kind: "adapter", exposure: "effect" }
{ kind: "composition" }
```

slice base exact keys 为 `id/owner/provider_locality/classification/allowed_direct_consumers/laws/semantic_evidence/justification`；bounded variant 额外且只额外允许 `allowed_effective_consumers`。未知 key RED。evidence identity 固定为 `(what_id,path,title,surface_module)`；所有 ID、consumer、law 与 evidence identity 唯一且稳定排序，所有 locality reference 必须解析。`allowed_direct_consumers` 对全部 slice 必填且非空；composition slice 有稳定 slice identity、owner、direct grant、law/evidence 与 justification，但禁止 `exposure`；其实际到达还必须逐边满足 composition-wiring relation。unreferenced locality 必须 private；任何有 inbound ProjectReference 或 actual source edge 的 provider 必须有且仅有一个 slice。slice ID 与 provider locality 都全局唯一；slice 的 owner/kind 必须与 owner-project graph 中同一 `provider_locality` 的唯一 metadata 相等。

terminal classifier 为：无 slice row → private；有 row → 取上述唯一 tag。private locality 禁止 inbound ProjectReference、inbound actual source edge，且不得作为 capability relation 的 provider；它仍可作为合法 consumer，包括 generated-module relation 的 consumer。由此 owner-project graph 的每个 live locality 恰有一种 terminal classification，manifest 不复制 locality 全集。

Analyzer 同次运行产生但不写入 manifest：

```text
actual_direct_consumers    := ProjectReference 的直接反向边
actual_effective_consumers := ProjectReference 的完整反向可达闭包
actual_source_edges        := compiler-resolved declaration use 映射出的 locality edge
```

规则：

- provider locality 全部 sibling `.fsi` exports 的并集是唯一 export inventory；manifest 不保存 `exports[]` 或 symbol ACL。
- `allowed_direct_consumers` 是 provider owner 批准的规范集合。
- `actual_direct_consumers` 必须与允许集合精确相等；漏登记与 stale grant 都失败。
- `allowed_effective_consumers` 是 bounded slice 的规范上界。
- `actual_effective_consumers` 由反向闭包推导，必须是该上界的子集。
- shared 不枚举完整 effective 上界，但必须通过无 effect authority 的机械矩阵。
- effect 的 actual direct/effective consumer 必须全为 composition；仍需逐条 direct grant，不能只靠 kind 放行。
- composition 的每条 actual direct edge 同时需要 direct grant 与 exact composition-wiring；relation 不能代替 grant。
- `laws[]` 至少一项，只允许 provider locality owner 自己定义的 semantic WHAT。`STRUCTURED-WORKFLOW-011` 由 v2 gate 自动作用于全部 slice，不复制进每条 row。
- 每个 law 至少有一份 exact evidence；每份 evidence 的 `what_id` 必须已列入 `laws[]`，拒绝 orphan law/evidence。
- 每份 `semantic_evidence` 保留 exact `{path,title,what_id,surface_module}`：WHAT definition owner、proof 所在 requirement package、HOW 唯一 active edge、registered Surface owner 与 slice owner 必须相同，callback closure 必须实际消费该 Surface。evidence 只证明语义，不产生 grant，也不缩小或扩大 `.fsi` union。以下对象只固定 evidence element shape，不是 slice/grant row：

```json
{
  "path": "requirements/durable-events/tests/event-store-identity-collision.test.mjs",
  "title": "WHAT[DURABLE-EVENTS-003] same_EventId_different_canonical_bytes_fail_closed",
  "what_id": "DURABLE-EVENTS-003",
  "surface_module": "Persistence/EventStore/CodecSurface.js"
}
```

physical port、adapter、composition wiring 不并入模糊的普通 slice edge。迁移为等价的 exact capability relation。`capability_relations[]` element 的 exact keys 为 `id/kind/consumer_locality/provider_slice/consumer_module/provider_surface_module/laws/semantic_evidence/justification`；未知 key RED，ID 与 evidence identity 唯一且稳定排序。`kind` 只能取下列三个 JSON literal，不能写 union string 或自由文本：

```text
CapabilityRelationKindV2 =
  | "physical-port"
  | "adapter"
  | "composition-wiring"
```

`provider_slice` 必须解析为一个现存 slice，包括无 exposure 的 composition slice。specialized relation 与合法 graph/slice policy 取交集：physical-port、adapter 仍需 provider direct grant；composition-wiring 仍需 composition provider direct grant；relation 不得放行 private、非法 kind/exposure、缺失 ProjectReference 或越界 actual source edge。relation 的 law/evidence 归 consumer locality owner，按相同 exact validator 证明 adapter/wiring 行为；provider law/evidence只放 provider slice，同一 proof 不得同时冒充两侧证据。

relation kind × terminal classification 是 closed matrix：

| relation kind | consumer | provider | 额外约束 |
|---|---|---|---|
| `physical-port` | contract/runtime/adapter/composition | contract shared/bounded | provider完整surface只能含`PureRepresentation`与`CapabilityTypeOnly`，且至少含一个`CapabilityTypeOnly`；禁止value/factory/effect/physical/mutable/Unknown。 |
| `adapter` | adapter effect | contract shared/bounded | 同一 consumer/provider pair 必须另有 `physical-port` relation；consumer 是该 exact relation 的 physical implementation owner，不能消费 runtime/adapter implementation。 |
| `composition-wiring` | composition | contract shared/bounded、runtime effect、adapter effect或composition | runtime/adapter/composition provider 的每条 direct edge都必须有该 relation；contract provider 仅在真实 terminal construction/wiring 时登记，不把普通 query import冒充 wiring。 |

其余 endpoint 组合全部 RED。每条 relation 还必须同时匹配 exact consumer/provider locality、direct ProjectReference、provider direct grant、consumer/provider module 与 actual compiler-resolved module edge；一个 relation 不能授权 sibling module或 transitive-only consumer。

physical-port 的判定只消费4.4同一fact set，精确公式为：

```text
SurfaceSemanticClasses(provider) ⊆ {PureRepresentation, CapabilityTypeOnly}
∧ CapabilityTypeOnly ∈ SurfaceSemanticClasses(provider)
∧ provider不存在authority/mutable/Unknown label
```

immutable request/result/error/incident DU、opaque identity与纯转换属于`PureRepresentation`，因此FatalProcess、PTY、Temporal等port无需把支持词汇伪装成capability type。任一`CapabilityValue`、`CapabilityFactory`、`EffectConstructor`、非空`authorities`、非空`mutable_resources`或`Unknown`得到exact `invalid-physical-port-surface`。consumer可以引用该完整纯surface；是否在consumer内构造/持有/执行capability由consumer自己的actual facts与locality-kind policy裁决，不恢复per-symbol ACL，也不以“只用了type”文本推断放行。

现有 physical import uniqueness、adapter target、composition callback reachability 与 semantic-evidence validator 必须继续消费这些关系。physical port slice 的 `.fsi` 必须只含该 relation 授权的完整 port；若两个 adapter 获得的能力不同，就拆成不同 port slice。schema 迁移不得把 exact relation 降级为 owner pair、裸路径或仅 ProjectReference 存在。

deterministic repository-generated module 使用独立 exact relation，不得复用当前 `compile_contract_support` 的裸 F# source-path allowlist：

```json
{
  "id": "loop-detector-envelope",
  "kind": "compile-contract-support",
  "consumer_locality": "execution-session-loopdetector",
  "import_specifier": "#wanxiangshu-loop-detector-envelope",
  "generated_owner": "degeneration-guard",
  "package_import_target": "./dist/Execution/Session/LoopDetectorEnvelope.js",
  "generator": {
    "path": "scripts/lib/derive-loop-detector-envelope.mjs",
    "entry": "writeLoopDetectorEnvelopeArtifact"
  },
  "build_invocation": {
    "path": "scripts/build.mjs",
    "entry": "verifyArtifacts"
  },
  "input_selector": {
    "path": "scripts/lib/loop-detector-repository-corpus.mjs",
    "entry": "loopDetectorRepositoryInputFiles"
  },
  "runtime_surface_module": "Execution/Session/LoopDetectorSurface.js",
  "laws": ["DG-004"],
  "determinism_proof": {
    "path": "requirements/degeneration-guard/tests/loop-detector.test.mjs",
    "title": "WHAT[DG-004] LOOP_004_runtime_envelope_is_freshly_derived_from_the_current_repository_without_numeric_snapshots",
    "what_id": "DG-004"
  },
  "justification": "Repository-derived deterministic build artifact; no ambient or physical authority is granted."
}
```

`generated_module_relations[]` element 的 exact keys 为 `id/kind/consumer_locality/import_specifier/generated_owner/package_import_target/generator/build_invocation/input_selector/runtime_surface_module/laws/determinism_proof/justification`；`kind` 只能是 `"compile-contract-support"`。`generator`、`build_invocation`、`input_selector` 只允许 `path/entry`，其中input selector entry的closed contract是只返回filesystem path array；selector boundary将其规范为canonical repository-relative paths，拒绝root外路径、duplicate与无来源text/bytes。`determinism_proof`只允许`path/title/what_id`。`laws` 必须是 singleton 且精确等于 `[determinism_proof.what_id]`；不允许 orphan law 或一个 proof 暗中替多个 law 作证。新增第二条独立 law 需要提升 schema 并显式改为双向全覆盖的 proof collection，不能在 v2 自行扩张。未知 key RED；relation ID、law 与 proof identity 唯一且稳定排序。现有loop-detector generator在M6.3a改为调用`loopDetectorRepositoryInputFiles`并经注入reader读取bytes；`loopDetectorRepositoryTexts`不能继续充当normative selector，因为其返回值无法绑定stage-0 path identity。

M6.3a先按WHY→WHAT→HOW→GAP把该relation与`GeneratedArtifactRowV1`写入`structured-workflow`，再实现schema。actual imported member、package target、artifact/output digest与selected-input digest由同次analyzer/build产生，只进入canonical observed artifact row，不复制进manifest。relation-specific validator消费closed directed execution edges，证明build invocation可达exact generator、generator可达input selector，actual artifact linkage与normative relation一致；exact test callback必须同时触达generator entry与registered runtime Surface，但不要求测试执行build entry。并列entry名称不构成reachability，普通semantic-evidence validator也不能代替generator lineage proof。`laws[]`与determinism proof的owner必须等于`generated_owner`，该relation仍与consumer locality kind/capability policy叠加。gate必须拒绝missing/stale/duplicate artifact或relation、actual import/relation/linkage mismatch、specifier/target/build invocation漂移、artifact/input digest mismatch、缺determinism proof、非repository-content-determined output，以及任何`authorities`非空、`mutable_resources`非空或`Unknown`的artifact冒充compile support；`RuntimeV1.Node`单独存在不得触发拒绝。

当前 `published-contracts.json.compile_contract_support` 的 `{path,owner,justification}` 记录是旧 owner gate 的 source-path 豁免，不是上述 relation。M6.4 必须把这些 F# source 纳入普通 locality slice 的完整 `.fsi` 语义后删除旧字段与 parser；禁止兼容读取两种 shape。`#wanxiangshu-loop-detector-envelope` 是当前唯一已裁决实例，仍须由 package import linkage、generated member 存在与 executable determinism proof 共同验收。

### 5.1 v1 → v2 clean-break ledger

当前 v1 顶层为 `schema_version`、`compile_contract_support`、`compiler_boundary_localities`、`contracts`、`physical_adapters`、`composition_roots`、`requirement_dependencies`；parser 还接受当前 JSON 不存在的 `owner_cycle_justifications`。M6.4 同一 commit 逐项完成。

clean-break 映射规则固定：v1 `owner/path/consumer/port/wires` 只作为定位旧 row 的迁移输入，必须与 fresh owner-project graph、source/module edge 重验；v2 slice `owner` 由 provider locality 的 fresh graph metadata 唯一派生，relation law/evidence owner 由 consumer locality 的 fresh graph metadata验证。旧 metadata 不产生新授权。多个 v1 row 映射到一个 v2 slice/relation 时，不拼接、挑选或自动继承旧 `justification`；对应 v2 owner 必须依据 formal adjudication 重写一个唯一理由，旧字符串随 v1 row 删除。

| v1 字段 | v2 命运 |
|---|---|
| `schema_version: 1` | 改为 `2`；v2 exact top-level keys 只有 `schema_version/slices/capability_relations/generated_module_relations`。 |
| `compiler_boundary_localities[]` | 删除字段/parser；locality/source/owner/kind/ProjectReference 全部从 owner-project fsproj graph 唯一派生。 |
| `compile_contract_support[]` | 313 个当前 signed F# source 由 locality sibling `.fsi` union 承接，旧 `path/owner/contract/justification` shape 全删；只有真实 repository-generated JS exception 进入 `generated_module_relations[]`。 |
| `contracts[]` | `published-contract` 按 provider locality 合并进 slice；`physical-port` 进入 provider contract slice + exact capability relation；`semantic-evidence` 不再是授权 kind，proof 迁到 slice/relation。旧 `path/consumers/symbols/symbol_roots/law/proof/node/contract/justification` 删除；consumer owner 扩张改为 fresh direct consumer locality；目标 provider/relation owner 按上述规则重写唯一 justification。当前 8 条 physical-port 没有 law/proof，必须由 relation consumer owner 在 M6.3a 新增 `laws[] + semantic_evidence`；当前两条 semantic-evidence 只有在各自 provider locality 的完整 `.fsi` union 可共同授权后迁移，否则先拆 locality。 |
| `physical_adapters[]` | 迁为 `capability_relations[kind=adapter]`；consumer/port path 只用于映射并重验 exact locality/module；旧 `symbols/node/justification` 删除，relation consumer owner 重写唯一 v2 justification。当前记录没有 law/proof；M6.3a 必须由 relation consumer owner 新增 `laws[] + semantic_evidence`，缺失则不得迁移/cutover。 |
| `composition_roots[]` | 迁为 `capability_relations[kind=composition-wiring]`；consumer/provider 只用于映射并重验 exact locality/slice/module；旧 `wires/node/justification` 删除，relation consumer owner 重写唯一 v2 justification。当前记录没有 law/proof；M6.3a 必须由 relation consumer owner 新增 `laws[] + semantic_evidence`，缺失则不得迁移/cutover。 |
| `requirement_dependencies[]` | 当前为空且不属于编译授权；字段/parser 删除。requirement dependency 只由 requirements HOW + requirement-trace 拥有。 |
| `owner_cycle_justifications[]` | 当前 manifest 不存在；删除兼容 parser/fixture。ProjectReference DAG 无豁免；若 requirement owner cycle 仍需治理，由 requirement system 的唯一 registry 承担。 |

同一 commit 更新所有 manifest consumer：`scripts/checks/owner-projects.mjs`、`scripts/checks/owner-contracts.mjs` 共同消费一个 v2 parser/pure validator，`scripts/check.mjs` 仍只接一套权威；同步迁移 `requirements/structured-workflow/tests/owner-project-boundaries.test.mjs`、`requirements/structured-workflow/tests/owner-dependencies.test.mjs` 与 `requirements/semantic-trace/tests/x-trace-capture-boundary.test.mjs`。最后一项的旧 `symbols[]` assertion 必须改为 production extractor 得到的 exact provider-locality sibling `.fsi` union，禁止为保测试恢复旧 symbol inventory。

## 6. 迁移步骤

### M6.0：把老板裁决写入 WHY → WHAT → HOW → GAP

老板裁决已成立。第一提交必须先改变规范事实：

1. 授权最小单位定义为 `consumer locality → provider slice`。
2. provider locality 全部 sibling `.fsi` exports 的并集定义为 slice 唯一 export inventory。
3. 删除 exact symbol × exact owner consumer 的现行承诺。
4. 明确所有 cross-locality edge 都受授权约束，same owner 不豁免。
5. 定义 actual source edge、ProjectReference closure、direct/effective audience。
6. 定义 private、shared、bounded、effect 与 kind × exposure 矩阵。
7. 保留 semantic-evidence、physical port、adapter、composition wiring 的 exact relation。
8. 记录 future exact symbol isolation 需要保留 symbol identity 的 analyzer 或真实 assembly isolation。
9. 将 GAP-031 从“exact symbol + exact consumer compile authorization”改写为“locality-slice authorization + compiler-resolved closure completeness”。
10. GAP-031 必须在新 production gate、永久反例与全量 census 同时成立后才能 CLOSED；禁止沿用旧命题直接改状态。

修改范围至少包括：

- `requirements/structured-workflow/WHY.md`。
- `requirements/structured-workflow/WHAT.md`。
- `requirements/structured-workflow/HOW.md`。
- `requirements/GAP.md`。

完成条件：规范只承诺 production/compiler 真正执行的约束，且旧 exact-symbol 命题不存在残余同义表述。

建议 commit：

```text
spec(architecture): adopt locality slice authorization
```

### M6.1：建立轻量 compiler-resolved analyzer 与永久反例

先实现独立的 locality dependency analyzer：

1. 从真实编译器结果读取 declaration use。
2. 把 consumer/provider source 映射到唯一 locality。
3. 去掉 same-source 与 same-locality edge。
4. 输出规范化、去重、稳定排序的 cross-locality edge。
5. 验证 provider locality 属于 consumer locality 的 ProjectReference closure。
6. 不输出 symbol ACL；不持久化 snapshot；不做 delta、mtime 或跨 run cache。
7. M6.1 不实现 changed-locality shortcut；fixture aggregate 只证明 compiler extraction，不构成增量分析能力。pre-cutover live report 与 M6.4 release lane 均 fresh 扫描完整 production compile set。

固定反例：

- consumer 使用 aggregate 中存在的 public provider symbol。
- consumer project 不引用 provider，且其 ProjectReference closure 也不包含 provider。
- flattened aggregate 仍能编译。
- analyzer 必须产生 missing-closure-edge，并使 architecture verdict 为 RED。

同时固定：

- 合法 direct edge GREEN。
- 合法 transitive closure edge GREEN。
- declaration alias/open/generic/type/pattern use 均能映射到 provider locality。
- external/package symbol 不被误判为 production locality edge。

该提交只证明 analyzer 本身，不把新 manifest 设为 release 权威；旧 gate 仍是唯一 release gate，因此不存在双权威。

建议 commit：

```text
test(architecture): expose source edges outside owner closure
```

### M6.2：在旧 gate 权威期间完成阻塞性结构拆分

用新 analyzer 的只读报告找出会阻止原子 cutover 的事实，但继续由旧 gate 决定 release：

1. actual source dependency 不在 ProjectReference closure。
2. effect/Host import 与 contract source 混装。
3. private locality 已被其他 locality 使用。
4. composition 同时承担 terminal wiring 与普通 provider。
5. 一个 source 同时发布无法共享 audience 的 authority。

逐组修复：owner source → provider slice/port → consumer reference → 删除旧路径。每组保持旧标准 gate 绿色；禁止用 allowlist、baseline 或兼容 facade 暂时遮挡。

这一阶段只处理“新 gate 不可能原子变绿”的 blocker，不提前追求全部性能型 slice 拆分。

建议 commit：

```text
refactor(owner): remove <cutover-blocker> locality leak
```

### M6.3：准备全量新 manifest，不建立第二权威

为所有 locality 准备终态分类：

- private。
- shared。
- bounded。
- effect。
- composition kind。

同时准备：

1. 每个 slice 的 `allowed_direct_consumers`。
2. 每个 bounded slice 的 `allowed_effective_consumers`。
3. `laws[]`。
4. exact `semantic_evidence`。
5. physical-port、adapter、composition-wiring capability relations。
6. exact `generated_module_relations`；旧 `compile_contract_support` 裸路径记录的迁移与删除。
7. canonical locality capability facts、generated artifact rows与JavaScript traversal coverage；facts只引用artifact/traversal ID。
8. production pure `buildCanonicalWorldV1`、`deriveAdjudicationCandidates` 与三个 canonical query。

新 manifest 可在 cutover 工作树中准备和验证，但不得先以独立绿色提交落地并与旧 manifest 同时成为权威。实际集合一律由 analyzer 临时生成，不写入 manifest。

执行前必须 fresh 生成 census；当前 178/711/1,853、4,420 actual source edges、1 missing closure 只作参考。

裁决工件生命周期只有三态：

1. M6.3b `OWNER-CONTRACT-SLICE-ADJUDICATION-WORKSHEET.json`：迁移工作纸，只有4.5定义的migration-only closed schema，不含digest、无授权力、不得被release读取；可在绿色节点反复更新。
2. M6.3c `OWNER-CONTRACT-SLICE-ADJUDICATIONS.json`：仅在最终staged cutover input上生成的formal snapshot；生成后只允许修复validator指出的mismatch并整体重生，不能手改digest。
3. M6.4：对同一staged state一次验真并与cutover一同commit；同时删除worksheet。此后formal snapshot冻结，M6.5/M6.6/M6.7与永久gate均不得拿它比较live world或重写它。

M6.3 完成条件：

1. 同次 fresh canonical world 的每个 locality 均有 terminal classification；`deriveAdjudicationCandidates(world)` 的 key universe 固定为全部 live locality。`records[].locality_id`集合必须与其精确相等。当前 92 个 composition provider 只是 reason 含 `CompositionProvider` 的 pre-cutover 子集，不得把 92 硬编码进 gate 或完成集合。
2. 零 `undecided`；零从当前 ProjectReference、旧 owner ACL 或 composition 标签自动生成的 grant/relation。
3. 每份 record 只含4.5 exact fields；target classification、manifest claim IDs、WHAT/proof机械绑定同一world与manifest。禁止保存任何query result、export、consumer、source edge、capability fact或fact ID list。
4. live report 可展示actual direct/effective/source/capability集合；生成dump在cutover前删除。worksheet随M6.4删除；formal snapshot只保存cutover审计，不是manifest、allowlist或release fact source。
5. 对应 WHY → WHAT → HOW → GAP 与 executable negative oracle 已落盘；“RED”指旧世界被 oracle 识别为违规，提交后的 test suite 必须全绿。
6. 旧 gate 下可独立绿色的 contract/port split 已完成；新 pure validator/property 可提交，但 live 新模型只能 report-only，不能阻断 release。
7. `C(W)` disposition与`J(W)` traversal coverage分别完整；generated artifact output/input digests、linkage与fact ID reference零mismatch，`RuntimeV1.Node`不被当作authority。
8. M6.3c严格执行4.5 cutover index协议；最终staged input已fresh重建canonical world、candidate与query digests。record locality/world/query/index、classifier、manifest claim、WHAT/proof任一失配均已重新裁决，最终mismatch为零。
9. 进入M6.3c前，1–7必须全部成立且fresh report不再有未裁决blocker。M6.3c只准备旧ACL无法表达的最小production cut、全量终态manifest与exact relations；第8项成立才算M6.3完成，随后进入M6.4。仅完成点名owner裁决不等于M6.3完成。

### M6.4：单个原子提交切换权威

一个 commit 同时完成：

1. 启用 compiler-resolved locality dependency analyzer。
2. 启用新 slice schema 与全量 locality 分类。
3. 强制 locality ID 全局唯一。
4. 每条 cross-locality direct ProjectReference 精确匹配 `allowed_direct_consumers`，owner identity 不参与判断。
5. 每条 actual source edge 位于声明 closure。
6. bounded actual effective audience 不超过 allowed 上界。
7. shared 通过 authority/physical-import 机械矩阵。
8. effect 只被 composition 反向到达。
9. composition 只被 composition consumer 通过 exact composition-wiring relation 到达；private locality 无外部引用。
10. canonical capability facts、generated artifact rows、JavaScript traversal coverage、exact generated-module、semantic-evidence、physical adapter、composition wiring通过同一pure policy validator。
11. 对M6.3c同一staged state一次性验证formal snapshot：全部locality已落实终态slice/relation；record locality/world/query/index、terminal classifier、manifest claims与WHAT/proof零mismatch；无stale/duplicate grant、reference、relation或缺law/evidence。
12. 删除 owner-wide authorization expansion、per-symbol consumer ACL、旧 schema/parser 与旧 `compile_contract_support` 裸路径豁免。
13. 删除dead production `symbolUses: []`、旧FCS snapshot/delta/cache、临时actual fact/query dump、M6.3b worksheet、compat facade、过渡adapter、旧空路径与pre-cutover report-only release bypass；formal snapshot随commit冻结且永久gate不再读取，`owner-projects`继续唯一拥有source→locality、ProjectReference DAG与closure。
14. 新 schema/validator 进入 release authority；fresh production compiler scan 恰一次进入 integration release path。

同一 commit 的 gate 必须全绿。不存在“先启新 gate、后分类”或“新 gate 已启用、旧模型 M6.7 再删除”的过渡态。`published-contracts.json` 可以原位迁移到终态 schema；必须删除旧字段/parser，不要求删除文件本身。

full production compiler-resolved scan 接线固定如下：

- 保留 `requirements/structured-workflow/tests/integration/locality-dependency-analyzer.test.mjs` 的 aggregate-green/missing-edge compiler fixture。
- 新增唯一真实扫描文件`requirements/structured-workflow/tests/integration/locality-dependency-production-scan.test.mjs`，exact title为`WHAT[STRUCTURED-WORKFLOW-011] release lane fresh-scans the complete production locality graph`。它必须fresh提取`compiler.productionFiles`，断言其集合精确等于owner-locality inventory中全部production `.fs` union，再把完整canonical world交给同一production validator并断言`violations = []`；禁止筛changed files、复用snapshot或只断言count。
- 在`requirements/verification-system/tests/support/integration-node-test-steps.mjs`恰注册一个只含该文件的独立step；step名、文件路径与`perTestTimeoutMs`由closed step row声明。预算只由`PROJECT_CHECK_TIMEOUT_MS`→`perTestTimeoutMs`→test/child-process传播。
- 现有aggregate fixture与新production test必须删除`120_000/110_000`等本地常量；Node test timeout与child-process timeout都由同一个`PER_TEST_TIMEOUT_MS`派生，child只保留有因果意义的清理余量。
- `requirements/structured-workflow/tests/*.test.mjs` fast tier只测试closed schema、canonical encoder/world、candidate/query、record binding与production pure validator/fast-check；输入为fixture/generated canonical facts，绝不启动FCS或读取production tree。`scripts/check.mjs`只接这些pure checks，不接真实project scan。
- `VERIFICATION-SYSTEM-001`的永久proof必须解析integration step registry与`package.json`，断言production-scan path全仓release orchestration只出现一次、该step只含此文件、`package.json`/其他step不直接执行它；`VERIFICATION-SYSTEM-006`另断言timeout只从declared step budget传播。
- M6.4 后删除或封闭 production `--report-only` release bypass，保证 release invocation 无法降级。
- 最终唯一验收命令是 `npm run format-build-test`；fresh production scan、fixed canary 与 properties 均由该 sink 内部执行。

建议 commit：

```text
refactor(architecture): cut over locality slice authorization
```

#### M6.4A：用 fast-check 证明生产图算法

graph analyzer 必须是 production pure function；property test 直接调用它，不复制 closure 或 authorization 公式。

生成：

- 2–40 个 locality 的随机 DAG。
- owner assignment 与随机 owner merge/rename。
- locality kind/exposure、direct grant、bounded audience。
- actual compiler-resolved locality edges。
- canonical source-capability facts。
- exact generated-module、physical-port、adapter 与 composition-wiring relations。

生成器先构造一个 schema-valid world，调用 production pure validator 并断言 `violations = []`，再对每条性质施加一个目标 mutation。mutation 后必须精确断言预期 stable violation code + 最窄 relation coordinates；禁止只断言非空、任意 throw、message substring 或 regex。若一项语义必然产生级联错误，property 必须事先列出完整 expected code multiset，并证明没有额外 code；不得让 cycle、shape 或排序错误抢先满足 RED。

fast-check shrink 的值必须始终是 `{ legal_base_world, single_target_mutation }`；每次 shrink 后先重验 base world 仍为零 violation，再重验 mutation 只改变目标坐标并产生同一 expected code multiset。owner rename/merge 与 legal split 两条正向 metamorphic property 则要求变换前后都零 violation，再比较 normalized authorization projection。violation code 是 closed v2 schema 的稳定词汇；FCS symbol/line、诊断文案与 owner 名不参与 code identity。fast-check 只生成 canonical analyzer 已产出的 facts，不扫描源码或复制 effect/closure 公式。

性质：

1. actual source edge 逃出 ProjectReference closure → exact `missing-closure-edge`。
2. direct reference 缺 exact grant，包括 same-owner edge → exact `missing-slice-grant`。
3. stale grant → exact `stale-slice-grant`；duplicate grant → exact `duplicate-slice-grant`。
4. bounded effective audience 越界 → exact `bounded-audience-exceeded`。
5. shared 携带canonical authority/mutable/capability value/factory/effect-constructor fact → exact `impure-contract-slice`；physical-port surface只含`PureRepresentation + CapabilityTypeOnly`且至少一个type为GREEN，加入任一被禁label → exact `invalid-physical-port-surface`。
6. effect 被任一 non-composition consumer 反向到达 → exact `effect-consumer-not-composition`。
7. composition 被普通 consumer 引用 → exact `invalid-composition-consumer`；缺 relation → exact `missing-composition-wiring`。
8. private locality 被引用 → exact `private-provider-referenced`。
9. contract closure 含 non-contract locality → exact `contract-closure-kind-violation`；contract transitive production source count 超过 100 → exact `contract-closure-budget-exceeded`。
10. ProjectReference cycle → exact `locality-reference-cycle`。
11. owner rename/merge 后 normalized authorization projection 不变。
12. legal split 不扩大 capability audience：生成 `old locality → new locality partition`、source/export/capability 映射及 consumer/grant/reference 重映射；证明每个 source 恰映射一次、旧边均有映射、新边不跨 capability 偷渡，并按 capability 比较 normalized external direct/effective audience。owner 名不得作为映射键。
13. generated-module relation分别断言：missing/stale/duplicate/semantic-key duplicate、specifier/target/member mismatch、lineage missing/duplicate/stale/mismatch、proof missing/duplicate/stale/owner/law/identity mismatch、runtime Surface missing/duplicate/stale/callback mismatch、authority/mutable/Unknown artifact，各自取得第5节规定的exact code。relation/artifact/traversal/actual-import/lineage/surface/proof/traversal-observation set任一open或malformed row → `generated-module-observed-evidence-invalid`；duplicate observed fact/import → `generated-module-observed-evidence-duplicate`。artifact row另分别断言：missing → `generated-artifact-missing`；stale → `generated-artifact-stale`；duplicate → `generated-artifact-duplicate`；fact引用missing/stale ID → `generated-artifact-reference-missing`/`generated-artifact-reference-stale`；linkage mismatch → `generated-artifact-linkage-mismatch`；output digest mismatch → `generated-artifact-digest-mismatch`；selected-input digest mismatch → `generated-artifact-inputs-digest-mismatch`。每个mutation独立，不得用一个泛化RED覆盖多种失败；`RuntimeV1.Node`单标签mutation保持GREEN。
14. 任一`CapabilityDispositionV1.Unknown` → exact `unknown-capability-classification`；mutation只把一个已知GREEN observation的disposition改为一个closed `UnknownClassV1` case，禁止同时改变locality kind、grant或relation。
15. capability observation universe `C(W)`与disposition partition必须全等：删除一条disposition → exact `capability-observation-missing`；复制一条 → exact `capability-observation-duplicate`；同fact ID换payload → exact `capability-fact-id-collision`。每次mutation只改一个partition坐标。
16. JavaScript traversal `J(W)`独立证明node全覆盖：traversal row missing/stale/duplicate/source mismatch分别精确得到`javascript-traversal-missing`、`javascript-traversal-stale`、`javascript-traversal-duplicate`、`javascript-traversal-source-mismatch`；删除visit、复制visit、改成unknown node type分别精确得到`javascript-ast-node-unvisited`、`javascript-ast-node-duplicate-visit`、`javascript-ast-node-unknown`；普通declaration/literal/operator只改变coverage，不凭空增加capability fact。generated artifact还要求每个traversal恰有一条closed `TraversalObservationSetV1`：missing/duplicate/stale分别得到`javascript-traversal-observation-set-missing`、`javascript-traversal-observation-set-duplicate`、`javascript-traversal-observation-set-stale`；删除全部JS facts而保留visitor union、或同步伪造空union但coverage仍含capability-emitting node，都得到`javascript-traversal-source-mismatch`。该row只承接同次production traversal validator输出，不进入canonical world；M6.3b extractor负责生产，不允许测试镜像或第二scanner。

条目数量不是验收目标；每条性质必须消灭独立错误世界。固定 seed，失败输出最小 counterexample graph。`.fsi` export extraction、compiler declaration extraction、compile-set drift、Import/Emit 分类与 package-import linkage 由固定 fixture/真实 compiler gate证明，不伪装成随机图性质。pure `Emit` 与已批准 deterministic compile support 是 GREEN；canonical effect-capability 才是 RED。

### M6.5：切换后实施收益明确的 slice 拆分

M6.5 只承接可测量的 authority/audience/closure/impact 优化，不承接任何 M6.4 correctness debt。若发现旧权威、未落实 adjudication、stale relation、composition 业务判断或 capability matrix 违规，必须重开 M6.3/M6.4 修复。

M6.5每批只更新live owner graph、manifest、requirements与executable proof；禁止更新M6.4 formal snapshot，也禁止以snapshot world/query digest作为本批验收。split使cutover digest过期是预期历史，不是stale-record错误。

changed-locality scan 不属于 M6.5 correctness。只有 full-scan 耗时的 tracked 性能证据达到另立 node 的门槛时，才可在 M6.5 之后实现；其 permanent property 必须证明对任意 changed set，局部 finding 是同一 world full-scan finding 的保守投影，release 仍只信 full scan。

优先顺序：

1. effect 与 data/decision 混装。
2. bounded effective audience 明显超出需求。
3. reverse closure/impact compile 大且修改频繁。
4. 一个 project 同时服务不相干 capability。
5. 拆分可显著降低 impact compile。
6. 纯文件整理不拆。

优先审查对象由 fresh census 排序；以下历史对象只作候选：

- provider projection model。
- host digest。
- session recovery model。
- foundation identity。
- context companion facts。
- plugin runtime scope。
- TaskResult/foundation utilities。
- host signal adapter。
- participant roles。
- delegation sync model。
- Git integration gateway。
- durable projection 与 durability port。
- `ProcessEventLog + Store` 的 consumer cohort/authority 是否仍允许共用完整 surface。
- 完整 HostEvent/HostSignal 的纯 vocabulary/decoder 是否值得继续拆分。
- 已正确迁出的 Delegation owner projection 是否值得进一步按 audience/closure 优化。

每个 slice 的固定步骤：

1. 定义 capability、authority 与 exposure。
2. 建立新 locality/project。
3. 移动完整 source；source 混合 authority 时拆 source。
4. 收紧 `.fsi`。
5. 只迁移必要 ProjectReference。
6. 更新 flattened aggregate compile order。
7. 更新 slice manifest。
8. 删除旧 export、旧 reference 与空 project。
9. 编译 provider、direct consumer、reverse impact closure。
10. 加入一个旧世界可编译、新世界必须失败的 canary。

按相关能力每 3–5 个 slice 组成一批。每批一个绿色 commit。禁止 facade、compat project 与双 manifest。拆分数量服从 authority 与 closure 收益，不服从 190–210 的数字目标。

建议 commit：

```text
refactor(owner): isolate <capability> contract slice
```

### M6.6：最后合并 owner

必须在 locality authorization 生效后执行；否则当前 owner-wide gate 会放大权限。

owner merge同样只比较变换前后的live normalized authorization projection；禁止更新或读取M6.4 formal snapshot。snapshot中的旧owner/locality命名是cutover时点事实，不是当前授权声明。

每个候选 owner group：

1. 对比 WHY/WHAT。
2. 列出 vocabulary、decision、failure、effect。
3. 任一核心项不同即拒绝合并。
4. 更新 requirement ownership 与 semantic-owners。
5. locality/project 保持独立。
6. 重命名因旧 owner 名称而误导的 project 文件。
7. 比较稳定排序、去 owner label 的 normalized authorization projection：locality identity、ProjectReference edge、provider slice/exposure、slice grant、actual source edge、effective audience、physical/adapter/composition/generated-module capability relation与 authorization violation set必须完全相同。
8. owner/evidence metadata、requirement ownership、WHAT law与重命名路径另行验证更新后合法；诊断文本、owner 名和文件重命名路径不进入授权等价比较，也不得被 normalization 用来隐藏 law/evidence 缺失。
9. 运行两个原 owner 的全部 proof。

预计只有 5–10 组合格，owner 数可能约降至 39–44。该数字只作导向；语义不满足时保留原 owner。

建议 commit：

```text
refactor(owner): unify <owners> under <semantic-owner>
```

### M6.7：最终闭环

旧授权模型与全部迁移残余已经在对应 M6.4/M6.5/M6.6 绿色节点删除。M6.7 不施工 production/schema cleanup，只允许：

1. fresh final census。
2. 完整 `npm run format-build-test` 复验。
3. 输出迁移前后 owner/project/ref/closure/build-time 报告。
4. 更新 GAP-031 evidence/status并提交 CLOSED。

final census只读live analyzer+manifest；formal snapshot不参与final comparison或release verdict。

若发现 stale grant/relation、遗留 source/reference、compat facade、过渡 adapter、空 project、无 law/evidence 或其他 production/schema 残余，重开其来源阶段；禁止在 M6.7 顺手补债。只有正式 analyzer、全部 hard acceptance 与唯一 release sink 同时绿色时，GAP-031 才能 CLOSED。

建议 commit：

```text
docs(architecture): close locality slice authorization gap
```

## 7. 验证阶梯

### 每个迁移批次

1. format。
2. compiler-resolved locality dependency analyzer。
3. semantic-owner、owner-project、owner-contract、authority、physical-import、architecture gate。
4. slice unit/property tests。
5. provider + direct consumer compile。
6. reverse impact closure compile。
7. 对应 requirement proof。

### 每约五个 slice

1. 完整 owner graph 检查。
2. 测量 impact project/source 数。
3. 比较 reverse closure 是否缩小。
4. 检查新增 project 是否产生重复 API 或循环。

### 最终验证

1. 执行唯一命令 `npm run format-build-test`；其 integration lane内部恰执行一次 fresh全量 compiler-resolved production scan，fixed negative canary与 fast-check graph properties由该 sink的适当 unit/integration/e2e lane执行。
2. sink 内的 GitGateway、missing ProjectReference、effect factory、transitive closure、compile-support linkage 真实 canary绿色。
3. sink 内的 semantic-evidence、physical adapter、composition wiring 无降级证明绿色。
4. 输出迁移前后 owner/project/ref/closure/build-time 对照。

禁止使用 `dotnet build`。

## 8. 验收标准

### 正确性

- 每个 production source 恰有一个 owner 与一个 locality。
- locality ID 全局唯一。
- ProjectReference graph 为 DAG。
- 每条 actual cross-locality source edge 都位于 consumer 的 ProjectReference closure。
- 每条 cross-locality direct edge 恰有一个 slice grant；same owner 不豁免。
- 未声明 ProjectReference/closure 的真实 production source dependency 为 RED，即使 aggregate compile 为 GREEN。
- bounded/effect audience violation 为 0。
- shared slice引入canonical authority/mutable/capability value/factory/effect-constructor为RED；physical-port允许pure support vocabulary+capability type，拒绝value/factory/effect/physical/mutable/Unknown；pure `Emit`与exact deterministic generated-module relation不得误杀。
- effect implementation 不进入普通 consumer closure。
- provider locality 全部 sibling `.fsi` exports 的并集是公开 symbol 唯一事实源。
- owner rename/merge 前后 normalized authorization projection 完全相同；owner/law/evidence metadata独立合法。
- semantic-evidence exact `{path,title,what_id,surface_module}` proof 无降级。
- physical port、adapter target、composition wiring capability relation 无降级。
- 无旧授权 schema、baseline、suppression、compat facade。

### 规模

- 190–210 个 project 与不超过 220 只作容量规划导向，不构成正确性验收。
- 39–44 个 owner 只作候选合并后的估算，不构成正确性验收。
- owner 只按语义合并；project 只按 authority/closure 收益拆分。

### 性能

性能是可重放报告，不是 correctness/CI gate。任何 M6.3b production split 开始前，在当前 pre-split commit 建立 tracked `scripts/checks/owner-impact-corpus.json`，记录 exact baseline commit 并完成 baseline structural measurement；测量入口只读该文件。初始 stable case 不得因结果不利删除或替换，source 移动只更新同一 ID 的 successor path：

| stable case ID | baseline changed path | change kind / coverage |
|---|---|---|
| `canonical-codec-implementation` | `src/Wanxiangshu/Persistence/EventStore/CanonicalEventCodec.fs` | target contract implementation；低 closure |
| `loop-detector-runtime` | `src/Wanxiangshu/Execution/Session/LoopDetector.fs` | runtime implementation；中 closure |
| `host-signal-adapter` | `src/Wanxiangshu/OpenCode/Signals/HostSignalAdapter.fs` | adapter implementation；中/高 closure |
| `host-signal-bootstrap` | `src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs` | composition implementation；高/full fallback |
| `canonical-codec-signature` | `src/Wanxiangshu/Persistence/EventStore/CanonicalEventCodec.fsi` | public sibling signature；高/full fallback |
| `fatal-process-implementation` | `src/Wanxiangshu/Foundation/FatalProcess.fs` | effect implementation；低 closure |
| `delegation-pty-adapter` | `src/Wanxiangshu/Execution/Delegation/Fork/Host/Pty.fs` | adapter implementation；高 closure |

fsproj 与 toolchain control 分别固定为 `src/Wanxiangshu/Wanxiangshu.Owner.host-boundary.host-fatal-effect.fsproj`、`package.json`；它们验证 full fallback，不进入 impact median。

baseline/candidate 对同一 stable ID 集逐项调用 production `planImpactCompile`，固定 aggregate、`fullThreshold=0.6`、lockfile 与 tool manifest，记录 mode、reason、root/project/source 数及 compile-item identity。impact source count 是确定性主指标；中位数下降 25%只作优化方向。

full release build 与 fresh FCS scan 的 wall-clock 比较使用同一机器、toolchain、dependency cache、命令与输入；baseline commit 和 candidate commit 各做三次 clean run，保留三份 raw sample并取中位数。超过 5% 只触发报告与因果调查，不自动改变 correctness verdict或阻断 CI；“5%以内”不宣称统计等价。新 project 若既不收窄 normalized audience，也不缩小 fixed corpus 中任何相关 closure/impact，应撤销该优化 split。

## 9. 可行性与风险

### 有利条件

- locality 已存在且当前快照全部唯一。
- owner project DAG 与 impact compile 已存在。
- compiler-boundary source 已具有 `.fsi` 体系。
- Fable source-merge 行为已有真实 canary。
- owner 与 locality 已是独立 metadata。
- 现有 semantic-evidence、authority、physical-import 与 composition validator 可复用，不需复制弱扫描器。

### 主要成本

- 161 个unique foreign provider需要分类。
- 1,614 条foreign direct reference需要核对；239条same-owner direct reference也必须纳入locality authorization。
- 797 条指向composition provider的direct reference需要重新判断边界。
- 混合 authority source 需要真实拆分，不能仅移动 manifest。
- compiler-resolved analyzer 必须在 release lane fresh 扫描，带来可测量但不可省略的集成成本。

### 不可回避的限制

当前 Fable source merge 不提供 per-consumer assembly visibility。project 增加本身也不是无成本：

- full flattened build 不一定加速。
- project parsing 与 graph maintenance 会增加。
- 只有依赖闭包真正缩小时，增量编译才会改善。

因此必须以 closure、audience 与测量结果决定拆分，而不是以 project 数量作为成绩。

## 10. 执行前需重新确认的事实

正式执行前重新生成并写入迁移说明：

1. 当前 upstream commit。
2. owner/locality/source/project/ref 数。
3. 全部 provider 与 reverse closure 排名，不按 owner 关系过滤。
4. 当前全量测试结果与耗时。
5. 需要 owner 裁决的 owner merge 候选。
6. analyzer 全量执行耗时与 release budget。
7. project 数量导向是否仍为 190–210。

exact symbol ACL 的废止已经由老板裁决，不再作为执行前待决事项。数字变化不改变迁移模型；若 Fable 构建机制已改变，则必须先重新验证 aggregate 与传递 source merge canary，再决定是否仍需 closure-based audience。

## 11. 执行记录

### 2026-09-03 — 接手与 pre-cutover census

- 分支：`codex/verification-closure-v3`；接手 HEAD：`c58a501ac`。
- upstream：`f8c968fb1802e9cf8d772b3e436d58ec789cdb70`，已是接手 HEAD 祖先。
- 保留既有局部切分，不覆盖 `GitGateway`、`NodeFs`、`RequestKind/FallbackFacts`、Review 五类与 crash canary 成果。
- 现有正式 gate live census：49 semantic owners、178 localities、711 production sources、1851 ProjectReferences、784 份旧 contract records；DAG 绿色。
- 仓库没有可执行的 compiler-resolved locality dependency analyzer；`owner-contracts.mjs` production path 仍传入 `symbolUses: []`。因此本条只叫 pre-cutover structural census，不冒充 actual source-edge census。第一份正式 source-edge census 必须由 M6.1 新 analyzer fresh 生成。
- M6.0 将老板既有裁决写入 tracked WHY/WHAT/HOW/GAP：授权主键改为 locality→slice，same-owner 不豁免，`.fsi` 成为唯一 export inventory，GAP-031 保持 PARTIAL。
- M6.0 验证：`spec.mjs`、`requirement-trace.mjs`、旧权威 `owner-contracts.mjs`、`owner-projects.mjs` 全绿。该绿色只证明规范文档闭合且旧 gate 未被提前切换，不宣称 M6 新 gate 已实现。
- M6.1 RED：`locality-dependencies.test.mjs` 首次执行因 production analyzer module 不存在而失败；该测试先固定 missing closure、合法 direct/transitive、same-owner 不豁免、open/type/pattern 与 external symbol 边界，再实现纯 analyzer。
- M6.1 analyzer：`locality-symbol-uses.fsx` 从 fresh fingerprint flat project 读取 FCS declaration use；`locality-dependencies.mjs` 以 consumer/provider source pair 映射、去重并验证 locality closure。symbol 与 line/column 只服务本次诊断，不参与 edge identity 或授权；临时结果随 invocation 删除。无 snapshot、delta、mtime、跨 run cache 或 symbol ACL。
- M6.1 永久反例：fixture 的 consumer 与 provider 同属 `fixture` owner、consumer 无 provider ProjectReference；真实 flattened Fable aggregate 编译绿色，而 analyzer 必须输出唯一 `fixture-consumer → fixture-provider` missing-closure-edge。fixture 同时覆盖 open、alias/generic type、union-case pattern、value use 与 external package 排除。
- M6.1 fresh live census：178 localities、711 production sources、4,420 条 actual cross-locality source edges、3 条 missing closure edges；FCS 35.6s。三处 blocker 是 `InstitutionalLearningTools.fs → ToolHostCodec.fs`、`EventKWayMerge.fs → CanonicalEventCodec.fs`、`MessageVisibility.fs → HostEventCodec.fs`。这些事实进入 M6.2 修复，不写入 manifest/baseline。
- M6.1 仍不改变 release 权威：新 analyzer 只提供报告与自证反例；旧 `owner-contracts.mjs` 继续单独决定 release，直至 M6.4 原子切换。
- M6.2a：`enforcer-institutionallearning-fold` 已补上其既有 `HostToolContext`/`ToolSpec` 依赖对应的 `host-signal-adapter` ProjectReference。该 capability 在旧 manifest 已由 `host-boundary.Contract → institutional-learning` 精确授权；本次只让 compiler closure 与既有 production dependency 一致，不增加新语义依赖。
- M6.2b RED：直接令 `eventstore-merge-runtime → eventstore-core-runtime` 会触发旧 gate 的 `foreign-runtime-reference/composition-only-runtime-binding`；因此拒绝用扩大 runtime closure 的方式掩盖缺边。
- M6.2b sequencing：`CanonicalEventCodec` 应从混装的 `eventstore-core-runtime` 移到独立 contract slice；但旧 gate 依靠 codec 与 `ProcessEventLog` 共处一 project，偶然放行三条既存 durable-convergence → core-runtime 引用。拆分后若为维持旧 gate 而新增 owner-wide `ProcessEventLog` ACL，会延续错误授权模型。因此该拆分不得形成旧 gate 下的独立提交，必须与 M6.3 manifest 和 M6.4 gate replacement 原子落地。
- M6.2c：`opencode-host-messagevisibility` 已补上其 `HostEventCodec.unwrap/eventTypeOf/tryMessageSessionId` 依赖对应的 `host-signal-adapter` ProjectReference。旧 manifest 已有 `host-boundary.Contract → participant-horizon` exact grant；本次只修复 compiler closure。
- M6.2 verification：旧权威 `owner-contracts.mjs` 与 `owner-projects.mjs` 绿色；178 localities、711 sources、1,853 refs、DAG。两个可独立提交的缺边已关闭，相关 consumer focused Fable compile 绿色。fresh analyzer 现只报告 `eventstore-merge-runtime → eventstore-core-runtime/CanonicalEventCodec` 一条原子 cutover blocker；它将在 M6.3 工作树准备并随 M6.4 单提交切换，不会制造旧 ACL 例外。

### 2026-09-03 — M6.3 classification preflight：历史裁决停点

- 当前 direct ProjectReference 按 provider kind：contract 921、composition 797、adapter 54、runtime 81；其中 foreign 1,614、same-owner 239。新模型不豁免后者。
- 92 个标为 composition 的 locality 正在充当 provider，共 797 条 direct edge：768 条来自 composition consumer、19 条来自 adapter、10 条来自 runtime。现有 10 份旧 composition-root relation 不能证明这 797 条边全是 terminal wiring；自动把它们全部改写成 wiring 会把错误分类固化。
- effect matrix 有 10 条直接违规：`execution-session-loopdetector → host-signal-adapter`、`host-session-runtime → host-signal-adapter`、`host-signal-adapter → host-diagnostics-runtime`；`delegation-host-adapter → delegation-{recovery,fork,fold,sync}-runtime`、`delegation-pty-adapter → delegation-host-adapter/delegation-fork-runtime`、`delegation-recovery-runtime → delegation-fold`。反向闭包共影响 12 个 effect provider。
- 这些事实要求逐 owner 判断：①旧 composition kind 是错标，可改为 shared/bounded contract；②locality 混合 public capability 与 terminal wiring，必须拆 source/project；③确属 terminal composition，只能增加 exact composition-wiring relation；④effect consumer 本身应成为 composition，或抽 port 并改为 capability injection。引用图无法替 owner 做此选择。
- 推荐裁决：保持 M6.0 kind × exposure 矩阵不变；授权按上述四分法逐 locality 审核，先处理 29 条 non-composition → composition direct edge与 10 条 direct effect violation，再审核 768 条 composition → composition edge。禁止把当前 ProjectReference 集合自动抄成 grant。
- 当时在裁决前停止 M6.3；未生成新 manifest，未启用新 gate，旧 gate 仍是唯一 release authority，工作树保持可回溯绿色停点。下节裁决现已解除该停点。

### 2026-09-03 — M6.3 owner 裁决：经源码、签名与 live graph 复核后生效

本节记录 owner 已批准的迁移边界。它授权后续施工，不替代产品规范；每组 production 修改仍必须先把本裁决按 WHY → WHAT → HOW → GAP 写入对应 `requirements/<package>/`，建立 executable RED，再实现。若施工发现本节未覆盖的新业务语义冲突，停止并请求新裁决；ProjectReference 数量变化、文件移动或实现细节不构成重新裁决理由。

复核事实：工作树干净；旧权威 `owner-projects.mjs` 为 178 localities、711 sources、1,853 refs、DAG，`owner-contracts.mjs` 为 784 contracts；fresh compiler-resolved scan 为 4,420 条 actual source edges，仅剩 `eventstore-merge-runtime/EventKWayMerge.fs → eventstore-core-runtime/CanonicalEventCodec.fs` 一条 missing closure。此前 10 条 direct effect-kind 违规成立，但该 census 只按现有 kind 计算，漏掉了被错标为 contract、实际执行 `console.error/process.kill` 的 `host-fatal-effect`；后续 effect purity census 必须同时检查源码能力，不能只信 kind metadata。

#### 全局裁决

1. M6.0 的 kind × exposure 矩阵保持不变：contract 只承载纯数据、纯函数、opaque identity 与 capability type，不得承载 capability value/factory；runtime/adapter 承载 effect；effect implementation 只能由 composition 构造并注入；composition 只做 terminal wiring。
2. contract 默认 `bounded`。只有无 authority、扩张 audience 不增加任何决策或操作能力、且确属跨域基础词汇的 locality 才可 `shared`。不得以减少 manifest 行数为由选择 shared。
3. 当前 ProjectReference、旧 owner ACL 或现有 composition 标签都不是授权证据。禁止把现有 graph 自动抄成 grant、wiring、allowlist、baseline 或 suppression。
4. 同一 provider locality 全部 sibling `.fsi` 的公开符号并集共同授权。consumer cohort 或 authority 不同就拆 source/project；禁止在 JSON 中恢复编译器不能兑现的 per-symbol 权限。
5. 确定性、仓库派生、无IO/ambient state的generated module可通过exact `compile-contract-support` relation被消费，不因Fable `Import`或`RuntimeV1.Node`自动归为物理effect。该relation必须绑定精确import specifier、生成owner、consumer locality与可执行determinism proof；任一Host/ProcessControl/Network/FileSystem/Git/Provider等authority、mutable resource或Unknown不适用此条。
6. M6.4 仍为唯一权威切换点：新 manifest、compiler-resolved analyzer、schema/gate、所有原子 blocker 与旧 schema/gate 删除必须同一 commit 全绿。此前不得形成第二 release authority。

#### EventStore 裁决

1. `CanonicalEventCodec.{fsi,fs}` 从 `eventstore-core-runtime` 拆为 semantic owner 仍为 `durable-events` 的独立 `eventstore-canonical-codec` bounded contract。六个公开函数 `encode/checkIdentity/mergeByIdentity/tryDecode/tryDecodeUtf8Text/tryDecodeUtf8` 同属一个 canonical identity protocol，批准共同授权；禁止只复制或另写 `checkIdentity` 公式。
2. 初始 direct consumers 必须由 cutover 工作树 fresh 推导，并至少包含五个已证实 locality：`eventstore-core-runtime`、`eventstore-merge-runtime`、`eventstore-sync-runtime`、`persistence-eventstore-canonicalintegrator`、`durable-runtime-surface`。same-owner consumer 不豁免；bounded effective audience 只写允许上界，不把本次约 49 个反向可达 locality 的临时数值当事实源。
3. 禁止新增 `eventstore-merge-runtime → eventstore-core-runtime`。该边会把 `ProcessEventLog`、Store factory、文件/锁 authority 带入 merge runtime，旧 gate 与新矩阵都应 RED。
4. 接受 M6.4 时剩余 `ProcessEventLog + Store` 暂为一个 effect slice：它们共同拥有本地 EventStore 创建、读取、append、锁与恢复生命周期，且当前九个 direct consumers 均为 composition。该裁决只承认编译器已经存在的完整 `.fsi` 可见性，不恢复旧 manifest 声称的伪 symbol 隔离；M6.5 必须按 consumer cohort 与 authority 使用量复测，若完整 surface 共同授权不可接受，再拆 ProcessEventLog、store factory 与 lock adapter。
5. codec 拆分、五个 consumer references、新 bounded grant、旧 codec symbol ACL 删除必须随 M6.4 原子落地。原因是旧 owner-wide expansion 正借 codec 与 ProcessEventLog 共处一 project 偶然放行三条 core-runtime 引用；不得为过渡新增 ProcessEventLog owner ACL。

#### Host codec、signal 与 diagnostics 裁决

1. `HostMessageCodec` 建立独立 bounded contract。已证实 direct production consumers 为 `host-session-runtime`、`interaction-repair-interactionrepair`、`opencode-codec-providerprojectionsurface`；cutover 时 fresh exact census 后登记。它不得与 Loop codec 合并：两者 vocabulary、consumer cohort 与变更原因不同，合并只会扩大双方 audience。
2. 抽取无状态 `HostEventEnvelope` bounded contract，只拥有 raw Host envelope 的 unwrap、event type 与 session/message-session identity 读取。实现不得修改输入对象；当前 `HostEventCodec.attachDirectoryIfMissing` 的原地写入不能进入 contract。`HostEventCodec` 与 `LoopEventCodec` 必须共同消费该唯一 envelope 公式，禁止各复制一套 dynamic field parser。
3. `LoopEventCodec` 建立独立 bounded contract，仅发布 `TextDelta` 与 loop-delta decode/query。其 direct consumers 当前为 `execution-session-loopdetector` 与 `host-signal-adapter`；它只依赖 `HostEventEnvelope` contract，不获得完整 provider failure/terminal codec。
4. M6.4 前不把完整 `HostEventCodec`、`HostSignalAdapter`、subscription、event bus 或 `ToolHostCodec` 改成 contract。完整 HostEvent decode 仍拥有 HostSignal/failure/terminal 适配语义；其同 project 还含 physical listener/disposer、随机 tool handle、mutable listener/sticky registry 与 process-shared bus。M6.5 再按 audience 决定是否把纯 HostSignal vocabulary/full decoder另拆 bounded slice。
5. 当前 `HostSignalSubscribe` 返回 `Task<Result<HostSignalSubscription option * string,string>>`，并用 `option + "events.listen"/"local-event-hook"` 拼状态；这不是 typed error且可表达非法组合。先定义封闭 `HostSignalSubscriptionError` DU 与 subscription mode/source DU，例如 `LocalEventHook | EventsListen of HostSignalSubscription`，返回 `Task<Result<HostSignalSubscriptionMode,HostSignalSubscriptionError>>`；移除未使用的 optional timer。JS/Surface边界负责把 typed failure 渲染成字符串，JS decode failure 不污染 core DU。
6. 删除 `HostSignalSubscribe → Diagnostic.fatal`。`HostSignalBootstrap` 在 composition 边界解释 typed failure并执行 fatal，成为同一失败的唯一 fatal owner；不得让 adapter 与 composition 重复 fatal。若未来要求 adapter 独立报告，只可注入最窄 typed event capability，不能注入整个 Diagnostic 模块。
7. `LoopSensor` 继续与 `LoopDetector` 同属 `execution-session-loopdetector` runtime；本次不为文件数量而强拆。向其构造器注入最窄 `emitDiagnostic: string -> (string * string) list -> unit`，由 `HostSignalBootstrap` composition 传入 `Diagnostic.emit`。行为 proof 必须令 `emitDiagnostic` 抛错，并证明 arm、interrupt、consume、continuation 结果完全不变。
8. `#wanxiangshu-loop-detector-envelope` 是仓库内容派生的确定性 tokenizer/envelope artifact，裁决为 `generated_module_relations[kind=compile-contract-support]`，不是 Host/IO effect。必须保留 repository-SSOT、生成 determinism 与 import linkage proof；`LoopDetector` 因 process-local Dictionary scratch 继续是 runtime，不伪装成纯 contract。
9. `host-diagnostics-runtime` 保持 runtime；`Diagnostic` 读取 process env、写 console，`ReliabilityCounters` 持有可变状态。完成第 5–7 条后，非 composition consumer 不再直接引用该实现。
10. `host-fatal-effect` 当前 kind=`contract` 与源码冲突：`FatalProcess` 写 console 并调用 `process.kill/process.exit`。不为它设特例。bounded `FatalProcessPort` contract 只定义 incident vocabulary与 capability type，不包含 value/factory；fatal report path 的唯一 Node process adapter拥有该路径的 `console.error`、`process.kill`、`process.exit` 与 report/kill物理执行。该唯一性不吞并 `host-diagnostics-runtime` 的普通 diagnostic console effect。
11. composition 以必填构造参数把 fatal capability注入 Diagnostic、journal、recovery与 fresh census 找到的全部直接调用者。禁止 optional/default fallback、module-global mutable binding、service locator或直接到达 physical adapter；漏注入必须造成构造签名/类型失败。同一 typed fatal incident只有一个 report owner与一个 kill owner，不得重复输出/kill；committed/unknown settlement evidence必须先于 fatal。
12. FatalProcess RED 固定：漏注入、同 incident重复 fatal、先 fatal后 settlement、non-composition直接到达 physical adapter，以及 kind误标为 contract但源码仍有 process capability。实施前 fresh枚举全部 `FatalProcess.trip`、`FatalProcess.kill`、`Diagnostic.fatal` caller，并把所有受影响 semantic owner的 WHY/WHAT/HOW/proof纳入 M6.3a；不得依赖当前调用点或 package硬编码清单。
13. Host 规范与 proof 必须先改：现有 HOST-BOUNDARY-026/WHAT/HOW 和 closure tests 明文把全部 codec 固定在 `Host.Signal.Adapter`。实施时先重写这些事实，再建立三个 closure RED：`host-session-runtime` 不含 signal adapter、loop runtime 不含 signal/diagnostic implementation、signal adapter 不含 diagnostics runtime。M6.3a 还必须更新 `degeneration-guard`，固定 envelope repository-SSOT、determinism、package import linkage与 diagnostic noninterference。

#### Delegation 裁决

1. 拒绝把整个 `delegation-host-adapter` 只改标签为 composition。该动作虽能在 PTY 解耦后消除四条 runtime matrix 违规，却会把完整可变 `HostForkRuntime`、pending-run registry、join/child-dispatch lifecycle 冒充 terminal wiring。将现 locality 拆为 `delegation-host-runtime` 与必要的窄 Host adapter：runtime 只消费 delegation-owned contract ports；直接 OpenCode/Host 表示转换若存在则留在 adapter。当前源码有 5 个 `HostForkRuntime` constructor site，分布于 4 个 source/locality：3 个普通 composition construction root与 1 个 delegation proof Surface，其中一个普通 root含两个 site。graph 中 14 个 composition direct consumer不是 constructor census，另有 1 个 PTY adapter direct consumer；两类数字必须分开命名。是否建立最窄 `delegation-host-composition`，只由 cutover 时 fresh constructor census与重复 wiring证据决定，不由 reverse/direct consumer数量决定。同步修改 DELEG-028、WHY/HOW 与 compile-boundary proof，删除误导的旧 `delegation-host-adapter` locality/name，不保留兼容 facade。
2. `delegation-pty-adapter` 保持 adapter，禁止仅改成 composition 掩盖依赖。它不得直接读取 `HostForkRuntime.Gate/PtyRuns/TerminalByName/Runtime/Now/PtyPort`。禁止复用现有具体可变 `PtyPort`、携带 `TaskCompletionSource` 的 `PtyTypes.ReadPlan` 或宽 `IForkRuntimeBackend`。建立 source-pure capability contracts，至少表达：delegation-host-runtime-owned PTY name/binding；Fork snapshot register/unregister；Process PTY fork/send/read、completion subscription、close与 parent-abort registration lifecycle；mandatory clock由既定 Temporal port注入。contract只含不可变 record/DU与函数签名，不暴露 Gate、Dictionary、registry、TCS、stage/phase或 runtime object。删除 `HostForkRuntime` 内的默认 Node clock/PTY backend；若保留 parent-abort registry，只能经 Process-owner显式 injected capability到达，否则由 composition wiring取代。composition 从各 owner implementation构造最窄 capability并注入 adapter；adapter不得引用 HostForkRuntime、fork runtime或Process physical implementation。同一原子变更先删 `pty → host/fork/process` 再接 `composition → pty`，并固定 closure/SCC/illegal-state/missing-injection RED。
3. Delegation 领域 constructor 只返回 `ExecutionFactCases`、`DelegationFactCases` 或等价 typed intent，不得返回 durable composition 的 `AgentFact`。`AgentFact` 继续由 `durable-events` composition 拥有；`delegation-ledger` composition 是把领域 case 包进 `AgentFact.Execution/Delegation` 并 append 的唯一位置。禁止把 `AgentFact` 下沉成全域 shared contract，也禁止复制 outer-union wrapper。
4. 删除“把剩余 `delegation-fold` 整体归为 composition”的捷径。当前 fold拥有 handoff frontier单调性、estimate非负、handle identity/conflict/lifecycle等 Delegation业务规则；composition只能 terminal wiring，不能借改标签吞掉规则。M6.4 前先抽 delegation-owned linkage/estimate/handoff projection state、child-session handle index与 pure fold/decision；`DelegationFactCases`/`ExecutionFactCases` 由领域 owner产生，fold rejection也改为 delegation-owned closed type。durable composition只负责 `AgentFact` outer-union routing、调用 owner folds、把 owner rejection映射为 durable rejection并组合 projection结果；只有 `PromptAuthority` 更新通过 authority owner decision协调，禁止在 composition复制任一业务规则。不是把当前整个 fold原样改成 contract，而是切开 owner fold与outer coordinator。`delegation-recovery-runtime` 只依赖 delegation-owned fact/query/append capability。该项是 correctness迁移，不得推迟到 M6.5；M6.5只可继续按 audience/closure收益优化已正确切开的 projection。
5. `HandleController`、`ChildRecoveryWorkflow` 等 runtime 不再接收完整 `AgentJournal`、central projection 或 composition clock。定义 delegation-owned append/query/wait/clock capability types，composition 注入实现；runtime 只产生 typed request/intent 与消费 typed result。
6. `foundation-temporal` 至少拆为 pure clock/timer capability、pure Deadline、Node adapter、virtual verification implementation与独立 SessionStartedAt projection；`execution-session-wait-causalwait` 至少拆为 vocabulary/frontier contract、registry/await runtime、Node diagnostic adapter、CompletionMailbox runtime与独立 proof Surface；`process-processrequest` 至少拆为 process request/outcome/error contract、one-shot capability、Node process adapter、owner-pure PTY vocabulary/narrow capability、Node PTY adapter，并按 consumer cohort裁决 spool/output runtime。现有 `PtyTypes` 因携带 `ManagedAgent`/TCS不能原样升为 contract。Delegation runtime/PTY 只能依赖 contract或注入值，不能继续把这些 composition locality当公共 provider。
7. 迁移顺序固定为：Temporal/CausalWait/Process capability → Delegation fact/ledger port与 owner projection/fold → Host runtime的 recovery/fork/fold/sync injection ports → PTY去反向依赖 → 既有 composition roots构造 Host/PTY capabilities → durable outer coordinator只保留routing/combine → 删除旧 references/names。共享 type/port先立，consumer后迁，旧 owner最后删；中途不得提交双实现或 compatibility facade。

#### 当前 92 个 composition provider 的裁决方法

1. 先处理全部 non-composition → composition edge；这些边不得登记 composition-wiring。纯 vocabulary/decision/codec 拆为 bounded/shared contract，effect 通过 capability injection，真正 consumer orchestration 改到 composition。
2. 再处理 composition → composition edge。只有“下游 composition 是上游 terminal wiring 的组成层”且两端 module、capability、WHAT law 与 semantic evidence 均明确时，才登记 exact composition-wiring；公共 query、fact、projection、codec、registry 或 helper 不因 consumer 也是 composition 就自动合法。
3. `Composition.Durable.Fact/Projection` 保持 durable composition，不改成公共 shared contract。非 composition runtime 必须改为返回 owner-owned case/intent、消费 owner-owned query/append capability；由 durable composition统一 outer routing与跨 projection协调。
4. `deriveAdjudicationCandidates`为每个fresh live locality输出stable key+reasons；M6.3b worksheet只服务迁移，M6.3c formal snapshot使用4.5 closed schema，保存world/query/index digest、terminal classification、exact manifest claim IDs与WHAT/proof，不复制actual集合。`records[].locality_id`必须与fresh candidate keys精确相等，classifier/claims/proofs零mismatch、零`undecided`、零current-reference-derived grant；split由locality key变化自动反映，不硬编码92。

#### 裁决后的施工批次与停止条件

1. M6.3a：先更新`requirements/structured-workflow/{WHY,WHAT,HOW}.md`与全局`requirements/GAP.md`，固定v2 schema、`C(W)`/`J(W)`分离、generated artifact canonical row、Node runtime非authority、semantic staged-input closure及全部violation code；再更新durable-events、host-boundary、degeneration-guard、delegation、time-capability、causal-wait、process-execution及fresh census找到的所有FatalProcess caller owner WHY/WHAT/HOW/GAP。建立行为oracle与architecture/closure illegal fixture；先观察旧世界被新oracle判为违规，再提交全绿fixture，禁止提交红色suite。该步骤可立即开始；这些规则进入正式WHAT并全绿之前不得启动M6.3b production extractor。
2. M6.3b：提交report-only pure validator/property、fresh全集worksheet，以及旧gate下可独立绿色的contract/port split。worksheet可随绿色节点更新，但只有migration-only closed schema、无digest/authority；pure property进入unit sink，live新模型不得阻断release。每组一个绿色Git节点，运行provider、direct consumer与reverse impact compile。
3. M6.3c：只在同一未提交cutover工作树准备旧gate确实无法表达的最小production切换与最终manifest；不夹带M6.5优化，不形成独立commit。按4.5协议stage全部input并核对index/working tree，fresh重建world/candidates/query，生成formal snapshot；locality/world/query/index/classifier/claim/proof任一mismatch都要重裁，零mismatch后才可进入M6.4。
4. M6.4：对同一staged state一次复验，单个绿色commit启用pure validator/schema/new authority，接入恰一次production fresh scan，激活最终manifest，并删除旧owner-wide/per-symbol/compile-support权威、worksheet、临时actual dump及所有迁移路径；formal snapshot随commit冻结，post-cutover gate不再读取。执行fixed negative oracle、fast-check、fresh production scan与完整release sink。
5. report-only parser/analyzer/fixture存在不等于第二权威；只有 live old/new gate同时能够阻断 `format-build-test` 才是双 release authority。M6.4 后不得保留可供 release 降级的 report-only bypass。
6. M6.5：只按可测量 audience/closure收益继续拆 `ProcessEventLog + Store`、完整 HostEvent/HostSignal codec与已正确迁出的 Delegation projection；没有收益就不拆，不承接 correctness debt。
7. 任一阶段若需要放宽矩阵、把 current refs 自动变 grant、让 adapter/runtime直接消费 effect implementation、把 central composition下沉为公共 contract，或新增本节未定义的业务 owner 转移，必须停止并请求新裁决。

### 2026-09-03 — 外部建议逐项复核与施工边界修订

- 已按源码、`.fsi`、fsproj、现行 gate与 fresh FCS scan复核全部建议；EventStore既有裁决不变。
- 状态与完成集合改为全部 live locality classification/adjudication；92只保留为当前带 `CompositionProvider` reason的子集。
- public surface固定为同 locality全部 sibling `.fsi` exports并集；新增 canonical locality capability facts与 clean-break exact generated-module relation。旧 `compile_contract_support`裸路径语义明确退役。
- Host subscription改用 typed error/mode计划；FatalProcess固定 mandatory capability injection与唯一 fatal Node adapter；HostForkRuntime constructor census修正为5 sites/4 localities；PTY/Temporal/CausalWait/Process边界按实际 effect面扩充。
- Delegation business fold不得改标签塞入 composition；M6.4前迁 owner projection、child index与closed rejection，durable composition只保留outer routing/combine并调用 PromptAuthority owner decision。
- production fresh scan固定只在integration release path执行一次；fast-check消费production pure decision与canonical facts，采用legal-world + single mutation；M6.5只优化，M6.6比较normalized authorization projection，M6.7只做final census/release/report/GAP close。
- 三处没有按当轮建议例句的字面shape落盘：不把任意`Import`/`Emit`等同effect；不把92硬编码为完成数量；adjudication record不复制actual facts。当轮采用versioned world/query digest；后续review又补齐index、manifest claim与snapshot生命周期，见下一节。前两项会分别误杀pure/generated case、在split后失真；第三项避免把review evidence变成第二事实源。
- 验证：`spec.mjs` 291条款绿色；`requirement-trace.mjs` 780 WHAT/3977 tests绿色；`owner-contracts.mjs` 784 contracts绿色；`owner-projects.mjs` 178 localities/711 sources/1853 refs/DAG绿色；`npm run check`完整fast gate绿色。fresh analyzer重现4420 actual source edges与唯一已知missing closure，故GAP-031保持PARTIAL。

### 2026-09-03 — reviewer 第二轮执行阻断与收口意见闭合

- 4 个 P1 全部接受。v2 顶层、slice、capability relation、generated relation 均改为 closed schema；private 由无 slice row 唯一表示，composition 有 slice identity但禁止 exposure。v1 全部顶层字段、nested metadata与 parser兼容路径逐项给出 clean-break命运。
- `deriveAdjudicationCandidates(world)`固定以全部live locality为key universe；graph、capability、composition、每种closed relation endpoint与missing closure只增加稳定reason。增删locality、split、mismatch与全部endpoint均有指定fixture，M6.3以`records[].locality_id == candidate keys`验收。
- 当轮adjudication record保存schema/world/query digest与decision；actual export/audience/edge/fact只进ephemeral live report。后续review进一步把它固定为M6.4 one-shot snapshot并增加index/claim binding，见下一节；本条不再定义post-cutover live一致性。
- 当轮capability fact已拆开observation与classification；本轮进一步收敛为raw observation+唯一disposition+多轴labels。manifest metadata只属normative claim；Unknown阻断cutover，physical observation不能被metadata降格。`observation_id`覆盖去诊断位置后的完整raw observation，`fact_id`再绑定disposition；world/query共用versioned canonical JSON规则。
- 4 个 P2 全部接受。slice law只归provider owner，架构law自动施加；slice/relation evidence与law双向覆盖，specialized relation与graph/direct grant/source edge取交集。M6.3a首项显式更新structured-workflow与全局GAP。fast-check固定legal GREEN base→single mutation→exact code/coordinates，shrink保持同一前提；Unknown另有exact property。不存在的changed-locality lane从当前能力删除，仅可在M6.5后凭性能证据另立node，release永远信full scan。
- 低风险项全部接受：symbol/line只允许ephemeral diagnosis；fixed impact corpus在任何M6.3b production split前落盘，baseline/candidate各三次同环境clean run并保留raw/median，5%只触发调查；执行记录按真实三项非字面采用修正。
- 后续终检补出的三处歧义也已闭合：删除会被误认成live grant且consumer不完整的EventStore row，改为空schema skeleton与独立evidence shape；generated v2固定`laws = [determinism_proof.what_id]`；v1 owner/path只作迁移定位并与fresh graph重验，N→1 justification不拼接或继承，由目标owner依据formal adjudication重写。
- 两个独立 blocker-only复核均返回“无阻断”。本轮只修改计划，不启用v2 schema/gate、不改变production，也不改变GAP-031=PARTIAL。
- 验证：`spec.mjs` 291条款；`requirement-trace.mjs` 780 WHAT/3977 tests；`owner-contracts.mjs` 784 contracts/0 requirement dependencies；`owner-projects.mjs` 178 localities/711 sources/1853 refs/DAG；structured-workflow 244/244；`node scripts/check.mjs`完整fast gate全部绿色。

### 2026-09-03 — reviewer 第三轮执行协议裁决

- P1-1接受：adjudication工件拆成M6.3b worksheet与M6.3c formal snapshot；M6.4只对同一staged state验真一次。snapshot随cutover冻结，M6.5–M6.7及永久release gate不得与live world比较、更新或读取授权。
- P1-2接受：补齐`CanonicalWorldV1`、normative projections、query rows、closed DU JSON、closure/self约定、identity/sort/dedupe/illegal-value规则与唯一`encodeCanonicalJsonV1`/`serializeCanonicalWorldV1`。当轮先把generated linkage收进observed fact；下一轮进一步规范为唯一`GeneratedArtifactRowV1`，fact只引用ID，见下节。
- P1-3接受：record改为sorted array，terminal classification必须等于production `classifyTerminalV1`；`manifest_claim_ids`必须等于同world的slice/consumer-relation投影；WHAT/proof shape、existence、HOW、Surface与owner校验及closed RED codes全部固定。
- P1-4接受：capability extractor固定完整observation partition与多轴标签；Date/time/random/timer/console/env/process/fs/network及alias/FQN有closed rule，unsupported输入fail-closed。下一轮将JavaScript普通AST node从capability universe拆到独立traversal coverage，保留同等零遗漏约束，见下节。
- P1-5接受：physical-port完整surface允许`PureRepresentation + CapabilityTypeOnly`且必须至少一个capability type；value/factory/effect/authority/mutable/Unknown全部拒绝。consumer仍由自身actual fact/kind policy验证，不恢复symbol ACL。
- 两个收口项接受：fast tier只跑schema/canonical/pure validator与fast-check；唯一production full scan进入独立integration step并由release sink恰执行一次。cutover使用排除formal snapshot/worksheet后的全tracked staged-entry digest，并在scan前后核对working tree；不存包含snapshot自身的tree OID，避免OID↔内容自指。成本census更新为161 unique foreign providers、1,614 foreign refs、239 same-owner refs、797 composition-provider refs。
- 无顶层建议被拒绝。仅拒绝两个可能的字面实现：①post-cutover持续比较snapshot与live world，会使合法split/owner merge永远RED；②把包含snapshot自身的完整tree OID写进snapshot，无法构造固定点。两者分别由one-shot lifecycle与filtered staged-entry digest实现同一目标。
- 本轮仍只修改计划，不启用v2 schema/gate、不改变production，GAP-031保持PARTIAL。

### 2026-09-03 — reviewer 第四轮 extractor 前协议闭合

- P1-1接受：`C(W)`只含可由closed schema表示的capability observations并执行exact disposition partition；`J(W)`独立枚举每个Emit/generated-JS AST node。structural enumerator与semantic visitor的key集合必须相等，ordinary node只记coverage，missing/duplicate/unknown node各有exact RED。
- P1-2接受：新增唯一canonical `GeneratedArtifactRowV1`，承载artifact path/output digest、selector-input digest、linkage与traversal ID。Fable import/generated-JS facts只引用artifact ID；query从ID投影原row，manifest仍只保存normative relation，不制造第二事实源。
- P1-3接受：统一所有purity/compile-support表述。`RuntimeV1.Node/Bun/Browser/ExternalPackage`本身不决定RED；只有authority/mutable/forbidden semantic class/Unknown决定拒绝。新增`RuntimeV1.Node`单标签GREEN property。
- P2-1接受：worksheet始终只有4.5 migration-only closed schema；“无固定schema”全部改为“不含digest、无授权力”。
- P2-2接受：cutover input closure改为tracking reader的actual reads、repository-local import closure与selector outputs，不按扩展名猜测。generator/build/selector及每个selected input必须命中stage-0 tracked entry；唯一build-output exception由artifact row承接。Git object format只取`git rev-parse --show-object-format`。
- 无建议被拒绝。源码复核确认当前loop-detector generator仍调用返回text的`loopDetectorRepositoryTexts`，而同模块已有path-producing `loopDetectorRepositoryInputFiles`；M6.3a必须先迁generator到后者并经reader取bytes。当前仓库object format为`sha1`，三条linkage entry均为stage-0 tracked blob。
- 计划复验：`git diff --check`零错误、Markdown code fence为64个且闭合；`node --test requirements/structured-workflow/tests/*.test.mjs`为244/244；`node scripts/check.mjs`全绿，包含291条spec、711个production source、178个locality、784个contract与780 WHAT/3977 tests requirement trace。
- 执行边界：M6.3a可立即开始；本节schema、规则、RED code与proof必须先进入正式`structured-workflow` WHY/WHAT/HOW/GAP并全绿，随后才可启动M6.3b production extractor。无需重开EventStore、Host、FatalProcess、Delegation、PTY或老板裁决。
- 本轮只修改计划，不启动M6.3a production、不启用v2 gate，GAP-031保持PARTIAL。

### 2026-09-03 — M6.3a执行前preflight与upstream同步

- 起点为reviewer批准的干净HEAD `bfbee8f2abf4ed788cc3b6383b096e88604ed385`。执行第10节preflight时发现upstream前移到`edc97d45bc2ad4898117abc2aceec654040b1572`（`canonical single-version roles, universal cursor mode, and failover summary retry`），先完成语义合并再建立基线；Git object format由`git rev-parse --show-object-format`确认为`sha1`。
- 三处文本冲突均按细粒度语义解开：① admission decision proof保留本地固定production-bound counterworld，拒绝恢复Cartesian `expectedFor`镜像，同时采用upstream唯一`coder`身份；② participant identity HOW采用upstream PID-001..009与single-version role语义，把本地recovery proofs重映射到PID-008；③ identity recovery proof同时保留本地exact durable payload/typed rejection因果断言与upstream实际`recoverActiveIdentity`断言。两套断言调用同一production surface，无平行oracle。
- 合并后主动修复一处upstream原内容：`src/Wanxiangshu/Interaction/Authority/Child.fs`在`edc97d45b`中把二参数application的第二参数缩进到错误列，`format:check`必红；Fantomas只恢复规范缩进，不改变表达式、类型或控制流。最终完整sink的format/build/unit/integration/E2E证明该修正。
- 三次失败基线均保留因果分类。首次非特权sink为3928/3930 unit：Host canary因沙箱禁止`127.0.0.1`监听而失败，沙箱外重放2/2绿；proof-ladder真实发现M6.1 `locality-dependencies.mjs`既非pre-build gate也未登记为alternate entrypoint。修复后将它明确登记为M6.4前的report-only integration analyzer，proof-ladder 12/12绿，未赋予第二release authority。第二次sink在integration发现analyzer错误从隔离后的`HOME`寻找Fable程序集；改为`NUGET_PACKAGES → DOTNET_CLI_HOME → OS home`解析，复现runner隔离环境的compiler fixture 1/1绿。第三次sink发现本地process-restart scenario仍以已退役`fast-coder`建立正向身份；该文件不在upstream，属于本地proof对upstream语义的漏迁。改为`coder`后真实进程重启canary 1/1绿；其余legacy agent字符串均为拒绝负例，保留。
- 最终`npm run format-build-test`从头退出0，wall约111s：format绿色；fast gate为290条spec、49 owners、178 localities、711 production sources、1,853 refs、784 contracts、779 WHAT/3,978 tests；Fable 5.13.0单次clean full invocation解析1,460 sources、编译1,422 impact items用31.624s，build总计36.2s，165 surfaces/780 emitted modules闭合；unit 3,930/3,930；全部integration绿色，其中owner compiler/analyzer组5/5、verification harness 273 cases；Long Stroke单独复验60 steps/6.6s flow、进程15.0s；`npm pack --dry-run`产出2.2MB tarball、2,059 files。
- fresh production analyzer用19.716s得到178 localities、711 sources、4,421 actual cross-locality source edges与唯一`eventstore-merge-runtime/EventKWayMerge.fs → eventstore-core-runtime/CanonicalEventCodec.fs` missing closure。它仍是M6.3c/M6.4既定原子blocker，不是新增M6.2遗漏；19.716s远低于185s project-check预算，M6.4仍只允许release integration lane fresh执行一次。
- 全部163个direct provider的无owner过滤反向闭包排名见`docs/OWNER-CONTRACT-SLICE-PREFLIGHT-CENSUS.md`；完整row digest为`7fb847f52189438e13746c8d57ee84f46272add4b44a440a047a8941f1e0dbb0`。最高三项为`foundation-roles` 166 projects/672 sources、`foundation-identity` 163/663、`participant-provider-projection-model` 134/584；`git-gateway`仍为4 projects/22 sources，证明统计口径与历史canary一致。
- 本次upstream变更没有产生需要新老板/owner裁决的owner merge候选；数量相近或引用密集不能自动推出语义合并。现有49 owner与历史M6.5候选保持待测，不在M6.3a扩大范围。project规划仍以190–210作容量导向：当前178不构成不足，后续只因effect隔离、授权边界或可测closure收益增加locality。
- test运行产生的fixture-local NuGet migration marker加入精确`.gitignore`路径；不扩大通配范围，不把production、manifest或cutover input隐藏。M6.3a下一节点严格从structured-workflow与全局GAP的WHY→WHAT→HOW→GAP开始；production-owned oracle及全部RED fixture全绿前不得进入M6.3b。

### 2026-09-03 — M6.3a canonical oracle与owner边界闭合

- 正式规范先行：`structured-workflow`新增STRUCTURED-WORKFLOW-013..016，分别固定唯一canonical world/classifier/query、`C(W)` capability partition与`J(W)` AST traversal、generated module artifact/input/lineage、同一staged input与worksheet/formal snapshot生命周期。全局GAP-031保持PARTIAL；M6.3a只建立目标语义与pure oracle，不宣称production extractor、全量adjudication或v2 cutover已完成。
- 唯一canonical primitive落在`scripts/lib/canonical-json-v1.mjs`：Unicode code-point comparator、closed JSON bytes、repository path与SHA-256。array额外属性、accessor、symbol key、sparse slot、非法number、unpaired surrogate、non-plain object全部拒绝。`GeneratedArtifactRowV1.id`严格等于无额外domain的`generated-artifact/v1:SHA256(encode({artifact_path,linkage}))`；artifact bytes与selected input rows分别产生独立digest，traversal ID只由`{source_kind,source_id}`派生，不允许test placeholder。
- `scripts/lib/locality-slice-world-v1.mjs`实现closed canonical projection、source/signature pairing、fact coverage重算、law/evidence双向覆盖、唯一provider slice、source/locality reference、forward/reverse closure、terminal classifier、全部locality candidate universe与surface/audience/capability三种query。candidate始终含`TerminalClassificationRequired`，并机械增加slice/capability/generated relation endpoint、capability-bearing、kind mismatch、composition provider与missing closure reason；capability query只从fact reference投影artifact/traversal，不保存第二事实源。
- `scripts/lib/capability-observations-v1.mjs`实现raw observation/disposition/fact closed algebra、stable identity、唯一partition、Node-neutral多轴classification、generic JavaScript AST enumerator与独立visitor。parsed Emit由traversal拥有，parse失败才是`UnparsedInterop`；`node:path/posix`纯反例、ambient path、Date.parse/Date.now、filesystem/process/console/timer/Host与generated tokenizer规则均有相邻fixture。ordinary AST node只进`J(W)` coverage；missing、duplicate、unknown与source-union mismatch各有独立exact RED。
- `scripts/lib/generated-artifact-v1.mjs`实现tracking reader、selected-input digest、artifact row与generated relation validator；missing/stale/duplicate relation、artifact、traversal，specifier/target/linkage/output/input drift、fact reference、determinism与physical authority分别命中独立code。`scripts/lib/cutover-inputs-v1.mjs`实现semantic import/selector/read closure、stage-0/index/working-tree binding、object-format OID规则及closed migration worksheet；`scripts/lib/locality-slice-adjudication-v1.mjs`实现one-shot formal snapshot的world/query/index/classifier/claim/WHAT/proof binding。
- reviewer点名的generator旁路已消除：`loopDetectorRepositoryInputFiles`只选择Git tracked filesystem path，不再读取bytes；`loadLoopDetectorRepositoryCorpusV1`在reader边界拒绝root外路径并规范为canonical repository-relative identity，随后才由`createTrackingReaderV1`取得bytes并decode/filter/tokenize。`writeLoopDetectorEnvelopeArtifact`以同一input rows与实际output bytes返回canonical artifact row。真实clean build重新生成dist后，repository freshness、语料选择与tracking-reader proof共同绿色。
- 首次完整sink发现selector返回shape被误改成repository-relative path，导致既有build-freshness消费者遗漏临时仓库的新输入。修复保留selector的filesystem path public contract，把root内校验、relative identity与duplicate判定放在tracking-reader前的generator boundary；root外输入在读取前得到`generated-selected-input-outside-root`。既有build-freshness、local export exclusion、absolute selector与tracked identity四个反例共同绿色，未恢复selector内`readFileSync`。
- integration harness又发现classifier把`gpt-tokenizer/encoding/o200k_base.*`写成filesystem `.startsWith`判据，触发“无法命中仓库路径的伪gate”永久反例。最终实现用`identityIsOrExtends`表达module/member ownership，tokenizer exact member为pure external package、`o200k_base64`近似名为Unknown；同一helper也统一`node:path/posix`与`node:path`边界。正式273-case harness为273/273。
- 首轮boundary test先永久RED：18个owner test因唯一`locality-slice-policy-v1.mjs`不存在统一失败。实现后又主动复核并拒绝首版“测试手写authority/semantic class”的blueprint，因为它会形成test-only mirror；最终policy只接受classifier产生的`CanonicalCapabilityFactV1`与dependency mode。contract purity、effect compile reachability、physical-port完整surface，以及fatal mandatory injection/settlement/incident唯一性均由同一production pure oracle判断。
- fresh lexical caller census先枚举所有`FatalProcess.trip/kill`与`Diagnostic.fatal` source，再用semantic-owner registry映射；排除`Enforcer/Host.fs`中唯一comment decoy后，25个actual caller source属于14个owner：behavior-diagnosis 1、capability-enforcement 1、context-compression 3、delegation 2、dispatch-protocol 1、durable-events 3、execution-model-routing 1、finality 1、host-boundary 5、interaction-authority 1、knowledge-reuse 1、managed-session-lifecycle 2、obligation-ledger 2、repository-programming 1。上述owner全部新增WHY/WHAT/HOW与exact fixture；另为EventStore、Host codec/signal/diagnostic、loop detector、Delegation、Temporal、CausalWait与Process/PTY点名边界新增owner law。M6.3b必须以compiler/parser observation重做caller census；本计数不是永久allowlist。
- RED→GREEN阶梯：point-boundary missing-module为18/18 RED；实现后owner fixtures为22/22 GREEN。canonical/artifact/input/adjudication fixed tests与固定seed fast-check加入后，受影响组合为31/31 GREEN；clean Fable build解析1,460 sources并编译1,422 items后，包含真实loop-detector freshness的定向阶梯为40/40 GREEN。随后`node scripts/check.mjs`全绿：300条spec、711个production source、49 owners、178 localities、784份旧contract、805 WHAT/4,010 tests requirement closure。M6.3a checkpoint只在最终`npm run format-build-test`对同一工作树退出0后提交；完整sink事实写入该Git节点正文，避免文档更新再次改变repository-derived artifact。
- M6.3a没有启用v2 manifest/parser/gate，没有把worksheet或formal snapshot写入仓库，也没有让new model阻断release；不存在双重权威。下一节点M6.3b必须接production F#/FCS/JS extractor、以report-only模式生成fresh capability/world findings与全locality worksheet，并实施旧gate下可独立绿色的contract/port split；production owner migration proof必须走registered Surface，当前architecture fixtures不得冒充迁移完成。EventStore唯一missing closure继续留给M6.3c/M6.4原子切换。

### 2026-09-03 — M6.3a PR前最终upstream同步

- PR前fetch确认upstream由`edc97d45b`前移到`ed21b6d31`，新增5个提交，作用面为Host ingress/session agent、model-routing reservation、managed chat attempt-plan acceptance及`proposals/Sphinx.md`。merge节点`2b28654a8`无文本冲突；`node scripts/check.mjs`仍为300条spec、711个production source、49 owners、178 localities、784份旧contract、805 WHAT/4,010 tests，全绿。
- merge后的首次完整`npm run format-build-test`在unit层得到3,961/3,962，唯一失败为既有production-bound proof `MISC_ingress_session_id_sources`：upstream `ef66bdb7c`为支持对象形状的`session`新增`sessionIdOfPart`，却用`ses_`值前缀识别字符串，使原本合法的opaque字符串`"s1"`被投影成`null`。该失败由合并引入，不是M6.3a canonical oracle改动，也未通过削弱断言消除。

### 2026-09-04 — M6.3a 独立复核与修复裁决

- 完整release sink与GitHub CI只能证明既有assertion；对最终diff执行“违反WHAT且proof仍绿”的反向审计后，确认M6.3a初版不能宣称pure oracle闭合。确定阻断包括：canonical fact disposition未绑定唯一classifier；generated validator读取test-only顶层`artifact_id`并接受第二`artifact_references`事实源；J(W) visitor输出被同一测试回填为expected；known AST上的computed/CJS/parameterless Date/dynamic target可退为NoCapability；world artifact/traversal reference、traversal result union及semantic import scan closure存在fail-open；generated relation未绑定member、callback、execution lineage与proof owner/law。上述均由既有STRUCTURED-WORKFLOW-014..016唯一裁决，不重开业务owner。
- JS visitor技术接口固定为scope-aware input：binding provenance只能是`local | imported | free | unresolved`；M6.3a observation schema必须能够保存该provenance，`unresolved`得到Unknown。M6.3b负责用compiler/Acorn scope resolution产生provenance，不得让pure oracle按字符串猜local/free，也不得把scope resolution留给测试。
- capability/traversal边界完成第二轮收紧：observation、disposition、fact、diagnostic与visit collection的非array输入统一返回`capability-extraction-incomplete`；traversal validator从raw AST与scope resolver内部重建唯一node universe，不再接受可与visits协调删减的caller node rows；canonical world拒绝`ast_node_count=0`。永久反例固定同步删node/visit/fact、非法collection与零node canonical row，防止自洽假闭集通过pure oracle。
- generated relation observed evidence使用closed row：actual import、generator/build/selector execution lineage、registered determinism proof与runtime Surface callback分别拥有stable identity。canonical capability fact的正式observation payload是artifact reference与authority的唯一来源；删除`artifact_id + disposition`和任意artifact-reference list镜像。同consumer+specifier是relation semantic identity，不同ID重复该key、同consumer多余未命中relation、member/callback/lineage/proof漂移分别产生独立exact violation。
- generated relation的relation/artifact/traversal及全部observed evidence现均执行exact-key schema；零node或`unknown_node_count>0`不得成为合法artifact traversal。`validateJavaScriptTraversalV1`显式返回同次raw-AST traversal产生的`emitted_observation_ids`；M6.3b extractor须把它作为唯一`TraversalObservationSetV1`输入relation validator。该ephemeral row不进入canonical world且不授权，只用于与exact artifact的canonical JavaScript fact observation-ID union交叉验证；missing/duplicate/stale set、删除全部JS facts及伪造空union均有独立永久RED。本条只闭合M6.3a pure oracle，未实现或冒充M6.3b production extraction。
- Host ingress复核同时发现同类runtime边界债：Fable擦除`unbox<bool>`后truthy scalar可伪造compaction/synthetic/abort，非Array parts可抛异常，session/agent响应可经字符串化取得领域身份。该语义归还`dispatch-protocol`与`host-boundary`，不再借`PROVIDER-PROJECTION-003`承载。SessionId保持opaque原字节；全部正式carrier同值才合并，任一显式非法或异值冲突fail-closed；嵌套对象只接受plain own-property JSON record。
- cutover input反向审计确认原state validator接受caller预构造closure，并另收一份build-output与任意exclusion；空closure可与自洽index一起变绿，`src/**`也可从digest消失。修复后validator只收closed raw closure input并内部唯一求closure/build outputs；formal snapshot/worksheet exclusion固定在协议内。全部collection与index row先验closed schema，全index的nonzero stage、`120000`、unsupported object format、额外byte row及selected-input/build-output交集各有exact RED，未合并项不再因closure无关而静默跳过。
- 本轮先形成一个绿色M6.3a repair Git节点，再启动M6.3b。upstream/master复核仍为`ed21b6d31659e5bc180cd41399145270f383c608`，当前分支已包含，无待合并提交。
- 前一节点的session修复保持upstream新增能力并恢复旧合约：decoder按JavaScript真实类型区分string与object；任意非空字符串继续作为opaque SessionId，对象只接受`id/sessionID/sessionId`三个字段，未知对象与非字符串标量fail-closed。当时语义暂记在`PROVIDER-PROJECTION-003`，本轮已归还真正owner并扩展为完整carrier algebra。
- 前一节点最终diff审计补齐对象字段、直接字段为number及空白字符串的非法世界；所有字符串入口复用同一typed reader。第一次`node scripts/check.mjs`据此发现`fromSource`新增`if → if` control pyramid；随后以null-safe candidate projection恢复零baseline。本轮进一步证明该例子集仍未覆盖carrier冲突、bool truthiness、非Array parts与Host response字符串化，因此重新打开完成判断。

#### 后续执行账本

1. `R0 M6.3a repair`（DONE）：关闭canonical fact、J→C、generated relation、world/traversal/input closure与Host raw membrane反例；定向proof、fast gate、clean build、受影响测试与真实Host Long Stroke全绿后建立Git节点。
2. `B1 M6.3b extractor`（DONE）：B1-A=`5830fa473`实现report-only fresh F#/FCS/JS/generated extractor、唯一canonical world/summary/full report与178-record closed worksheet；B1-A2=`4c831aca0`固化baseline定义与clean exact-commit writer；A3=`e6268f35a`删除synthetic FCS node alias。B1-B在A3 exact clean checkout完成fixed impact corpus，production gate仍未接入。
3. `B2 EventStore slice`（PROOF DONE；PRODUCTION DEFERRED TO C/D）：production-bound codec proof已锁定六项公开协议；在M6.3c同一未提交cutover state抽`eventstore-canonical-codec` bounded contract并迁fresh direct consumers，再随M6.4原子提交。它不能形成旧gate下独立节点：旧ACL正借codec与`ProcessEventLog`同project偶然授权，过渡节点若维持绿色只能扩张错误的owner-wide权限。provider/direct/reverse-impact编译仍须在原子提交前全绿。
4. `B3 Host slices`（CODEC + DIAGNOSTIC + SUBSCRIPTION DONE；FATAL PENDING）：HostMessage/HostEventEnvelope/LoopEvent三个bounded contract、LoopSensor mandatory diagnostic injection、closed typed subscription与Bootstrap唯一failure owner已完成；下一节点迁fatal port/adapter。
5. `B4 foundational capabilities`（TEMPORAL PROOF + SOURCE/PROJECT SPLIT DONE；INJECTION + CAUSALWAIT + PROCESS/PTY PENDING）：Temporal synthetic blueprint已替换为production-bound行为proof；clock/timer capability、Deadline、SessionStartedAt projection、Node adapter、virtual implementation与representation Surface已分为六个locality。ordinary runtime对Node adapter的direct construction/default fallback仍须在后续B4/B6迁为mandatory injection；CausalWait与Process/PTY继续按先contract、再consumer、最后删旧路径施工。
6. `B5 Delegation ownership`（PENDING）：迁owner fact/append/query/wait/clock ports与pure projections/folds，durable composition只保留outer routing/combine。
7. `B6 Delegation Host/PTY wiring`（PENDING）：拆host runtime/窄adapter，删除PTY对HostForkRuntime与Process implementation反向依赖，由现有composition roots注入。
8. `B7 full adjudication`（TAXONOMY CHECKPOINT B DONE；ADJUDICATION PENDING）：F# compiler-resolved结构、mutable scope、exact external authority/pure family、recursive closed-type algebra与stable occurrence identity已闭合两批；fresh仍有172,735条fail-closed Unknown与`RootWorkspace` extraction blocker。后续按exact family/type/escape证据分组关闭，再逐条写入非current-reference-derived裁决，最终零undecided、零extractor blocker。
9. `C M6.3c staged audit`（PENDING，禁止独立commit）：只准备旧ACL无法表达的最小cutover与终态manifest；stage全部input，fresh重建world/query/index并生成一次formal snapshot，任一mismatch整体重生。
10. `D M6.4 atomic cutover`（PENDING）：同一staged state复验；单commit启用v2/fresh scan唯一authority并删除v1 owner-wide/per-symbol/compile-support权威、worksheet与迁移残余。随后fixed RED、fast-check、真实production scan、完整release sink、upstream同步、diff审计与PR更新。

### 2026-09-04 — R0 M6.3a反向审计收口

- canonical capability fact不再接受caller自报disposition：validator对每行调用唯一classifier并比较canonical结果；binding provenance与public signature export kind进入closed vocabulary，unresolved/unsupported只能成为Unknown。所有collection入口拒绝非array，world拒绝零node traversal。
- JavaScript traversal由raw AST与scope resolver内部重建node universe；caller只能提交visit partition，不能协同删node。visitor返回的emitted observation IDs与独立书写的canonical expected facts按source精确交叉验证，删除node/visit/fact、伪造空union、unknown node与跨source引用均有独立RED。
- generated relation的relation、artifact、traversal、actual import、execution lineage、proof callback、runtime Surface与ephemeral traversal-observation set全部改为closed row。artifact reference只来自正式observation payload；build→generator→selector与proof→generator+Surface分别验证，禁止并列名称、test mirror或第二artifact-reference事实源冒充reachability。
- cutover validator只接受raw closure input并内部唯一求closure/build-output exception；全index stage、mode、object format、byte map与working tree绑定精确闭合。unsupported object format、nonzero stage、symlink、额外bytes、selected input/build output重叠及caller自带closure/exclusion均永久RED。
- Host ingress边界按JavaScript真实primitive/plain-own-data shape解码。SessionId保持opaque原字节；同义carrier只允许唯一同值，非法/冲突/accessor/boxed/coercible值fail-closed；parts只接受Array，boolean不接受truthy替代。ToolHost optional number把null/undefined解释为缺省，string/bool/array/boxed number拒绝，修复了G6 finalize被提前返回后永久等待prompt的真实回归。
- 完整sink首次暴露upstream Long Stroke未声明的合法Manager JoinGuard并发suffix。durable日志证明`manager.1`已先承担唯一400 fault，fresh JoinGuard随后与`manager-resume`并发；因此没有复制fault或放宽matcher。scenario新增exact Manager tool surface的optional suffix，逐项镜像resume.1..10；event-driven `waitAny`原子选择winner并注销全部loser waiter，终局逐步要求original+guarded=1且禁止中途换支。
- 收口阶梯：structured-workflow 258/258；Host/dispatch/loop/G6定向测试全绿；verification waitAny定向12/12；harness 275/275；`node scripts/check.mjs`全绿（807 WHAT、4,032 tests）；clean Fable build解析1,460 sources、编译1,422 items；真实Host Long Stroke绿色。最终`npm run format-build-test`退出0是建立R0提交的硬前提，完整计数与耗时写入该Git节点正文。

### 2026-09-04 — M6.3b B1-A production extractor与fresh report

- `CompilerObservationsV1`由一次production aggregate FCS check与每个`.fs` implementation check生成，闭合711份implementation、711份sibling signature、declaration/external use、typed F# expression、Fable interop与public export。fixture保留aggregate-green/missing-closure反例，并补`.fsi`、Import/Emit/emitJsExpr及正式scanner taxonomy。
- production extractor只接受inventory、compiler observations、raw JavaScript unit与generated artifact。Acorn AST、scope provenance、visit partition、canonical fact与traversal均在内部同次生成；caller提交这些mirror字段稳定RED。JavaScript fact按source预索引后交正式traversal validator，消除原先每个traversal扫描全部fact的二次复杂度，不改变source projection。
- canonical digest不再先构造超大字符串；同一canonical encoder直接stream到SHA-256。Unicode scalar comparator改为零中间数组。默认report输出summary，显式`--full`才展开actual query；两者共享同一world digest/census/candidates。summary只压缩Unknown violation坐标，不删除canonical fact或coverage count。
- fresh production run成功生成178条全`undecided`worksheet：178 localities、711 production sources、1,853 ProjectReferences、4,402 actual source edges、868,429 capability facts、459 JavaScript traversals、1 generated artifact。FCS 83.761s、dependency 0.459s、artifact 2.835s、extractor 58.713s、canonical world 115.524s。唯一missing closure仍是EventStore既定atomic blocker。
- 本轮没有伪称classifier已闭合。fresh census真实暴露857,589条Unknown与`SharedState.RootWorkspace` mutable public export；旧fixture使用scanner不会产生的`constant`别名也已改为真实`const`并绑定integration scan。这些是B3/B7的明确输入；M6.4前必须归零。
- B1-A验证：production report exit 0并生成178条closed worksheet；定向canonical/capability/extractor/report/FCS tests绿色；`node scripts/check.mjs`作为本节点Git前置验证。B1-B必须在本节点exact commit的clean checkout执行固定corpus structural + fresh FCS + full release三次raw/median测量，完成前禁止任何production split。
- B1-A Git节点为`5830fa473`。后续复核发现原runner只固定ID，可替换PLAN指定path/config/command；也允许dirty tree被标记为HEAD，且需人工拼baseline。B1-A2因此固定全部定义，只允许successor path迁移；baseline要求clean exact commit、null-only写入、测量前后Git复验、完整schema复验与atomic rename。candidate拒绝null baseline与dirty/changed checkout。永久proof覆盖definition/command drift、dirty、HEAD mismatch与overwrite。
- B1-A2首次clean baseline正确拒绝写入：full release unit暴露7个M6 boundary fixture仍手写scanner不会产生的`record` node，另一个Host canary因受限sandbox禁止`127.0.0.1`监听而EPERM。没有记录失败timing。fixture已改为正式`PublicSignatureExport[pure-type]` observation；10条相关owner proof与fast gate绿色。后续baseline以包含该修复的新exact commit在非沙箱环境重跑。
- B1-B在detached、clean、exact `e6268f35a8a3bbff6587960160bd4ceb3b64dbc3` checkout于非沙箱环境完成。fresh production scan记录`64,736/67,141/63,156ms`，median=`64,736ms`；完整release sink记录`141,550/140,316/140,148ms`，median=`140,316ms`。结构baseline固定7个stable case与2个full-fallback control：implementation改变保持focused，`CanonicalEventCodec.fsi`、`HostSignalBootstrap.fs`、fsproj与toolchain control触发full；全部compile-item identity、环境、lockfile/tool manifest/dependency-cache digest进入closed corpus。正式validator复验通过，定向corpus proof 3/3绿色。该baseline只发现影响面或性能变化，不取得correctness或release authority。

### 2026-09-04 — M6.3b B2 EventStore production-bound proof

- fresh source census确认`CanonicalEventCodec`六项公开协议的direct consumer恰为五个已裁决locality；旧v1 ACL只列四项且把codec与`ProcessEventLog/Store`混装，故project切分仍保留给M6.3c/M6.4原子提交。
- DURABLE-EVENTS-023不再由synthetic contract blueprint冒充production proof。正式`Persistence/EventStore/CodecSurface.js`直接调用同一个production codec，固定canonical roundtrip、valid-but-noncanonical拒绝、非法UTF-8在text/envelope两条入口同类拒绝、same-ID same-bytes、same-ID different-bytes collision、different-ID decoy与dedupe/distinct/collision merge。唯一新增Surface成员`decodeUtf8Text`直接投影`tryDecodeUtf8Text`，不复制parser、identity或merge公式。
- executable RED先在旧Surface稳定得到`eventCodec.decodeUtf8Text is not a function`，再补最窄production投影。clean Fable build、durable-events 165/165、Surface manifest/gate、test-boundary、requirement-trace、spec与旧owner gates全绿；未改fsproj、live manifest或release authority。

### 2026-09-04 — M6.3b B4-T0 Temporal production-bound proof

- TIME-008删除synthetic contract blueprint；三个正式测试直接穿越registered `Process/Surface.js`与`Process/DeadlineSurface.js`，分别固定virtual clock/timer实例隔离、Deadline只由显式clock input决定、构造Node capability不改变virtual state。
- proof不读取墙钟、不等待真实timer、不扫描源码。Node clock/timer只验证最窄opaque capability construction；物理时间正确性仍属于adapter canary，locality/source/closure/injection仍由后续production inventory/v2 validator承接。
- executable RED由旧Surface缺少`createNodeClock`/`createNodeTimer`稳定触发；实现只增加production `PtyTiming` Node capability的直接opaque投影及timer disposal，不复制时间算法或第二状态机。time-capability、Surface/trace meta、clean Fable build与旧release gates全绿；本节点未切fsproj、未启用v2 authority。

### 2026-09-04 — M6.3b B4-T1 Temporal source/project split

- `Foundation/Temporal`、`Process/Deadline`与`SessionStartedAtProjection`分别进入零实现值的bounded contract；原`PtyTiming` clean-break为唯一Node物理adapter `NodeTiming`与独立virtual verification runtime `VirtualTiming`，旧module/source删除。registered `DeadlineSurface + ProcessSurface`独占无production consumer的representation composition locality，不把virtual factory传给ordinary runtime。
- 六个provider source闭集、kind、direct consumer cohort与零reverse consumer的representation locality由production owner-project inventory proof固定。17个真实direct consumer按compiler-resolved需要迁到最窄ProjectReference；没有保留旧project/ref、duplicate Compile或compat facade。Node adapter仍有Fork/Host default fallback及Join、OneShot、Change、Review、Distillation、Process等ordinary direct consumer；该mandatory injection债明确留给后续B4/B6，本节点不宣称TIME-008已全闭合。
- 独立consumer compile暴露`process-largegatesurface`的dead `open Wanxiangshu.Mission.WorkRecord`此前依赖旧wide transitive closure；删除dead open后绿色，没有新增宽ref或suppression。旧`PtyTiming`只保留于不可改写的历史PLAN文本、fixed impact baseline与synthetic token fixture，不存在production、time spec、semantic owner、published contract、release node或ambient-time allowlist残留。
- 验证：6/6 provider与17/17 direct consumer focused compile绿色；5个signature union的full reverse impact执行1,426 items并绿色；隔离staged index production build为1,426 items、165 Surfaces、782 modules；time + process targeted 181/181；TIME-008 4/4；e2e-watchdog与requirement trace 807 WHAT/4,053 tests绿色；diff check绿色。同期全`check.mjs`唯一红灯来自未提交B7 fixture新增的5个`fsharp-type` hit，与本节点文件无关且不据此削弱验收。

### 2026-09-04 — M6.3b B3 Host codec与diagnostic injection

- `HostEventEnvelope`成为raw envelope unwrap、event type、session/message-session identity的唯一无状态公式；拒绝非primitive/blank identity与event type，不修改payload。`HostMessageCodec`、`LoopEventCodec`分别成为bounded contract；message、loop、visibility与delegation consumer从完整`host-signal-adapter`迁到最窄slice。
- `LoopSensor`改为composition必填diagnostic callback，内部吸收观测异常；`HostSignalBootstrap`注入`Diagnostic.emit`，`PluginRuntimeScope`删除默认no-op sensor。production-bound DG-013固定callback抛错时arm、interrupt、consume、continuation不变；loop runtime不再引用完整signal或diagnostics implementation。
- direct compile另暴露两条upstream `ed21b6d31`已存在、此前被flattened aggregate隐藏的closure：`opencode-host-sessionbindingsurface/Sessions.fs → host-diagnostics-runtime/Diagnostic.fs`与`resources-promptsurface/SessionExecutionBinding.fs → host-signal-adapter/HostEventCodec.fs`。源码行为未改；给两个真实composition consumer补exact ProjectReference后各自owner compile绿色。另把`dispatch-runtime/Send.fs → Diagnostic.fatal`记为显式现状边，留给后续fatal-port节点删除，不借wide codec closure偶然放行。
- 3个provider、全部direct consumer、Host bootstrap/runtime-scope与四个`.fsi` full reverse-impact compile绿色；受影响targeted 77/77、fresh worksheet后短复验16/16、`node scripts/check.mjs`全绿。fresh report为181 localities、712 sources、1,864 ProjectReferences、4,406 source edges、460 traversals；closure只剩既定EventStore原子切换blocker。worksheet仍0 decided且无授权力；旧v1 gate仍为唯一release authority。

### 2026-09-04 — M6.3b B3 Host typed subscription

- `HostSignalSubscribe`删除`option + string`、未消费的timer与伪health状态，改为closed `HostSignalSubscriptionError`及`LocalEventHook | EventsListen of HostSignalSubscription`。legacy listener的disposer由opaque `IDisposable`唯一持有；无legacy capability的公开Hook路径不制造资源。
- dynamic membrane只接受plain record。primitive、array、boxed carrier、坏direct events/client、accessor/Proxy异常、缺失或非函数listen、非函数/Promise disposer及任意JavaScript thrown value均有永久反例；坏direct carrier不能借client fallback旁路。listener读取/执行失败收敛为typed error，合法disposer自身异常原样交给资源owner。
- `HostSignalSubscribeSurface`只用F# typed pattern match投影plain JS verdict，不读取Fable `.tag/.fields`。`HostSignalSubscribe`不再到达Diagnostic或Temporal；`HostSignalBootstrap`是唯一`signal-subscribe-failed`解释者。旧release authority未改变。
- 最终字节验证：provider与direct Bootstrap project独立编译绿色；真实reverse impact因ProjectReference变化执行full 1,424 items并绿色；clean production build绿色；Host targeted 40/40；requirement trace为807 WHAT/4,052 tests；test-boundary、owner-contracts、owner-projects、control-pyramid、Fantomas与diff check全绿。同期全`check.mjs`的唯一红灯来自未提交B7 fixture新增的5行`fsharp-type`分类，不属于本节点且不据此削弱B3验收。

### 2026-09-04 — M6.3b B7-A compiler-resolved capability taxonomy

- F# scanner不再按同anchor的所有节点共享ordinal；先按ephemeral source range+symbol去重同一external occurrence，再按`(source, anchor, raw case, full payload)`分别编号。相同payload的两个真实range保持两个连续ID，无关observation插入不再改变fact identity。
- classifier只把显式穷举、其能力已由child/compiler symbol/dependency observation承接的结构node判irrelevant。FCS `FullType`、mutability、module/local scope、declaring entity与field facts区分primitive pure value、local scratch、top-level mutable、runtime cell与capability carrier；array、object/IL/address/trait/`this`及未证明immutable carrier继续Unknown。
- external classification只采用exact FQN family/member/component。删除ArrayModule整族pure与substring authority命中；FileSystem、ProcessControl、Environment、Console、Clock、Randomness、Timer、Network、Host均有positive与near-miss反例。assembly名不能使symbol自动变pure。
- 基于独立B3 commit `b166c0c77`的fresh run耗时约266.3s：181 localities、712 sources、1,864 refs、4,406 edges、861,657 facts、460 traversals；Unknown从857,589降为315,949，其中F# node 234,212、external 81,290、JS/Fable 447。唯一extraction diagnostic仍是`SharedState.RootWorkspace` signature export；唯一closure finding仍是既定EventStore blocker。该节点只增强report-only extractor，不启用v2 authority、不伪称B7 adjudication完成。

### 2026-09-04 — M6.3b B7-B closed immutable algebra

- scanner以FCS `FullType`执行invocation-local、cycle-safe recursive algebra；展开abbreviation与closed generic substitution，tuple、record与union递归检查全部字段。cache key包含assembly-qualified closed constructor shape；无法建立exact key的类型不缓存。
- primitive、immutable container与array通过同一exact assembly+namespace+compiled-name classifier；production canary固定wrong/blank assembly不命中。external pure/authority同样要求exact assembly+FQN/component；8个authority family均有wrong-assembly exact counterexample。
- recursive/mutual recursion、changing generic cycle、nested capability/array/function、mutable field、wrong-assembly与ordinal stability均由production-bound fixture固定；test中的Fable类型字面改为真实compiler-resolved `FSharpEntity` observation，JS boundary保持零债。
- final fresh report：186 localities、713 sources、1,888 refs、4,408 edges、861,926 facts、457 traversals、172,735 Unknown、1 diagnostic，digest `sha256:d278fb53142f30691dad36b17932f5683123b1f090c360469be8dadbbeb0ef7a`。中间156,553 Unknown结果因assembly-agnostic false green废弃。`RootWorkspace` diagnostic与EventStore blocker明确保留给B7后续/M6.3c前关闭；旧gate仍是唯一authority。targeted 12/12、JS boundary与`node scripts/check.mjs`全绿。

### 2026-09-04 — M6.3b remaining-scope ruling

- 本轮只完成M6既有owner裁决要求的contract/runtime/adapter分离、mandatory capability injection、authority closure与production-bound proof；不得把施工中发现的更强设计偏好冒充M6产品语义。现有WHAT已足以机械实施剩余节点，外部owner阻断集为零。
- `RootWorkspace`保留现有first-bind结果，只把public mutable移入private Host runtime并公开最窄bind/read capability；不引入Git-derived family root。Causal diagnostics保留`CAUSAL-008`的process-local single target，只隔离path/Node adapter并由composition注入；不引入multi-family partition。
- Fatal按现有caller-owner settlement law迁sealed incident与mandatory port；`NoOwnedEffect`只允许真实pre-effect失败。重复规则只覆盖现有“same incident exact-once”；不新增different-incident优先级。partial initialization只按既有law先逆序settle已取得资源，再fatal，不定义新的业务结果。
- Process spool采用runtime-owned bracketed lifetime，消除当前Git consumer遗失临时文件的明确资源bug；LargeGate仍归`output-distillation`并保留现有`world_lock → Large`语义，只拆pure capability、mutable runtime与composition injection，不迁owner或重定义admission。
- Delegation terminal无需新裁决：`IA-018`、`DELEG-025`、`MANAGED-SESSION-008/017`唯一推出`HandleCompleted` envelope为`ChildLogicalRunTerminal` source witness，envelope `EventId`为`FactId`，并绑定exact owner/child logical run、Authority Root、provider run与closed outcome；`HandleRetired`仅为consumption tombstone。rootless physical failure不得反查current active run或发布logical closure。

### 2026-09-04 — upstream `a92d6278b` integration与M6.3b B7-C0 Unknown census

- upstream已合并M6.3a PR `23370fea9`，其tree与本地`f3ee0fb39`精确相等；新upstream另含SelfPrediction与Sphinx。新累计分支从`a92d6278b`建立，只重放`f3ee0fb39`之后的M6.3a repair与M6.3b节点，避免把已squash的旧历史再次提交。重放零冲突，fast gate为738 production files、187 localities、823 WHAT/4,117 tests，全绿。
- B7-C0增加report-only `unknown_capability_census`：只在canonical world验证后按observation case、closed Unknown class、syntax kind与raw identity分组；组内fact总数必须等于canonical `unknown_count`。完整affected locality/source集合只进入distinct count与domain-separated digest，各组最多输出5个canonical representative。
- census不进入world、worksheet、manifest、cutover或授权输入；fact permutation与完全相同的duplicate observation不改变group/digest，full与summary投影一致。production-bound proof 15/15；`node scripts/check.mjs`为823 WHAT/4,118 tests且全绿；未刷新worksheet与fresh production report。

### 2026-09-04 — M6.3b G4 CausalWait green checkpoint

- `causal-wait`从旧混合locality clean-break为`execution-session-wait-contract`、`execution-session-wait-runtime`、`execution-session-wait-diagnostic-adapter`、`execution-session-wait-completion-mailbox`与`execution-session-wait-proof-surface`。旧project、global `CausalWaitHub`与`Process/Surface` mailbox mirror删除；proof Surface无production consumer。
- `CausalWaitProcess.local()`只在Host composition取得process-local runtime；Change、Finality、Delegation、Tool与Host workflow全部显式注入`IWaitObserver`。diagnostic sink first-bind且只写，Node/path/fs只留在adapter；G4保持现有single-target与RootWorkspace first-bind结果，不引入workspace-family新语义。
- worksheet writer增加topology-change bootstrap：仅当现有记录全部`undecided`时，才可由同次live owner inventory原子替换locality key全集；任何decided记录仍拒绝覆盖。worksheet保持migration-only、无digest、无授权力。
- 验证：五个provider locality独立compile绿色；五个public `.fsi`联合reverse impact触发full flat 1,476 items并绿色；aggregate Fable为1,514 sources/1,476 items；causal、`PROC-008`、`TIME-008`与worksheet regression合计51/51；`node scripts/check.mjs`为738 production files、191 localities、1,911 refs、823 WHAT/4,121 tests且全绿；diff check绿色。staged fresh world为4,438 edges、967,429 facts、512 traversals、188,498 Unknown、1 diagnostic，digest `sha256:9f2fa4d90a7230dbd5d6b1f394bfb98ebb2ce060428af912af6d6264977093cb`。12条missing closure中1条为既定EventStore edge，另11条来自upstream Sphinx（9条到Foundation/EventStore，2条到`sphinx-event-vocabulary-contract`）；均保留为M6.3c前blocker，不归因于G4。

#### G4后暂停与恢复

- 暂停边界：G4提交后不启动Fatal、Process/PTY、Delegation或B7-C1。恢复时先执行`git status --short --branch`、`git log -3 --oneline`与`node scripts/check.mjs`，确认工作树干净且HEAD为本checkpoint。
- 下一实施节点为B7 RootWorkspace effect隔离：保持现有first-bind结果，把public mutable移入private Host runtime并注入最窄bind/read capability；该节点同时为Fatal partial-init exact acquisition token铺路。随后依次执行Fatal原子迁移、Process/PTY、Delegation G6/G7、B7 compiler-evidence分类、M6.3c staged audit与M6.4原子cutover。
- release sink前另有五个非G4 Fantomas失败需要单独绿色节点收口：`Interaction/Authority/Child.fs`、`OpenCode/Host/LoopSensorSurface.fs`、`OpenCode/Host/HostSignalSurface.fs`、`OpenCode/Codec/HostEventCodec.fs`、`OpenCode/Codec/HostEventEnvelope.fs`。不得把格式修正混入G4语义提交。

### 2026-09-04 — RootWorkspace节点完成记录

- 暂停基线：分支`codex/m6-owner-slice-cutover`，已提交HEAD=`f116ae670`；本节以下RootWorkspace与G4补完仍未提交，`host-root-workspace-effect-isolation`保持`IN_PROGRESS`。禁止把当前状态称为release closure、PR-ready或RootWorkspace DONE。
- RootWorkspace已从`SharedState.RootWorkspace`公开mutable迁入独立Host contract/runtime；composition取得唯一binder，其余Change、Finality、Delegation、Dispatch、Review、Tool与provider路径均显式传递`IRootWorkspaceReader`。production runtime与目录selector共同拒绝`None`、空串与纯空白；首次合法path原子绑定，后续候选不覆盖。旧Surface的任意set/clear删除，正式proof固定非法空白、first-bind、later redirect、无clear与显式目录优先。
- 全量unit首次把G4 checkpoint未闭合的两个事实暴露出来：`delegation-sync-runtime`与`delegation-fork-runtime`在`f116ae670`被误标成composition。Sync的两个observed-await现由Host adapter注入；Join/Outcome/Wake/Batch纯词汇已从具体`CompletionMailbox`迁入wait contract；Fork runtime只消费注入的mailbox factory。DELEG-028/029保持原runtime语义，未修改或削弱测试。
- 已完成验证：aggregate clean Fable build=1,518 sources/1,480 items；联合public-signature reverse impact=1,480 items；Root/Causal/DELEG/mailbox定向16/16；full unit=4,073/4,074，唯一失败为sandbox禁止loopback的`EPERM`，提升权限后真实OpenCode canary=2/2；wait contract/mailbox/sync/fork/recovery/host adapter及Dispatch proof adapter focused compile全绿。owner-projects、control-pyramid、authority、causal-wait、DSL、JS/test boundary、requirement trace、deadcode与P0 recovery gate在各自最新合法输入上全绿。
- 第一次staged fresh report成功完成：193 localities、740 production sources、1,915 ProjectReferences、4,465 actual source edges、968,007 capability facts、512 JavaScript traversals、188,662 Unknown、0 extraction diagnostic，digest=`sha256:cbe3737159911238896d07310c8f84f541ae033c4d6b57bf2b0044a9697fa9f9`。RootWorkspace原唯一diagnostic已归零；14项finding为13条`missing-closure-edge`加1项Unknown汇总。
- 相对G4 checkpoint的12条既有missing closure新增1条：`delegation-host-adapter`中的`HostForkRuntime`直接构造causal-wait `CompletionMailbox`，但项目只声明wait contract。直接补foreign runtime ProjectReference虽可focused compile，却被`owner-projects`正确拒绝为`foreign-runtime-reference/composition-only-runtime-binding`；该无效ref已撤回，gate未放宽。

#### 恢复步骤

1. 先运行`git status --short --branch`与`git diff --check`；预期HEAD仍为`f116ae670`，所有施工字节已暂存但无新commit。不要丢弃工作树或重做已完成迁移。
2. 关闭唯一新增closure：让`HostForkRuntime`接收窄mailbox factory/capability，具体`CompletionMailbox`只在合法composition locality构造并注入。不得让delegation adapter直接引用foreign runtime，不得把Fork改回composition，不得复制mailbox状态机或新增owner-project豁免。先枚举并迁移5处`HostForkRuntime(...)`构造点，再以focused compile与DELEG-028/029证明边界。
3. 精确stage全部最终输入，重新运行`node scripts/checks/locality-slice-report.mjs --write-fresh-worksheet`。验收必须是diagnostic=0、missing closure恢复为既有12条；任何新增项继续修复。由于本节文档与closure修复改变staged bytes，禁止复用上面的digest冒充最终report。
4. fresh与全部非循环gate绿色后，才可把`host-root-workspace-effect-isolation`从`IN_PROGRESS`改为`DONE`并运行`node scripts/check.mjs`。随后建立G4 closure与RootWorkspace可回溯Git节点；若metadata文件交叠无法安全拆分，提交正文必须分别列出两条因果链。
5. 单独收口五个既有Fantomas失败并建立format-only节点。再fetch/细粒度合并最新upstream，运行最终`npm run format-build-test`、diff审计、push与PR。PR创建后立即暂停，不启动Fatal、Process/PTY、Delegation G6/G7或B7-C1。

#### 恢复后施工记录

- 唯一新增closure已按mandatory injection关闭：pure wait contract新增泛型`ICompletionMailbox<'agent,'pty,'interrupt,'wake>`，只表达wake资源capability；具体Queue/TCS/cancel状态仍唯一留在`CompletionMailbox`。`CompletionMailboxRuntime.create`是唯一physical factory，Fork runtime仅保存绑定领域类型的alias。
- `HostForkRuntime`删除concrete constructor与十一字段projection，factory改为必填构造参数。5处构造点由`git-integrationgate`、`mission-finality-prompt`、`opencode-host-pluginruntimescope`及proof-only`delegation-runtime-surface`四个composition locality显式注入；Finality补唯一缺失的exact ProjectReference。`delegation-host-adapter`与`delegation-fork-runtime`均不引用foreign mailbox runtime，DELEG-028/029继续保持runtime分类。
- 永久inventory proof要求所有physical mailbox direct consumer均为composition，四个constructor locality声明exact provider，Host adapter与Fork runtime不得引用mailbox runtime。aggregate build为1,518 sources/1,480 items；wait contract、mailbox、Fork runtime、Host adapter与四个composition consumer focused compile全绿；Root/Causal/DELEG/mailbox/PTY定向25/25，owner-projects=193 localities/740 sources/1,916 refs/DAG，DSL、control-pyramid、causal-wait、authority、test/JS boundary与requirement trace全绿。
- pre-DONE staged fresh report为193 localities、740 sources、1,916 refs、4,468 edges、967,993 facts、512 traversals、188,664 Unknown、0 diagnostic，digest=`sha256:2b545b76a70418ca1720f776857ccf5e7c35c48d7b6dbffce5fd37dd302fe18f`；missing closure从首次report的13回到既有12，证明新增foreign runtime edge已消失。RootWorkspace release node随后置`DONE`，`node scripts/check.mjs`全绿：316条规范、740 production files、791 contracts、193 localities、1,916 refs、824 WHAT/4,122 tests。最终文档与DONE metadata的fresh复验digest记录在Git commit正文，避免digest写回自身输入产生漂移。
