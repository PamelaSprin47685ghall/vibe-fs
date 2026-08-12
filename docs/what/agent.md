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
| Orchestrator | `commission`, `join`, `horizon` |
| Manager | `fork`, `join`, `horizon`, `todowrite`, `fission`, `suicide` |
| Coder | `read`, `write`, `edit`, `glob`, `grep`, `inspect`, `mv`, `rm`, `bash-honeypot`, `js-coder` |
| Inspector | `read`, `glob`, `grep`, `query-shell`, `fetch`, `js-inspector` |
| DevOps | `read`, `glob`, `grep`, `js-devops`, `inspect`, `establish-behavior`, `repair-behavior`, `run`, `open-terminal`, `send-terminal`, `read-terminal`, `signal-terminal`, `horizon`, `join` |
| Browser | `read`, `glob`, `grep`, `js-browser`, stealth-browser MCP（AGENT-026） |
| Inquiry | `inspect` only（见 AGENT-025） |
| Reviewer | `read`, `glob`, `grep`, `judge`, `js-reviewer` |
| Blogger | `chronicle` |
| Distiller | 无工具 |
| Bookkeeper（内部） | `js-bookkeeper` |

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

## AGENT-010：fast/deep 权限一致

```text
permissions(fast-ROLE) = permissions(deep-ROLE)
```

不得出现 fast 只读、deep 才可写。

## AGENT-011：Manager 无普通工具

Manager 只有 `fork` / `join` / `horizon` / `todowrite` / `fission` / `suicide`。  
不能直接读文件、跑终端、改仓库，也不能 `inspect`。

## AGENT-012：Coder 的 Inspector 不透明

Coder 可见 `inspect` 工具，但 prompt 只能把它描述为不透明只读调查服务。  
不得泄露 Inspector 的 `query-shell` / 取证权限，不得把 Inspector 当常规验证代理。

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

## AGENT-025：Inquiry 能力（inspect-only + epistemic style）

正式工具面（普通 Work Session；事实调查只经 SyncDelegate）：

```text
Inquiry → { inspect }
```

禁止再持有：`read` / `glob` / `grep` / `write` / `edit` / `run` / `establish-behavior` /
`repair-behavior` / `fork` / `commission` / 终端动词 / `join` / `horizon` / stealth-browser MCP，以及任何 filesystem 直读面。

职责只有：reason / question / compare / challenge / synthesize。分层固定为
`Inquiry = reasoning`，`Inspector = evidence acquisition`（AGENT-024 边 `Inquiry → Inspector`）。

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

不得把 Sphinx Kernel / Steward 写成现行能力。  
Student/Teacher 角色已删除（AGENT-020 空缺）；`meditator` 不得 alias 回本角色。

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

Semble 是进程内语义搜索：stdio MCP `search(query, repo, top_k)` → `Hit list`。历史用于 Strength 投机注入假 `read`；当前无调用者。能力必须存在。

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
5. 新增 `@modelcontextprotocol/sdk` 依赖

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
