# 总结判断

这次重构**还没有进入“只剩收尾”的状态**。旧的东西已经不再主要表现为 `V39.fs`、`V40.fs` 这类文件名，而是转化成了更隐蔽的五种形态：

1. **已经无人使用，却仍参与编译的空文件和废弃拆分文件**；
2. **新旧两套实现并存，实际运行仍走旧路径**；
3. **为了兼容旧调用方或旧测试而保留的转发门面**；
4. **本应归属 `RuntimeScope` 的全局可变状态和测试后门**；
5. **调试期间产生的临时文件、自动 profiler 和历史补丁注释**。

当前仓库约有 **567 个 F# 文件**，其中大量文件只有几十行，最大文件也不到约 250 行。这说明目前的主要问题已经从“文件过大”反转成了**机械性过度拆分、职责被切碎、迁移完成度不一致**。以下结论基于本次 Repomix 静态快照；被 `.gitignore` 排除的生成文件、测试产物和二进制不在检查范围内。

---

# 一、可以优先肃清的明确临时文件

## 1. `Runtime/Search/SembleMcp.fs`（已完成）

已删除。`SembleMcp` 与 `SembleSearchClient` 高度重复的 MCP 连接、搜索和日志处理逻辑已统一到 `SembleSearchClient` + `SembleSearchTypes`。`SembleInjection.fs` 的 trace 调用已迁移到 `SembleSearchTypes.trace`。`tests/RemovedProductionFilesTests.fs` 断言该文件不回归。

---

# 二、静态扫描显示无人使用的删除候选

下面这些文件没有形成可靠的外部调用链，应列入“删除候选清单”：

* `Kernel/OmpPrompts.fs`
* `Kernel/Nudge/RetryProgress.fs`
* `Kernel/Wanxiangzhen/EventLogParse.fs`
* `Kernel/ReviewReplayPolicy.fs`
* `Runtime/Tooling/WebFetch.fs`
* `Runtime/EventStore/ReviewReportSubmission.fs`
* `Runtime/MessageTransform/PlanCodec.fs`
* `Runtime/Subsession/Flow.fs`
* `Runtime/Fallback/ContinuationSupervisor.fs`
* `Hosts/OpenCode/Fallback/ContinuationHost.fs`

不能只因为它们“看起来以后可能有用”就保留。

## 特别需要点名的几个文件

### `Runtime/EventStore/ReviewReportSubmission.fs`

这是明显的占位实现。

其思路中仍然存在类似：

> 未来生产环境中，ReviewReportBuffer 应成为 SessionState 的一部分。

但当前实际缓冲内容被构造成空值。这样的文件最危险，因为它：

* 名字像正式实现；
* 能通过部分编译；
* 但语义并未完成；
* 将来容易被误接入生产路径。

处理原则只有两个：

* 现在完成它；
* 现在删除它。

不允许继续以“未来会用”的状态留在生产源码树。

### `Runtime/Subsession/Flow.fs`

这是一个独立的、带大量理论说明的 “Minimal Flow Kernel”，但当前未被主运行链路采用。

它更像研究草稿或架构试验。

应当：

* 移到设计文档或实验分支；
* 或删除；
* 不应作为无调用的生产源码继续参与项目结构。

### `Runtime/MessageTransform/PlanCodec.fs`

序列化计划的能力没有被实际运行链路使用。若计划不是持久化协议的一部分，就不该预先维护 Codec。

### 删除候选的标准流程

不要由开发者凭感觉说“没人用了”。

对每个候选文件执行：

1. 搜索模块全名。
2. 搜索所有公开类型名。
3. 搜索所有公开函数名。
4. 检查 `.fsproj` 编译顺序附近是否有隐式初始化需求。
5. 检查测试是否只是在验证旧 API，而非真实业务行为。
6. 从 `.fsproj` 暂时移除。
7. 编译生产项目。
8. 编译测试项目。
9. 运行 E2E。
10. 没有失败便删除物理文件。

---

# 三、尚未完成的关键迁移

这些比“删除几个文件”更重要。它们意味着**新架构虽然已经写出来，但系统仍在走旧路**。

---

## 2. Mux 的 session/subsession 能力仍然是降级实现

多处注释明确表达：

