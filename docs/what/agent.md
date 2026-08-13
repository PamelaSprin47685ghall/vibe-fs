# Agent 与能力 — 可观察行为

条款前缀：`AGENT-`。  
权限写入点与双层边界见 `shape/agent.md`。

## AGENT-001：Canonical Role 与 Agent Tier

Canonical Role 决定工具权限与 system prompt。

```fsharp
type Role =
    | Orchestrator | Manager | Coder | Inspector | DevOps
    | Browser | Inquiry | Reviewer
    | Blogger | Distiller

type AgentTier = Fast | Deep
```

`Bookkeeper` 保持 InternalLeaf + Attached：拥有机器身份与 Persona（AGENT-028），**不**进入本 public Role DU。

Tier **只**改变模型绑定（Execution Binding，AGENT-029）。fast-ROLE 与 deep-ROLE 的 Role Law、工具权限、能力矩阵必须相同。

Canonical Role **不**决定 Companion 资格（COMPANION-001/002）。

## AGENT-002：必须存在的 22 个 Agent

```text
fast-orchestrator     deep-orchestrator
fast-manager          deep-manager
fast-coder            deep-coder
fast-inspector        deep-inspector
fast-devops           deep-devops
fast-browser          deep-browser
fast-inquiry          deep-inquiry
fast-reviewer         deep-reviewer
fast-blogger          deep-blogger
fast-distiller        deep-distiller
fast-bookkeeper       deep-bookkeeper
```

缺任一 → 启动失败。每个 Agent 必须有非空且 pair 内互异的 model 字符串。

`fast-bookkeeper` / `deep-bookkeeper` = 强制内部执行身份（Persona Clerk / Curator；可复用 inspector 模型绑定）。不进 Manager fork 面，不进 public Role DU。

G3 clean-break：`Role.Student` / `Role.Teacher` 与 `fast|deep-student|teacher` **已删除**；无 alias。  
GrandRewrite clean-break：`meditator` / `executor` **已删除**；无 alias。  
Mandatory baseline = 22（含 Bookkeeper pair）。  
推理职责由 Inquiry（AGENT-025）承接。

## AGENT-003：Peer

```text
peer(fast-ROLE) = deep-ROLE
peer(deep-ROLE) = fast-ROLE
```

Peer 名称必须在启动配置验证阶段证明存在。

## AGENT-004：非法旧名

下列全部非法，无 alias、无自动补全：

```text
orchestrator, manager, build, plan, coder, inspector, devops,
browser, meditator, inquiry, reviewer, student, teacher,
blogger, executor, distiller, bookkeeper,
fast, deep, reviewer-fast, fast_reviewer
```

`meditator` / `executor` 不得映射到 Inquiry / Distiller。  
`student` / `teacher` 及 `fast|deep-student|teacher` 一律 fail closed；不得映射到 Inquiry / Inspector。  
裸名（无 `fast-` / `deep-` 前缀）一律非法。

## AGENT-005：用户必须显式选择

新的公开 Authority Root 必须携带准确 Agent（如 `fast-coder`）。  
省略、旧名或 build/plan → `HostContractUnsupported`。

## AGENT-006：能力矩阵

| 角色 | 工具 |
|------|------|
| Orchestrator | `commission`, `join`, `horizon`, `auto-injected` |
| Manager | `fork`, `join`, `horizon`, `todowrite`, `fission`, `suicide`, `auto-injected` |
| Coder | `read`, `write`, `edit`, `glob`, `grep`, `inspect`, `mv`, `rm`, `bash-honeypot`, `js-coder`, `auto-injected` |
| Inspector | `read`, `glob`, `grep`, `query-shell`, `fetch`, `js-inspector`, `auto-injected` |
| DevOps | `read`, `glob`, `grep`, `js-devops`, `inspect`, `establish-behavior`, `repair-behavior`, `run`, `open-terminal`, `send-terminal`, `read-terminal`, `signal-terminal`, `horizon`, `join`, `auto-injected` |
| Browser | `read`, `glob`, `grep`, `js-browser`, stealth-browser MCP（AGENT-026）、`auto-injected` |
| Inquiry | `inspect` + Sphinx MCP（AGENT-025、AGENT-030）、`auto-injected` |
| Reviewer | `read`, `glob`, `grep`, `judge`, `js-reviewer`, `auto-injected` |
| Blogger | `chronicle` |
| Distiller | 无工具 |
| Bookkeeper（内部） | `js-bookkeeper` |

