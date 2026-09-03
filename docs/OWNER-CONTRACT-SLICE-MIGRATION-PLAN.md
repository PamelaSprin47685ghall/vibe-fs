# Owner、Locality 与 Contract Slice 迁移方案

日期：2026-09-03

状态：M6.0–M6.2 已完成；M6.3 全局规则及 EventStore/Host/Delegation 点名边界已裁决；全部 live locality 的 terminal classification/adjudication、完整 capability census 与全量 slice manifest 尚未完成；不得进入 M6.3c/M6.4。`deriveAdjudicationCandidates` 的 key universe 固定为 fresh owner-project graph 的全部 locality；当前 92 个 composition provider 只是带 `CompositionProvider` reason 的 pre-cutover 子集，不是永久 gate 数量。

适用背景：Fable owner-project 编译边界、published contract 授权与 semantic owner 重整

## 简略介绍

当前 contract manifest 声称能够实施 exact symbol × exact consumer owner 授权；实际 gate 会把一个 consumer owner 的授权扩张到该 owner 的全部 project，Fable 又会合并 ProjectReference 的传递源码闭包。因此，manifest 的精度高于编译边界真正能兑现的精度。

本方案改用三层模型：

- owner 管 vocabulary、invariant 与业务决策责任；数量可在语义一致时适度减少。
- locality/project 管真实编译边界；数量适度增加，以缩小依赖闭包与增量编译范围。
- contract slice 管一组共同演化、共同授权的公开能力；provider locality 中全部 sibling `.fsi` exports 的并集是唯一公开符号清单。

授权绑定稳定的 locality，不绑定 owner 名称。每条跨 locality 依赖都必须经过 slice grant；owner 是否相同不参与授权判定。Fable 的真实有效 audience 按 ProjectReference 反向可达闭包计算；轻量 compiler-resolved analyzer 证明实际源码依赖没有逃出该闭包。高风险 effect implementation 只能由 composition 到达，普通 consumer 只依赖 port/capability。

当前 fresh census 为 49 个 owner、178 个 locality、711 个 production source、1,853 条 ProjectReference、784 份旧 contract record、4,420 条 actual cross-locality source edge；尚有 1 条 missing closure edge。数字随施工变化，只作计划快照。190–210 个 project、39–44 个 owner、增量影响下降 25%只作规划导向，不构成正确性定义。模拟显示，优先增加约 30 个高价值 slice，能消除理想化 project-level 模型中约 50.8% 的额外暴露；继续增加至 50 个的收益仅升至约 55.4%，边际收益明显下降。

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
| actual cross-locality source edge | 4,420 |
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

源码能力必须形成唯一、规范化的 production fact set。提取边界读取 F# source、`.fsi`、compiler declaration use 与 build linkage；owner、locality kind、目标 exposure、grant、relation 与既有 authority annotation 全部是待验证的 normative claim，不能制造或删除 observed fact。normalizer、locality join 与 policy decision 必须是可由 fixture/property test 直接调用的 production pure function。

fact schema 固定为 version 1 的封闭代数；新增 case 必须提升版本并同步 fixture、record 与 world digest：

```text
ObservedCapabilityFactV1 =
  | FableInteropUse of ImportOrEmit * RawInterop * Observation
  | PhysicalUse of PhysicalAuthority * Observation
  | MutableRuntimeResource of MutableResourceClass * Observation
  | CapabilityDeclaration of CapabilityForm * Observation
  | GeneratedModuleImport of ObservedGeneratedLinkage * Observation
  | UnknownObservation of UnknownClass * Observation

ClassifiedCapabilityV1 =
  | PureRepresentation of FactId
  | VerifiedRepositoryGenerated of FactId * RelationId
  | PhysicalAuthorityUse of FactId * PhysicalAuthority
  | MutableRuntimeUse of FactId * MutableResourceClass
  | CapabilityTypeOnly of FactId
  | CapabilityValue of FactId
  | CapabilityFactory of FactId
  | EffectConstructor of FactId
  | UnknownCapability of FactId * UnknownClass

ImportOrEmit = Import | Emit
RawInterop = { specifier_or_expression }
PhysicalAuthority = Node | Host | Process | FileSystem | Network | Git | Provider
CapabilityForm = TypeOnly | Value | Factory | EffectConstructor | UnknownForm
MutableResourceClass = TopLevelMutable | Registry | Waiter | TaskCompletionSource | RuntimeCell
UnknownClass = UnclassifiedInterop | UnclassifiedCapability | UnsupportedSourceConstruct | IncompleteGeneratedLinkage
ObservedGeneratedLinkage = {
  import_specifier, package_import_target,
  generator, input_selector, build_invocation
}
Observation = {
  locality_id, source_path, semantic_declaration_anchor,
  same_anchor_occurrence_ordinal, semantic_payload
}
```

