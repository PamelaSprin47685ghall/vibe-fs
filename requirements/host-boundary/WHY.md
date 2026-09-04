# host-boundary — WHY

## 不可替代的存在理由

业务系统的语义真理必须建立在外部宿主（Host）稳定、可验证的物理能力与一致的快照观察之上，绝不能寄托于传输层的流式碎片事件、私有内部状态或偶然的时序竞争。

1. **禁止流式碎片拼凑业务真相**。流式事件（如 `message.updated`、`part.delta`）的顺序与格式极易随宿主版本漂移。从流式碎片推导业务完成或失败，等同于将系统因果绑定在传输噪声之上。系统的唯一真相源必须是唤醒后读取的完整 SDK 快照。
2. **传输状态机不得侵入领域层**。底层的忙碌、排队与流式传输属于通信细节，不属于领域事实。领域层仅接受类型化的粗粒度唤醒信号（`HostSignal`），且信号只负责唤醒，不携带业务事实本身。
3. **物理能力的显式证明与安全失败**。系统依赖的所有宿主能力（如快照定位、Hook 时序、上下文压缩控制等）必须具备可验证的测试证明。一旦环境能力缺失或观测出现二义性，必须显式安全失败（fail closed），严禁在未经验证的环境中静默降级运行。
4. **物理身份的可靠因果提取**。从快照解析运行身份必须基于严格的因果关系，命中 0 个或多个候选时一律安全失败，绝不依赖位置猜测进行模糊匹配。
5. **公开边界上的 typed membrane**。插件仅通过宿主公开的 Hook 与 SDK 集成，严禁修改宿主源码，也不把 private Host state、未公开 callback ordering 或 UI 展示行为当作系统承诺。所有 Hook 先把公开 evidence 收敛为 `execution-failure-policy` 的封闭失败类型；fatal 必须在 exact capacity/message settlement 后发生。
6. **真实 canary 而非模拟承诺**。Host 物理能力必须由针对受支持真实 Host build、经公开 Hook/SDK 运行的 canary 证明；mock、源码形状检查与 UI 观察都不能替代物理证据。
7. **Contract/Runtime 分离与无泄漏编译闭包**。业务契约若因引用粗粒度 Host 模块而连带编译 Sphinx MCP 适配器、诊断状态、消息就地修改或进程静止门禁，会导致编译依赖爆炸与边界侵蚀。无状态的 Session/Signal Contract 必须与具体的 Adapter 和 Runtime 严格解耦，契约闭包仅消费契约，禁止反向污染。
8. **动态值必须在膜上按JavaScript类型收敛**。Fable的`unbox`不会替JavaScript执行运行时检查；truthy字符串、数字、对象与伪数组若穿过膜，会把Host噪声变成compaction、synthetic、abort或领域identity，甚至在hook内抛异常。
9. **进程级 workspace effect 必须由显式 capability 承载**。公开可写的 root workspace atom 允许任意 consumer 改写后续 Host 路径选择，也把 first-bind 规则藏在调用顺序中。该 effect 必须收进 Host runtime；composition 只注入只读 capability，普通 consumer 不得取得 binder。

## 核心不变量

- 信号仅作唤醒，完整 SDK 快照是唯一业务事实来源。
- 调和器实行单飞执行（single-flight），依据因果事件驱动收敛，严禁无界的墙钟轮询。
- 观测不足或存在多解时一律安全失败（fail closed）。
- 插件加载初始化阶段保持纯洁，严禁执行业务恢复或反向调用宿主业务接口。
- Host 契约闭包严格排除 Sphinx、诊断、消息修改与进程运行时实现。
- Host envelope、message codec与loop codec拥有不同consumer cohort；共享解析只下沉到无状态envelope contract，不能用一个宽adapter公开全部codec、subscription与diagnostics。
- Node runtime只是机制标签。纯`node:path/posix`表示不得被误判为authority；console、environment、process control及mutable registry按实际capability facts判定。
- fatal incident vocabulary与capability type属于纯contract；console report、process kill/exit属于唯一adapter。composition负责mandatory injection及settlement ordering，普通runtime不能直接到达physical implementation。
- Host signal订阅必须把“使用公开local hook”与“持有legacy listener资源”表达为互斥状态；只有composition可把typed订阅失败解释为fatal，物理adapter不得同时拥有失败解释与进程效果。
- raw Host value只由封闭string/bool/array/plain-record reader解释；malformed值不得通过truthiness、`string value`或擦除后的`unbox`取得领域意义，任一公开hook对malformed envelope必须确定性fail-closed且不抛异常。
- root workspace 只接受 process-local runtime 的首次 `Some path`；`None`不占用槽位，后续候选不能改写已绑定值。只有Host composition取得binder，所有读取经注入的reader完成。

## 违反边界的后果（RED）

- 业务层消费流式碎片，导致在网络重排或上游更新时产生虚假完成判定。
- 将传输重试或中间错误误判为业务终止，破坏降级与恢复预算。
- 上下文压缩开关失效却继续运行，导致物理历史丢失而无法感知。
- 模糊匹配工具调用与消息 ID，引发跨会话的数据错配与假绿测试。
- 契约工程泄漏运行时或外部适配器依赖，导致领域层编译闭包膨胀并产生隐式耦合。
- 任意业务consumer直接读写公开workspace atom，可绕过first-bind并把另一插件实例重定向到错误目录。