`auto-injected` 是 HOST-013 的真实 no-op entity（空参数，live execute 恒返回 `OK`），不是业务能力。Blogger / Distiller / Bookkeeper 不含此项。

已删除（不得再出现于矩阵）：`list`、`verdict`、`blog`、`executor`（工具）、`fork-pty`、`edit-qa`、`return`、`fork-manager`、`fork-agent`、`inspector`（工具名）、`coder`（同步委派工具名）。

## AGENT-008：内部 Agent 不可见

Blogger、Distiller、Bookkeeper（含 `fast-bookkeeper` / `deep-bookkeeper`）不得出现在任何模型可见的 enum、schema 或工具参数提示中。

## AGENT-009：示踪面可见集合

| 暴露面 | 可见集合 |
|--------|---------|
| Manager `fork` | fast/deep coder, inspector, devops, browser, inquiry |
| Orchestrator `commission` | fast-manager, deep-manager |
| `inspect` 工具 | fast-inspector, deep-inspector |
| `establish-behavior` / `repair-behavior` | fast-coder, deep-coder |
| `horizon()` | 在场名册（Byname / TerminalName 等），无 id |

不可 fork：reviewer、blogger、distiller、bookkeeper。

`fork` 工具描述必须按 ARCH-017 写明五个 Office 的 entitled consequence，并写明：`navigator`（Fast Browser）与 `researcher`（Deep Browser）只从 public web 建立事实，不得用于本地文件或仓库。描述不得出现 `fast-` / `deep-` 机器名。两个 calling 名只差 persona / 深度，不差 authority。

## AGENT-010：fast/deep 权限一致

```text
permissions(fast-ROLE) = permissions(deep-ROLE)
```

不得出现 fast 只读、deep 才可写。

## AGENT-011：Manager 无普通工具

Manager 只有 `fork` / `join` / `horizon` / `todowrite` / `fission` / `suicide`，外加 HOST-013 `auto-injected` no-op。  
不能直接读文件、跑终端、改仓库，也不能 `inspect`。

## AGENT-012：Coder 的 Inspector 不透明

Coder 可见 `inspect` 工具。Role Law 与 `inspect` description 都必须把 Inspector 写成见证者，不是第二双编辑的手（PROMPT-021）。  
不得泄露 Inspector 的 `query-shell` / 取证权限，不得把 Inspector 当常规验证代理，不得请 Inspector 实现或修复代码。

## AGENT-013：DevOps 独占终端与有界执行

只有 DevOps 可 `open-terminal` / `send-terminal` / `read-terminal` / `signal-terminal`，以及 `run`。  
文件修改只能经同步 `establish-behavior` / `repair-behavior` 委派，不能直接 `write`/`edit`。  
DevOps 的 `join` 有 10s 等待预算：无完成项时结束本次等待（Host 事实）；不向 provider 暴露 `status` / `code` / `TIMED_OUT` 等 DTO 字段。Orchestrator 与 Manager 的 `join` 无此 10s 预算。

## AGENT-014：Reviewer 只读

Reviewer = 只读工具 + `judge`。不能写文件、不能跑命令。

## AGENT-015：Orchestrator 只 commission Manager

`commission` 接受 `fast-manager` / `deep-manager`（新路）或按 Byname 续做既有路（同 job 续做，同 worktree/session；GLORY-068）。不暴露 job id / worktree / `reused` 等机器字段。

## AGENT-016：mv / rm 仅 Coder

`mv`/`rm` 只进 Coder 矩阵。其它角色（含 DevOps）不得获得。双层 fail-closed 适用。