信任边界固定为 `ObservedFacts := extract(source/.fsi/FCS/build)`、`NormativeClaims := owner/kind/exposure/grant/relation/generated_owner/law/evidence metadata`、`ClassifiedCapabilities := policy(ObservedFacts, NormativeClaims)`。manifest 不能直接提交 observed/classified fact；只有 extractor/normalizer 能产生 `ObservedCapabilityFactV1`，只有 production policy 能产生 `ClassifiedCapabilityV1`。`UnknownObservation`、`UnknownCapability` 与 `UnknownForm` 一律阻断 adjudication/cutover。physical observation 优先于任何 metadata；normative claim 不得把 physical、mutable、value、factory 或 effect constructor 降格为 pure。`PureRepresentation` 只由 production classifier 的显式 closed rule 产生，每条允许的 `Emit` template 都有正例与邻近物理反例；未匹配 interop 进入 `UnknownCapability`。`VerifiedRepositoryGenerated` 只有在 observed import、package target、generator、repository input selector、build invocation 与 normative generated-owner/determinism proof 全部闭合后由 policy 产生；仅存在 manifest relation 不足以自证。

每条 fact 的稳定身份必须覆盖去掉诊断位置后的完整 observed DU，而不是只覆盖公共 `Observation.semantic_payload`：

```text
fact_id := "sha256:" + SHA256(
  UTF8("capability-fact/v1\u0000" + canonicalJson(
    stripDiagnosticLocation(fullObservedCapabilityFactV1)
  ))
)

stripDiagnosticLocation(case(constructor_payload..., observation)) := {
  fact_case: case,
  constructor_payload: constructor_payload...,
  observation: {
    locality_id,
    source_path,
    semantic_declaration_anchor,
    same_anchor_occurrence_ordinal,
    semantic_payload
  }
}
```

`constructor_payload` 必须保留各 case 的全部 payload：`ImportOrEmit + RawInterop`、`PhysicalAuthority`、`MutableResourceClass`、`CapabilityForm`、`ObservedGeneratedLinkage` 或 `UnknownClass`。`semantic_declaration_anchor` 是 compiler-resolved fully-qualified containing declaration；module initializer 使用稳定 `<module-init>`，同名 local declaration 再带 AST child-index path。`same_anchor_occurrence_ordinal` 是同一 `(source_path,semantic_declaration_anchor,fact_case,constructor_payload,semantic_payload)` 在 compiler traversal 中的 zero-based occurrence。它保留同一 declaration 内两个内容完全相同的 use，同时不把 line/column 写入 identity。fixture 必须证明：不同 constructor payload 不碰撞；两处完整 payload 相同的 occurrence 产生两个 fact ID；重复提取同一 occurrence 才按 fact ID 去重。

`canonicalJson` 递归按 key 排序；set/array 按各类型的稳定 identity 排序；路径转为 POSIX repository-relative form；字符串保持原 UTF-8 值。symbol、line/column、诊断文案与扫描临时路径不进入 fact/world，只留在单独的本次诊断 side channel。

canonical fact query 至少输出：

- provider locality 的全部 sibling `.fsi` surface。
- 每个 Fable `Import`/`Emit` 的 exact specifier/expression 及其语义分类；语法 token 本身不等于 effect。
- Node、Host、process、network、fs、Git、provider physical capability。
- top-level mutable state、registry、waiter 与 `TaskCompletionSource`。
- capability type、capability value、factory 与 effect constructor 的区别。
- deterministic repository-generated module 及其 producer/build/input linkage。

actual capability facts 不得复制进 manifest、baseline 或 adjudication record 形成第二事实源。slice validator、effect purity census、generated-module validator、physical-port/adapter/composition checks 与 fast-check 只消费同一 typed fact set；不得重扫源码、解析其他 gate 的诊断字符串，或以 locality kind metadata 代替源码事实。

