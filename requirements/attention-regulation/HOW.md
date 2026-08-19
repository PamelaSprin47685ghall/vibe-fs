# attention-regulation — HOW（非 normative）

## 目标实现形状

保持和现有 `AssumeTool` 同一风格：小 schema、重语义 description/return、零通用 framework。

```text
EnoughTool        string -> provider-visible reinforcement
AbandonTool       string -> provider-visible reinforcement
DeferTool         string -> append DeferredWork
DeferredProjection(participant) -> outstanding items
prepareResurface(participant, celebrationOccurrence) -> frozen batch candidate
```

推荐把 `enough` / `abandon` 做成薄 OpenCode tool adapter + provider resources；它们不需要 Domain store。`defer` 复用统一 EventStore：一个 accepted fact + 一个 celebrate-resurfaced fact 足够，不建 feature DB、不建 timer、不开后台 worker。`prepareResurface` 只 staging 当前 outstanding batch，不先写 drain；`institutional-learning` 成功时把本包 owner 的 `DeferredWorkResurfaced` facts 与 learning receipt / optional rule birth 放入同一 atomic commit，避免半提交。

## provider contract

- `enough`：sufficient for this decision；more evidence is not valuable merely because it exists；reopen only for named decision-changing evidence。
- `abandon`：previous self-commitment is retired from attention；does not cancel real obligations or authority。不要求 `reason` 第二字段。
- `defer`：recorded for later；not active work / not owed now；return to current line。
- 参数均保持单一自然语言字符串；禁止 confidence、priority、owner、deadline 等填表字段。

## 预计实现落点

- `src/Wanxiangshu/OpenCode/Tools/{EnoughTool,AbandonTool,DeferTool}.fs`
- attention owner 的最小 Deferred fact/fold/projection/surface；最终物理路径在 GAP/实现阶段按现有 owner tree 裁决。
- `resources/provider/tool/{enough,abandon,defer}/...`
- `institutional-learning` 的 celebrate workflow 只调用本包公开的 `prepareResurface` capability，不读内部 projection；真正 resurfacing facts 由其 atomic learning transaction 提交。

## DEPENDS ON

`attention-regulation → participant-identity, durable-events`

## 验证与测试落点

当前只落正式语义；可执行 proof 在用户 review 后写 GAP 时逐条建立。

| WHAT | 最低充分 proof |
|---|---|
| ATTENTION-REGULATION-001 | tool contract + return semantic test；证明无持久状态/无 authority side effect |
| ATTENTION-REGULATION-002 | tool contract + negative authority/obligation mutation test |
| ATTENTION-REGULATION-003 | pure/event semantic test：defer 后仍无 obligation/work execution |
| ATTENTION-REGULATION-004 | replay/idempotency + participant isolation + owner-life termination retires outstanding defer |
| ATTENTION-REGULATION-005 | temporal：celebrate 先学习后尾部 drain；同 occurrence replay 不重 drain |
| ATTENTION-REGULATION-006 | architecture negative：无 planner/stage/timer/background executor |

## 历史与弃权

- `assume` 已存在且继续归 `cognitive-environment`/现有 tool boundary；本包不复制其实现或语义所有权。
- `park` / `bound` / `unknown` / `qualify` 等候选不进入当前 accepted suite。
- WIP Gate/owner/scope 等结构性机制不借此包复活；本包只接受最终七件套中的三个 attention verbs。
