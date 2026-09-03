# durable-events — WHY

## 1. 领域动机与核心矛盾

当系统各个业务模块自造持久化存储、日志与重放机制时，会引发严重的世界分叉：
1. **多重真相源与状态分裂**：内存直接修改、散落在各处的私有状态文件或私有 Git ref 导致崩溃重放时无法构建全局一致的业务现实。
2. **重写历史与伪造事实**：试图通过原地修改 JSON 状态或删除旧事件来“纠正错误”，导致重放历史与事实因果彻底脱节。
3. **将 Git 当作在线事务数据库**：每次运行时事实追加都触发 Git object、tree、ref 构建与 CAS 重试，使写入开销随历史规模急剧膨胀。

`durable-events` 建立系统唯一的持久化事实底座（Universal Durable Substrate）：
- **Event 是唯一真相**：动态业务状态完全由不可变事件流表达，状态投影仅是纯函数的衍生视图；
- **Append-Only 与单进程单写者**：每个进程独占单个持续增长的本地 NDJSON 文件，写入成本与历史大小无关；
- **单一 Integrator**：历史合并与当前状态折叠由唯一的 Canonical Integrator 统一裁决。
- **领域契约与物理实现分离**：业务消费者只编译 `EventEnvelope`、append/read port 与稳定事件词汇；本地文件、Git object/ref、codec、replay 与 Host adapter 留在 Runtime/Adapter locality。否则 Fable 会把整条物理实现闭包合并进每个消费者，owner 工程只剩名义边界。

## 2. 核心不变量与破坏后果

- **事实不可篡改**：已提交事件永远不可删除、修改或原地升级；错误必须通过追加新事实纠正；若破坏，审计链与重放确定性即刻失效。
- **本地提交以完整 NDJSON 行为准**：运行时追加绝不创建 Git object、Git ref 或执行 Git CAS，仅当完整 canonical 行落盘并释放门禁后视为提交。
- **Current 不能领先于事实**：积分可以在内存中预计算，但物理追加失败时必须丢弃预计算结果；否则查询会看见无法由重启重放恢复的未来。
- **共享 Integrator 必须由行为证明**：注册表名称、函数名和调用次数只能证明源码形状。每个注册业务 oracle 必须在 live append 后改变 production Current，并从相同 durable history 重启得到同一观察结果。
- **StorageInvalid 全局 Fail-Closed**：JSON 损坏、非 canonical 格式、标识冲突或环状依赖必须立即拒绝构建投影并阻断运行，防止系统在错误地基上继续派生事实。
- **纯 codec 与物理 store 分居**：canonical identity codec 是可共同授权的纯协议；process log、store factory、锁与文件 authority 是 effect。把二者放进同一 public slice会让只需 canonical bytes 的 consumer获得完整存储闭包。
- **fatal 是注入的物理 fuse**：durable owner只决定 typed semantic-cut incident，并先持久化cut-tail与settlement evidence；console/kill/exit由Host唯一physical adapter执行。直接调用全局fuse会让领域owner同时拥有进程authority并允许重复fatal。

## DEPENDS ON

无