实现时从现有 `authority-boundary`、`dsl-ownership` 与 owner-project parsing 抽取、复用 pure observation primitives；旧 annotation 只能帮助定位 symbol/owner，不能决定 effect classification。唯一新增层是 observation normalization + source facts → locality join。compiler-resolved locality dependency analyzer 继续只拥有 declaration-use edge，不扩成第二 capability scanner。固定 fixture 必须覆盖：kind 误标为 contract、源码却执行 `console.error/process.kill` 的 `FatalProcess` 反例；Node/process import 反例；mutable registry 与 capability type/value/factory/effect-constructor 边界；pure `Emit` 正例与相邻 unknown/physical 反例；缺 linkage 的 generated import RED；通过 exact relation 的 deterministic generated-module GREEN。同一组 facts 必须驱动全部相关 policy verdict。

### 4.5 Canonical world、adjudication candidate 与 freshness

production pure `buildCanonicalWorldV1` 产生唯一 adjudication 输入：

```text
CanonicalWorldV1 = {
  fact_schema_version: 1,
  observed: {
    localities: stable(locality id → source/.fsi inventory),
    project_references: stable(direct locality edges),
    actual_source_edges: stable(source/locality pairs without diagnostics),
    capability_facts: stable(ObservedCapabilityFactV1),
    generated_linkage: stable(observed build/package/import linkage)
  },
  normative: {
    semantic_owners_and_declared_kinds,
    staged_v2_slice_authorization_projection,
    staged_relation_projection,
    exact_law_and_evidence_identities
  }
}
```

`canonical_world_digest := "sha256:" + SHA256(UTF8("canonical-world/v1\u0000" + canonicalJson(CanonicalWorldV1)))`。adjudication record、自述 justification、诊断 payload 与 actual dump 不进入 world，避免循环自证；授权字段、law/evidence identity 与 relation 会进入 world，改变它们必然使旧 record 失效。

candidate 不再采用可漏项的选择性筛选。每个 live locality 都需要 terminal classification，因此：

```text
deriveAdjudicationCandidates(world) :=
  stableSort(world.observed.localities.keys).map(locality_id => ({
    locality_id,
    reasons: stableSort({
      TerminalClassificationRequired,
      ReferencedProvider
        if exists C: (C, locality_id) in ProjectReference or actual-source edges,
      CompositionProvider
        if declaredKind(locality_id) = composition and ReferencedProvider,
      CapabilityBearing
        if exists capability fact owned by locality_id,
      KindCapabilityMismatch
        if capabilityPolicy(observedFacts(locality_id), declaredKind(locality_id)) = RED,
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
  | GeneratedModule of consumer_locality * generated_owner

TerminalClassificationV1 =
  | Private
  | ContractShared
  | ContractBounded
  | RuntimeEffect
  | AdapterEffect
  | CompositionTerminal
```

三个 production pure query 的 ID 与 result shape 固定为：

```text
surface/v1:<locality> =
  stable(exact sibling-.fsi export identity + signature digest)

audience/v1:<locality> = {
  direct_project_consumers,
  actual_source_consumers,
  reverse_closure_effective_consumers,
  relation_endpoints: stable(RelationKindV1),
  missing_closure_violations
}

capability/v1:<locality> = {
  observed_fact_ids_and_cases,
  classified_capabilities: stable(ClassifiedCapabilityV1),
  declared_kind_mismatch
}
```

export/signature digest 与 query digest 都复用同一 `canonicalJson + SHA256`；query digest 为 `SHA256(UTF8("query/v1\u0000" + query_id + "\u0000" + canonicalJson(query(world, locality))))`。query result 可在 live report 展示，禁止写入 record。

`docs/OWNER-CONTRACT-SLICE-ADJUDICATIONS.json` 的顶层 exact shape 为 `{schema_version: 1, records: {<locality_id>: <record>}}`；未知字段 RED。每个 record 只允许：

```text
{
  locality_id,
  fact_schema_version,
  canonical_world_digest,
  queries: {
    surface: { query_id, query_digest },
    audience: { query_id, query_digest },
    capability: { query_id, query_digest }
  },
  decision: {
    reason,
    target_classification: TerminalClassificationV1,
    migration_path,
    what_ids,
    proofs
  }
}
```

