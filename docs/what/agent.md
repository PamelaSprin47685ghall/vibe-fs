# Agent 与能力 — 可观察行为

条款前缀：`AGENT-`。  
权限写入点与双层边界见 `shape/agent.md`。

## AGENT-001：Canonical Role 与 Agent Tier

Canonical Role 决定工具权限与 system prompt。

```fsharp
type Role =
    | Orchestrator | Manager | Coder | Inspector | DevOps
    | Browser | Meditator | Reviewer | Student | Teacher
    | Blogger | Executor

type AgentTier = Fast | Deep
```

Tier **只**改变模型绑定。fast-ROLE 与 deep-ROLE 的 system prompt、工具权限、能力矩阵必须相同。

Canonical Role **不**决定 Companion 资格（COMPANION-001/002）。

## AGENT-002：必须存在的 24 个 Agent

```text
fast-orchestrator     deep-orchestrator
fast-manager          deep-manager
fast-coder            deep-coder
fast-inspector        deep-inspector
fast-devops           deep-devops
fast-browser          deep-browser
fast-meditator        deep-meditator
fast-reviewer         deep-reviewer
fast-student          deep-student
fast-teacher          deep-teacher
fast-blogger          deep-blogger
fast-executor         deep-executor
```

缺任一 → 启动失败。每个 Agent 必须有非空且 pair 内互异的 model 字符串。

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
browser, meditator, reviewer, blogger, executor, fast, deep,
reviewer-fast, fast_reviewer
```

## AGENT-005：用户必须显式选择

新的公开 Authority Root 必须携带准确 Agent（如 `fast-coder`）。  
省略、旧名或 build/plan → `HostContractUnsupported`。

## AGENT-006：能力矩阵

| 角色 | 工具 |
|------|------|
| Orchestrator | `fork-manager`, `join` |
| Manager | `fork-agent`, `join`, `list` |
| Coder | `read`, `write`, `edit`, `glob`, `grep`, `inspector`, `mv`, `rm` |
| Inspector | `read`, `glob`, `grep`, `executor` |
| DevOps | `fork-pty`, `executor`, `read`, `glob`, `grep`, `inspector`, `coder`, `join`, `list` |
| Browser | `read`, `glob`, `grep`, network tools |
| Meditator | `read`, `glob`, `grep`, `inspector` |
| Reviewer | `read`, `glob`, `grep`, `verdict` |
| Student | 由 AGENT-020 的 request kind 决定 |
| Teacher | 普通执行工具全集 + `return`；不含 `fork-agent` / `fork-manager` / `join` / `list` / `fork-pty` |
| Blogger | `blog` |
| Executor | 无工具 |

## AGENT-008：内部 Agent 不可见

Blogger、Executor、Teacher 不得出现在任何模型可见的 enum、schema 或工具参数提示中。

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

## AGENT-020：Student / Teacher

`fast-student` / `deep-student` 是公开、只能由 HumanRoot 显式选择的主动学习 Agent；不得做
意图识别、自动路由或从其它角色自动升级。`fast-teacher` / `deep-teacher` 是内部 Agent，只能由
Student 的 `teacher` 工具创建或恢复。

Student 与 Teacher 的 tier 固定相同；两者都由 Agent 配置解析 model，发送时始终
`Agent = Some effectiveAgent`、`Model = None`。Teacher 是叶子 Satellite：无 Companion，不进入
fork/list/join catalog，不创建新的 Satellite。

Student 工具面由同一 `AttemptExecutionProfile.RequestKind` 原子决定：

```text
StudentLearn   → { teacher }
StudentCompile → { read, glob, grep, write, edit, return }
```

学习面不得出现文件、执行、委派或最终 `return`；编译面不得出现 `teacher`、委派、PTY 或网络工具。
Teacher 的 `return` 只把自由文本交还等待中的 `teacher` 工具，普通正文、reasoning、idle 或工具流
都不是回答。Student 的最终 `return` 只在编译面可执行。

## AGENT-022：Student SKILL 可加载制品

StudentCompile 的写入目标只能是精确形态 `.agent/skills/<skill-name>/SKILL.md`；不得把 `.md` 平铺在
`skills` 目录，也不得写绝对路径、穿越路径、额外嵌套或其它文件。每个 `SKILL.md` 必须是 UTF-8，
以 `---` 包围的 YAML frontmatter 开头，其中包含非空 `name` 与 `description`，且 `name` 与目录名完全
相同；frontmatter 后必须有非空 Markdown 正文。

write/edit 在副作用前校验目标形态；Student 最终 `return` 前重新读取并校验本次触达的全部 SKILL，
且至少触达一个。任一文件缺失、不可解码或不满足上述契约时不得删除 QA、不得进入最终 completion。
新 SKILL 只保证供新的 OpenCode 进程/会话发现；最终说明必须提醒用户重启 OpenCode 后加载。
