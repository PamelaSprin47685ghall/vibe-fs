# 权限内核草稿

目标：把 Opencode 和 Mux 的工具权限统一成一个核心真值函数：

```fsharp
Agent -> Tool -> bool
```

宿主只做两件事：

- Opencode：运行时按 `canUse agent tool` 擦除不可用工具。
- Mux：创建 agent 时按 `canUse agent tool` 过滤当前工具集。

原则：不 normalize 宿主工具名，不为 `build` 单独建分支；任何未知 agent 与 `build` 行为一致，等价于 `manager + coder + reader`。

```fsharp
module VibeFs.Kernel.ToolPolicy

type Agent = string
type Tool = string

let private knownAgents =
    [ "manager"; "reader"; "coder"; "reviewer"; "browser"; "meditator"; "executor" ]

let canUse (agent: Agent) (tool: Tool) : bool =
    let toolHas subs = subs |> List.exists tool.Contains
    match agent with
    | _ when toolHas ["agent_report"] -> true
    | _ when toolHas ["bash"; "task"] || tool = "grep" -> false
    | _ when toolHas ["stealth"] -> agent = "browser"
    | _ when toolHas ["return"] -> toolHas [agent]
    | "meditator" | "executor" -> false
    | _ when toolHas ["read"] -> true
    | "reviewer" | "browser" -> false
    | "reader" when toolHas ["executor"] -> true
    | _ when toolHas knownAgents || toolHas ["todo"; "question"; "web"] -> agent <> "reader" && agent <> "coder"
    | _ when toolHas ["write"; "edit"; "patch"] -> agent <> "reader" && agent <> "manager"
    | "manager" -> tool <> "fuzzy-grep"
    | _ -> true
```

某些工具改了名，但宿主端的工具保留真实名字：`todo_write` / `todowrite`、`apply_patch`、`web_search` / `web_fetch`、`stealth_browser_mcp_*`。

现状是 orchestrator/editor/greper/reviewer/browser/reverie/summarizer，改成 manager/coder/reader/reviewer/browser/meditator/executor，一一对应

submit_review_result 改名为 return-reviewer

/plan 整个功能都删除，不用管权限了

fuzzy_* 改名为 fuzzy-*

## 有意设计变更

以下与 `OPENCODE.md` / `MUX.md` 现状不一致，均属于预期的破坏式设计变更，不作为兼容缺陷处理：

- 角色体系从 `orchestrator/editor/greper/reviewer/browser/reverie/summarizer` 改为 `manager/coder/reader/reviewer/browser/meditator/executor`。
- `build` 和未知 agent 不再按宿主旧语义处理，统一视为 `manager + coder + reader` 的能力集合。
- 权限来源收敛为唯一真值函数 `canUse agent tool`，Opencode 和 Mux 只负责按宿主真实工具名过滤。
- 不做宿主工具名 normalize，宿主继续保留 `todo_write` / `todowrite`、`apply_patch`、`web_search` / `web_fetch`、`stealth_browser_mcp_*` 等真实名字。
- `/plan` 功能整体删除，相关 plan tools、slash command、wrapper、权限限制和文档语义都随之移除。
- `submit_review_result` 改名为 `return-reviewer`。
- `fuzzy_*` 改名为 `fuzzy-*`；`grep` 只拒绝精确工具名 `grep`，不再误伤 fuzzy grep 或 greper 类工具。
- `agent_report` 作为报告返回通道对所有 agent 放行。
- `meditator` 与 `executor` 默认无工具能力；若需要读取上下文，应由调用方显式注入内容。
- `glob` 不再继承 Orchestrator 旧权限矛盾，是否可用完全由 `canUse` 的统一规则决定。