record 禁止保存 `.fsi` export、direct/effective consumer、source edge、capability fact、fact ID list 或 query result。`records.keys` 必须精确等于 `deriveAdjudicationCandidates(world).locality_id`，每个 value 的 `locality_id` 必须等于 key，零 duplicate/undecided。

M6.3c 完成 production/source/`.fsi`/fsproj/target manifest/relation 修改后，必须在最终 staged tree 重新运行 fresh full scan，重建 canonical world、candidate keys 与全部 query digest。任一 key、world digest 或 query digest 失配即令该 decision 失效；必须由 owner 重新裁决，禁止自动改摘要。零失配才可进入 M6.4。临时 actual fact/query dump 必须删除；adjudication file 作为绑定该 world digest 的正式历史执行记录保留，但永不参与 release authorization。目标 grant/relation 进入 manifest，理由与 WHAT/proof 留在 manifest justification、requirements 与该执行记录。

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

纯度不由手写 `"exposure": "shared"` 自证。gate 必须消费 4.4 的 canonical capability facts。`Import`/`Emit` 是待分类的源码事实，不是 effect 的同义词：Node/Host/process/network/fs/Git/provider authority 与 mutable/capability value/factory 必须 RED；纯表示 `Emit` 与 exact deterministic generated-module relation 不得误杀。

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
| `physical-port` | contract/runtime/adapter/composition | contract shared/bounded | provider 只含 capability type；contract consumer 的 actual use 必须全为 `CapabilityTypeOnly`，不得取得 value/factory。 |
| `adapter` | adapter effect | contract shared/bounded | 同一 consumer/provider pair 必须另有 `physical-port` relation；consumer 是该 exact relation 的 physical implementation owner，不能消费 runtime/adapter implementation。 |
| `composition-wiring` | composition | contract shared/bounded、runtime effect、adapter effect或composition | runtime/adapter/composition provider 的每条 direct edge都必须有该 relation；contract provider 仅在真实 terminal construction/wiring 时登记，不把普通 query import冒充 wiring。 |

其余 endpoint 组合全部 RED。每条 relation 还必须同时匹配 exact consumer/provider locality、direct ProjectReference、provider direct grant、consumer/provider module 与 actual compiler-resolved module edge；一个 relation 不能授权 sibling module或 transitive-only consumer。

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
    "entry": "loopDetectorRepositoryTexts"
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

`generated_module_relations[]` element 的 exact keys 为 `id/kind/consumer_locality/import_specifier/generated_owner/package_import_target/generator/build_invocation/input_selector/runtime_surface_module/laws/determinism_proof/justification`；`kind` 只能是 `"compile-contract-support"`。`generator`、`build_invocation`、`input_selector` 只允许 `path/entry`，`determinism_proof` 只允许 `path/title/what_id`。`laws` 必须是 singleton 且精确等于 `[determinism_proof.what_id]`；不允许 orphan law 或一个 proof 暗中替多个 law 作证。新增第二条独立 law 需要提升 schema 并显式改为双向全覆盖的 proof collection，不能在 v2 自行扩张。未知 key RED；relation ID、law 与 proof identity 唯一且稳定排序。

M6.3a 先按 WHY → WHAT → HOW → GAP 把该 relation 写入 `structured-workflow`，再实现 schema。actual imported member、package import target、generated output 与 repository input digest 仍由同次 analyzer/build 推导，不复制进 manifest。relation-specific validator 必须证明 build invocation 触达 exact generator、input selector，且 exact test callback 同时触达 generator lineage 与 registered runtime Surface；普通 semantic-evidence validator 不能代替 generator lineage proof。`laws[]` 与 determinism proof 的 owner 必须等于 `generated_owner`，该 relation 仍与 consumer locality kind/capability policy 叠加。gate 必须拒绝 missing、stale、duplicate、actual import/relation mismatch、specifier/target/build invocation漂移、缺 determinism proof、非 repository-content-determined output，以及 Node/Host/process/network/fs/Git/provider import 冒充 compile support。

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
7. canonical locality capability facts。
8. production pure `buildCanonicalWorldV1`、`deriveAdjudicationCandidates` 与三个 canonical query。

新 manifest 可在 cutover 工作树中准备和验证，但不得先以独立绿色提交落地并与旧 manifest 同时成为权威。实际集合一律由 analyzer 临时生成，不写入 manifest。

执行前必须 fresh 生成 census；当前 178/711/1,853、4,420 actual source edges、1 missing closure 只作参考。