## AGENT-017：mv 语义

POSIX `mv`：参数 `source`、`destination`；目标存在则覆盖；目录/跨文件系统按 POSIX。

## AGENT-018：rm 语义

POSIX `rm`，但**禁止删非空目录**：文件与空目录可删，非空目录拒绝。参数 `path`。

## AGENT-023：bash-honeypot 仅 Coder

`bash-honeypot` 只进 Coder 矩阵。无参数；调用不执行任何 shell，只返回越权拒绝文本。  
Host 内置 `bash` 对所有 managed role 仍保持 deny（AGENT-007）；本工具不是放行 bash。

## AGENT-020：（空缺）Student / Teacher — G3 已删除

**编号永久空缺。** G3 clean-break 删除 `Role.Student` / `Role.Teacher`、`fast|deep-student|teacher`、
Learn/Compile / QA / SKILL / `teacher` 工具协议。无 alias、无 deprecated mode。
后继：推理 → Inquiry（AGENT-025）；证据 → SyncDelegate Inspector（AGENT-024）。

## AGENT-022：（空缺）Student SKILL — G3 已删除

**编号永久空缺。** StudentCompile / `.agent/skills/.../SKILL.md` 制品门已删除。无 successor skill 协议。

## AGENT-024：SyncDelegate DAG 与 InvocationMode

允许的同步委派边（dedicated `inspect` / `establish-behavior` / `repair-behavior` → SyncDelegate，见 EXEC-026/028）：

```text
Inquiry → Inspector
Coder   → Inspector
DevOps  → Inspector
DevOps  → Coder
```

图必须是 DAG。禁止反向或成环边（例如 `Inspector → Coder`、`Inspector → Inquiry`、`Coder → DevOps`）。
嵌套 `DevOps → Coder → Inspector` 合法。启动/配置须静态证明 sync delegate 图无环。

`InvocationMode = SynchronousDelegate` 时：callee 按普通 Assistant completion 结束当前 invocation；Host 物化 bounded WorkRecord（`includeOpening=false`）并投影给 caller。  
**删除**独立 `return` 通道与 `Returned → Completion` 双 await。细节见 EXEC（SyncDelegate / WorkRecord）。

本条只宣布 SyncDelegate DAG 与 `InvocationMode`。不得把已删除的 Teacher leaf / no-Companion 拓扑套到
Dedicated Inspector/Coder（后者是 Work + Attached，HOST-008）。

## AGENT-025：Inquiry 能力（inspect + Sphinx MCP + epistemic style）

正式工具面（普通 Work Session；事实调查只经 SyncDelegate；认识求解经 Sphinx MCP）：

```text
Inquiry → { inspect, sphinx MCP, auto-injected }
```

`auto-injected` 是 HOST-013 空参 no-op entity，不是 Inquiry 业务能力。`sphinx MCP` = Host MCP `sphinx` 的工具面（AGENT-030 / SPHINX-003）。仍禁止 filesystem 直读。

禁止再持有：`read` / `glob` / `grep` / `write` / `edit` / `run` / `establish-behavior` /
`repair-behavior` / `fork` / `commission` / 终端动词 / `join` / `horizon` / stealth-browser MCP，以及任何 filesystem 直读面。

职责：reason / question / compare / challenge / synthesize；经 Sphinx co-yield 推进认识状态。
分层固定为 `Inquiry = reasoning`，`Inspector = evidence acquisition`
（AGENT-024 边 `Inquiry → Inspector`）；Sphinx = 认识状态求解器（SPHINX-001），不是证据扫库。

Prompt discipline 吸收原 Student **epistemic style**（不是 workflow protocol）：

```text
先形成当前理解
主动寻找反例
把事实问题委派 Inspector，并针对回答继续追问
区分证据 / 推论 / 不确定性
在理解收敛前避免草率终止
```

不得重新引入：`LearningState` / QA / Compile / `MeditatorLearn|Compile` / Student 式 final `return`
作为 Inquiry 业务阶段或 RequestKind。终端就是普通 Assistant completion。

