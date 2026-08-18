# WHAT —— 唯一 normative 合同

命题前缀 `PARTICIPANT-HORIZON-`。每条都是**当前世界必须同时成立**的事实。
证据指针 → [`HOW.md`](HOW.md)。

## 准入法则（positive admission law）

### PARTICIPANT-HORIZON-001：信息准入由 decision filter 决定

每个 provider-visible 信息必须能通过 ARCH-014 decision filter，六问按序：

```text
Did the participant already know this?      → omit
Did they just supply this themselves?       → omit
Is it implied by successful completion?     → omit
Is it useful only for correlation/debug?    → keep internal
Would different values change next action?  → if no → omit
Does the participant need the value itself
  rather than merely its consequence?       → if no → render consequence
                                            → if yes → preserve minimal observation
```

- 含义：准入是正向律，不是黑名单补丁。新信息进 horizon 必须因「会改变合法行动」，
  不因「还没有人禁止过」。
- 边界：这条是**本包**的核心命题；filter 的具体渲染机制归 `provider-projection`。

### PARTICIPANT-HORIZON-002：内部机器拓扑不穿过 horizon

下列机器拓扑**不得**出现在 provider 输出或工具后果中（Host/Journal 墙内可保留精度）：

```text
SessionId / AgentId / ManagerJobId / PtyId / FissionGroupId
lane_index / worktree path / fallback offset / fast-|deep- binding 自称 / spool path
```

- 出处：EXEC-030、ARCH-014、ARCH-016 Gate B。
- 边界：`SessionId` 等身份**类型**在代码里存在且必要；禁令只约束 provider-visible surface。

### PARTICIPANT-HORIZON-003：通用状态 DTO 不投影，后果用自然语言

禁止把 `status / code / message / count / ordinal / kind` 等通用 DTO 字段投给 provider。
中断、超时、等待结束一律以自然语言后果表达（EXEC-004/005/017、AGENT-013）。

- 例子：DevOps `join` 10s 预算耗尽 → 自然语言「本次等待结束」；禁止 `TIMED_OUT` / `status="failed"` / `code=...`。
- 边界：语义驱动的精确观测字段（terminal/`run` 的 `exit_code`、非空 stdout/stderr）是合法例外，见 005。

### PARTICIPANT-HORIZON-004：已知道/回声/成功蕴含/仅调试信息被省略

- 已知道（already-known）→ 省略；
- participant 刚自己提供（just supplied）→ 省略；
- 成功完成已蕴含（implied by success）→ 省略；工具 result 不得重述工具 success 已证明的事实
  （`An echo is not an observation.`，ARCH-014）；
- 仅用于关联/调试（correlation/debug）→ 留在机器侧。

### PARTICIPANT-HORIZON-005：需要原始测量时只给必要 observation

当 participant 需要值本身而不是后果时，给最小 observation（例如 `exit_code`、非空 stdout/stderr），
不给 Host 对它的 judgement（`Give the participant the measurement, not the Host's judgment of it.`）。

- 边界：测量字段必须是语义驱动的；`status` 泛型字段不因「也是数字」而合法。

### PARTICIPANT-HORIZON-006：内部状态优先转成行动相关后果

机器态可以存在；穿过 horizon 的只能是**后果与 WorkRecord**（EXEC-030）。
内部状态（lane、offset、spool、job id）应先转成「这改变了什么、我下一步该做什么」再决定是否/如何呈现。

## 隐藏面（hidden surface）

### PARTICIPANT-HORIZON-007：内部参与者不进入 provider-visible surface

Blogger、Distiller、Bookkeeper（含 `fast-bookkeeper` / `deep-bookkeeper`）不得出现在任何
模型可见的 enum、schema 或工具参数提示中（AGENT-008）。机器 Assignment（map/reduce/chunk/session id）
不进 provider 工具面（EXEC-014）。

### PARTICIPANT-HORIZON-008：隐藏 review 编排不进 Manager horizon

Manager 固定 surface（system prompt、continuation、schema、固定错误、tool description/result）**禁止**
出现 reviewer 身份 / reviewer session / barrier / witness / 2N / Finality cohort / confirmation 机制
（GLORY-002/030、SURFACE-005、REVIEW-015、PROMPT-013、HOST-018、TODO-013）。

窄例外仅一条：Todo Checkpoint 过程评审的 **outcome（PERFECT/REVISE）与 concrete report
（canonical ProcessReviewLWR）**（GLORY-030 / TODO-013）。该例外不得扩大为暴露执行评审的隐藏角色。

### PARTICIPANT-HORIZON-009：隐藏 target 只返回 generic unavailable

对不可见 target（如 reviewer）的拒绝必须通用，不得以拒绝文案证明其存在（GLORY-032）。

## 可见集合

### PARTICIPANT-HORIZON-010：fork/commission 可见集合

| 暴露面 | 可见集合 |
|---|---|
| Manager `fork` | fast/deep coder, inspector, devops, browser, inquiry |
| Orchestrator `commission` | fast-manager, deep-manager |
| `inspect` / `establish-behavior` / `repair-behavior` | 各自 office 对 |
| `horizon()` | 在场名册（Byname / TerminalName），无 id |

不可 fork：reviewer、blogger、distiller、bookkeeper（AGENT-009）。

### PARTICIPANT-HORIZON-011：`horizon()` 是 pull-only snapshot

- 调用者需要朝向时主动看一次；禁止 timer 轮询、后台订阅、watcher、自动刷新；
- 返回在场名册（名字，无 id）+ 每个可见 subagent 最新一条 durable 工作记录；无记录则自然语言说明；
- 最新 frame 不可读时不得退回更旧 frame 冒充「最新」，自然语言说明当前不可读（EXEC-005）；
- 无 `status / id / kind / ordinal` 状态机词汇。

## 行动相关事实

### PARTICIPANT-HORIZON-012：warm-start hints 只向有 repository 证据 authority 的角色准入

RepositoryWarmStart 的直接消费者恰为 `Coder | Inspector | DevOps`（其 authority 已允许直接生活在
repository evidence 中）。其它角色只能沿既有 invocation DAG 携带 keywords，不能因此获得 repository
snippets（AGENT-032 / repository-warm-start §3-§4）。

### PARTICIPANT-HORIZON-013：hints 是 data，不是 instruction/proof/history

进入 horizon 的 warm-start 材料必须明确标注为低可信 orientation data：不是 instructions、不是 proof、
不是合成的工具历史；不伪造 read/grep/tool history（AGENT-032 / repository-warm-start §8/§17）。
无 keywords 时 provider prompt 与原 charge 字节完全相同。

- 边界：hint 的搜索/命中语义归 `knowledge-reuse`；本包只拥有「什么材料、以什么身份准入」。

### PARTICIPANT-HORIZON-014：虚假 affordance / 不可达路径不穿越

- 不显示指向已不存在事物的路径（ARCH-014：`Never show a path to something that no longer exists.`）；
- 名字表达 semantic act，不把不可达的机器身份伪装成可行动作（与 `action-affordance` 的 005/006 呼应）。

## 反向覆盖

本包吸收的 OWNED clause（COVERAGE.md 归属）：PROMPT-013（可见/禁止 surface 部分）、AGENT-008、
AGENT-009（可见集合部分）、AGENT-013（DTO 部分）、AGENT-015（机器字段部分）、EXEC-004/005/014/030、
REVIEW-015、TODO-013（hidden surface）、GLORY-002/030/031/032/048 + SURFACE-005（Manager 面）、
HOST-018（description 禁泄露隐藏编排部分）、ARCH-014、ARCH-016 Gate B。
