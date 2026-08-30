# intra-participant-parallelism — WHAT

## INTRA-PARTICIPANT-PARALLELISM-001: 一人多 present，不增加 participant

Fission 仅增加同一 logical participant 的并发 execution presents。所有 lanes 共享同一 logical identity、CanonicalRole、authority/responsibility owner、逻辑父子关系与外部子任务集合；不得因物理 lane 会话数量的增加而向外部暴露新的 AgentId、handle 或增加父级的 join 义务。

## INTRA-PARTICIPANT-PARALLELISM-002: canonical lane array

`fission(prompts: String Array)` 中每个数组元素对应一条独立的 lane；元素数量必须 N≥2，且每个元素必须包含非空白字符。每个 lane 的 prompt 字符串必须逐字节完整保留，包含其内部的换行符与格式，严禁二次拆分、格式修剪或静默丢弃空元素。

## INTRA-PARTICIPANT-PARALLELISM-003: fresh sibling replacement transport

Fission 的调用者必须是具备物理 Host parent 的 subsession，根会话（root session）严禁发起裂变。每条 lane 使用独立的 fresh Host session，其物理 parentID 必须等于原 caller 的 parentID。lane 的初始任务由原 caller 当时的 canonical Lifecycle Work Record 与该 lane 的输入构成；lane 会话不成为新的独立委托主体。

## INTRA-PARTICIPANT-PARALLELISM-004: all-or-none admission

Fission 准入必须原子地建立全部 N 条 lanes。任一 lane 的创建、绑定或初始化发送失败时，必须全量回滚所有已建立的 lanes，原 caller 保持正常执行，严禁部分生效或缩减 lane 数量。

## INTRA-PARTICIPANT-PARALLELISM-005: old caller silent interrupt

仅在全部 lanes 成功建立并准入后，原 caller 才发生 Fission 专有的静默中断（silent interrupt）。该中断不发布业务级的 `Aborted` completion，不触发故障恢复流程，不取消已有子任务，仅安全退休被 lanes 替代的旧物理执行实体。

## INTRA-PARTICIPANT-PARALLELISM-006: pre-fission outstanding completion 广播

裂变准入前已处于未决状态的子任务与进程属于 logical owner 的共享既有债权。其每一个完成结果必须以确定性载荷向每一条 Fission lane 进行 exactly-once 广播投递，不得产生重复的 WorkRecord 或多次逻辑完成。

## INTRA-PARTICIPANT-PARALLELISM-007: post-fission completion lane affinity

裂变准入后由特定 lane 新发起的子任务或进程完成项，严格绑定至发起该任务的 lane。其他 lanes 不得消费该任务的完成结果；针对既有子会话的追加提示（nudge）不改变原有亲和归属。

## INTRA-PARTICIPANT-PARALLELISM-008: keyed work convergence

每条 lane 的工作记录在 group 中以 lane 索引为唯一 key。相同 key 且内容一致的合并操作具有幂等性，相同 key 但内容冲突时必须 fail-closed。最终产物由 keyed union 决定，严禁依赖到达时序或字符串无序拼接。

## INTRA-PARTICIPANT-PARALLELISM-009: single logical completion

仅在所有 lane 的自有工作记录、裂变前广播债权与各 lane 亲和任务均完成结算后，group 方可收敛。一个 Fission group 最终仅向逻辑父级交付一次普通的 terminal completion，并将结果写回原 logical participant 的 completion cell。

收敛后的最终接管属于同一个 logical lane 生命周期，而不是某一条固定的 physical user message。最终接管期间若发生 nudge、provider recovery/AABB 或 degeneration-guard interruption，其后继 continuation 仍由该接管 lane 拥有；只有后继链最终产生普通 `TurnCompleted` 后才允许写入 `FissionConverged` 并发布 logical completion。

## INTRA-PARTICIPANT-PARALLELISM-010: durable replay，不猜 lane

裂变产生的 group 标识、lane 成员关系、替换关系与收敛终结均依赖不可变的 durable facts 进行审计与重放；严禁通过扫描外部相似会话推测并发实体。进程中断后未完成的裂变作为中断事实记录，不进行自动隐式恢复。

## INTRA-PARTICIPANT-PARALLELISM-011: V1 单 active group

一个 logical participant 同一时刻最多允许存在一个处于活跃状态的 Fission group。活跃 lane 再次调用裂变必须 fail-closed 为 already-fissioned，不支持递归裂变。

## INTRA-PARTICIPANT-PARALLELISM-012: eligibility 单一 consequence source

Fission 的角色权能准入必须从 office consequence 的单一源头投影至模型可见 schema 与运行时门禁，相同 office 的不同档位权限严格一致。仅具备裂变权能的角色（如 Manager、Coder、Inspector、Browser、Inquiry）可调用裂变。

## INTRA-PARTICIPANT-PARALLELISM-013: subsession-only origin

裂变的调用源校验与角色权能正交：调用方必须证明自身为物理 subsession。直面用户的根会话在任何资源预留、记录物化或中断前必须直接 fail-closed，且在向模型投影工具集时显式剔除 Fission 工具，防止根会话被错误裂变。

## INTRA-PARTICIPANT-PARALLELISM-014: control-plane successor precedes lane settlement

Fission 不拥有 nudge、provider fallback/AABB 或 degeneration-guard 的恢复语义。对 Fission lane 的 reconciled turn：`TurnInProgress`、`TurnNeedsContinuation`、`TurnFailed` 与由 `DegenerationGuard` 导致的 `TurnAborted` 必须让渡给普通 Turn/Application owner。上述路径不得 materialize lane、不得失败 group、不得发布 logical completion。仅稳定 `TurnCompleted` 可进入 lane materialization / final takeover completion；真正的外部 abort 才可终止 group。

## INTRA-PARTICIPANT-PARALLELISM-015: deterministic ring convergence

Ring convergence 的顺序只由 canonical lane index/count 决定。V1 的 ring fold 从 lane `0` 按索引递增环行至 lane `N-1`，以 keyed union 合并记录，并由确定的终点 lane `N-1` 接受最终 takeover。不得持久化或读取“最后到达/最后 materialize 的 lane”来选择接管者；不同完成到达顺序必须得到同一 merge order、同一 takeover lane 与同一 aggregate。

## INTRA-PARTICIPANT-PARALLELISM-016: Result traversal preserves input cardinality and order

Foundation 的 `TaskResultList.traverseM` 是按输入基数有界的顺序 Result traversal，不是 retry。mapper 对每个已到达输入严格调用一次并保持输入顺序；成功时到达全部输入，首个 `Error` 原样短路且不得调用其后的输入，空输入不得调用 mapper。取消与异常沿 mapper task 传播且停止 traversal；该组合不拥有 deadline 或 recovery policy。
