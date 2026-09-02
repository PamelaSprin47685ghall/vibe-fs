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
- **StorageInvalid 全局 Fail-Closed**：JSON 损坏、非 canonical 格式、标识冲突或环状依赖必须立即拒绝构建投影并阻断运行，防止系统在错误地基上继续派生事实。

## DEPENDS ON

无