* Mux 尚没有自己的 per-session mailbox；
* 某些清理只是 best effort；
* abort 不受 host 支持；
* Mux 没有完整 `SubsessionHostAdapter`；
* 某些 reconcile 只能传入 `None` 或走 no-op。

这不是单纯的“不同 Host 能力不同”，而是当前代码没有把差异建模完整。

### 当前危险

调用方可能看到相同接口，却得到三种不同语义：

* OpenCode：真正执行；
* OMP：另一套实现；
* Mux：静默 no-op 或 capacity downgrade。

### 必须做出明确决策

#### 方案 A：Mux 正式支持这些能力

那么就需要补齐：

* per-session dispatcher；
* abort port；
* close notification；
* subsession host adapter；
* reconciliation；
* lifecycle disposal。

#### 方案 B：Mux 明确不支持

那么就必须：

* 在能力注册阶段不注册相关工具；
* 返回明确的 typed capability error；
* UI 或工具 schema 明确不可用；
* 不允许静默成功；
* 不允许用 no-op 伪装实现。

当前这种“接口存在，但内部尽量做一下”的状态必须结束。

---

## 3. Continuation 未组合实现已删除

已完成。生产链唯一状态机与 `ContinuationExecution` 保留，第二套 command processor、supervisor、host、event codec、projection 与 decision 已删除，`Continuation.fs` 仅保留 prompt builder 所需 DTO。`tests/ContinuationCleanupTests.fs` 防止回归。

---

## 4. Backlog/Todo session 已统一到 Runtime 核心

已完成。`Runtime/Execution/BacklogSession.fs` 是唯一核心，`MagicTodo.fs` 与 Host 级重复 wrapper 已删除。`BacklogSessionRuntimeTests` 验证 scope 隔离；三 Host 实际调用链已覆盖。

---

# 四、旧兼容层仍然大量存在

下面这些文件仍然承担“旧名字转发到新实现”的职责：

* `Hosts/OpenCode/HookSchema.fs`
* `Hosts/OpenCode/HookExecute.fs`
* `Runtime/Fallback/FallbackConfigCodec.fs`
* `Runtime/Execution/BacklogProjectionBuild.fs`
* `Hosts/OpenCode/SubsessionHostAdapter.fs`
* `Runtime/Messaging/OpencodeSessionEventCodec.fs`
* `Runtime/Search/FuzzySearch.fs`
* `Kernel/ReviewSession/Facade.fs`
* `Hosts/Omp/SessionLifecycleHooks.fs`
* `Runtime/EventStore/EventLogRuntime.fs`

常见注释包括：

* “for backward compatibility”
* “for existing openers”
* “so existing tests can call”
* “convenience re-export”

## 为什么现在必须处理

兼容层在发布 SDK 时可能合理，但这是内部大规模重构。内部兼容层长期存在会导致：

* 生产代码不知道应该引用所有者模块还是 facade；
* 测试继续固化旧路径；
* 同一函数出现多个合法入口；
* 循环依赖被 facade 掩盖；
* 文件拆分看似完成，依赖方向实际上没有改变。

## 清理办法

建立一份“兼容出口迁移表”，每个 re-export 都记录：

| 字段    | 内容     |
| ----- | ------ |
| 旧模块   | 当前兼容入口 |
| 旧成员   | 被转发的符号 |
| 真正所有者 | 新模块    |
| 生产调用方 | 必须优先迁移 |
| 测试调用方 | 第二批迁移  |
| 删除批次  | 何时移除   |

迁移顺序：

1. 生产调用方直接引用真正所有者。
2. 测试调用方同步迁移。
3. 删除 re-export。
4. 编译。
5. 删除仅剩转发作用的文件。
6. 添加架构规则，禁止再次引用旧模块。

不要因为“现有测试还在调用”而保留兼容层。测试应该服从架构，不应该反过来决定生产 API。

---

# 六、全局状态尚未迁入生命周期

以下状态仍然带有明显的进程级或模块级可变性：

* `Hosts/Omp/ExecutorTools.fs` 中的进程级 `ompScope`

  * 全局 projection；
  * 可变 workspace root。

 * `Hosts/Omp/NudgeRuntime.fs`

  * ~~singleton fallback runtime~~ → `FallbackRuntimeStore` 通过函数参数显式传递，不再存在进程级单例。

 * `Runtime/EventStore/EventLogRuntimeStore.fs`

  * 全局 stores map；
  * 缺少清晰的 remove/dispose。

 * `Runtime/Execution/SessionExecutor.fs`

  * ~~全局 active runs~~ → `activeRuns` 已迁移到 `RuntimeScope` extState，随 `scope.Remove` 清理。

