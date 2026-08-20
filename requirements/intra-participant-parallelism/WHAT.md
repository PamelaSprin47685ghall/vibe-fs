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

## INTRA-PARTICIPANT-PARALLELISM-010: durable replay，不猜 lane

裂变产生的 group 标识、lane 成员关系、替换关系与收敛终结均依赖不可变的 durable facts 进行审计与重放；严禁通过扫描外部相似会话推测并发实体。进程中断后未完成的裂变作为中断事实记录，不进行自动隐式恢复。

## INTRA-PARTICIPANT-PARALLELISM-011: V1 单 active group

一个 logical participant 同一时刻最多允许存在一个处于活跃状态的 Fission group。活跃 lane 再次调用裂变必须 fail-closed 为 already-fissioned，不支持递归裂变。

## INTRA-PARTICIPANT-PARALLELISM-012: eligibility 单一 consequence source

Fission 的角色权能准入必须从 office consequence 的单一源头投影至模型可见 schema 与运行时门禁，相同 office 的不同档位权限严格一致。仅具备裂变权能的角色（如 Manager、Coder、Inspector、Browser、Inquiry）可调用裂变。

## INTRA-PARTICIPANT-PARALLELISM-013: subsession-only origin

裂变的调用源校验与角色权能正交：调用方必须证明自身为物理 subsession。直面用户的根会话在任何资源预留、记录物化或中断前必须直接 fail-closed，且在向模型投影工具集时显式剔除 Fission 工具，防止根会话被错误裂变。