M6.3 完成条件：

1. 同次 fresh canonical world 的每个 locality 均有 terminal classification；`deriveAdjudicationCandidates(world)` 的 key universe 固定为全部 live locality。`records.keys` 必须与其精确相等。当前 92 个 composition provider 只是 reason 含 `CompositionProvider` 的 pre-cutover 子集，不得把 92 硬编码进 gate 或完成集合。
2. 零 `undecided`；零从当前 ProjectReference、旧 owner ACL 或 composition 标签自动生成的 grant/relation。
3. 每份 record 只含 locality ID、fact-schema version、canonical-world digest、surface/audience/capability query ID+digest，以及 decision reason、target classification、migration path、WHAT/proof；禁止保存任何 query result、export、consumer、source edge、capability fact 或 fact ID list。
4. live report 可展示 actual direct/effective/source/capability 集合；生成 dump 在 cutover 前删除。`docs/OWNER-CONTRACT-SLICE-ADJUDICATIONS.json` 保留为绑定 cutover world digest 的正式历史执行记录，但不是 manifest、allowlist 或 release fact source。
5. 对应 WHY → WHAT → HOW → GAP 与 executable negative oracle 已落盘；“RED”指旧世界被 oracle 识别为违规，提交后的 test suite 必须全绿。
6. 旧 gate 下可独立绿色的 contract/port split 已完成；新 pure validator/property 可提交，但 live 新模型只能 report-only，不能阻断 release。
7. M6.3c 最终 staged tree 已 fresh 重建 canonical world、candidate keys 与 query digests；任一 record key/world/query digest 失配均已重新裁决，最终 mismatch 为零。
8. 进入 M6.3c 前，1–6 必须全部成立且 fresh report 不再有未裁决 blocker。M6.3c 只准备旧 ACL 无法表达的最小 production cut、全量终态 manifest 与 exact relations；第 7 项成立才算 M6.3 完成，随后进入 M6.4。仅完成点名 owner 裁决不等于 M6.3 完成。

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
10. canonical capability facts、exact generated-module、semantic-evidence、physical adapter、composition wiring 通过同一 pure policy validator。
11. 同次 fresh canonical world 的全部 locality classification 与 adjudication record 已落实为终态 slice/relation；record keys/digests 零 mismatch，无 stale/duplicate grant、reference、relation 或缺 law/evidence。
12. 删除 owner-wide authorization expansion、per-symbol consumer ACL、旧 schema/parser 与旧 `compile_contract_support` 裸路径豁免。
13. 删除 dead production `symbolUses: []`、旧 FCS snapshot/delta/cache、临时 actual fact/query dump、compat facade、过渡 adapter、旧空路径与 pre-cutover report-only release bypass；保留 formal adjudication record，且保留 `owner-projects` 对 source→locality、ProjectReference DAG 与 closure 的唯一职责。
14. 新 schema/validator 进入 release authority；fresh production compiler scan 恰一次进入 integration release path。

同一 commit 的 gate 必须全绿。不存在“先启新 gate、后分类”或“新 gate 已启用、旧模型 M6.7 再删除”的过渡态。`published-contracts.json` 可以原位迁移到终态 schema；必须删除旧字段/parser，不要求删除文件本身。

full production compiler-resolved scan 接线固定如下：

