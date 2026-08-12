# Agent 与能力 — 可观察行为

条款前缀：`AGENT-`。  
权限写入点与双层边界见 `shape/agent.md`。

## AGENT-001：Canonical Role 与 Agent Tier

Canonical Role 决定工具权限与 system prompt。

```fsharp
type Role =
    | Orchestrator | Manager | Coder | Inspector | DevOps
    | Browser | Meditator | Reviewer
    | Blogger | Executor

type AgentTier = Fast | Deep
```

Tier **只**改变模型绑定。fast-ROLE 与 deep-ROLE 的 system prompt、工具权限、能力矩阵必须相同。

Canonical Role **不**决定 Companion 资格（COMPANION-001/002）。

## AGENT-002：必须存在的 20 个 Agent

```text
fast-orchestrator     deep-orchestrator
fast-manager          deep-manager
fast-coder            deep-coder
fast-inspector        deep-inspector
fast-devops           deep-devops
fast-browser          deep-browser
fast-meditator        deep-meditator
fast-reviewer         deep-reviewer
fast-blogger          deep-blogger
fast-executor         deep-executor
```

缺任一 → 启动失败。每个 Agent 必须有非空且 pair 内互异的 model 字符串。

G3 clean-break：`Role.Student` / `Role.Teacher` 与 `fast|deep-student|teacher` **已删除**；无 alias。
Mandatory baseline = 20（Casebook Bookkeeper pair 仍为条件性扩展，不计入本表）。
推理职责由 Meditator（AGENT-025）承接。

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
browser, meditator, reviewer, student, teacher, blogger, executor,
fast, deep, reviewer-fast, fast_reviewer
```

`student` / `teacher` 及 `fast|deep-student|teacher` 一律 fail closed；不得映射到 Meditator / Inspector。

## AGENT-005：用户必须显式选择

新的公开 Authority Root 必须携带准确 Agent（如 `fast-coder`）。  
省略、旧名或 build/plan → `HostContractUnsupported`。

## AGENT-006：能力矩阵

| 角色 | 工具 |
|------|------|
| Orchestrator | `fork-manager`, `join` |
| Manager | `fork-agent`, `join`, `list` |
| Coder | `read`, `write`, `edit`, `glob`, `grep`, `inspector`, `mv`, `rm`, `bash-honeypot` |
| Inspector | `read`, `glob`, `grep`, `executor` |
| DevOps | `fork-pty`, `executor`, `read`, `glob`, `grep`, `inspector`, `coder`, `join`, `list` |
| Browser | `read`, `glob`, `grep`, stealth-browser MCP（AGENT-026） |
| Meditator | `inspector` + Sphinx MCP（AGENT-025、AGENT-028） |
| Reviewer | `read`, `glob`, `grep`, `verdict` |
| Blogger | `blog` |
| Executor | 无工具 |

## AGENT-008：内部 Agent 不可见

Blogger、Executor 不得出现在任何模型可见的 enum、schema 或工具参数提示中。

## AGENT-009：示踪面可见集合

| 暴露面 | 可见 Agent |
|--------|-----------|
| Manager fork-agent | fast/deep coder, inspector, devops, browser, meditator |
| Orchestrator fork-manager | fast-manager, deep-manager |
| Inspector tool | fast-inspector, deep-inspector |
| Coder tool | fast-coder, deep-coder |
| list() | 当前运行中的 handle（非可创建清单） |

## AGENT-010：fast/deep 权限一致

```text
permissions(fast-ROLE) = permissions(deep-ROLE)
```

不得出现 fast 只读、deep 才可写。

## AGENT-011：Manager 无普通工具

Manager 只有 `fork-agent` / `join` / `list`。  
不能直接读文件、跑终端、改仓库。

## AGENT-012：Coder 的 Inspector 不透明

Coder 可见 `inspector` 工具，但 prompt 只能把它描述为不透明只读调查服务。  
不得泄露 Executor 权限，不得把 Inspector 当常规验证代理。

## AGENT-013：DevOps 独占 PTY

只有 DevOps 可创建/操作 PTY。  
文件修改只能经同步 `coder` 工具委派，不能直接 `write`/`edit`。  
DevOps 角色的 `join` 工具配置 10s 超时预算（`DevOpsJoinTimeoutMs = 10_000`），无完成项 10s 后返回 `ForkError.TimedOut` (`status="failed"`, `code="TIMED_OUT"`)，防止 PTY 进程 hang 死卡住控制流；Orchestrator 与 Manager 的 `join` 无 10s 超时。

## AGENT-014：Reviewer 只读

Reviewer = 只读工具 + `verdict`。不能写文件、不能跑命令。

## AGENT-015：Orchestrator 只 fork Manager

`fork-manager` 接受 `fast-manager` / `deep-manager`（新 Job）或已有 Manager job id（同 job 续做，同 worktree/session，`reused=true`；GLORY-068）。

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
后继：推理 → Meditator（AGENT-025）；证据 → SyncDelegate Inspector（AGENT-024）。

## AGENT-022：（空缺）Student SKILL — G3 已删除

**编号永久空缺。** StudentCompile / `.agent/skills/.../SKILL.md` 制品门已删除。无 successor skill 协议。

## AGENT-024：SyncDelegate DAG 与 InvocationMode

允许的同步委派边（dedicated `inspector` / `coder` 工具 → SyncDelegate，见 EXEC-026/028）：

```text
Meditator → Inspector
Coder     → Inspector
DevOps    → Inspector
DevOps    → Coder
```

图必须是 DAG。禁止反向或成环边（例如 `Inspector → Coder`、`Inspector → Meditator`、`Coder → DevOps`）。
嵌套 `DevOps → Coder → Inspector` 合法。启动/配置须静态证明 sync delegate 图无环。

`InvocationMode = SynchronousDelegate` 时，callee 在角色基线工具面之上增加 `return`：
`return` 是 AttemptExecutionProfile / InvocationMode 投影，**不是**业务程序计数器（PC）或新阶段；
只完成当前同步 invocation（Returned），dedicated Session 生命周期仍由 OwnerReuseScope 决定（HOST-008）。

本条只宣布 SyncDelegate DAG 与 `InvocationMode`。不得把 Teacher leaf / no-Companion 拓扑套到
Dedicated Inspector/Coder（后者是 Work + Attached，HOST-008）。

## AGENT-025：Meditator 能力（inspector + Sphinx MCP + epistemic style）

正式工具面（普通 Work Session；事实调查只经 SyncDelegate；认识求解经 Sphinx MCP）：

```text
Meditator → { inspector, sphinx MCP }
```

`sphinx MCP` = Host MCP `sphinx` 的工具面（AGENT-028 / SPHINX-003）。仍禁止 filesystem 直读。

禁止再持有：`read` / `glob` / `grep` / `write` / `edit` / `executor` / `coder` /
`fork-agent` / `fork-manager` / `fork-pty` / `join` / `list` / stealth-browser MCP，以及任何 filesystem 直读面。

职责：reason / question / compare / challenge / synthesize；经 Sphinx co-yield 推进认识状态。
分层固定为 `Meditator = reasoning`，`Inspector = evidence acquisition`
（AGENT-024 边 `Meditator → Inspector`）；Sphinx = 认识状态求解器（SPHINX-001），不是证据扫库。

Prompt discipline 吸收原 Student **epistemic style**（不是 workflow protocol）：

```text
先形成当前理解
主动寻找反例
把事实问题委派 Inspector，并针对回答继续追问
区分证据 / 推论 / 不确定性
在理解收敛前避免草率终止
```

不得重新引入：`LearningState` / QA / Compile / `MeditatorLearn|Compile` / Student 式 final `return`
作为 Meditator 业务阶段或 RequestKind。终端就是普通 Assistant completion。

Student/Teacher 角色已删除（AGENT-020 空缺）；不得 alias 回本角色。

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
5. 新增 `@modelcontextprotocol/sdk` 依赖（本条只约束 Semble；Sphinx 路径见 SPHINX-005 / AGENT-028）

## AGENT-028：Host Sphinx MCP 自动注入

Meditator 的认识求解面 = Host MCP `sphinx` 的工具面。不是插件 ToolRegistry 工具，不是虚构 `sphinx` 业务工具名。内核行为见 SPHINX-001..005。

Host-final `opencode.json` 必须由 config hook 注入：

```text
mcp.sphinx = {
  type = "local"
  command = node <packageRoot>/dist/sphinx/mcp-server.js
  enabled = true
}
```

测试 / 运维启动（环境前缀 `SPHINX_MCP_*`）：

```text
SPHINX_MCP_DISABLED 为真            → enabled = false，不 spawn
SPHINX_MCP_FIXTURE 非空             → command = node <fixture>，enabled = true
WANXIANGSHU_TEST 为真且无 fixture   → enabled = false
否则                                → command = node <packageRoot>/dist/sphinx/mcp-server.js，enabled = true
```

权限（AGENT-007 第一层；fast/deep 相同，AGENT-010）：

```text
CanonicalRole.Meditator → allow  sphinx_*
其它 managed role       → deny   sphinx_*
```

域能力是 `ToolPermission.Sphinx`。Host schema 键是 `sphinx_*`。OpenCode 把该 MCP 的每个工具暴露为 `sphinx_<tool>`（服务器内名 `start` / `resume`）。

Sphinx MCP 与 stealth-browser MCP 同类：Host-native，不进 ToolRegistry，不进 `js-*` 投影。第二层 execution gate 不适用于本 MCP 面。

Sphinx 服务器实现允许 `@modelcontextprotocol/sdk`（及 zod）。AGENT-027 第 5 款仍禁止 Semble 路径引入该 SDK。

禁止：

1. 把 Sphinx MCP 编入 ToolRegistry / `js-*`
2. 给非 Meditator 角色 allow `sphinx_*`
3. 依赖用户手工在 `opencode.json` 配置该 MCP
4. 在 `WANXIANGSHU_TEST` 且无 fixture 时 spawn 生产 `mcp-server.js`
5. 万象术内嵌 Sphinx Closure / EpistemicState / Canonical Answer 逻辑
