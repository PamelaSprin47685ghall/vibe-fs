# Owner、Locality 与 Contract Slice 迁移方案

日期：2026-09-03

状态：M6.0–M6.2 已完成；M6.3 全局规则及 EventStore/Host/Delegation 点名边界已裁决；全部 locality terminal classification、graph + capability facts 派生的全部 live candidate adjudication、完整 capability census 与全量 slice manifest 尚未完成；不得进入 M6.3c/M6.4。当前 92 个 composition provider 只是 pre-cutover candidate 子集，不是永久 gate 数量或完整 adjudication universe。

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

本方案选择轻量 compiler-resolved locality dependency analyzer：读取编译器解析后的 consumer declaration use，只保留 consumer locality 与 provider locality，随后丢弃 symbol identity。它不承担 per-symbol ACL，不保存 snapshot，不做 delta/cache 复用。

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

symbol 只用于编译器解析 declaration owner；映射到 provider locality 后立即丢弃，不进入 ACL 或持久 snapshot。

必须验证：

```text
∀ actual edge C → P where C != P:
P ∈ projectReferenceClosure(C)
```

增量开发可以只分析 changed locality；release sink 必须对完整 production compile set 重新分析。禁止复用旧 FCS snapshot、delta、mtime cache 或人工 baseline。

### 4.4 Locality capability facts

源码能力必须形成唯一、规范化的 production fact set。提取边界读取 F# source、`.fsi`、owner project 与既有 authority metadata；其 normalizer、locality join 与 policy decision 必须是可由 fixture/property test 直接调用的纯函数。至少输出：

- provider locality 的全部 sibling `.fsi` surface。
- 每个 Fable `Import`/`Emit` 的 exact specifier/expression 及其语义分类；语法 token 本身不等于 effect。
- Node、Host、process、network、fs、Git、provider physical capability。
- top-level mutable state、registry、waiter 与 `TaskCompletionSource`。
- capability type、capability value、factory 与 effect constructor 的区别。
- deterministic repository-generated module 及其 producer/build/input linkage。

actual capability facts 不得复制进 manifest、baseline 或 adjudication record形成第二事实源。adjudication record只保存同次 fresh canonical fact query 的稳定 fact ID/digest与决策摘要，不能保存可被后续当成 actual 集合的副本。slice validator、effect purity census、compile-contract-support validator、physical-port/adapter/composition checks 与 fast-check 只消费同一 typed fact set；不得重扫源码、解析其他 gate 的诊断字符串，或以 locality kind metadata 代替源码事实。

实现时从现有 `authority-boundary`、`dsl-ownership` 与 owner-project parsing 抽取、复用 pure fact primitives；唯一新增层是 source facts → locality join。compiler-resolved locality dependency analyzer 继续只拥有 declaration-use edge，不扩成第二 capability scanner。固定 fixture 必须覆盖：kind 误标为 contract、源码却执行 `console.error/process.kill` 的 `FatalProcess` 反例；Node/process import 反例；mutable registry 与 capability value/factory 反例；pure `Emit` 正例；通过 exact relation 的 deterministic generated-module 正例。同一组 facts 必须驱动全部相关 policy verdict。

### 4.5 Contract slice

每个允许被其他 locality 使用的 provider locality 是一个 contract slice。一个 slice 只能拥有：

- 一个 semantic owner。
- 一个 authority class。
- 一组共同演化的 API。
- provider locality 中全部 sibling `.fsi` exports 的并集；每个 production `.fs` 均有同 locality sibling `.fsi`，并按 `.fsi` → `.fs` 顺序编译。
- 一组 exact direct consumer locality。
- 一个由 DAG 推导的 effective audience。

grant 授权该并集的完整 public surface；manifest 不得复制 symbol 清单。同一 slice 内全部 `.fsi` exports 对全部 effective audience 可见。若并集中任一 export 不能与其余 export 共同授权给同一 audience，必须拆 locality/slice；禁止用 JSON 写出无法执行的更细权限。

`private` locality 不是 slice：除自身 source 外，不允许任何 locality dependency 或 ProjectReference 指向它。

### 4.6 三种 exposure 与机械矩阵

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

### 4.7 Port + capability injection

高风险 effect 不直接把 constructor、runner 或 mutable registry 发布给 consumer：

1. shared/bounded contract project 只定义 port type 或函数能力类型。
2. consumer 通过参数获得 capability value。
3. effect implementation 位于独立 runtime locality。
4. composition root 构造 implementation 并注入 consumer。
5. 普通 consumer 的传递 closure 不得包含 implementation project。

这把 Fable 的传递可见性限制在无物理 authority 的 contract 上。

## 5. Manifest 终态

