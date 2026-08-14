# WHY — 为什么 repository-investigation 必须独立存在

## 不可替代的存在理由

> repository claim（「这个文件存在」「这个符号在这里被定义」「这个配置是这样」）必须由对**本地已存在世界**的真实观察建立。推理、semantic search hint、旧 Case、外部 web 都不能自动冒充当前 repository evidence——否则「以为的事实」会以「观察到的事实」的身份进入下游判断（`17-repository.md`）。

Inspector 的存在理由正是一句话（`resources/provider/tool/inspect/description/en.md`）：

```text
Ask an Inspector to establish facts that already exist in the repository.
The Inspector is read-only in the causal sense.
```

## 为什么不能并入其它包

- 不是 `knowledge-reuse`：reuse 消费**已建立**的知识做 freshness hint；本包保证「当前 claim 由当前观察建立」。investigation RED = 推理/缓存/hint 冒充 fact；reuse RED = 旧 Q/A 被当当前事实。warm-start 管线横跨两者：hint 的低信任定位是本包，hint 能否被 cache 复用是 reuse。
- 不是 `external-investigation`：Browser 的外部/public-web facts 有不同 source/authority boundary（`17-repository.md` 拆分裁决：不能因为两者都「查资料」并入一个包）。
- 不是 `office-capability`/`capability-enforcement`：谁能取证（Inquiry 只能 inspect/sphinx、Inspector 只有只读权限集）是 office/gate 的事；本包拥有「取证得到的 observation 如何成为 evidence」的证据合同。
- 不是 `repository-programming`/`process-execution`：本包不拥有 repository 变换与进程执行；观察只读是它的边界，不是它的工具。

独立 change 测试：替换具体文件查询工具 / semantic search orientation（换掉 Semble、换掉 query-shell），而 evidence contract 不变——本包命题全部成立（`17-repository.md` INDEPENDENT CHANGE）。

## RED 是什么样（失败模式）

```text
RED = 推理、旧缓存、搜索 hint 或修改后观察被当成原 repository fact
    ∨ 为了观察改变 repository / 运行应用制造新行为
    ∨ provider 无法定位证据（无 provenance）
    ∨ 搜索命中被写成「Semble 确认 X 不存在」
```

具体可观察形态：

| 形态 | 违反 |
|---|---|
| warm-start hint 被渲染成 instructions / proof / 工具历史 | 001/006 |
| 无 keywords 时 provider prompt 与 charge 字节不同（被悄悄加了东西） | 007 |
| 非 Coder/Inspector/DevOps 角色收到 repository snippets | 008 |
| 搜索零命中时 provider 被告知「Semble 确认 X 不存在」 | 006（absence 不是 absence evidence） |
| Inspector 为了回答问题而 build/test/运行应用 | 005 |
| 用旧 Case 的 A 直接回答「当前 repo 是什么样」而不重放 | 001（交叉 knowledge-reuse） |
| 搜索串行 await（非并行 wave）导致时序影响结果 | 009 |

## 历史背景（为什么这些命题不是纸上谈兵）

- **`changes/completed/repository-warm-start.md`**：warm-start 是显式 keywords 驱动的低可信仓库定向能力。核心裁决：hints 不是 instructions、不是 proof、不是合成的工具历史；`charge` 是 assignment/authority，`keywords` 是 optional discovery hints——两者 authority 不同（§8）。fail-open 是正确性依赖的反面：Semble disabled/timeout/launch failure/单 query failure 都不能让工作 invocation 失败（§16）。「Absence of hints is not evidence of absence」：provider 措辞永远不能说「Semble confirmed X does not exist」，只能说「no warm-start hints were obtained for this query」（§17）。
- **`changes/completed/perm-inspector.md`**：Casebook 的 replay 机制建立在这个合同上——observation 是 typed 的、可重放的；fetch 的重放结果只是 freshness hint，不是正确性证明。
- **AGENT-027**：Semble 是进程内 stdio MCP 语义搜索，不是 Host MCP、不是 provider tool、不是 permission、不是 Strength 能力——它的输出永远不能伪装成 `read` 或工具历史（`docs/why/agent.md` 拒 Host mcp：语义搜索会漏进所有角色 schema；拒 Strength 注入：假 read 污染 primary 可见历史）。

## 历史拒绝方案（被拒 ≠ 永久命题，记录 WHY）

| 被拒方案 | 拒绝理由 | 现行命题 |
|---|---|---|
| 把 Semble 注册成 Host MCP / provider tool / ToolPermission | 语义搜索漏进所有角色 schema；结果成为可调用能力而非 orientation | 001/006 |
| 自动从 charge 抽词 / tokenizer / cross-call cache | 把优化变成第二 assignment/memory；无证据的「猜词」是推理冒充调查 | 007 |
| warm-start 注入 provider-visible `read`（假工具历史） | 假 read 污染 primary 可见历史，replay 无法区分真假 | 006 |
| 搜索零命中 → 告知「确认不存在」 | 零命中可能是 disabled/timeout/截断/index 行为，不是 absence | 006 |
| 给非直接消费者角色发 repository snippets | 只有本来就允许直接生活在 repository evidence 中的 Coder/Inspector/DevOps 可看 | 008 |
| 猜 `repoPath = "."` | 错误的 repository hint 比没有 hint 更糟 | 008 |
