# Owner、Locality 与 Contract Slice 迁移方案

日期：2026-09-03

状态：老板裁决已成立；M6.0–M6.1 已完成，M6.2 待执行

适用背景：Fable owner-project 编译边界、published contract 授权与 semantic owner 重整

## 简略介绍

当前 contract manifest 声称能够实施 exact symbol × exact consumer owner 授权；实际 gate 会把一个 consumer owner 的授权扩张到该 owner 的全部 project，Fable 又会合并 ProjectReference 的传递源码闭包。因此，manifest 的精度高于编译边界真正能兑现的精度。

本方案改用三层模型：

- owner 管 vocabulary、invariant 与业务决策责任；数量可在语义一致时适度减少。
- locality/project 管真实编译边界；数量适度增加，以缩小依赖闭包与增量编译范围。
- contract slice 管一组共同演化、共同授权的公开能力；`.fsi` 是唯一公开符号清单。

授权绑定稳定的 locality，不绑定 owner 名称。每条跨 locality 依赖都必须经过 slice grant；owner 是否相同不参与授权判定。Fable 的真实有效 audience 按 ProjectReference 反向可达闭包计算；轻量 compiler-resolved analyzer 证明实际源码依赖没有逃出该闭包。高风险 effect implementation 只能由 composition 到达，普通 consumer 只依赖 port/capability。

当前只读 census 已增长到 175 个 locality、711 个 source、1,844 条 ProjectReference；这些数字继续变化。190–210 个 project、39–44 个 owner、增量影响下降 25%只作规划导向，不构成正确性定义。模拟显示，优先增加约 30 个高价值 slice，能消除理想化 project-level 模型中约 50.8% 的额外暴露；继续增加至 50 个的收益仅升至约 55.4%，边际收益明显下降。

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

结论：`symbols`、`symbol_roots` 不能继续充当 consumer ACL。为避免双事实源，终态以 `.fsi` 为唯一 export inventory；manifest 只记录 locality-slice grant、WHAT laws 与 evidence relation。

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
| owner locality/project | 175 |
| production source | 711 |
| published contract 记录 | 783 |
| ProjectReference | 1,844 |
| 跨 owner ProjectReference | 1,607 |
| 被跨 owner 引用的 provider project | 158 |
| 指向 contract kind 的跨 owner reference | 798 |
| 指向 composition kind 的跨 owner reference | 711 |
| 指向 adapter kind 的跨 owner reference | 41 |
| 指向 runtime kind 的跨 owner reference | 57 |

57 条 foreign runtime reference 当前全部来自 composition，符合已有 composition-only runtime 规则。711 条指向 composition kind 的 foreign reference 则说明 composition 标签被大量当作普通共享 provider 使用；迁移必须逐项判断它们是 kind 错标、公开 API 与 wiring 混装，还是依赖方向错误。

### 3.1 两个极端

以下 project-count 模拟使用较早的 170-locality census，保留它只为展示粒度曲线。当前 live census 已是 175；正式执行必须由 analyzer 重算，不能直接套用表中总数。

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

locality 是稳定、全局唯一的编译身份。当前快照中 175 个 locality 已全局唯一，可直接作为授权主键。

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

### 4.4 Contract slice

每个允许被其他 locality 使用的 provider locality 是一个 contract slice。一个 slice 只能拥有：

- 一个 semantic owner。
- 一个 authority class。
- 一组共同演化的 API。
- 一个 `.fsi` public surface。
- 一组 exact direct consumer locality。
- 一个由 DAG 推导的 effective audience。

同一 slice 内全部 `.fsi` exports 对全部 effective audience 可见。若该事实不可接受，必须拆 slice；禁止用 JSON 写出无法执行的更细权限。

`private` locality 不是 slice：除自身 source 外，不允许任何 locality dependency 或 ProjectReference 指向它。

### 4.5 三种 exposure 与机械矩阵

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

纯度不由手写 `"exposure": "shared"` 自证。gate 必须复用现有 authority-boundary、physical-import、mutable-registry 与 locality-kind 检查结果；同一事实只保留一个 analyzer，不新造第二套正则扫描器。

### 4.6 Port + capability injection

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

