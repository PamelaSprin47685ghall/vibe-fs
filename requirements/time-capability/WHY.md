# time-capability — WHY

## 不可替代的存在理由

**时间如果从隐式的全局环境（ambient wall clock / global timer）偷渡进入业务逻辑，同一个事实在不同机器或不同运行时刻就会产生不同的裁决，导致系统的证明体系、回放能力（replay）与测试确定性彻底崩溃。**

可重放性与可测试性的核心基石是「相同输入必定产生确定性的相同输出」。物理环境墙钟是最大的隐式随机源：它随宿主机性能快慢漂移、受系统负载与时区设置干扰。如果业务决策直接读取全局时间（如 `DateTimeOffset.UtcNow`、`Date.now` 或 `setTimeout`）：
- 相同逻辑在不同运行环境下随机失败或通过；
- 并发时序的证明退化为依赖调度器快慢的运气，无法可靠复现与排查；
- 失败用例无法通过记录的数据流进行确定性重放。

因此，时间严禁作为隐式全局状态存在，必须作为**显式能力（capability）**跨越依赖边界进行注入：任何需要感知时间或设置延时的组件，必须在构造时显式声明对时钟或定时器端口的依赖。

## 核心张力与设计原则

- **时间是输入，绝非权威（Time is input, never authority）**：时间值自身不携带任何业务裁决权，只有具体的领域规则结合显式注入的时钟才能给出业务判定。
- **强类型截止时间（Typed Deadline）**：截止时间必须封装为强类型对象，通过纯函数进行过期与剩余量计算，彻底消除裸时间戳比较带来的时区失配与数值溢出风险。
- **完全可虚拟化（Virtualizability）**：所有时间端口在测试环境中必须能够被确定性的虚拟时钟与虚拟定时器无缝替换，使时序逻辑能够在毫秒级内通过离散推进完成穷尽验证。

## 核心不变量与违约状态（RED）

仓库处于 RED 状态，当且仅当出现以下任一破坏时间确定性的违约：
1. 业务层（Domain / Application / Session）直接调用或引用原生全局时间 API。
2. 业务逻辑脱离领域规则，将时间戳作为独立权威直接用于驱动状态转移或分支选择。
3. 截止时间未通过强类型 `Deadline` 封装，散落为裸 `DateTimeOffset` 或整数毫秒的手工比较。
4. 测试用例因缺少虚拟时间支持，被迫使用真实等待（sleep）进行时序验证。
5. Session 起始时间原点在重试或回放中发生漂移，未能严格执行单次绑定。
6. pure clock/timer capability type与Node clock/timer implementation处于同一slice，使普通consumer传递获得ambient time、timer或mutable runtime authority。
7. pure `Deadline` 或 `SessionStartedAtProjection` 与Node/virtual timing实现共处同一project，导致只需确定性表示或投影的consumer被迫获得物理timer与可变verification runtime的完整编译闭包。

## M6 Temporal locality 裁决

compiler-resolved census证明六类知识具有不同consumer cohort：clock/timer capability type、pure `Deadline`、bind-once `SessionStartedAtProjection`、Node timing adapter、virtual timing implementation、production-bound representation Surface。它们不得继续借同一个project互相扩张可见面。前三者各自形成bounded contract；Node adapter与virtual implementation分居独立locality；原`foundation-temporal`只保留representation Surface。consumer按真实source edge引用最窄slice，同一consumer确实使用多个知识时显式引用多个project。