建议把现有 per-symbol consumer ACL 替换为规范性 slice 记录。允许集合写入 manifest；实际集合只由 analyzer 推导，禁止落盘形成第二事实源：

```json
{
  "id": "git-convergence",
  "owner": "change-integration",
  "provider_locality": "git-convergence-contract",
  "exposure": "bounded",
  "allowed_direct_consumers": [
    "git-hook-sync"
  ],
  "allowed_effective_consumers": [
    "git-hook-sync",
    "git-hook-composition"
  ],
  "laws": [
    "STRUCTURED-WORKFLOW-011",
    "DURABLE-CONVERGENCE-..."
  ],
  "semantic_evidence": [
    {
      "path": "requirements/.../tests/...test.mjs",
      "title": "WHAT[...] ...",
      "what_id": "...",
      "surface_module": "..."
    }
  ],
  "justification": "..."
}
```

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
- effect 由 gate 强制 composition-only，不授权普通 consumer。
- `laws[]` 至少一项，可链接多个真实 WHAT；不存在单个 `law` 限制。
- `semantic_evidence` 保留 exact `{path,title,what_id,surface_module}` validator 与 requirement-trace/Surface closure 证明，不降低为文件存在或 prose。

physical port、adapter、composition wiring 不并入模糊的普通 slice edge。迁移为等价的 exact capability relation：

```json
{
  "kind": "physical-port | adapter | composition-wiring",
  "consumer_locality": "...",
  "provider_slice": "...",
  "consumer_module": "...",
  "provider_surface_module": "...",
  "laws": ["..."],
  "justification": "..."
}
```

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

M6.3a 先按 WHY → WHAT → HOW → GAP 把该 relation 写入 `structured-workflow`，再实现 schema。actual imported member、package import target、generated output 与 repository input digest 仍由同次 analyzer/build 推导，不复制进 manifest。relation-specific validator必须证明 build invocation触达 exact generator、input selector，且 exact test callback同时触达 generator lineage与 registered runtime Surface；普通 semantic-evidence validator不能代替 generator lineage proof。gate 必须拒绝 missing、stale、duplicate、actual import/relation mismatch、specifier/target/build invocation漂移、缺 determinism proof、非 repository-content-determined output，以及 Node/Host/process/network/fs/Git/provider import 冒充 compile support。

当前 `published-contracts.json.compile_contract_support` 的 `{path,owner,justification}` 记录是旧 owner gate 的 source-path 豁免，不是上述 relation。M6.4 必须把这些 F# source 纳入普通 locality slice 的完整 `.fsi` 语义后删除旧字段与 parser；禁止兼容读取两种 shape。`#wanxiangshu-loop-detector-envelope` 是当前唯一已裁决实例，仍须由 package import linkage、generated member 存在与 executable determinism proof 共同验收。

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
7. changed-locality lane 可分析局部；release lane 必须 fresh 全量分析。

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
6. exact `compile-contract-support` relations；旧 `compile_contract_support` 裸路径记录的迁移与删除。
7. canonical locality capability facts。

新 manifest 可在 cutover 工作树中准备和验证，但不得先以独立绿色提交落地并与旧 manifest 同时成为权威。实际集合一律由 analyzer 临时生成，不写入 manifest。

执行前必须 fresh 生成 census；当前 178/711/1,853、4,420 actual source edges、1 missing closure 只作参考。

M6.3 完成条件：

1. 同次 fresh census 的每个 locality均有 terminal classification；graph与canonical capability facts派生的全部 live adjudication candidate均有 record。当前 92 个 composition provider只是其中一个 pre-cutover子集，不得把 92硬编码进 gate或完成集合。
2. 零 `undecided`；零从当前 ProjectReference、旧 owner ACL 或 composition 标签自动生成的 grant/relation。
3. 每份 record 包含当前 owner/kind、provider locality 全部 sibling `.fsi` exports、direct/effective consumers、source effect/physical capability fact ID/digest与决策摘要、唯一 decision owner、终态 kind/exposure、拆分或 injection 路径、WHAT/proof。
4. record 只作 review evidence，不是授权事实源，不进入 manifest/allowlist；actual direct/effective/source/capability 集合仍由同次 analyzer 推导，临时 ledger 在 cutover 前删除。
5. 对应 WHY → WHAT → HOW → GAP 与 executable negative oracle 已落盘；“RED”指旧世界被 oracle 识别为违规，提交后的 test suite 必须全绿。
6. 旧 gate 下可独立绿色的 contract/port split 已完成；新 pure validator/property 可提交，但 live 新模型只能 report-only，不能阻断 release。
7. 进入 M6.3c 前，1–6 必须全部成立且 fresh report 不再有未裁决 blocker。M6.3c 只准备旧 ACL 无法表达的最小 production cut、全量终态 manifest 与 exact relations；这些准备完成才算 M6.3 完成，随后进入 M6.4。仅完成点名 owner 裁决不等于 M6.3 完成。

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
10. canonical capability facts、exact compile-contract-support、semantic-evidence、physical adapter、composition wiring 通过同一 pure policy validator。
11. 同次 fresh census 的全部 locality classification与 graph/capability candidate adjudication已落实为终态结构和 relation，无 stale/duplicate grant、reference、relation 或缺 law/evidence。
12. 删除 owner-wide authorization expansion、per-symbol consumer ACL、旧 schema/parser 与旧 `compile_contract_support` 裸路径豁免。
13. 删除 dead production `symbolUses: []`、旧 FCS snapshot/delta/cache、临时 classification ledger、compat facade、过渡 adapter、旧空路径与 pre-cutover report-only release bypass；保留 `owner-projects` 对 source→locality、ProjectReference DAG 与 closure 的唯一职责。
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
- exact compile-contract-support、physical-port、adapter 与 composition-wiring relations。

