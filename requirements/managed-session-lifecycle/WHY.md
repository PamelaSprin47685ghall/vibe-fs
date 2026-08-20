# managed-session-lifecycle — WHY

## 不可替代的存在理由

只要系统创建与托管 session，就必须有单一且权威的 lifecycle owner 负责创建、复用、停止、回收与替换。如果各业务特性自行复制 parent 映射、取消机制与恢复规则，生命周期状态机必将四分五裂。

1. **所有权事实的单一写口**。多处并行的生命周期管理器会导致崩溃恢复与级联取消出现分歧，同一子会话在不同路径下会获得相互矛盾的终态。
2. **终态的不可逆性与 Tombstone 语义**。`Retired` 与 `Abandoned` 是持久化的最终状态。若缺乏严格的墓碑语义，系统重启时可能错误重放已消费的完成结果，或将已终结的子会话重新当作活动人使用。
3. **安全可证明的复用判据**。重启恢复必须基于 journal 关联（SessionId + agent + title）进行精确匹配；未关联的直接新建，匹配冲突直接安全失败（fail closed），严禁收养无关联的孤儿会话。
4. **互斥的生命周期形态**。长期复用的 Dedicated 会话（以 ReuseScope 驱动）与即用即弃的 OneShot 会话具有截然不同的生命周期，绝不能混用回收与销毁策略。
5. **级联取消的物理因果保证**。父会话在对外宣布终止前，必须确保所有子会话（包括 Companion Blogger 等）的物理中断已完全完成，杜绝异步竞争导致的悬挂进程。
6. **Attempt 中断与 Logical 取消的权限分离**。内部控制收束（如循环终止、求助等）仅能请求中断当前的物理尝试（physical attempt），绝不得篡夺父会话的逻辑取消权限，亦不得擅自中断用户根会话。

## 核心不变量

- Handle 生命周期遵循严格的四态模型：`Active → CompletedAwaitingJoin → Retired` 与 `Active | CompletedAwaitingJoin → Abandoned`。
- Completion cell 实行单赋值竞争，首个到达的完成事实具有唯一权威。
- 内部尝试中断必须显式闭合后继处理者（successor）或转化为明确的 Failed 终态，防止孤儿尝试挂死父级 join。
- 系统关闭与资源清理必须等待全部已准入的 durable 操作彻底排空。

## 违反边界的后果（RED）

- 同一 `(ReuseScopeId, role)` 产生两个并行的 live Dedicated 会话。
- 重启后同一 handle 绑定到错误的子会话，或已 Retired 的完成结果被重复消费。
- 父会话宣布中断后，后台子会话仍在隐蔽运行并产生副作用。
- 内部错误将局部 attempt 中断放大为整棵会话树的逻辑取消，导致根会话意外退出。
- 插件释放持久化存储时仍有未排空的异步写入，引发写入损坏与悬挂异常。