* `Runtime/Search/SembleSearch.fs`

  * ~~全局 `lastBreakpoint`~~ → 已迁移到 `RuntimeScope` extState，由 `clearBreakpoint` 清理。
  * 没有与 session close 对称的 forget → 已提供 `clearBreakpoint scope sessionID`，session close 路径可调用。

* `Kernel/HostTools.fs`

  * 全局 E2E sandbox 标志。

这些状态与当前已经引入的 `RuntimeScope` 思路冲突。

## 统一治理规则

### 可以保留的进程级状态

只有满足以下全部条件的状态才允许全局存在：

* 与 workspace、session、turn 无关；
* 无敏感数据；
* 有大小上限；
* 无需 dispose；
* 并发安全；
* 有明确注释说明为什么是 process singleton。

### 必须进入 scope 的状态

凡是键中包含以下内容之一，就必须归属 scope：

* workspace ID；
* session ID；
* turn ID；
* task ID；
* iterator ID；
* continuation ID。

### 迁移步骤

1. 搜索所有顶层：

   * `mutable`
   * `Dictionary`
   * `Map`
   * `ResizeArray`
   * singleton instance。

2. 对每项标注所有者：

   * process；
   * workspace；
   * session；
   * turn。

3. 对 workspace/session 状态提供对称生命周期：

   * Create；
   * Get；
   * Close；
   * Dispose；
   * Forget。

4. 让 Host 的 session close 事件真正调用清理。

5. 进行泄漏测试：

   * 创建并关闭 1000 个 session；
   * map 数量应恢复到基线；
   * iterator、evidence、breakpoint、active run 均为零。

---

# 七、生产插件测试后门（已完成）

已完成：

* Mux 与 OpenCode 的公开插件对象不再写入 `__runtimeScope`、`__reviewStore`、`__fallbackRuntime`。
* `registerTestHooks` 已删除；`tool.execute.before` 与 `systemTransform` 作为正式运行时 hook 注册。
* `createReviewTestSurface` 仍作为内部 seams 的 ReviewStore 构造 helper，不再挂到公开插件对象。
* `tests/PluginObjectContractTests.fs` 与 `IntegrationOpenCodeContractTests` 持续断言公开插件对象无 `__` 前缀键。
* 测试与 JS harness 通过 `createRegistrationWithSeams` / `pluginForWithSeams` 内部 seam 获取 scope、reviewStore、fallbackRuntime。

其余 `ForTest` 生产 API 见第六节全局状态收拢，后续单独处理。

---

# 八、明确属于临时产物的运行时文件

## 1. 自动 CPU/Heap Profiler（已完成）

`RuntimeScope.create()` 不再触发 profiler；`Profiler.initGlobal` 及固定 5 分钟定时器已删除。仅显式 `Profiler.start()` 可启用采集。`stopAndSave` 接收 `string option` 输出目录，或读取 `WANXIANGSHU_PROFILER_DIR` 环境变量，默认 `/tmp`。文件名格式 `<pid>-<timestamp>-<random>.{cpu,heap}.profile`，保证唯一且不覆盖。`activeSession` 为 `private ref`，保存后重置。`tests/ProfilerOutputTests.fs` 回归断言不再生成固定 `/tmp/wanxiangshu.*profile`。

---

## 2. Semble 注入日志（已完成）

`SembleMcp` 中的重复日志实现已随模块删除。唯一日志入口为 `SembleSearchTypes.trace`，受 `SEMBLE_INJECT_DEBUG=1` 环境变量门控（默认关闭）。目录可通过 `SEMBLE_INJECT_DEBUG_DIR` 配置，缺省使用 `node:os` `tmpdir()`。文件名格式 `wanxiangshu-semble-{unixTimestampMs}-{guid8}.log`，每次进程启动唯一，不再使用固定共享 `/tmp/wanxiangshu-semble-inject.log`。

---

## 3. `.wanxiangzhen-e2e-meta.json`

来源：`Hosts/OpenCode/PluginWanxiangzhenE2eMeta.fs`。

