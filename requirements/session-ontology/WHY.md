# session-ontology — WHY

## 不可替代的存在理由

在多代理与子会话系统中，`SessionId` 是可复用的物理执行容器，不是 participant identity，也不是 logical participant run。「拥有一个 session」绝不等于「出现一个新 participant」。如果 execution class、durable logical ownership、attachment 形式与 run-scoped personhood 互相纠缠，runtime 拓扑就会冒充业务身份，导致能力继承、身份恢复与生命周期管理全面失控。

1. **单轴分类无法表达执行能力边界**。长期持有上下文与知识的工作会话（如 Dedicated Sync*）需要完整的 Companion 与 context 能力；而短命的叶子节点（如 Bookkeeper、StrengthReplica）则是即用即弃的内部执行单元。单轴分类必然把两者揉成一团，模糊两者的执行能力边界。
2. **所有权事实必须具有单一来源**。若每个特性各自维护 parent/child 映射与生命周期，级联取消与崩溃恢复逻辑必然出现分歧。
3. **物理拓扑不得冒充逻辑归属**。Host 的物理展示层级保持扁平（深度为 2），所有逻辑归属关系完全由持久化 journal 事实承载。若依据物理 parentID 推断逻辑归属，系统恢复时将错误收养其他会话的子节点。
4. **Runtime 拓扑不决定角色与权威**。Session 的本体属性由 ExecutionClass 与 Ownership 正交决定，Role、Persona、工具集与 Authority 绝不参与底层分类。否则物理容器与执行绑定会被误解为业务身份。
5. **Identity 生命周期必须独立于容器生命周期**。同一 SessionId 只有在 `interaction-authority` 持久化 exact `AuthorityLogicalRunClosed` 并释放 active identity binding 后才可承载 fresh root；association removal、detach/attach、idle/timeout 或 Host 观察都不是 closure。Session ontology 只发布容器分类与 durable association，`participant-identity` 才拥有 identity 解析，root acceptance 与 identity installation 则共享一个 durable `AuthorityRootAccepted` payload。

## 核心不变量

- 每个 session 严格且唯一落在 `Work | InternalLeaf` 与 `Root | Attached` 的正交组合中。
- Attached session 必须恰好属于一个 ownerSessionId，且禁止自引用链接。
- InternalLeaf 节点禁止持有 Companion、禁止递归附挂子叶，亦不得成为其他 Attached 节点的 owner。
- 物理 Host parent 统一指向 family root，逻辑归属仅由 journal 关联事实定义。
- SessionId 仅命名可复用物理容器；logical-run identity 由 `participant-identity` 的版本化 evidence 命名。
- 领域内彻底消除旧式的 Student / Teacher 概念与拓扑。

## 违反边界的后果（RED）

- 依赖代理名称或工具白名单猜测会话是否为 Companion 或是否具备上下文能力。
- 崩溃恢复时依据物理层级匹配子节点，导致错误收养或孤儿会话。
- 业务角色与执行拓扑强绑定，导致执行绑定切换时意外篡改业务身份与权限。
- 内部决策用的临时叶子节点被持久化为长期成员，破坏后续决策的独立性。