- 保留 `requirements/structured-workflow/tests/integration/locality-dependency-analyzer.test.mjs` 的 aggregate-green/missing-edge compiler fixture。
- 新增独立 production-scan integration test，fresh 扫描完整 production compile set 并断言 violation 为零。
- 在 `requirements/verification-system/tests/support/integration-node-test-steps.mjs` 恰注册一个独立 step；预算只由 `PROJECT_CHECK_TIMEOUT_MS`/`perTestTimeoutMs` 传播，测试不得另写硬编码 timeout。
- 不把真实 FCS project scan接入 `scripts/check.mjs` fast tier，也不在 `package.json` 重复执行。
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
5. shared 携带 canonical physical/mutable/capability value/factory fact → exact `impure-contract-slice`。
6. effect 被任一 non-composition consumer 反向到达 → exact `effect-consumer-not-composition`。
7. composition 被普通 consumer 引用 → exact `invalid-composition-consumer`；缺 relation → exact `missing-composition-wiring`。
8. private locality 被引用 → exact `private-provider-referenced`。
9. contract closure 含 non-contract locality → exact `contract-closure-kind-violation`；contract transitive production source count 超过 100 → exact `contract-closure-budget-exceeded`。
10. ProjectReference cycle → exact `locality-reference-cycle`。
11. owner rename/merge 后 normalized authorization projection 不变。
12. legal split 不扩大 capability audience：生成 `old locality → new locality partition`、source/export/capability 映射及 consumer/grant/reference 重映射；证明每个 source 恰映射一次、旧边均有映射、新边不跨 capability 偷渡，并按 capability 比较 normalized external direct/effective audience。owner 名不得作为映射键。
13. generated-module relation 分别断言：missing → `missing-generated-module-relation`；stale → `stale-generated-module-relation`；duplicate → `duplicate-generated-module-relation`；specifier mismatch → `generated-module-specifier-mismatch`；target mismatch → `generated-module-target-mismatch`；nondeterministic → `generated-module-nondeterministic`；physical import → `generated-module-physical-authority`。每个 mutation 独立，不得用一个泛化 RED 覆盖多种失败。
14. 任一 `UnknownObservation`、`UnknownCapability` 或 `UnknownForm` → exact `unknown-capability-classification`；mutation 只把一个已知 GREEN fact 改为对应 unknown case，禁止同时改变 locality kind、grant 或 relation。

条目数量不是验收目标；每条性质必须消灭独立错误世界。固定 seed，失败输出最小 counterexample graph。`.fsi` export extraction、compiler declaration extraction、compile-set drift、Import/Emit 分类与 package-import linkage 由固定 fixture/真实 compiler gate证明，不伪装成随机图性质。pure `Emit` 与已批准 deterministic compile support 是 GREEN；canonical effect-capability 才是 RED。

### M6.5：切换后实施收益明确的 slice 拆分

M6.5 只承接可测量的 authority/audience/closure/impact 优化，不承接任何 M6.4 correctness debt。若发现旧权威、未落实 adjudication、stale relation、composition 业务判断或 capability matrix 违规，必须重开 M6.3/M6.4 修复。

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
- shared slice 引入 canonical effect-capability、mutable registry、capability value 或 factory 为 RED；pure `Emit` 与 exact deterministic generated-module relation不得误杀。
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

- 158 个 foreign provider 需要分类。
- 1,607 条 foreign direct edge 需要核对；237 条 same-owner direct edge 也必须纳入 locality authorization。
- 711 条指向 composition kind 的 edge 需要重新判断边界。
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
5. 确定性、仓库派生、无 IO/ambient state 的 generated module 可通过 exact `compile-contract-support` relation 被消费，不因使用 Fable `Import` 自动归为物理 effect。该 relation 必须绑定精确 import specifier、生成 owner、consumer locality 与可执行 determinism proof；Node/Host/process/network import 不适用此条。
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
4. `deriveAdjudicationCandidates` 为每个 fresh live locality 输出 stable key + reasons；formal record 只保存 locality ID、fact-schema/world/query digest 与 decision reason、target classification、migration path、WHAT/proof。live report 可显示 `.fsi` exports、actual audience/source/capability facts，record 禁止复制这些集合。`records.keys` 必须与 fresh candidate keys 精确相等，零 `undecided`、零 current-reference-derived grant；split 由 locality key 变化自动反映，不硬编码 92。

#### 裁决后的施工批次与停止条件