Steward 属未来预留（AGENT-028）；不得把 Steward 写成现行能力。  
Student/Teacher 角色已删除（AGENT-020 空缺）；`meditator` 不得 alias 回 Inquiry。

## AGENT-026：Browser stealth-browser MCP

Browser 的 network tools = Host MCP `stealth-browser-mcp` 的工具面。不是插件 ToolRegistry 工具，不是虚构 `network` 工具。

Host-final `opencode.json` 必须由 config hook 注入：

```text
mcp.stealth-browser-mcp = {
  type = "local"
  command = uvx --python 3.13 --from git+https://github.com/vibheksoni/stealth-browser-mcp.git@{ref} python -m server
  enabled = true
}
```

`ref` 默认 `master`。`STEALTH_BROWSER_MCP_REF` 非空则覆盖。

测试启动：

```text
STEALTH_BROWSER_MCP_DISABLED 为真            → enabled = false，不 spawn
STEALTH_BROWSER_MCP_FIXTURE 非空             → command = node <fixture>，enabled = true
WANXIANGSHU_TEST 为真且无 fixture            → enabled = false
```

权限（AGENT-007 第一层；fast/deep 相同，AGENT-010）：

```text
CanonicalRole.Browser → allow  stealth-browser-mcp_*
其它 managed role     → deny   stealth-browser-mcp_*
```

域能力仍是 `ToolPermission.Network`。Host schema 键是 `stealth-browser-mcp_*`。OpenCode 把该 MCP 的每个工具暴露为 `stealth-browser-mcp_<tool>`。

stealth-browser MCP 与 `read` / `glob` / `grep` 同类：Host-native，不进 ToolRegistry，不进 `js-*` 投影。第二层 execution gate 不适用于本 MCP 面。

禁止：

1. 把 stealth-browser MCP 编入 ToolRegistry / `js-*`
2. 给非 Browser 角色 allow `stealth-browser-mcp_*`
3. 用虚构 `network` 工具名冒充 MCP 面
4. 依赖用户手工在 `opencode.json` 配置该 MCP
5. 在 `WANXIANGSHU_TEST` 且无 fixture 时 spawn 真实 `uvx`

## AGENT-027：内部 Semble MCP 搜索

Semble 是进程内语义搜索：stdio MCP `search(query, repo, top_k)` → `Hit list`。它仍不是 Host MCP、provider tool、permission 或 Strength 能力。现行调用者只有 AGENT-032 `RepositoryWarmStart`；搜索结果始终是低可信 orientation data，不是 repository fact/evidence。

不是 Host MCP。不得写入 `config.mcp`。不得进入 AGENT-006 角色工具矩阵、permission schema、ToolRegistry、`js-*`、Strength Replica 工具面。

启动判定与 stealth 同形，环境前缀 `SEMBLE_MCP_*`：

```text
SEMBLE_MCP_DISABLED 为真            → Disabled，search 返回 []
SEMBLE_MCP_FIXTURE 非空             → command = node <fixture>
WANXIANGSHU_TEST 为真且无 fixture   → Disabled
否则 uvx --from "semble[mcp] @ git+https://github.com/MinishLab/semble.git@{ref}" semble
```

`ref` 默认 `main`。`SEMBLE_MCP_REF` 非空则覆盖。

禁止：

1. 注入 Host `config.mcp.semble` 或任何 role schema 键
2. 把 search 结果伪装成 provider-visible `read`（历史 injection 已废止）
3. Strength Replica 工具面加入 Semble
4. 在 `WANXIANGSHU_TEST` 且无 fixture 时 spawn 真实 `uvx`
5. 新增 `@modelcontextprotocol/sdk` 依赖（本条只约束 Semble；Sphinx 路径见 SPHINX-005 / AGENT-030）

## AGENT-031：显式 NEEDHELP 协作升级