生成器先构造一个合法 world，再对每条性质施加单一目标 mutation；避免 cycle 等无关早期错误遮蔽目标 verdict。fast-check 只生成 canonical analyzer 已产出的 facts，不扫描源码或复制 effect/closure 公式。

性质：

1. actual source edge 逃出 ProjectReference closure → RED。
2. direct reference 缺 exact grant，包括 same-owner edge → RED。
3. stale 或 duplicate grant → RED。
4. bounded effective audience 越界 → RED。
5. shared 携带 canonical effect-authority、mutable、capability value/factory fact → RED。
6. effect 被任一 non-composition consumer 反向到达 → RED。
7. composition 被普通 consumer 引用或缺 exact composition-wiring relation → RED。
8. private locality 被引用 → RED。
9. contract closure 含 non-contract locality或越过明示预算 → RED。
10. ProjectReference cycle → RED。
11. owner rename/merge 后 normalized authorization projection 不变。
12. legal split 不扩大 capability audience：生成 `old locality → new locality partition`、source/export/capability 映射及 consumer/grant/reference 重映射；证明每个 source 恰映射一次、旧边均有映射、新边不跨 capability 偷渡，并按 capability 比较 normalized external direct/effective audience。owner 名不得作为映射键。
13. compile-contract-support missing、stale、duplicate、specifier/target mismatch、nondeterministic 或 physical import → RED。

条目数量不是验收目标；每条性质必须消灭独立错误世界。固定 seed，失败输出最小 counterexample graph。`.fsi` export extraction、compiler declaration extraction、compile-set drift、Import/Emit 分类与 package-import linkage 由固定 fixture/真实 compiler gate证明，不伪装成随机图性质。pure `Emit` 与已批准 deterministic compile support 是 GREEN；canonical effect-capability 才是 RED。

### M6.5：切换后实施收益明确的 slice 拆分

M6.5 只承接可测量的 authority/audience/closure/impact 优化，不承接任何 M6.4 correctness debt。若发现旧权威、未落实 adjudication、stale relation、composition 业务判断或 capability matrix 违规，必须重开 M6.3/M6.4 修复。

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
7. 比较稳定排序、去 owner label 的 normalized authorization projection：locality identity、ProjectReference edge、provider slice/exposure、slice grant、actual source edge、effective audience、physical/adapter/composition/compile-support capability relation与 authorization violation set必须完全相同。
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

- 代表性改动的 impact source 中位数下降 25%是优化方向，不是 correctness gate。
- full release build 不得出现无法解释的显著退化；5% 以内视为测量波动，超过则必须分析。
- 新 project 若既不收窄 audience，也不缩小 compile closure，应撤销。

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
- M6.1 analyzer：`locality-symbol-uses.fsx` 从 fresh fingerprint flat project 读取 FCS declaration use；`locality-dependencies.mjs` 将 use 映射为稳定去重的 cross-locality source edge，再验证 ProjectReference transitive closure。扫描产物只存在于本次临时目录；无 snapshot、delta、mtime、跨 run cache 或 symbol ACL。
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