1. M6.3a：先更新 `requirements/structured-workflow/{WHY,WHAT,HOW}.md` 与全局 `requirements/GAP.md`，固定 v2 schema、canonical facts/world/candidates、generated-module relation 与 violation code；再更新 durable-events、host-boundary、degeneration-guard、delegation、time-capability、causal-wait、process-execution及 fresh census 找到的所有 FatalProcess caller owner WHY/WHAT/HOW/GAP。建立行为 oracle与 architecture/closure illegal fixture；先观察旧世界被新 oracle判为违规，再提交全绿 fixture，禁止提交红色 suite。
2. M6.3b：提交 report-only pure validator/property、fresh 全集 adjudication evidence，以及旧 gate 下可独立绿色的 contract/port split。pure property可进入 unit sink；live新模型不得阻断 release。每组一个绿色 Git节点，运行 provider、direct consumer与 reverse impact compile。
3. M6.3c：只在同一未提交 cutover工作树准备旧 gate确实无法表达的最小 production切换与最终 manifest；不夹带 M6.5优化，不形成独立 commit，不长期堆入与 cutover无关改动。最终 staged tree 必须重建 canonical world/candidates/query digests；任何 formal record mismatch 都要重裁，零 mismatch 后才可进入 M6.4。
4. M6.4：单个绿色 commit启用 pure validator/schema/new authority，接入恰一次 production fresh scan，激活最终 manifest，并删除旧 owner-wide/per-symbol/compile-support权威、临时 actual dump及所有迁移路径；formal adjudication record保留为历史 review evidence。执行 fixed negative oracle、fast-check、fresh production scan与完整 release sink。
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
- 三处没有按建议例句的字面 shape 落盘：不把任意 `Import`/`Emit`等同 effect；不把92硬编码为完成数量；adjudication record不复制 actual facts，只保存 versioned world/query digest与decision。前两项会分别误杀pure/generated case、在split后失真；第三项避免把review evidence变成第二事实源。三者均以更强、可执行的语义条件替代。
- 验证：`spec.mjs` 291条款绿色；`requirement-trace.mjs` 780 WHAT/3977 tests绿色；`owner-contracts.mjs` 784 contracts绿色；`owner-projects.mjs` 178 localities/711 sources/1853 refs/DAG绿色；`npm run check`完整fast gate绿色。fresh analyzer重现4420 actual source edges与唯一已知missing closure，故GAP-031保持PARTIAL。

### 2026-09-03 — reviewer 第二轮执行阻断与收口意见闭合

- 4 个 P1 全部接受。v2 顶层、slice、capability relation、generated relation 均改为 closed schema；private 由无 slice row 唯一表示，composition 有 slice identity但禁止 exposure。v1 全部顶层字段、nested metadata与 parser兼容路径逐项给出 clean-break命运。
- `deriveAdjudicationCandidates(world)` 固定以全部 live locality 为 key universe；graph、capability、composition、每种 closed relation endpoint与 missing closure只增加稳定 reason。增删 locality、split、mismatch与全部 endpoint均有指定 fixture，M6.3以`records.keys == candidate keys`验收。
- adjudication record只保存 schema/world/query digest与decision；actual export/audience/edge/fact只进ephemeral live report。M6.3c最终staged tree必须fresh重建world/candidate/query digest，失配即重裁；只删除actual dump，formal decision record永久保留但不参与release authorization。
- capability fact拆成observed DU与classified DU；manifest metadata只属normative claim。Unknown阻断cutover，physical observation不能被metadata降格。fact ID哈希去掉诊断位置后的完整observed DU，包含case全部constructor payload、semantic anchor与同anchor occurrence ordinal；world/query digest共用versioned canonical JSON规则。
- 4 个 P2 全部接受。slice law只归provider owner，架构law自动施加；slice/relation evidence与law双向覆盖，specialized relation与graph/direct grant/source edge取交集。M6.3a首项显式更新structured-workflow与全局GAP。fast-check固定legal GREEN base→single mutation→exact code/coordinates，shrink保持同一前提；Unknown另有exact property。不存在的changed-locality lane从当前能力删除，仅可在M6.5后凭性能证据另立node，release永远信full scan。
- 低风险项全部接受：symbol/line只允许ephemeral diagnosis；fixed impact corpus在任何M6.3b production split前落盘，baseline/candidate各三次同环境clean run并保留raw/median，5%只触发调查；执行记录按真实三项非字面采用修正。
- 后续终检补出的三处歧义也已闭合：删除会被误认成live grant且consumer不完整的EventStore row，改为空schema skeleton与独立evidence shape；generated v2固定`laws = [determinism_proof.what_id]`；v1 owner/path只作迁移定位并与fresh graph重验，N→1 justification不拼接或继承，由目标owner依据formal adjudication重写。
- 两个独立 blocker-only复核均返回“无阻断”。本轮只修改计划，不启用v2 schema/gate、不改变production，也不改变GAP-031=PARTIAL。
- 验证：`spec.mjs` 291条款；`requirement-trace.mjs` 780 WHAT/3977 tests；`owner-contracts.mjs` 784 contracts/0 requirement dependencies；`owner-projects.mjs` 178 localities/711 sources/1853 refs/DAG；structured-workflow 244/244；`node scripts/check.mjs`完整fast gate全部绿色。
