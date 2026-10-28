# time-capability — WHAT

## [001] 时钟与定时器是显式注入的 capability

业务读取当前时刻或安排延迟，只能消费显式注入的时钟、定时器能力；延迟返回可取消的强类型句柄，不隐式取得全局时间权限。

## [002] deadline 与 elapsed 有 typed 表达，不散落为裸时刻比较

截止时间只能由当前时刻和预算构造为强类型 Deadline；剩余量、到期与下一段等待由纯计算结合注入时钟得出。业务不自行比较裸时间戳，计算不得因溢出或时区表示而改变结果。

## [003] 时间可虚拟化；测试用虚拟实现替换物理时钟与 timer

时钟与定时器可由确定性虚拟实现替换：从固定原点推进或设置时间，推进至截止点才触发到期任务，支持取消与清理。时序证明不依赖物理墙钟等待。

## [004] Domain / Application / Session 禁止直接读 ambient 时间

Domain、Application 与 Session 不直接读取全局时钟或安排全局定时器。静态门禁覆盖全部业务源码；物理适配器的例外须逐一显式声明，不按目录整体豁免。

## [005] 时间值本身不是 authority；只有消费它的领域规则 + 注入时钟决定意义

时间值是输入，不是业务权威。状态转移须由领域规则结合注入时钟裁决；时间经过本身不能制造成功、授权或完成事实。

## [006] deadline 是 causal-wait 的可选 escape

causal-wait 按需将截止时间作为等待的可选终止路径。时间能力独立提供，不反向依赖等待机制；到期只终止等待，不制造被等待的业务事实。

## [007] HOST-013 的 SessionStartedAt 绑定首次 prompt，一次采样形成新 marker 的 elapsed

面向 Provider 的 Session 在首次开始构建或发送 prompt 时，从注入时钟采样并持久化单次绑定 SessionStartedAt。每个新 occurrence 再从同一时钟采样，计算人类可读的 elapsed，与 MarkerText 一起固化持久化；重试或重启不重置原点，也不重算历史 marker。

## [008] temporal vocabulary、capability、adapter与projection必须分居

时钟/定时器能力类型、Deadline、会话起点投影各有独立纯契约，不携带物理实例或工厂；物理适配器、虚拟验证实现与表示边界各自分离，不借共享编译单元扩大可见能力。

消费者只依赖实际需要的契约，运行时消费组合层注入的必需能力；纯消费者及契约不得反向依赖物理或验证实现，普通生产消费者不得带入虚拟时间。表示边界仅为实际投影显式组合所需依赖，不提供 ambient fallback。边界由声明的依赖闭包及编译证明检查，扫描限制遵守 verification-system-018。