1. `CanonicalEventCodec.{fsi,fs}` 从 `eventstore-core-runtime` 拆为独立 `eventstore-canonical-codec` bounded contract。六个公开函数 `encode/checkIdentity/mergeByIdentity/tryDecode/tryDecodeUtf8Text/tryDecodeUtf8` 同属一个 canonical identity protocol，批准共同授权；禁止只复制或另写 `checkIdentity` 公式。
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
8. `#wanxiangshu-loop-detector-envelope` 是仓库内容派生的确定性 tokenizer/envelope artifact，裁决为 exact `compile-contract-support`，不是 Host/IO effect。必须保留 repository-SSOT、生成 determinism 与 import linkage proof；`LoopDetector` 因 process-local Dictionary scratch 继续是 runtime，不伪装成纯 contract。
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
4. 同次 fresh census 的每个候选 locality 输出同一 adjudication record：当前 owner/kind、全部 sibling `.fsi` exports、actual direct/effective consumer query、source effect/physical capability fact ID/digest与决策摘要、唯一 decision owner、裁决后的 kind/exposure、拆分或 injection 路径、对应 WHAT/proof。记录是 review evidence，不是允许集合，不复制 actual fact set；actual 集合仍由 analyzer 同次推导。零 `undecided`、零 current-reference-derived grant；若切分改变候选数，以 fresh 集合全覆盖为准，不硬编码 92。

#### 裁决后的施工批次与停止条件

1. M6.3a：更新 durable-events、host-boundary、degeneration-guard、delegation、time-capability、causal-wait、process-execution与所有 FatalProcess caller owner 的 WHY/WHAT/HOW/GAP；建立行为 oracle与 architecture/closure illegal fixture。先观察旧世界被新 oracle判为违规，再提交全绿 fixture；禁止提交红色 suite。
2. M6.3b：提交 report-only pure validator/property、fresh 全集 adjudication evidence，以及旧 gate 下可独立绿色的 contract/port split。pure property可进入 unit sink；live新模型不得阻断 release。每组一个绿色 Git节点，运行 provider、direct consumer与 reverse impact compile。
3. M6.3c：只在同一未提交 cutover工作树准备旧 gate确实无法表达的最小 production切换与最终 manifest；不夹带 M6.5优化，不形成独立 commit，不长期堆入与 cutover无关改动。
4. M6.4：单个绿色 commit启用 pure validator/schema/new authority，接入恰一次 production fresh scan，激活最终 manifest，并删除旧 owner-wide/per-symbol/compile-support权威及所有临时迁移路径。执行 fixed negative oracle、fast-check、fresh production scan与完整 release sink。
5. report-only parser/analyzer/fixture存在不等于第二权威；只有 live old/new gate同时能够阻断 `format-build-test` 才是双 release authority。M6.4 后不得保留可供 release 降级的 report-only bypass。
6. M6.5：只按可测量 audience/closure收益继续拆 `ProcessEventLog + Store`、完整 HostEvent/HostSignal codec与已正确迁出的 Delegation projection；没有收益就不拆，不承接 correctness debt。
7. 任一阶段若需要放宽矩阵、把 current refs 自动变 grant、让 adapter/runtime直接消费 effect implementation、把 central composition下沉为公共 contract，或新增本节未定义的业务 owner 转移，必须停止并请求新裁决。

### 2026-09-03 — 外部建议逐项复核与施工边界修订

- 已按源码、`.fsi`、fsproj、现行 gate与 fresh FCS scan复核全部建议；EventStore既有裁决不变。
- 状态与完成集合改为“全部 locality classification + graph/capability派生的全部 live candidate”；92只保留为当前 composition-provider子集。
- public surface固定为同 locality全部 sibling `.fsi` exports并集；新增 canonical locality capability facts与 clean-break exact compile-contract-support schema。旧 `compile_contract_support`裸路径语义明确退役。
- Host subscription改用 typed error/mode计划；FatalProcess固定 mandatory capability injection与唯一 fatal Node adapter；HostForkRuntime constructor census修正为5 sites/4 localities；PTY/Temporal/CausalWait/Process边界按实际 effect面扩充。
- Delegation business fold不得改标签塞入 composition；M6.4前迁 owner projection、child index与closed rejection，durable composition只保留outer routing/combine并调用 PromptAuthority owner decision。
- production fresh scan固定只在integration release path执行一次；fast-check消费production pure decision与canonical facts，采用legal-world + single mutation；M6.5只优化，M6.6比较normalized authorization projection，M6.7只做final census/release/report/GAP close。
- 两条字面建议未采用：不把任意 `Import`/`Emit`等同effect；不把92硬编码为完成数量。前者会误杀pure `Emit`与已裁决generated module，后者会在split后失真。两者均已替换为更强的语义条件。
- 验证：`spec.mjs` 291条款绿色；`requirement-trace.mjs` 780 WHAT/3977 tests绿色；`owner-contracts.mjs` 784 contracts绿色；`owner-projects.mjs` 178 localities/711 sources/1853 refs/DAG绿色；`npm run check`完整fast gate绿色。fresh analyzer重现4420 actual source edges与唯一已知missing closure，故GAP-031保持PARTIAL。
