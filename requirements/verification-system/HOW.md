# verification-system — HOW

## 架构与实现机制

`verification-system` 作为验证体系的元规则包，通过分层运行机制、静态门禁管理与因果监督器共同落实证据完整性：

### 1. 证据阶梯与构建编排（`proof ladder`）

`tests/proof-ladder.test.mjs` 对全局构建与测试命令链（`package.json` 中的 `format-build-test` 及 `scripts/check.mjs`）进行强约束：
- 严格锁定第 0 层静态门禁、第 1–3 层纯逻辑与时序单元测试、第 4 层单入点物理 Long Stroke 与第 5 层 Release 构建的执行顺序。
- `format-build-test` 以 Fantomas `--check` 只读验证提交字节；`format` 仅供开发者主动改写。`check/build` 允许经 Wireit 缓存调度，但 proof 同时锁定顶层 `npm run` 顺序与每个 Wireit step 的 exact command；间接调度不得隐藏、替换或跳过 Fantomas、text gate、owner contract gate 与 clean Fable build。DSL 门禁严禁任何自定义 FCS 扫描：不提取 declaration/application evidence，不生成完整 capability report，不设独立 report-only 入口；check 与任何 lane 均不得为门禁计算 FCS 分类结果（见 verification-system-001 全仓 FCS 禁令）。capability/authorization 的执行依据是声明式 ProjectReference DAG 与精确编译闭包、sibling `.fsi` 与普通 Fable 签名/私有可见性编译 canary，以及已注册行为证明。
- 顶层 release sink 只调用 unit、唯一 integration orchestrator、Long Stroke 与 pack。integration orchestrator 在任何 integration child 前恰好 warmup 一次，并唯一调度 distribution package child；顶层不得重复这两个 leaf step。`proof-ladder.test.mjs` 同时对真实组合与 duplicate-owner mutants 判 RED。
- build fingerprint 覆盖整个 workspace，仅排除自身 `dist/` 与 `.fable-build/` 输出；因为 `LoopDetectorEnvelope` 从 Git 跟踪的全部源码/文档语料派生，按固定目录枚举输入会让新增目录或文档修改错误复用旧 artifact。
- 确保 `scripts/check.mjs` 中注册的所有门禁脚本路径在磁盘上真实存在，且任何门禁失败时其非零退出码均能正确向上传播（fail-closed）。
- `scripts/build.mjs` 每次调用都持有跨进程 build lock，先删除上一轮 `dist/` artifact tree，再以显式 `Debug` configuration 执行一次真实 Fable compile；compiler 成功退出后才验证 `dist` 与 Surface Manifest。源码删除因此不会留下可被 package 收走的陈旧 JS。configuration 不依赖 Fable 的 watch/one-shot 默认值。不存在 watch-daemon、source-touch barrier、ack、artifact-exists fast path 或 wall-clock freshness 猜测，因此旧 `dist` 不能冒充当前源码的编译结果。
- 验证输入快照（`requirements/verification-system/tests/verification-inputs.test.mjs`）确保构建与测试前捕获完整输入集合（Git-tracked 语料、配置文件与规范文档），任何构建或验证执行期间的输入扰动均导致立即失败；
- 运行状态监督与统一事件汇聚（`requirements/verification-system/tests/reporter-supervision.test.mjs`、`support/test-run-state.mjs`、`support/run-inner.mjs`）确保单个 leaf test 与文件 worker 的真实完成状态由唯一不可伪造的 TestRunState 规范化管理，杜绝多重统计口径或未排空假绿。

### 2. 因果看门狗与静默监督（`e2e-watchdog-feed`）

`tests/e2e-watchdog-feed.test.mjs` 与因果原语套件负责守卫时序推进契约：
- 确保 E2E 物理测试中看门狗计时器仅由明确的因果事件（如目标事实增长、检查点达成）驱动续期。
- 严禁顶层测试用例直接调用底层计时器的内部 advance 接口，防止由于传输层噪声或背景任务活动导致看门狗被非法延期。
- unit/integration/package 的 process-isolated node:test child 由外部 supervisor 管理 verdict-silence 与 suite backstop；`run-inner.mjs` 不把叶子预算下发为整份文件的 timeout，也不把共享 AbortSignal 扇出到全部文件 worker。需要 timeout-and-forget 的叶子 proof 在自己的 `test` options 中声明 timeout。
- Long Stroke `waitAny` 只等待 schema 闭集内首个 expectation 事件；winner 原子取消全部 sibling waiter 并返回精确 signal id，禁止用轮询、sleep 或双重独立等待猜测竞态结果。
- Long Stroke `manager-reopened-loop` 绑定 `runtime/manager-assess`：属主再开迭代时该文案可落到 Manager 会话的 step=0（fresh turn）或 step=1（finish-resource 竞态续写）。脚本必须同时声明 optional `runtimeStep = 0`（`review`）与 `runtimeStep = 1`（`suicide`）；缺 step=0 会在 `resolveEntry` 上以 `no-declared-turn` 假失败。`internal` turn 不得声明 `lane`；候选里的 `@undefined` 是未绑 lane 的诊断噪声，不是 inspector 误路由。证明：`e2e-event-ceiling.test.mjs` 钉死 assess@step0/1 → `manager-reopened-loop.0/.1`。

### 3. 物理契约显式声明（`physical-contract`）