Pair Hint 鼓励 managed Work agent 在当前视角不足、需要更强推理或独立视角时尽早在 reasoning 中发出精确 `[NEEDHELP]`。这是正常协作，不是 provider failure、资源匮乏、羞辱或失败声明；provider-visible guidance 不暴露 fast/deep 内部身份。

升级规则由触发该 `ProviderRunIdentity` 的 Host assistant message 上真实 `Agent` binding 决定；不得从 FallbackCursor、SelectedAgent 或可选 AttemptPlan 反推。这样 fast→deep continuation 后即使 fallback cursor 仍停在 fast，下一次 deep `[NEEDHELP]` 也会正确进入 consultation：

- `fast-ROLE` 命中：同一 Session、LogicalRun、AuthorityRoot、CanonicalRole、Persona 与 transcript 上，用对应 `deep-ROLE` 发送 typed `NeedHelpEscalation` continuation；只改变该 assistance continuation 的 ExecutionBinding，不写 FallbackCursor。
- `deep-ROLE` 命中：先 claim 当前 assistance abort，但 **AbortWake 不得立即创建 physical child**；必须等同一 aborted turn 由 fresh `SessionIdle` 触发 `IdleRevisit`，以该 transport fence 证明 OpenCode 已完成 parent-abort descendant sweep，再创建一个真实、独立的 consultation child。现行 managed catalog 已 clean-break 删除 legacy `meditator` 身份，因此 V1 以真实 `deep-inquiry` Work Session 承担 Meditator 职责，**不**复活 `meditator` alias。冻结求助时的 parent frontier，物化 canonical `LifecycleWorkRecord(includeOpening=true)` 作为 `CommissionerRecord`；child assignment 必须以 `如何解决这个 agent 的当前困难？` 开头。consultation 的普通完成由 assistance 路由消费；最后一条助手文本须已在 child XTrace parts（Recent work）中。`captureTerminal` 只写私有完成标记，不构成 LWR 段。随后物化 `LifecycleWorkRecord(includeOpening=false)`，作为 typed `NeedHelpAdvice` continuation 返回**原来的 deep binding**。

consultation 是真实 child Session，不是假 completion、不是 hidden prose injection；它不继承 owner Persona，不得递归 NEEDHELP。每个 LogicalRun 的 consultation 次数有限、owner single-flight；额度耗尽只给确定性 continuation，不向 provider 暴露数值或 budget 机制。取消/终结后迟到 advice 不得复活 owner。sentinel 自身在 XTrace capture 前从 reasoning evidence 中剥离，不写入 WorkRecord/Chronicle evidence。

## AGENT-032：Repository Warm Start

`RepositoryWarmStart` 是显式 keywords 驱动的低可信仓库定向能力。V1 直接消费者恰为 `Coder | Inspector | DevOps`；其它角色只能在既有 invocation DAG 上把 keywords 携带给这些角色，不能因此获得 repository snippets。Reviewer V1 拒绝任意 caller keywords；Orchestrator `commission` 不增加 keywords。

`keywords` 为可选多行文本：按 `SyntheticToml.normalizeNewlines` 统一换行，按 LF 分行、trim、删空、稳定 exact-dedupe（区分大小写），只取前 `MaxKeywords = 8`。每个保留行是一个完整 Semble query，不再按空格切词。无 keywords/全空白时必须零 Semble 工作且 provider prompt 与原 charge 字节完全相同。

所有独立 query 在一个 bounded parallel wave 中执行，`TopKPerKeyword = 4`；单 query failure、Semble disabled/timeout/launch failure 均 fail-open。merge 恢复 `keyword ordinal → local rank`，按 `FilePath + StartLine + EndLine + Content` 稳定去重，最多 `MaxHintsTotal = 24`。最终 warm-start 文档最多 `MaxWarmStartBytes = 64 KiB`；超限只删除完整 hint entry，绝不截断 TOML 字符串。