- `.fsi` 是唯一 export inventory。
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

## 6. 迁移步骤

### M6.0：把老板裁决写入 WHY → WHAT → HOW → GAP

老板裁决已成立。第一提交必须先改变规范事实：

1. 授权最小单位定义为 `consumer locality → provider slice`。
2. `.fsi` 定义为 slice 唯一 export inventory。
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
6. compile-contract-support public surface 的去留。

新 manifest 可在 cutover 工作树中准备和验证，但不得先以独立绿色提交落地并与旧 manifest 同时成为权威。实际集合一律由 analyzer 临时生成，不写入 manifest。

执行前必须 fresh 生成 census；当前 175/711/1,844 只作参考。

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
9. semantic-evidence、physical adapter、composition wiring 通过原有 exact validator。
10. 删除 owner-wide authorization expansion。
11. 删除 per-symbol consumer ACL 与旧 schema parser。
12. 删除 dead production `symbolUses: []` 路径以及旧 FCS snapshot/delta/cache 残余。

同一 commit 的 gate 必须全绿。不存在“先启新 gate、后分类”或“新 gate 已启用、旧模型 M6.7 再删除”的过渡态。

建议 commit：

```text
refactor(architecture): cut over locality slice authorization
```

#### M6.4A：用 fast-check 证明生产图算法

graph analyzer 必须是 production pure function；property test 直接调用它，不复制 closure 或 authorization 公式。

生成：

- 2–40 个 locality 的随机 DAG。
- owner assignment 与随机 owner merge/rename。
- locality kind、exposure、direct grant、bounded audience。
- actual compiler-resolved locality edges。

性质：

1. actual source edge 逃出 ProjectReference closure → RED。
2. unauthorized direct edge → RED。
3. 传递路径扩大 bounded audience → RED。
4. owner rename/merge → authorization verdict 完全不变。
5. 合法 project split → audience 不扩大。
6. shared 引入 effect dependency/factory → RED。
7. effect 出现任一非-composition reachable consumer → RED。
8. cycle → RED。
9. 删除 live edge后 stale grant → RED。

固定 seed，失败输出最小 counterexample graph。fast-check 证明组合性质；真实 compiler fixture 证明 F# declaration edge extraction。

### M6.5：切换后实施收益明确的 slice 拆分

优先顺序：

1. effect 与 data/decision 混装。
2. bounded effective audience 明显超出需求。
3. reverse closure 大且修改频繁。
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
7. 证明 ProjectReference graph、slice grant、actual source edge 与 effective audience 前后完全相同。
8. 比较新 gate 的完整 authorization verdict，必须 byte-for-byte 等价。
9. 运行两个原 owner 的全部 proof。

预计只有 5–10 组合格，owner 数可能约降至 39–44。该数字只作导向；语义不满足时保留原 owner。

建议 commit：

```text
refactor(owner): unify <owners> under <semantic-owner>
```

### M6.7：最终闭环

旧授权模型已在 M6.4 原子删除。本阶段只清理迁移产生的真实残余：

- stale slice grant 与无意公开的 compile-contract-support surface。
- 被拆 project 的遗留 source、reference 与 locality entry。
- 过渡 adapter、compat facade 与空 project。
- 无 proof 的 law、无 law 的 evidence、无 live edge 的 capability relation。

重新生成最终 census，更新 GAP-031 的问题陈述、证据和状态。只有正式 analyzer、全部 hard acceptance、release sink 同时绿色时，GAP-031 才能 CLOSED。

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

1. `npm run format-build-test`。
2. fresh 全量 compiler-resolved source-edge scan。
3. 全部 fixed red canary。
4. fast-check graph properties。
5. GitGateway、missing ProjectReference、effect factory、transitive closure 真实 canary。
6. semantic-evidence、physical adapter、composition wiring 无降级证明。
7. 输出迁移前后 owner/project/ref/closure/build-time 对照。

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
- shared slice 引入 effect import、mutable registry、capability value 或 factory 为 RED。
- effect implementation 不进入普通 consumer closure。
- `.fsi` 是公开 symbol 唯一事实源。
- owner rename/merge 前后 authorization verdict 完全相同。
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