已改为仅在 `WANXIANGZHEN_E2E=1` 或 `WANXIANGZHEN_E2E_INPROCESS=1` 时生成，并可通过 `WANXIANGZHEN_E2E_META_DIR` 指定目录；e2e harness 现在会创建独占临时目录并设置该环境变量，元数据不再写入项目根。`.gitignore` 也已加入该文件。

---

## 4. `<target>.swap-tmp`

来源：`Runtime/Tooling/FileSwap.fs`。

`NodeFileSwapIO.WriteTemp` 已不再使用固定相邻名 `path + ".swap-tmp"`，改为 `System.IO.Path.GetTempFileName()`，并移动到目标文件所在目录（保证 `rename` 跨同一文件系统），异常路径已调用 `DeleteIfExists`。剩余项：启动时清理过期 `.tmp` 残留与 `flush`/`symlink`/`权限` 语义可后续补充。

---

## 5. `tempFilesByPrompt`

这是内存中的临时文件注册表，而不是磁盘文件，但同样需要完整生命周期。

必须确认：

* prompt 结束时删除；
* session abort 时删除；
* session close 时删除；
* workspace dispose 时删除；
* 异常和超时路径也删除。

不能只覆盖“正常完成”路径。

---

# 九、历史补丁注释已肃清 ✅

源码中仍能看到大量类似：

* `S-07 fix`
* `F-03`
* `N-01`
* `N-02`
* `R-01`
* `R-03`
* `TASK §5`
* `PRD-06`
* `Phase 7`
* `Phase 8`
* `REF.md`
* “until ... lands”
* “best effort for now”

这些注释记录的是开发过程，不是最终设计。

## 保留什么注释

只保留解释以下内容的注释：

* 为什么某个不变量必须成立；
* Host 官方限制；
* 不直观的并发顺序；
* 协议兼容边界；
* 安全或一致性理由。

## 应删除什么注释

* 谁修过哪个 ticket；
* 这是第几阶段；
* 过去曾经怎么实现；
* “暂时先这样”；
* “以后某模块落地后再改”。

历史信息应该进入：

* commit；
* issue；
* ADR；
* regression test 名称。

最终源码应像“一次写成”，而不是像事故现场记录。

---

# 十、文件拆分已经出现反向技术债

当前有大量 20～40 行文件，且若干目录极度扁平：

* `Hosts/OpenCode`
* `Hosts/Omp`
* `Kernel` 根目录
* `Runtime/Tooling`
* `Runtime/Messaging`
* `Runtime/Subsession`
* `Runtime/Fallback`

这说明开发者可能把“文件短”错误地当成“架构好”。

## 需要合并的文件类型

以下小文件通常应合并：

1. 只有一个私有 helper，且只被同目录一个模块调用；
2. 只有别名或 re-export；
3. 只有一个 architecture-test probe；
4. 只有一个薄包装，未形成独立端口；
5. 文件名必须结合相邻文件才能理解；
6. 拆开后产生循环 `open`；
7. 没有自己的测试、不变量或生命周期。

## 可以保持独立的短文件

以下情况即使只有几十行也合理：

* 稳定领域类型；
* 明确的 port/interface；
* 纯状态机 transition；
* 独立 wire codec；
* 安全策略；
* 可单独测试的算法；
* 必须控制编译依赖方向的 F# 类型文件。

目标不是减少文件数，而是让每个文件都有**独立存在理由**。

---

# 十一、建议的完整执行顺序

## 阶段 0：冻结基线

在删除任何东西之前：

1. 建立专门的 cleanup 分支。
2. 暂停向相关目录加入新功能。
3. 记录当前：

   * build 结果；
   * UT 结果；
   * OpenCode E2E 结果；
   * OMP/Mux smoke test。
4. 导出当前公开模块和插件返回键。
5. 记录运行一次后产生的所有临时文件。
6. 不允许一边重构一边顺手改业务语义。

---

## 阶段 1：先删百分之百无意义的东西

第一批只处理：

* 空模块；
* 假架构测试成员；
* no-op debug 函数；
* 已确认无引用的废弃 helper；
* 明显重复的 Semble 旧客户端。

每删除一小批就：

1. 更新 `.fsproj`。
2. 编译。
3. 跑对应单测。
4. 提交一次独立 commit。

不要积累成一个无法定位回归的大提交。

---