`tests/physical-contract.test.mjs` 强制要求唯一的 Long Stroke 物理入口显式声明其所依赖的不可模拟物理契约（如真实子进程生命周期、物理消息 ID 绑定）；无明确物理契约依赖的测试场景必须降级至底层 Pure 或 Temporal 证据层。

### 4. 覆盖率分母完整性守卫（`coverage-gate`）

`scripts/coverage.mjs`、`tests/support/coverage-policy.mjs` 与 `tests/coverage-runner.test.mjs` 确保覆盖率计算分母由 dist 生产文件与 c8 `--all` 语义对齐，报告位于 `.fable-build/coverage/<run-id>/report`，未触达模块以 0% 计入分母，杜绝未加载模块脱离统计分母导致的虚假高覆盖率。测试失败、输入变化或覆盖率数据损坏时均以非零状态安全退出。

### 5. JS 语义契约边界门禁（`js-boundary-gate`）

`tests/js-boundary-gate.test.mjs` 机械化断言语义测试环境与底层实现之间的彻底解耦：
- 验证生产语义测试中零深度导入（deep dist imports）、零混淆导出探测（mangled name lookup）以及零底层编译器表示依赖。
- 确保所有公开给测试的入口均在 `SURFACE_MANIFEST` 中完成完备注册并有明确命题授权。

### 6. 非机械度量政策

行数不是门禁，门禁系统不设置文件行数或尺寸机械限制逻辑，亦不为政策陈述设立机械扫描器门禁，确保质量保障专注于真实的架构语义和规范不变量。



### 8. 独立边界编译与合并 impact 编译的分工

两种验证各管一件事，互不代替：

- **独立 focused compile**（`scripts/compile-owner.mjs`）证明该 compile shard 的**声明自给自足**：scratch 工程只放入该 shard ProjectReference 闭包内的源码，闭包里多进或少进一个真实 provider，scratch 输入立刻变化。某 shard 漏声明一个真实 provider 时，该分片自己的编译是红的——即使 aggregate 或别人闭包里的更大工程依旧绿。「给某 shard 升级/平移归属」同样必须由它自己的输入指纹证明。
- **合并 impact 编译**（`scripts/compile-impact.mjs`）证明**同一批变化的所有受影响消费者兼容**：同一批 delta 先求并集成一次 flat 编译，awaken/组装顺序按 aggregate canonical order；它不证明每个分片的声明足够，也不要求所有不相关消费者都独立重编。

因此「只编极大元分片即证明全部独立边界」不成立：flat scratch 工程没有 `ProjectReference` 与 `open 解析上下文`，一个 shard 自己的闭包缺 provider，在包含该缺失 provider 的另一个更大闭包里照样被补齐成绿灯。反过来「每个分片逐一重编」也不成立：Σ(闭包) 的重复劳动违反一条线只编一次的纪律。正确的最小集合是：**承诺独立的新边界分片各自编译一次**（证明它的声明足够）；**普通影响范围走一次合并并集**（证明消费者兼容）。判断「引用删除是否安全」只比较 `compile-owner --plan-only` 的闭包文件清单是否逐分片一致，清单一致即不必重编。
只有纯代数、幂等、单调、prefix、round-trip、排列或有限事件竞争进入 property testing。generator 只构造输入；property 直接调用注册 production Surface，并以 WHAT 已独立声明的关系或 typed rejection 判断输出。若 expected result 只能通过复制 production algorithm 得到，改用固定反例、类型约束、shared analyzer 或更高层契约测试。

每项 property 保留最小固定正例与边界反例；生成域显式覆盖 production 代数的全部构造。固定独立 seed 与 `numRuns`；`fc.assert` 的失败报告保留 seed 与 shrink path。高风险性质用一个精确错误 mutant 验证 oracle 可红，再以返回的 seed/path 重放最小反例。shrunk counterexample 若代表历史缺陷，转成普通固定 regression；property 继续搜索邻域。

静态 ownership/absence、真实 Host/网络/进程与语义仍有争议的命题不使用 property testing。不得建立全仓 mutation framework，不得把随机次数解释为 exhaustive/comprehensive，也不得依赖传递依赖取得 property runner。

当前 fast-check 组合只覆盖状态空间确实是风险源的性质：durable convergence merge、writer tail truncation、managed session join/reuse、prefix append-only、structured-workflow owner-impact compile，以及 execution failure 的互斥 resolution 与 managed-chat recovery 的 stale/duplicate/terminal 时序不变量。`surface-charter.test.mjs` 只验证 runner provenance，不是产品性质。identity/capacity 的有限域穷举、managed chat closed table/crash cut、clean build freshness、canonical UTF-8/JSON 固定字节与真实 Host/process/network canary 保持 deterministic；把这些机械改成随机输入没有额外错误探测力。

文件名 `*.property.test.mjs` 不授予 property proof 权威。未调用 fast-check 的测试不得宣称随机 comprehensive。测试内 `violations()` 一类镜像公式及其 self-mutation 必须删除，或改为对 production Surface 的固定 counterexample；只有 production output 能使断言变红。

## GAP

- `verification-system-012` / `verification-system-014`（CLOSED）：行数非门禁原则与 Long Stroke 真实物理验收环境约束已闭合，落点 `tests/012.test.mjs` 与 `tests/014.test.mjs`。



