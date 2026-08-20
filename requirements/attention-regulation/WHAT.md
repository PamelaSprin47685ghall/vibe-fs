# attention-regulation — WHAT

## ATTENTION-REGULATION-001: enough 是决策调查的主动吸收态

`enough(decision)` 接受非空自然语言，表示 participant 判定当前已有信息已充分支撑该决策的下一步行动。调用成功后，系统明确要求停止为追求更多边缘证据而重复搜寻或重开同一判断。只有出现新的、此前未消费且足以改变决策路径的事实时，方可重新发起调查。

`enough` 不证明决策正确性、不生成权威事实、不建立持久认知状态机，无新事实时的重复调用不产生语义增量。

## ATTENTION-REGULATION-002: abandon 是无约束 decommit，不是真实义务取消

`abandon(commitment)` 接受非空自然语言，表示 participant 主动放弃自我生成的计划、推论方向或心理假设，使其不再因前文历史而在后续推理中自动获得注意力。

`abandon` 属于纯粹的认知解脱动作，不要求审批或理由检验。`abandon` 严禁用于取消 `obligation-ledger` 中的真实任务义务、撤销用户授权、删除仓库工作产物或终止会话生命周期。

## ATTENTION-REGULATION-003: defer 表示延后处理，非当前欠账且非自动执行

`defer(new_work)` 接受非空自然语言，将新发现且非阻塞的工作登记为 DeferredWork，使其移出当前工作记忆，使 participant 得以立即聚焦当前主线。

DeferredWork 既不是活动义务，也不是后台作业或授权。`defer` 不触发自动委派或后台执行，系统与参与者均不得将 DeferredWork 视为当前已欠付的任务债务。

## ATTENTION-REGULATION-004: DeferredWork 按 participant life 隔离与重放

每个被接受的 DeferredWork 具备内部稳定的 occurrence 标识，严格归属于特定的 participant life。在系统重启或重放时不得丢失、不得跨参与者泄漏，且同一 tool occurrence 重放不得产生重复条目。

若 participant life 在触发 resurface 前终止，未处理的 DeferredWork 随生命周期自然结束，不得跨生命周期继承或自动转为 durable mission debt。

## ATTENTION-REGULATION-005: 仅在 celebrate 尾部统一 resurface 且不自动激活

`institutional-learning` 的一次成功 `celebrate` 在完成经验沉淀后，必须将该 participant 尚未消费的 DeferredWork 于结果尾部统一返回，并标记为已露出；同一 celebration occurrence 重放时保持结果幂等。

重新浮现的条目不自动转变为活动义务。模型可自主决定当场处理、再次 `defer`、写入 formal obligation 或执行 `abandon`。

## ATTENTION-REGULATION-006: 不引入工作流引擎与状态机

本包严禁引入阶段流转（stage）、优先级（priority）、截止期（deadline）、依赖图谱（dependency graph）、自动恢复（auto-resume）、后台执行器或通用认知状态机。持久化状态仅限于 DeferredWork 的最小追加投影及 celebration 消费凭据。
