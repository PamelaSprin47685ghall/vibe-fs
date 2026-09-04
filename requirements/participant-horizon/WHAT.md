# participant-horizon — WHAT

## PARTICIPANT-HORIZON-001: 信息准入由 decision filter 决定

所有进入 provider-visible 视界的信息必须通过正向准入决策过滤器（Decision Filter）：
1. 参与者是否已知？已知则省略。
2. 是否为参与者自身刚提供的内容？是则省略。
3. 是否已被成功状态所蕴含？是则省略（回声不是有效观测）。
4. 是否仅用于内部关联或调试？是则留在机器侧。
5. 取值不同是否会改变下一步合法行动？否则省略。
6. 参与者是否需要数值本身而非其后果？若否仅渲染自然语言后果；若是仅保留最小物理观测。

## PARTICIPANT-HORIZON-002: 内部机器拓扑不穿过 horizon

任何底层机器拓扑标识（包括 `SessionId`、`AgentId`、`ManagerJobId`、`PtyId`、`FissionGroupId`、`lane_index`、`worktree` 路径、重试 offset、`fast-*`/`deep-*` 绑定自称以及内部 spool 路径）严禁出现在面向模型的提示词、工具参数或返回值中。

## PARTICIPANT-HORIZON-003: 通用状态 DTO 不投影，后果用自然语言

严禁向模型暴露 `status`、`code`、`message`、`count`、`ordinal`、`kind` 等通用 DTO 字段。超时、等待结束、中断及普通失败一律以自然语言后果表述（如 DevOps 等待预算耗尽渲染为自然语言说明，禁止返回 `TIMED_OUT` 或 `status="failed"`）。

## PARTICIPANT-HORIZON-004: 已知道/回声/成功蕴含/仅调试信息被省略

已为模型所知的信息、模型自身输入的重复回传、成功完成所隐含的事实以及仅用于内部追踪的元数据，一律从返回视界中剔除。工具成功返回不得机械重复输入内容作为观测。

## PARTICIPANT-HORIZON-005: 需要原始测量时只给必要 observation

当参与者确实需要物理指标以决定后续行动时（如进程退出的 `exit_code`、非空 stdout/stderr 输出），仅提供最小原始测量事实，不附加 Host 的主观判定或状态标签。

## PARTICIPANT-HORIZON-006: 内部状态优先转成行动相关后果

机器内部状态（任务槽位、缓冲区、重试轮次）在进入视界前必须转化为「该状态对当前工作意味着什么、下一步应采取什么行动」的语义后果与工作记录（WorkRecord）。

## PARTICIPANT-HORIZON-007: 内部参与者不进入 provider-visible surface

Blogger、Distiller、Bookkeeper 等内部辅助角色严禁出现在模型可见的 enum、Schema、fork 候选或参数说明中。底层的批处理任务切片与内部 session 标识严禁进入工具面。

## PARTICIPANT-HORIZON-008: 隐藏 review 编排不进 Manager horizon

面向 Manager 的所有固定界面（system prompt、continuation、schema、错误提示、tool description 及 result）严禁暴露第二评审身份、专用评审会话、确认屏障或双重检查机制。
assessment 结论只以原子物化的质量义务与工作权形式进入账本（relay-assessment ASSESS-004），不携带编排者身份。

## PARTICIPANT-HORIZON-009: 隐藏 target 只返回 generic unavailable

当模型尝试访问或调度不可见的目标（如 Distiller 等内部角色）时，系统仅返回通用的不可用拒绝响应，禁止在拒绝文案中提及该目标的存在或说明其为内部专有。

## PARTICIPANT-HORIZON-010: fork/commission 可见集合

- Manager `fork` 仅可见：coder、inspector、devops、browser、inquiry（各一个本名版本）。
- Orchestrator `commission` 仅可见：manager。
- `horizon()` 仅返回在场名册的 Byname 或 TerminalName，不暴露底层 id。
- Blogger、Distiller、Bookkeeper 等内部角色严禁出现在可 fork 集合中。

## PARTICIPANT-HORIZON-011: `horizon()` 是 pull-only snapshot

`horizon()` 是按需主动拉取的快照接口，禁止轮询、后台推送或 watcher 订阅。其返回当前在场各可见子智能体的最新工作记录；若记录暂不可读则直接说明，不得以陈旧数据伪装最新状态。

父级可见 child 一旦已经 durable 建立，就不得在其最终后果尚未交付给父级前从 horizon 消失。尤其 `Abandoned` 是“该 child 没有回来”的可行动后果：在 Join 将这项后果消费并把 handle 退休之前，`horizon()` 必须继续按 Byname 展示该 child 并明确说明其未返回。只有已 `Retired` 的 handle 才可从 roster 移除；不得把 `listable/outstanding` 等终结门禁视图误用成 horizon roster。

## PARTICIPANT-HORIZON-012: warm-start hints 只向有 repository 证据 authority 的角色准入

仓库热启动线索（WarmStart hints）仅向有权直接接触仓库证据的角色（Coder、Inspector、DevOps）准入。其余角色仅可沿调用链传递关键词，不得接收仓库代码片段。

## PARTICIPANT-HORIZON-013: hints 是 data，不是 instruction/proof/history

进入模型视界的热启动线索必须明确标记为低置信度的参考数据（orientation data），绝非指令、证明或合成的工具历史，严禁伪造文件读取或搜索历史。

## PARTICIPANT-HORIZON-014: 虚假 affordance / 不可达路径不穿越

视界中严禁展示指向已不存在实体的路径或标识，工具与动作名称仅表达真实的语义动作，不将无法执行的内部机器状态伪装成可选动作。
