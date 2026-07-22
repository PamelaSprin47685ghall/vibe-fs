# 实施计划

---

## 零、两时代

第一：OpenCode only；冻结 Mux/OMP；next 禁 import 旧生产。  
第二：他宿主；≥3 重复再抽共享。

---

## 一、冻结路径

Mux/Omp 生产、相关 e2e/tests/docs。

---

## 二、next 工程

```
next/StructuredFlow.fs
next/OpenCode/{Driver,Inbox,PromptProtocol,Journal,Session,Plugin,…}
.wanxiangshu-next/runtimes/<runtime-id>.ndjson   # 运行时产物，非源码
tests-next/GuideContract/
tests-next/JournalIsolation/   # 多 Runtime 场景
```

门禁：无 legacy import；无业务 EventBus；无 Flow AST；无 callOnce 公共 API；无 Nudge/idle；无谓词 Inbox；无共享 Context Flow 并行；  
**无** workspace/session lockfile 依赖；**无** Previous/Fork/Owner 代写路径。

---

## 三、总账

行为 ID；状态字段分类；耦合三问。无旧 NDJSON 兼容。

---

## 四、Phase

|Phase|内容|
|---|---|
|**0**|文稿闭合 + host spike + GuideContract 编译骨架|
|A|Flow 内核|
|B|Host + Gateway + **BootSnapshot + 本 Runtime 日志 CreateNew**|
|C|codec + Origin|
|D|Tools + CommandPort|
|E|MessageTransform|
|F|Journal：Frontier / k-way merge / 单写 / 损坏单源降级|
|G|Process|
|H|Session + Driver + 本地 PromptProtocol|
|I–P|Todo…万象阵|

### Phase A 详细出口

Phase A 只验证 Flow 闭包执行基座，不实现 OpenCode、Journal、Prompt 或 Session 业务：

1. `Flow`、`Bind`、`Delay`、`While`、`For`、`Using`、`TryFinally` 真编译；
2. `ProgressGuard` 不依赖肥胖 Context；
3. 通用 `Flow.run` 不吞 OCE，领域 run 才映射取消；
4. 异步释放只走 `Using`，同步 TryFinally 不承诺异步 compensation；
5. `attempt` 不引入伪 `never`；
6. 找到即停和有界重试使用尾递归，不依赖 CE early-return；
7. Task 层 `mapBounded` 保序、全 await、独立 Context、finally 释放 semaphore。

Phase A 的 Context 使用内存 fake；真实宿主和文件故障在后续 Phase 验证。

### Phase B～H 边界

|Phase|必须证明|禁止引入|
|---|---|---|
|B|RuntimeId、CreateNew、BootSnapshot、Hook Post|workspace 总锁、第二 Writer|
|C|DTO、Origin、UserMessageId/parentID|领域读取 `obj`|
|D|ToolContext、绝对 Deadline、CommandPort|Tool 直接写 Session Fact|
|E|固定纯 Transform、幂等|动态 Stage Registry、Transform IO|
|F|Frontier、k-way merge、单写、单源降级|Repair 他人文件、实时 tail|
|G|Spawn 前装 pump、async dispose|嵌套 timeout、fire-and-forget 清理|
|H|本 Runtime Driver、FIFO、local PromptProtocol|跨 Runtime Pending 全局锁、谓词 Wait|

### Phase 0 出口

文稿含：四阻断 + Lifetime Snapshot Isolation 正式语义 + Driver 局部化。  
host spike：MessageID/parentID/synthetic/时序。  
GuideContract 可编译核心签名。  
Status → 准许 Phase A（spike 完成后）。

文档检查必须确认旧语义没有回流：无 workspace/session lockfile、无 Previous/Fork/Owner 代写、无 `PromptKind.Nudge`/`idleProposals`、无 `Wait(predicate)`、无公共 `callOnce(stepId)`、无 for early-return、无下层重置 TimeSpan Deadline。历史反例必须标为 `[FORBIDDEN]`。

---

## 五、Journal 隔离测试（Phase F / 集成）[NORMATIVE]

1. 两 Runtime 同 workspace、不同 Session  
2. 两 Runtime 同 workspace、**同 Session**  
3. 同 Session 事件按 CommittedAt 交错重放  
4. 较晚 Todo 快照最终生效  
5. 两侧 Prompt 事实均保留  
6. 运行中 A 不见 B 新增  
7. 第三进程启动见双方已 flush 完整事件  
8. Frontier 后追加不进本次快照  
9. 活跃 Writer 半行被 Reader 忽略  
10. Reader 永不改 Writer 文件  
11. 同 Runtime 时钟回拨仍保 LocalSeq 序  
12. 同时刻 RuntimeId/LocalSeq 结果确定  
13. 一日志损坏只截断该源  
14. Snapshot+own ≡ 对应时间线子序列 Fold  
15. 多次重启归并确定  
16. **不存在** workspace/session lockfile  
17. 每进程唯一 FileStream Writer  
18. 日志不交叉写入  

---

## 六、体量 / 验证 / 切换

同前：看 next 同能力体积；E2E；重启；泄漏；Driver 死锁；AcceptanceUnknown；**多 Runtime 隔离 18 条**。

验证分层：编译（所有 normative 签名）；纯单测（Fold、Progress、PromptKey、Verdict、Fallback）；协议测（FIFO、取消、MessageId、CommandPort）；故障注入（半行、flush、泵死锁、kill、ProjectionBroken）；多 Runtime 集成；架构门禁。调试结论必须落成正式测试，不接受一次性脚本。

---

## 七、纪律

先甜后机制；迁行为不迁架构；迁事实不迁 PC；Phase 0 未闭合不编码业务。  
允许陈旧视图合法操作；拒绝实时一致性无底洞。

---

*KISS-13 终。*
