# Sphinx — 可观察行为

条款前缀：`SPHINX-`。万象术 Host 注入与 Meditator 权限见 AGENT-028。

## SPHINX-001：生成式认识状态求解器

Sphinx 是由数学内核控制、经 MCP 与 LLM co-yield 的生成式认识状态求解器。

用户给出疑问后，系统维护「目前知道什么、还不知道什么、什么问题值得继续问」的认识状态；不把一次自由文当作答案。

```text
Kernel → LLM：缺少该语义量，请测量 / 生成（structured request）
LLM  → Kernel：结构化观测及不确定性（structured observation）
```

Kernel 拥有：认识状态、全局闭包、判重与约简、方法选择、下一步控制、停止、Canonical Answer。  
LLM 只提供 Kernel 无法自行获得的语义观测与生成建议；不决定方法、下一步、真假裁决、posterior、停止时机或最终报告的认识内容。

continuation 永远属于 Kernel。LLM 不是平权 coroutine。

## SPHINX-002：handle 绑定 inquiry

每次 inquiry 由不透明 `handle` 标识。进程内 `Map<handle, EpistemicState>` 持有状态。

```text
无 handle            → 无状态；不得隐式复用「上一问」
未知 / 缺失 handle   → error
同一 handle          → 同一 EpistemicState 续作
```

V1 不跨进程持久化。进程退出 → handle 失效。

禁止：无 handle 的隐式会话、把 transcript/history 冒充 EpistemicState、用墙钟或外部文件当权威状态。

## SPHINX-003：MCP 工具面 start / resume

Sphinx 暴露为 Host MCP；服务器内工具名 `start` / `resume`。Host 把每个工具暴露为 `sphinx_<name>`（AGENT-028）。

`start(question: string)` → JSON：

```text
handle   （必填）
status   = yield | answered | error
yield    → request = structured Kernel→LLM request（Phase 0 首步：SemanticAssessmentRequest）
answered → answer  = Canonical Answer（Kernel 写出，非 LLM 自由文）
error    → 失败语义；不得假装 answered
```

`resume(handle: string, observation: object)` → 同形。`observation` 必须是 structured semantic observation；禁止把自由散文当控制输入。

禁止：第三工具名冒充控制面；无 handle 的 resume；把 LLM 原文直接当 Canonical Answer。

## SPHINX-004：Phase 0 内核义务

Phase 0 必须同时具备下列能力；缺一则不是 Sphinx Phase 0：

1. Root Question  
2. SemanticAssessment yield（inquiry 首步）  
3. EpistemicState 最小表示 `S = (R, B, E, D, A, C)`  
   - `R` Root Answer Contract belief  
   - `B` belief / semantic uncertainty  
   - `E` observation / evidence  
   - `D` dependency / provenance  
   - `A` 当前可执行认知动作  
   - `C` cost / budget / constraints  
4. GenerativeRule 接口：`G_i(S) → { Candidate Cognitive Actions }`  
5. candidate cognitive actions  
6. global Closure（absorb → infer → reduce → revalue → fixed point；仅 Core Reduction）  
7. simple novelty / dedup（semantic key）  
8. simple dominance replacement  
9. root-relative value（粗糙启发式允许；比较权属 Kernel）  
10. Stop 作为动作（与其它认知动作同一比较空间）  
11. Canonical Answer（Kernel 写出）  
12. MCP `start` / `resume` + handle（SPHINX-002/003）

方法库 V1 恰好：

```text
Multidisciplinary | Abduction | Analogy | Counterexample | Synthesis
```

规则只生成候选动作；无调度权。Stop 与「问什么」同属控制问题：`V_stop ≥ max_a V(a)` 时停止最优。

每次外生 observation 后必须 `S' = Closure(S ⊕ Y)` 且 `Closure(S') = S'`，才允许下一次 yield。

Phase 0 禁止实现完整 A*、通用 Bayesian Network、MCTS、e-graph / 高级表示优化、跨进程 durable journal。

## SPHINX-005：正交边界

Sphinx 是独立产品路径：`src/sphinx/` 纯 JS 内核 + MCP stdio server。

```text
Sphinx 不得 import 万象术 domain / Kernel / Host 业务模块
万象术不得内嵌 Sphinx 闭包 / EpistemicState / Canonical Answer 逻辑
```

万象术只拥有：MCP identity、launch 配置、`ToolPermission.Sphinx` → schema 键 `sphinx_*`、Meditator allow（AGENT-028）。

Sphinx 路径允许 `@modelcontextprotocol/sdk`（及 zod）。AGENT-027「Semble 不引入 MCP SDK」仍只约束 Semble 路径。

禁止：把 Sphinx 编进 ToolRegistry / `js-*`；依赖用户手写 `opencode.json` 配置该 MCP；把 Sphinx 能力漏给非 Meditator managed role。