Provider envelope 由 canonical `SyntheticToml` writer 渲染：charge 是 instruction/assignment；caller keyword 与 `repository_search`/`repository_hint` 都是 data，并明确标注 hints 不是 instructions、不是 proof、不是合成的工具历史；是否采信由 callee 自行判断。不得伪造 read/grep/tool history，不得把 hits 直接写入 Casebook。repoPath 只用真实 `WorkspaceDirectory`；缺失时跳过，禁止猜 `"."`。显式 keywords 每次 fresh search；无自动从 charge 抽词、无 cross-call warm-start cache。

## AGENT-028：Persona Registry

```text
Role × initial selected tier → SessionPersona（创建时绑定，不可变）
```

| Role / 内部 office | Fast Persona | Deep Persona |
|--------------------|--------------|--------------|
| Orchestrator | Integrator | Director |
| Manager | Coordinator | Lead |
| Coder | Coder | Engineer |
| Inspector | Scout | Investigator |
| DevOps | Technician | Operator |
| Browser | Navigator | Researcher |
| Inquiry | Analyst | Inquirer |
| Reviewer | Examiner | Auditor |
| Blogger | Scribe | Chronicler |
| Distiller | Condenser | Distiller |
| Bookkeeper（内部，非 public Role） | Clerk | Curator |

`fast-*` / `deep-*` 仍是内部 execution identity，不穿过 provider horizon 自称。  
Steward 属未来预留，V1 不创建、不写入现行能力。

## AGENT-029：Role / Persona / ExecutionBinding 分离

```text
Role            职责 office；决定工具矩阵与 Role Law；session 内不变
Persona         自我模型（AGENT-028）；session 创建时一次绑定，不可变
ExecutionBinding 物理模型 / tier / config（如 fast-coder）；可随 Peer Fallback / Strength 变化
```

Fallback 或 Strength 只改 ExecutionBinding：Persona 不变，system prompt 身份字节不变。  
换执行者 ≠ 换人；不得把 Binding 名冒充 Persona 自称。

## AGENT-030：Host Sphinx MCP 自动注入

Inquiry 的认识求解面 = Host MCP `sphinx` 的工具面。不是插件 ToolRegistry 工具，不是虚构 `sphinx` 业务工具名。内核行为见 SPHINX-001..010。

Host-final `opencode.json` 必须由 config hook 注入：

```text
mcp.sphinx = {
  type = "local"
  command = node <packageRoot>/dist/Sphinx/McpServer.js
  enabled = true
}
```

测试 / 运维启动（环境前缀 `SPHINX_MCP_*`）：

```text
SPHINX_MCP_DISABLED 为真            → enabled = false，不 spawn
SPHINX_MCP_FIXTURE 非空             → command = node <fixture>，enabled = true
WANXIANGSHU_TEST 为真且无 fixture   → enabled = false
否则                                → command = node <packageRoot>/dist/Sphinx/McpServer.js，enabled = true
```

权限（AGENT-007 第一层；fast/deep 相同，AGENT-010）：

```text
CanonicalRole.Inquiry → allow  sphinx_*
其它 managed role     → deny   sphinx_*
```

域能力是 `ToolPermission.Sphinx`。Host schema 键是 `sphinx_*`。OpenCode 把该 MCP 的每个工具暴露为 `sphinx_<tool>`（服务器内名 `start` / `resume`）。

Sphinx MCP 与 stealth-browser MCP 同类：Host-native，不进 ToolRegistry，不进 `js-*` 投影。第二层 execution gate 不适用于本 MCP 面。

Sphinx 服务器实现允许 `@modelcontextprotocol/sdk`（及 zod）。AGENT-027 第 5 款仍禁止 Semble 路径引入该 SDK。

禁止：

1. 把 Sphinx MCP 编入 ToolRegistry / `js-*`
2. 给非 Inquiry 角色 allow `sphinx_*`
3. 依赖用户手工在 `opencode.json` 配置该 MCP
4. 在 `WANXIANGSHU_TEST` 且无 fixture 时 spawn 生产 `dist/Sphinx/McpServer.js`
5. 万象术内嵌 Sphinx Closure / EpistemicState / Canonical Answer 逻辑
