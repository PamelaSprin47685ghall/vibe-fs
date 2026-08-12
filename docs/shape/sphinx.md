# Sphinx — 所有权与边界

行为：`what/sphinx.md`。Host 注入：AGENT-030。本页只规定 writer、编译边界与依赖方向。

## 模块边界

```text
src/Wanxiangshu/Sphinx/
  Types.fs              ← Question/Answer/Evidence/Action 领域语义类型
  RuntimeTypes.fs       ← Request/Observation/EpistemicState/solver 运行态类型
  State.fs              ← RootContract 派生、预算、基础状态变换
  Methodology.fs        ← generator library + applicability
  Bayes.fs              ← qualified likelihood inference
  Search.fs             ← strict graph A* embedding + epistemic frontier projection
  MonteCarlo.fs         ← graph-MCTS selection/rollout/backup
  Representation.fs     ← explicit equivalence + Pareto reduction
  Value.fs              ← root-relative action/stop utility
  Absorb.fs             ← typed Observation → EpistemicState
  Closure.fs            ← deterministic fixed-point closure
  Policy.fs             ← continuation / next action / Canonical Answer
  DecodePrimitives.fs   ← raw JS shape primitives at wire boundary
  ObservationCodec.fs   ← raw observation → strong Observation
  WireEncode.fs         ← Request / Canonical Answer → MCP wire object
  Codec.fs              ← public wire façade only
  Session.fs            ← process-local handle → EpistemicState
  McpServer.fs          ← MCP SDK / zod / stdio only

Fable
  ↓
dist/Sphinx/*.js        ← 唯一生产 JS 产物

Wanxiangshu.Kernel.SphinxMcp
  ← server identity / sphinx_* / production entry path only
Wanxiangshu.OpenCode.SphinxMcpConfig
  ← env → launch → config.mcp.sphinx only
Roles / StaticTools
  ← ToolPermission.Sphinx → Inquiry allow sphinx_*
```

## 所有权表

| 知识 | 唯一 writer | 禁止副本 |
|---|---|---|
| Question / Evidence / Action 领域类型 | `Sphinx/Types.fs` | prompt、Host、手写 JS |
| Request / Observation / EpistemicState 运行态类型 | `Sphinx/RuntimeTypes.fs` | transcript、Host session、wire object |
| QuestionForm → RootContract belief | `Sphinx/State.fs` | LLM hard label、Host adapter |
| 方法适用度 / 方法库 | `Sphinx/Methodology.fs` | prompt pipeline、Agent 角色表 |
| Evidence → Bayesian posterior | `Sphinx/Bayes.fs` | LLM 自报 posterior、Synthesis |
| strict A* embedding | `Sphinx/Search.fs` | generic epistemic priority 冒充 A* |
| graph-MCTS statistics | `Sphinx/MonteCarlo.fs` | Evidence / Canonical Answer |
| equivalence / Pareto representatives | `Sphinx/Representation.fs` | 文本 hash 直接定义 ontology |
| root-relative utility / Stop | `Sphinx/Value.fs` | LLM 直接决定停止 |
| typed Observation absorb | `Sphinx/Absorb.fs` | Host、Session、MCP handler |
| deterministic fixed-point closure | `Sphinx/Closure.fs` | Host、Session、MCP handler |
| continuation / Canonical Answer | `Sphinx/Policy.fs` | LLM、Host |
| raw JS shape primitives | `Sphinx/DecodePrimitives.fs` | Kernel 下游再次 duck type |
| Observation decode | `Sphinx/ObservationCodec.fs` | Policy / Closure 猜 wire shape |
| Request / Answer encode | `Sphinx/WireEncode.fs` | Session / MCP 复制 DTO 规则 |
| wire public façade | `Sphinx/Codec.fs` | 第二套 codec 入口 |
| `handle → EpistemicState` | `Sphinx/Session.fs` | Host Session、EventStore、transcript |
| MCP `start` / `resume` | `Sphinx/McpServer.fs` | ToolRegistry、`js-*` |
| Host launch identity | `Kernel/SphinxMcp.fs` + `SphinxMcpConfig.fs` | 第二套路径常量 |

## 依赖方向

```text
Types → RuntimeTypes
  ↓
State / Methodology / Bayes / Search / MonteCarlo / Representation / Value
  ↓
Absorb → Closure → Policy
  ↓
DecodePrimitives → ObservationCodec
WireEncode ───────────────┘
  ↓
Codec → Session → McpServer
```

Sphinx 源文件可以同处 `Wanxiangshu.fsproj`，但内核依赖不得反向指向 `Wanxiangshu.Domain`、Agent、Host、Journal、Session runtime。namespace 同属 `Wanxiangshu` 不等于语义所有权共享。

MCP SDK / zod 是最外壳依赖，只能出现在 `McpServer.fs`。Node crypto UUID 与 `handle/status` response envelope 只在 `Session.fs`；`Codec.fs` 刻意保持很小，作为 Observation decode / Request encode / Answer encode 三个公共入口的合法 seam，而不是第二层业务实现。其余认识逻辑保持纯函数或不可变状态变换。

## 不变量

1. `Codec.decodeObservation` 是对外唯一解码入口；其实现只委托 `ObservationCodec`，而 raw `obj` 检查只存在于 wire codec 层；失败即 error，不修改 state。
2. `Policy` 只接受与 `PendingRequest` 同型的 Observation；Investigation 还必须匹配 action id。
3. `Closure` 只处理已类型化 Observation；fixed point 后才回 Policy。
4. `SessionStore` 可变性只用于 handle 索引；`EpistemicState` 本身以旧值 → 新值替换。
5. `McpServer` 不包含认识判断；只注册工具、校验 schema、转发 wire payload。
6. Host 不 import Sphinx Kernel；只知道生产入口 `dist/Sphinx/McpServer.js` 与权限键 `sphinx_*`。

## 禁止

- `src/sphinx/*.js` 或其它第二实现。
- `scripts/build.mjs` copy Sphinx 源码。
- `evidenceMass`、LLM 自增 confidence、Synthesis 增加 Evidence。
- 同 semantic key 跨独立 dependency group 强行 merge。
- 用一个 `primaryForm` 丢弃 QuestionForm 分布。
- 用 action 排序 helper 冒充 strict graph A*。
- 用 visit counter 壳子冒充 MCTS rollout/backup。
- MCP/Host 层复制 Closure、Bayes、Stop 或 Canonical Answer。
