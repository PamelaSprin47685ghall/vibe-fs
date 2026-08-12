# Sphinx — 所有权与边界

行为：`what/sphinx.md`。Host 注入：AGENT-028。本页只规定 writer 与依赖方向。

## 模块边界

```text
src/sphinx/                 ← 正交产品：纯 JS 内核 + MCP stdio server
  Inquiry Kernel            ← EpistemicState / Closure / Policy / Canonical Answer
  MCP Server                ← start / resume + handle Map
Wanxiangshu Kernel          ← SphinxMcp identity / tool prefix only
Wanxiangshu Host            ← SphinxMcpConfig → config.mcp.sphinx
Roles / StaticTools         ← ToolPermission.Sphinx → Meditator allow sphinx_*
```

## 所有权表

| 面 | writer | 不可写 |
|----|--------|--------|
| EpistemicState / Closure / GenerativeRule / Stop / Canonical Answer | `src/sphinx` Inquiry Kernel | LLM、万象术 domain、Host |
| `Map<handle, EpistemicState>`（进程内） | Sphinx MCP server | 跨进程文件、Host Session、EventStore |
| MCP 工具名 `start` / `resume` 与 JSON 形 | Sphinx MCP server | ToolRegistry、`js-*` |
| 服务器名 / 工具前缀 `sphinx_` / launch 判定 | Wanxiangshu `SphinxMcp` identity + `SphinxMcpConfig` | env 直写 Host 对象、第二套 role→MCP 表 |
| Host schema `sphinx_*` allow/deny | `StaticTools.permissionObj` ← `ToolPermission.Sphinx` | `Roles.permissions` 字符串集塞 MCP 工具名 |
| structured observation → 状态转移 | Kernel（absorb + Closure） | LLM 自由文、Host adapter |

## 控制权不变量

1. continuation 唯一属于 Kernel；LLM 只回 structured observation。  
2. handle 是 inquiry 唯一钥匙；Host / Meditator 不得另造并行会话表。  
3. Canonical Answer 只由 Kernel 在 fixed point + Stop 最优时写出。  
4. 万象术只注入与锁权限；不得复制闭包算法。

## 禁止

1. 第二处注入 `config.mcp.sphinx`  
2. 把 MCP 工具名写进 `Roles.permissions` 字符串集；域能力只留 `ToolPermission.Sphinx`  
3. Sphinx import 万象术 domain，或万象术内嵌 Kernel 闭包  
4. 把 Sphinx 编入 ToolRegistry / `js-*`  
5. 用 A* / Bayes / MCTS / e-graph 模块冒充 Phase 0 内核本体
