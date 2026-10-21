# gen103 permit 主仓独立组合验收

输入为 root 构建成功后冻结的 gen103。仅写 `/private/tmp` 探针和日志，未改主仓输入、index、HEAD，未 build/stage/commit。root 另负责正式 requirement runner。

执行：Node v22.13.0 `node --test /private/tmp/vibe-fs-permit-gen103-independent.mjs`。最终 `/private/tmp/vibe-fs-permit-gen103-independent.log`：3 pass / 0 fail / 0 skip / 0 TODO，939.0ms，exit 0。

## 实际调用链

`ModelRoutingSurface` 的 opaque token WeakMap → `ModelRoutingRuntime` → `ExecutionCapacityOwner` typed projection WeakMap → 公开 `wanxiangli/permit` 实例。主仓 wrapper 保留容量账本、借用、排队、counter 与类型投影，已移走旧许可生命周期与重复计数器；library 保有签发/当前 handle/lifecycle/fence。

真实 plugin 正控使用已有 `withExecutablePlugin`，实际注册的 `chat.message` → `HostSignalBootstrap`/`ChatAdmissionTransaction` → durable acceptance + real routing acquisition/commit；实际 `experimental.chat.messages.transform` → deferred continuation admission + provider-step credit + 公开 Host snapshot witness → durable `ProviderStarted`。`hooks.event(message.updated)` → HostSignalRouter/ProviderLifecycle/ChatRecovery → durable terminal + exact physical release。没有用 `TransactionSurface.transactionScenario` 中本地假 Acquire/Commit 作为接线证明。

## 三组证明

1. 编译后的 opaque token API：
   - 同一身份重入返回同一冻结 token；原生 JSON serialization 被拒绝。
   - structuredClone、spread、同形对象、Object.create(null)、null 的 target/lifecycle 为空，commit/release 均 StaleFence；完整资源快照与 counters 都不变。它们未越过 opaque WeakMap，所以不把这类无效 token 计为 owner 的 stale transition。
   - 另一 runtime 的真实 token 被拒；该 runtime 只 stale 增加 2，ledger/token/custody/其他资源不变，原 runtime 完整快照不变。
   - 本 runtime 真 token 的成功、重复、错误 participant、已提交后 before-provider release 实际分类为 Applied、AlreadyApplied、Conflict、Conflict；duplicate 恰加 1、conflict 恰加 2。
   - 两个 runtime 均正常释放至空占用。
2. 编译后的旧 physical 反例：
   - 同 session A 被 B 替换，B 已 committed 并进入 provider step，activeCount=1。
   - 旧 A provider-step end、physical finish、commit、before-provider release 后，B 的 ledger/token/custody/executions/waiters/state counts/activeCount 全不变；三个 stale transition 恰计 +3。
   - 只读 committed 查询仍返回 B 原 token，A 不再能读 target。B 自己的 end/finish 能真正回收至空。
3. 真实 plugin/持久事实组合：
   - A `chat.message` 后断言 durable Accepted；transform 后断言真实 journal ProviderStarted。
   - A 的 Host `tool-calls` 结束 provider step，仍非 terminal；B 通过正常 human continuation PublishInput/transform 路径开始，断言 durable ProviderStarted。
   - 对旧 A 投递晚到 `finish: stop`，完整 B 资源快照（除 diagnostic counters）保持一致，B token target 仍可读，B durable projection 仍 ProviderStarted/非 terminal，A token 已无 target。
   - B 自己 `finish: stop` 后，journal 必须是 durable Terminal/Completed，且 session execution/custody 真正退出。若 event 只是被静默忽略，末项正控会失败。
   - 本 session 无 replacement prompt，createdChildren=0。fixture dispose 完成；日志中的 `/private/var/folders/g_/3ydzn7kx5lg9ylq8fd9p_13r0000gn/T/wxs-plugin-exec-gFS4Am` 随后实查不存在。

## 保留的探针失败

`/private/tmp/vibe-fs-permit-gen103-probe-interference.log`：初版独立探针在 B 的 PublishInput 后、真实 continuation transform 前调用 AcquireShared。这是 mutation，制造了一个新 Pending，令真实 continuation 无法找到原 committed witness，EMR-010 拒绝正确。本轮移到真实 transform 成功后才读取/重入既有 token，没有改生产代码。

`/private/tmp/vibe-fs-permit-gen103-host-clock-rejected.log`：第二版 Host double 给两个 assistant 相同 created=2，命中真实 NotLatestRun 公开 witness 校验。最终探针为 Host 事件提供确定性单调 created/completed 时间，保留该拒绝日志。这两份是探针输入/干扰失败，不能记成 permit 源码反例，也没有删除生产门禁来取得绿色。

## 005 新边界用例复审

真实 `EventStore.create` 接受修复后的 reader writerId；旧四位尾号真实拒绝，先前红日志确因同一生产禁尾规则失败。合法 handle 在 finally dispose；非法名称在 `ProcessEventLog.createWithSegmentCap` 的 safeWriterId 阶段即抛出，尚无 native owner/FD 分配；最外层 finally 移除临时目录。没有绕过解析或通过镜像正则替代生产验证。

## 结论与限制

本轮未发现 permit 接线新阻断。独立测试验证实际编译后的 routing/token/ledger，且有真实 plugin、实际持久 journal、Host public event 和 provider-start witness 组合；Host client 是仓内已有 double。`ProviderStarted` 在这里是持久边界事实，不是外部 provider 已收到请求或产生实际模型副作用的网络证明。没有运行新的外部 Host，也没有证明跨进程传递 token（其公开合同明确 process-local）、冷恢复或 Int64 counter 耗尽。正式 suite counts 与验收指纹以 root 的冻结 gen103 requirement run 为准。