## 阶段 2：清理临时输出和测试后门

优先处理：

* 自动 profiler；
* `/tmp` Semble 日志；
* E2E meta 文件；
* 固定 `.swap-tmp`；
* 生产插件 `__...` 字段。

这一阶段原则上不改变业务功能，却能显著降低运行时污染。

---

## 阶段 3：完成 epoch/evidence 迁移

这是最应优先完成的状态一致性工作。

必须做到：

* 所有 active caller 使用新 API；
* session 级清理不猜 epoch；
* 删除 legacy API；
* 高 epoch 测试通过；
* close/abort 后零残留。

在这一阶段完成前，不要宣布 subsession 重构完成。

---

## 阶段 4：统一 Backlog 实现

先建立跨 Host 契约测试，再把三套实现合为一套。

验收：

* 一个 Runtime BacklogSession；
* Host 只做 adapter；
* 无全局 projection；
* 三 Host 行为一致。

---

## 阶段 5：裁决 Continuation 双架构

召开一次只做这一件事的设计审查：

1. 画出现有两条调用图。
2. 标出真正被生产入口组合的模块。
3. 选择唯一 SSOT。
4. 抽离仍需要的 wire DTO。
5. 删除另一套未组合状态机。
6. 用 E2E 证明：

   * normal finish；
   * tool finish；
   * abort；
   * retryable error；
   * compaction；
   * zero-width continue；
   * duplicated terminal event。

禁止继续折中保留两套。

---

## 阶段 6：拆除兼容门面

按迁移表逐个消灭 re-export。

顺序必须是：

1. 生产调用方；
2. 测试调用方；
3. facade；
4. 旧模块文件。

不能一开始删 facade，导致开发者为了快速编译又重新加回来。

---

## 阶段 7：收拢全局状态

所有 session/workspace 相关状态进入 `RuntimeScope` 或专属 service。

每一种状态都要回答：

* 谁创建？
* 谁拥有？
* 谁关闭？
* 异常时谁关闭？
* workspace dispose 时怎样处理？
* 数量是否有上限？

答不出来就不允许保留全局 map。

---

## 阶段 8：重新整理目录和命名

最后才做文件移动和视觉整理。

建议以稳定 feature 组织：

* `Fallback`
* `Subsession`
* `Nudge`
* `Review`
* `Backlog`
* `MessageTransform`
* `Search`
* `Tooling`

每个 Host 下则只保留：

* Codec；
* Host port implementation；
* Plugin registration；
* Host-specific lifecycle；
* Host-specific capability downgrade。

不要再把共享业务复制进三个 Host。

---

# 十二、最终验收清单

下面每一项都应成为合并前的硬门槛：

* [ ] 不存在空 `.fs` 模块。
* [ ] 不存在仅为旧测试保留的生产 re-export。
* [x] 全仓只有一个 Semble MCP 客户端。
* [x] 全仓只有一套 Continuation 状态机和 command processor。
* [ ] Mux 不再有静默 reconcile/abort no-op。
* [ ] 所有能力降级都通过显式 capability 表达。
* [x] 生产插件不暴露 `__runtimeScope`、`__reviewStore` 等字段。
* [x] profiler 默认完全关闭。
* [x] 正常运行不生成 `.cpuprofile`、`.heapprofile`。
* [x] Semble 注入日志不再使用固定 `/tmp/wanxiangshu-semble-inject.log` 文件名。
* [ ] E2E 元数据只存在于测试临时目录并自动清理。
* [ ] file swap 不使用固定共享临时名。
* [ ] session/workspace 相关 map 都有对称清理。
* [ ] 创建并关闭 1000 个 session 后状态数量回到基线。
* [x] 源码中不再出现 `Phase 7/8`、ticket fix、temporary、until lands 等过程性注释。
* [ ] 无未引用生产源文件。
* [ ] `.fsproj` 的编译顺序反映真实依赖，而不是历史迁移顺序。
* [ ] OpenCode 全链路 E2E 通过后，再分别验证 OMP 和 Mux。
* [ ] 最终目录和文件名不依赖阅读重构历史才能理解。

**最优先顺序不是整理文件名，而是：裁决 Continuation 双架构，统一 Backlog，再拆兼容层。** 这三件事完成以后，才可以说旧架构已经从运行路径中真正肃清，而不只是从文件名上消失。
