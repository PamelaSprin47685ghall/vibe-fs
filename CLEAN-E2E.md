# CLEAN-E2E：发布链、物理验证与覆盖率精简方案

调研日期：2026-09-13。调查与运行样本起点：master，ec6c363bd；收尾核对 HEAD：7a347df02。本文是待实施方案，不是实施完成记录。两者之间的并发改动另列于第2.4节，不能把旧样本冒充新提交的验证结果。

范围：完整 integration、clean build、唯一 Long Stroke、package、coverage，以及它们之间的重复执行、产物交接和诊断。CLEAN.md 负责上一轮的 unit、治理测试和共享 reporter；本文接着往下查，不重新复制那份施工清单。

本次只新增本文。不删除测试，不修改生产代码，不调整超时，不修改 CI 或正式 WHAT。下文 CLEAN.md 指上一轮已交付的测试方案；收尾时该文件已由外部工作收起，不在当前根目录。本次不恢复或覆盖它，也不回退调查期间出现的其他修改。本文本身包含这五层的完整施工落点。

## 1. 先定取舍

这几层不能靠“日常不跑，发布再跑”就算精简。发布链本身也必须少做无效工作。

推荐终态：

1. 每次正式 release 恰好一次真实 clean Fable 编译。删除 integration 中不必要的第二次全仓编译；保留小型编译器边界 canary。
2. integration 按实际物理依赖分组，不再为每个小文件启动一整层 supervisor。普通参数、文本结构和确定性状态分支下移，Git、进程、文件、Host 协议保留真实边界。
3. E2E 仍只有一个真实 OpenCode serve 生命周期。缩减前置支线和辅助仪式，不删身份、投递、恢复、join、发布收敛的核心见证。
4. package 恰好打一个真实 tarball。校验全部运行时成员与字节，再从仓库外消费公共入口。不再把工作区目录检查、解包后几个文件存在、模块 namespace 是对象冒充完整包证明。
5. coverage 不进入日常或发布的默认关键路径。撤销为百分比而预导入全部生产模块的做法；改为显式调用的完整分母报告。报告生成失败仍失败，低百分比是否阻塞必须与正式规范修订一起决定。
6. 输出只说阶段、结果、耗时和失败原因。成功时不刷逐断言清单；失败时保留第一故障、物理证据和清理结果，不能用静音掩盖错误。

不做：测试成功缓存、随机抽样 E2E、反复重跑直到绿、用增量冒充 clean、扩大超时掩盖争用、全量 npm install 每个用例一次、自建通用调度平台、自建 FCS 扫描、dotnet build。

## 2. 实际入口与证据边界

### 2.1 当前执行链

源码位置：package.json；scripts/verify.mjs；requirements/verification-system/tests/integration/run.mjs；同包 tests/support/integration-node-test-steps.mjs。

```text
format-build-test
  format:check → check → build → unit → integration（日常选择）

verify:release
  format:check → check → build --clean → unit → integration（WXS_RELEASE=1）
  → e2e/entry.test.mjs → scripts/verify-package.mjs

unit --coverage
  freshness → supervisor → run-inner.mjs 的独立 coverage 分支
```

当前 integration 的完整清单有 12 组 node:test 步骤，随后还有 distribution/package 和 verification-system/harness 两个 child。只有 repository envelope 那一组标了 releaseOnly；编译器 canary 与 impact CLI 组没有这个标记，仍会进入日常 integration。不能按 verify.mjs 的注释认定它们已经只在发布运行。

当前 CI 调用 npm run verify:release，Node 配置为 20。本机前轮及本轮使用 Node 26.5.0、npm 11.17.0；本轮 Fable 实际输出版本 5.13.0。跨版本行为必须用相应版本上的正式 fixture 证明，不把在线最新文档的功能当成本机或 CI 已有功能。

### 2.2 本轮运行记录的读法

本轮使用现有正式命令，不插入一次性测试程序来充当验收。完整结果见本文末尾“本轮执行结果”。计时是本机一次运行的墙钟，不是性能承诺；不同输入快照的结果分别记录，不相互覆盖。

首次 clean build 在 Fable 成功后被最终输入检查拒绝：生成物输入在构建期间变化。开始时 Git 只有未跟踪 CLEAN.md，失败后出现 AGENTS.md 修改；差异是旧计划被替换为上一轮测试计划。AGENTS.md 属于 Git 跟踪的 envelope 语料，这与失败原因一致。本次没有改写或回退它。

随后按新的工作区输入重新执行一次 clean build，通过。这是输入变化后重建，不是把同一随机失败重跑到绿。旧失败仍保留在记录里。

### 2.3 已确认的问题

| 位置 | 实际行为 | 取舍 |
|---|---|---|
| owner-impact-compile-cli.test.mjs | 两项测试共调用 CLI 三次；FatalProcess.fs 的正式 plan 已是 full，无参轮次也触发全量 | 将 CLI 路径证明改用小型真实工程；不每轮重复全仓编译 |
| integration-node-test-steps.mjs | 编译 canary 在日常清单中；大量小组顺序启动 | 修正范围并合并同预算组 |
| scripts/warmup-opencode.mjs | --version 后再 --help；部分失败只要有输出就忽略 | 移除 --help 仪式；版本探测与真实 readiness 分工 |
| e2e/entry.test.mjs | 启动前断言多项 oracle 是函数，以及清单 covered=true | 删登记自证；由真正执行的 oracle 判定 |
| entry.test.mjs::waitCaptured | setImmediate 循环，每轮读两次 journal | 复用一个事实等待入口 |
| journal-observer.js | 每次查询全量读、parse、去重、排序、hash | 单次生命周期内增量索引 |
| journal-observer.js::readJournal | 真正的 case 未命中时退回 text.includes | 删除文本兜底，严格区分事实与文案 |
| event-ceiling.js / scenario-driver.mjs | 多个观察者独立监听并重扫同一 journal | 共用只读观察对象，计数与等待读取同一快照 |
| verify-package.mjs | 先解压再从文件树验证路径／重复 | 在归档成员流层验证，再提取 |
| verify-package.mjs | REQUIRED_MEMBERS 只举少量资源；两份名单分别验证，不比较完整集合／字节 | 以本次构建的运行时输入与输出为期望闭包 |
| verify-package.mjs | 链接仓库完整 node_modules；硬编码 Plugin.js；只判断 namespace 类型 | 隔离 runtime/peer 依赖并真正消费 package exports |
| verify-package.mjs | 返回 artifactPath 前已删掉所在临时目录 | tarball 生命周期交给发布属主，不返回悬空路径 |
| run-inner.mjs coverage 分支 | 调查起点 walk 相对路径少上一层；仍在 run() 前预导入生产模块 | 路径已由并发提交修复；本方案删除预加载分母机制 |
| run.mjs coverage 分支 | 调查起点 ROOT 上溯到 requirements，summary 位置与日志不符 | ROOT 已由并发提交修复；保留实际报告路径回归 |

### 2.4 收尾时已有修复，不再重复施工

调查期间外部工作提交了72f7ae248、7a347df02。收尾读到的源码已包含以下变化：

| 已落入源码的变化 | 本方案怎样衔接 |
|---|---|
| build-state.mjs 新增 collectVerificationInputs、diffVerificationInputs | verify 输入快照不再从零重写；测试／发布复用当前接口 |
| verify 导出 verificationSteps，并接受 root、output、logDirectory 注入 | 调度和静默测试使用已有注入面，不另造 runner |
| test-run-state.mjs 与实际 supervisor 共用文件完成判据 | 不重复创建第二套完成谓词；仍核验跨版本结果和真实异常结束 |
| run-inner 导出 drainTestStream，reporter-supervision 和输入测试改为真实调用 | 旧“测试复制自己实现”的问题不再作为未施工任务重复列入 |
| coverage 动态 import 上溯层数、run.mjs ROOT 路径已修正 | 本轮0.458秒失败是修复前样本；不声称当前提交仍必然遇到同一个错误 |

这些变化不是本次编辑产生的。本轮没有对7a347df02重新构建并运行完整 release 或 coverage，不能据源码修复宣称当前覆盖率已经可信。预导入机制仍在，archive 隔离、journal 增量观察、三次真实生产 CLI 编译和 E2E 缩减仍是本文重点。

## 3. 精简必须保住哪些事实

每一类证明只留下一个最便宜、但能真正抓错的落点。

| 必须证明的事实 | 最便宜的主要落点 | 不该为此重复什么 |
|---|---|---|
| .fsi 隐藏实现、漏 provider 编译失败 | 小型真实 Fable fixture | 重编全部生产工程 |
| clean 不留下旧 JS，失败没有有效凭据 | 构建编排测试 + 一次真实 clean | 每个测试自己清 dist |
| 合法图的 impact 并集与 canonical order | 纯 planner 性质测试 | 每张随机图真正启动编译器 |
| JS 相对模块及 named import 可链接 | 一次全 dist linkage | 每个领域重复导入全仓 |
| Git object、ref、worktree 行为 | 临时真实 Git 仓库 | 启动 OpenCode 或访问外部远端 |
| 崩溃后只恢复持久事实 | 真实进程重启 canary | 给全部时序排列都启动进程 |
| Host 分配身份、Hook／ToolPart 顺序与物理投递 | 必要 Host canary + 一个 Long Stroke | 用文本匹配或 mock 代替 Host |
| 双失败后成功、每次恢复不重复受理 | Pure/Temporal 全分支 + E2E 一个代表路径 | 每种错误码独立启动世界 |
| 所有运行资源随包交付且位置独立于 cwd | 真实 tarball 全闭包 + 非仓库消费 | 四组工作区目录存在性检查 |
| 没被测到的生产模块不从 coverage 消失 | 显式源文件集合补零 | 预执行这些模块的顶层代码 |

不能凭“已有 E2E”删小型边界测试。E2E 一条路径无法遍历持久化失败、取消、stale evidence、重复投递等所有边界。也不能凭“已有 unit”删唯一真实 Host 见证。

## 4. integration：逐组处置

路径前缀均为 requirements/。本表不是按名字批删文件；一份文件混合了运行契约与源码形状断言时，按断言拆开。

| 当前组／路径 | 终态 | 具体动作 |
|---|---|---|
| cognitive-environment/tests/integration/resources/prompts.test.mjs | 保留资源读取、组合与语言行为；删机械文案比例 | cwd 独立、法典组合顺序、语言对存在有价值；hanRatio>1.5 不是中文质量证明。固定标题和禁词测试逐条核对 WHAT，不能把所有资源文本当无关注释删掉 |
| behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs | 与资源组同一次 supervisor | 保留实际目录读取／规则身份／内容非空；与 catalog 测试相同的 whole-tree happy path 只留一次 |
| capability-enforcement/tests/integration/plugin/*.test.mjs | 保留真实 plugin 装配契约 | manager-tool-contract、auto-injected-tool、bash-honeypot 的权限拒绝和 Host schema 不能下放成源码 regex；纯参数表移入原领域 unit |
| change-integration/tests/integration/worktree-create.test.mjs | 保留真实 Git | 临时仓库独立；验收 worktree、ref 与失败后状态，不锁 helper 调用写法 |
| change-integration/tests/integration/branch-fast-forward-adapter.test.mjs | 保留真实 Git | 快进、冲突、陈旧目标语义不可删除；准备与上一组共享 fixture 工厂，不共享可写仓库 |
| structured-workflow/tests/integration/owner-project-compiler-boundary.test.mjs | 发布 compiler 组 | 用现有 owner-project-boundary fixture；保留彼此独立的 visibility／source merge 反例，不合并成一个只验成功的大工程 |
| structured-workflow/tests/integration/owner-impact-compile-cli.test.mjs | 发布 compiler 组；缩小输入 | 用 --aggregate/--projects/--props 指向小型 fixture；第二轮无 changed path 仍可测自动探测，但不得因此回到全仓 |
| repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs | 保留真实文件／plugin | 验证实际文件变更和越界拒绝；不为每个参数变体重建完整 plugin |
| speculative-investigation/tests/integration/strength/lifecycle.test.mjs | 保留真正物理部分 | 纯预算状态与 K2 次数边界留在领域 unit；真实 child、取消和清理仍实测 |
| managed-chat-execution/tests/integration/process-restart-canary.test.mjs | 保留真实重启 | 真实 PID 消失、新进程从持久事实重建；不能改成同进程清空 Map |
| durable-events/tests/integration/persist/{object-identity,leave-unread}.test.mjs | 保留真实 Git／I/O | object identity 对照 Git binary 有独立价值；leave-unread 必须仍能观察不该发生的读取 |
| durable-convergence/tests/integration/persist/dumb-server.test.mjs | 保留协议＋真实本地服务 | 远端使用本地可控服务器；源码含 git/runReferenceTransaction 的断言不能冒充协议执行 |
| degeneration-guard/tests/integration/loop-envelope-repository.test.mjs | 发布 repository oracle | 保留一份完整真实语料独立验证；小语料边界与数学关系留在 unit |
| distribution/tests/integration/package/{contents,layout,import,resources}.test.mjs | 拆出行为后退役重复工作区检查 | package 工作区模拟由真实 tarball 接管；资源加载算法、公共出口等小型行为迁回原 owner 测试 |
| verification-system/tests/integration/harness/run.mjs | 保留执行可靠性，退役通用治理扫描 | 与 CLEAN.md 第 8、11 节衔接；不要把已经决定删除的规则移到发布躲起来 |

### 4.1 合并调度，不合并可变世界

将现有 12 组顺序启动收敛为少量固定组：普通 adapter、compiler、repository oracle；harness 在完成自身精简前保留独立 child。普通 adapter 内仍按文件进程隔离，使用有限 concurrency。compiler 与真实 Host 不同时争抢资源；同一临时目录、同一 Git ref 或共享 npm cache 写入不并发。

程序落点：integration-node-test-steps.mjs 保留实际文件清单及必要 releaseOnly 信息；integration/run.mjs 负责分组调用 superviseNodeTest。不要再建第二份“谁属于哪个组”的 JSON 登记册。

全体已发现 integration 文件必须恰有一个实际入口；选中的文件必须全部完成。保留 assessIntegrationEntryCoverage 的集合关系与反例，删除精确组名、打印顺序和“至少多少组”的测试。

concurrency 的初始候选为 1、2、4，再结合机器可用并行度测量。数字只是实验参数，不是新增规范；比较墙钟、峰值内存和冷启动可靠性，不能只看 CPU 核数。保留已有叶子 timeout 与外部静默判据，不把组预算复制给每份文件。

### 4.2 warmup 的取舍

当前 warmup 先 --version 再 --help；本轮完整 integration 的日志记录两次合计约 1.016 秒。--version 约 0.569 秒，余下不是零成本。

先删除 --help。它既不证明 serve 可用，也不证明 plugin ready，当前错误处理还允许“有输出的失败”继续。保留一次精确版本探测，结果供本次所有 Host 证明复用；真实 serve 的 readiness 仍由 Host 自己的启动事件与 health 验证。不要把版本探测叫“物理启动已经验证”。

若后续冷机对照证明版本探测只为重复展示 package 版本，也可删除独立 warmup 命令，由第一个必要 Host 启动承担冷启动成本。但不得靠放宽 timeout 达成，必须先有 cold-start 回归。

## 5. clean build：删重复编译，不删可信发布

### 5.1 clean、cold、增量是三件事

clean：删除旧生产输出，完整源码真实编译，生成运行时 artifact，再完成后置验证。cold：连依赖下载、restore 和工具初始化缓存都没有。当前 --clean 会复用 restore assets；日志 noRestore 不代表复用了旧 JS。没有必要每次 release 清 NuGet、npm cache 或 SDK store。

日常增量与发布 clean 的生产字节应一致；但不应每次发布先增量、再 clean、再比较整仓两遍。把“增量与 clean 等价”放在小型内容变化 fixture 和构建算法变更验收中，正式 release 只用一次 clean 的结果。

本轮成功样本：Fable 解析报告 1534 个 source files，传入规划的仓库 compile items 为 1496；后置 linkage 报告 818 个 emitted modules。这三者统计对象不同，不写成同一个“文件数”。

### 5.2 编译器测试的具体修改

owner-project-compiler-boundary.test.mjs 现有 9 个直接编译分支，以及 flat-closure 的正反例，各自承担不同命题：公开入口、缺引用、source merge、传递合并、顶层 private module 的局限、private binding、.fsi 暴露与隐藏、signature-only 的不可用。保留这些语义，不以“都是编译器测试”合成一个必绿 fixture。

真正要删的是 owner-impact-compile-cli.test.mjs 对真实源码的重复全量工作：

本轮额外执行现有正式 --plan-only 命令核对 Foundation/FatalProcess.fs，得到 mode=full、reason=impact-exceeds-full-threshold。当前 planner 对源码变化展开反向消费者，该公共基础模块影响广，超过默认 full threshold。两项测试共三次 CLI 调用，其中显式 changed path 的两次都不是标题声称的 focused；第三次无 changed path 又走自动全量。此结论来自代码和真实规划结果，不是仅凭 48 秒的耗时猜测。

保留这个 full fallback 本身：它可能正是正确且便宜的编译选择。应该换的是测试夹具和断言，而不是为了让测试看起来快而削窄合法影响范围。

1. 复用或补充 requirements/structured-workflow/tests/fixtures/owner-project-boundary 下的小工程。每份源只承载一个真实依赖关系。
2. CLI 两轮都显式传 --aggregate、--projects、--props、--scratch、-o，根路径在临时目录下。第二轮省略 changed path 可以保留，以验证自动探测。
3. 第一轮 emit 后修改一处有可观察输出的 fixture 实现，再执行第二轮；验证 emit 内容确实变化，不只匹配 “Started Fable compilation”。
4. 编译-only 不写权威 build manifest，通过临时根完整输出集合检查；不要只检查一个可能根本不是实现实际写入位置的自选文件名。
5. 正式 release 的 clean 已证明真实生产全量可编译。若仍保留生产 focused canary，只留一个真正验证声明闭包与输出布局的样本，不能追加无参全仓轮次。

### 5.3 依赖图和 fingerprint 的计算

scripts/lib/owner-compile.mjs 同一调用内复用解析后的 aggregate、shard DAG、source owner 和反向依赖。图遍历使用 visited set，闭包并集后按 aggregate canonical order 发射；不要为每个受影响消费者单独编译。

scripts/lib/build-state.mjs 与 build.mjs 当前多次读取／hash 重叠文件。构建开始时共用一次按路径缓存的输入读取，分别派生 compiler、generated、artifact digest；结束时重新读取相关输入验证变化。结束快照不能复用开始缓存，否则又成假新鲜度。

内容 hash 是正确性依据；mtime、size 只能作为性能统计，不能独立授予复用资格。Git 语料枚举失败不能退成空列表后继续成功：collectGeneratedInputs 当前 catch 后 files=[] 的路径应改为显式失败，并用真实枚举失败 fixture 验证。

### 5.4 产物交接

唯一权威仍是构建结束后原子提交的 manifest。编译失败、资源生成失败、linkage 失败、输入中途变化，都不能留下可消费的成功凭据。

后续 integration、E2E、package 在独立 CLI 模式先调用 assertBuildFresh；在同一个 verify 调度中也必须绑定同一份 build receipt，不能只信“前一步跑过 build”。使用第2.4节已经落地的 collectVerificationInputs／diffVerificationInputs，不另造收集器。

不额外设计分布式事务或复杂双目录发布。先用当前 build lock 约束写者、内容凭据约束消费者、开始／结束快照识别运行期间污染。发现变化明确报告 INPUT_CHANGED，本次不授予 release 通过；禁止自动再编译后继续把前半段测试算作新字节的证据。

## 6. repository envelope：保留独立证明，减少重复劳动

源码位置：scripts/lib/derive-loop-detector-envelope.mjs；loop-detector-repository-corpus.mjs；requirements/degeneration-guard/tests/integration/loop-envelope-repository.test.mjs。

真实仓库派生是产品机制，不是可以随便关掉的构建统计。生产构建与独立 oracle 各做一次是两类证据；同一测试反复读取语料、重复 tokenize、重复排序才是浪费。

分工：构建生成真实 artifact；unit 用小语料固定数学边界、tokenize 等价、分位数和 fingerprint 失效；发布 oracle 对本次完整语料独立核对一次。不要把 expected 直接调用生成器后和自己比；相反，也不必为每个参数断言重算完整仓库。

一份 oracle 内先读取一次不可变 corpus，再分别喂给生产派生函数与独立参考计算；两者可共享原始输入，不能共享待验证的答案。分位数计算一次排序后取所需位置；最大值可线性求。并行编码必须保留与整流编码一致的边界证明、顺序重组和失败 worker 回收；默认 worker 数要结合实测，不能启动一组 Node 线程后又与编译器并行争核。

仓库文档也是当前语料，修改 AGENTS.md 可能改变 envelope。这是现有产品输入选择，不可为了提速偷偷排除文档；若要改变语料范围，须由 degeneration-guard 修订合同并独立校验行为。

## 7. E2E：一个世界，不等于把所有测试塞进一个世界

### 7.1 先修本轮红，再删冗余

本轮实际执行唯一入口，8.920 秒后退出 1。首个故障为：

```text
reason=no-declared-turn
lane=humanroot-manager|manager kind=chat step=0
lastUser="null"
MSG-ROLES: system
```

日志还列出了尚未完成的 orch、manager-loop、coder 步骤。因此这不是一份成功 Long Stroke 耗时，更不能把 8.9 秒作为当前完整 E2E 基线。日志位于 .fable-build/clean-e2e-long-stroke.log。

现有证据只能说明 HumanRoot 前置支线收到不符合声明的请求，不能断定一定是 mock 错了，或一定是 production 丢了消息。实施第一批先补最小重放：

1. 从同一 session 的已受理 user message、authority root、transform 输入／输出和 provider request 建立精确关联。保留物理 message ID 的等同关系，不把完整敏感正文默认打印到终端。
2. 若生产变换错误丢失合法 authority user message，在其领域 owner Surface 上补回归；expected 明确保留哪一个 root，而不是复制变换算法。
3. 若请求是允许的 internal/title 调用，证明其确切 request kind 与生命周期，再修 mock 分类或补精确声明。不能增加任意 system-only 请求都返回成功的兜底。
4. 若是支线间绑定串扰，用各自 session 与 logical run 的真实关系复现。不能通过删除 HumanRoot 测试把冲突藏掉。
5. 原始反例先在低层稳定变红，再修实现，最后运行一次原 E2E。没有因果修复前，不以多跑几次碰到绿灯验收。

### 7.2 现有场景怎样缩

实际源码：e2e/entry.test.mjs、e2e/scenarios/long-stroke.toml、e2e/support/long-stroke-oracles.mjs。当前不是只有一条简短主线：preFlow 已有 Strength、Inspector／Bookkeeper、HumanRoot 多段 canary，主线再运行 Orchestrator／Manager 的恢复和发布。

| 当前段落 | 必须保留的物理证据 | 可以下移／删掉的部分 |
|---|---|---|
| Strength owner + Replica | 真正 child 被启动，owner 不被阻塞，观察 horizon 正确结束 | 精确 K2 状态组合、每个重复与 stale 分支在 replica-transform 等 unit 中完成；保留合法 race tail，不把正常第二请求误杀 |
| G2 Inspector Q1/Q2/Q3 与 batch | 同属主复用真实子会话；批量调用物理上正确合并 | 同输入／不同问题的纯索引、格式与重复断言下移；减少一轮前必须说明它没有独立身份、复用或批量含义 |
| G6 Bookkeeper + later Coder fetch | session.deleted 引发真实捕获，另一会话能从真实持久化读取结果 | 静态句子、shelfmark 算法、相同结果的重复全文扫描下移；不能用仅在内存已存在的对象冒充 cold fetch |
| HumanRoot manager loop | HumanRoot 与 AgentOwnerRoot 不同；同一 physical session 的 Continue→新一轮→Accepted | 分数向量排列、全部 blocker 组合下移；保留至少一条真正 HumanRoot 再开迭代路径 |
| provider failure→failure→success | 两次不同 failed ProviderRun、两份 durable recovery claim，各最多一次物理受理，最后一次成功 join | 错误码矩阵、超时／取消／stale／重复的组合用 Pure/Temporal；不能把“两次”改为“一次” |
| blocked join + user_message | 真实 ToolPart 已 running 后注入，外部消息唤醒正确 join，child 不产生重复 completion | 纯等待排列与返回 shape 下移；不能只等 provider 回答中出现 join 就注入 |
| assessment／retirement／conflict／publish | 低分阻止发布；真实 worktree 冲突；同一任务恢复；最终目标 ref 与 Published 一致 | 纯评分矩阵和文本 nudge 下移；四轮是否可减须由领域协议决定，不能直接把脚本中间几轮删掉 |
| MagicTodo A/E/G/H wrapper | Host 消息 ID、工具定义／参数、after hook 时的 ToolPart 状态等真实边界 | 删除“oracle 名字存在”和 covered=true 自证；wrapper 保持观察者，不修改被测请求或结果 |

不要为了减少 E2E 行数，另外启动三个独立 canary 世界。下移是改用更便宜的确定性或单边界证明，不是从入口文件挪到 helper 后照样启动相同物理世界。

### 7.3 可以直接删除的仪式

entry.test.mjs 顶部一串 typeof ADVERSITY_ORACLES.* === 'function'、typeof CUSTOMS.* === 'function' 与 ADVERSITY_CHECKLIST.every(covered) 不证明执行结果。删掉它们及专用 covered 登记。场景需要的 custom 缺失应在解析／调用边界直接失败，真正的 oracle 由实际参数与事实判定。

runStaticGate 中保留有价值的禁止非法 watchdog feed 规则，但其真实仓库扫描只执行一次。入口启动再扫描自己的源码，不应成为每次物理启动的仪式。独立 E2E 命令仍要检查场景能解析、custom 能解析、生产入口与当前构建有效；这是执行前提，不是重复治理。

scenario-paths.js 目前按 cwd 和父目录试找 Plugin.js，还保留 mimocode/mimotui 名称。当前 verification-system 测试树检索只在这个映射里发现后两者。实施时再查全仓消费者；无实际入口就删，不保留模糊 fallback。默认生产根由模块位置解析；确有外部包消费需要时显式传 productionRoot。未知 variant 要拒绝，不自动退回 opencode。

### 7.4 断言只依赖自己的身份与阶段

当前主流程用全世界累计 AssessmentCommitted=3 等常量补偿 HumanRoot 前置的两份事实。这把不相关支线绑在一起。删一条前置流程就要到处改计数，正是脚手架感的来源。

改为明确查询条件：case、sessionId、logicalRun、必要时 providerRun／incumbencyId，并记录阶段开始时的观测边界。等待某事实到达可以使用新增量；涉及“只能一次”的命题必须在终态验证精确数量和 identity，而不是把 eq 改成 gte。

world 总 event ceiling 仍用于抓 runaway，不用它证明产品 exactly-once。单条支线的成功与世界总量分别统计。699 journal、3351 SSE 是当前配置，不是本方案新的目标值；减少支线后可按有效场景收紧，不能为掩盖循环而放宽。

同一 PID 的 durable continuation 不能证明 crash recovery。long-stroke.toml 明确禁止重启；真正的 process-restart-canary 必须继续存在于 integration。

## 8. journal 观察：从重复全仓扫描改为增量读取

### 8.1 当前开销与错误风险

journal-observer.js::readLocalSnapshot 每次读取所有 writer 文件，将完整行全部 JSON.parse，按 event_id 去重、排序，再计算 SHA-256。countFactCase、factPayloads、storeTip、readJournal 和 journalFactTail 都可能再次触发全量读取。watchJournal 自身也读 snapshot；event ceiling 与 waitFact 各有 watcher。

假设日志已有 B 字节、N 行、W 次唤醒，当前最坏工作近似 O(W×(B+N log N))。问题不是 699 行单次处理贵，而是一段物理等待里对同一批旧行重做很多遍。

另一个问题更重要：完整行 JSON 错误被忽略，readJournal 在 case 未命中时用 text.includes 补算。诊断字符串中写了某个事实名，不代表事实发生。精简时不能保留这种“宽松观察”换速度。

### 8.2 最小接口

保留 journal-observer.js 作为唯一实现，不额外建观察框架。setupScenario 为本次物理 world 创建一次对象，teardown 关闭。建议接口：

```js
const journal = createJournalObserver({ workDir, readFiles, watchDirectory });
await journal.refresh();
const before = journal.position();
const events = journal.select({ caseName, sessionId, logicalRun, after: before });
const release = journal.subscribe(onCommittedEvent);
await journal.close();
```

readFiles/watchDirectory 是可测试的 I/O 边界，不是生产业务副本。默认使用现有文件系统；测试用受控文件与通知。select 返回已解析的只读事件，不自己推断业务成功；业务 oracle 仍由所属测试判断。

内部只保留必要状态：每个 writer 的路径、文件身份、已读字节位置、未完成尾部；event_id 到完整事件的 Map；case 到事件的索引；诊断用有限事件尾部；等待订阅者。不要同时维护三套镜像状态机。

### 8.3 读取算法

1. 初始化枚举现有 .ndjson，每个 writer 从头读一次，记录文件身份与已消费的完整前缀。
2. 新通知只表示“可能有数据”，不代表成功，更不能给 watchdog 续期。合并同一轮通知，refresh 时补读新增字节。
3. 按字节处理换行与 UTF-8。未完成的最后一行保留，不解析，不计数；多字节字符被分到两个块不能变成替换字符。完整行 UTF-8／JSON 非法应报告路径和偏移，不能跳过后继续绿。
4. 发现新的 writer 文件时初始化它。已消费的完整前缀被截断、文件身份替换、同 event_id 不同字节应立即失败。正常恢复只截断尚未消费的残尾时不误报。
5. 同 event_id 同字节可按既定语义去重；原始追加数、唯一事件数和 SSE 帧数分别命名，不能偷偷改变当前 ceiling 的分母。
6. 每个新事件只 parse 一次、索引一次；后续查询按 case 和 identity 过滤。若查询密度尚低，先用 case 索引，不急着给每个字段建复合索引。
7. 正常结束与失败诊断前最后 refresh；对已消费完整前缀重读校验一次，抓同 inode、同长度的原位改写。不能只用 size/mtime 把任意文件改写宣称为追加。

这样旧字节通常读一次，结尾复核一次，主要复杂度变为 O(B+N+Q×候选事件数)，另有通知时 writer 目录枚举成本。这里的复杂度是设计分析，不是已经实测的加速比。

### 8.4 删排序和全文 hash 的边界

用于纯 wake 去重的 token 可改为观察版本计数；不能将它冒充可校验内容摘要。业务需要比对内容或前缀时仍用 hash。

按 event_id 字符串排序不等于因果时间顺序。查询结果若需要顺序，使用已存在的 writer sequence／业务因果 identity；诊断尾部可以明确标成“观察到达顺序”，不能声称是全局提交顺序。最终批量 oracle 可读取一个冻结 snapshot 并复用，不再每个 assert 重读磁盘。

### 8.5 必须直接测试实际观察器

新增 requirements/verification-system/tests/journal-observer.test.mjs，测试真实导出的 observer：多 writer 交错、尾行分块、UTF-8 分块、通知合并、通知缺失后显式 refresh、同 ID 冲突、完整行损坏、文件替换、前缀截断、最终刷新、关闭后不回调。

永久反例必须包括：payload 的普通 prose 含有 FailureRecorded，但实际 case 不是它；count 必须为零。不能在测试文件里复制一套计数器后只证明副本正确。

## 9. 等待、SSE 与进程清理：少轮询，不能少监督

### 9.1 一个事件源，多个精确等待者

scenario-driver.mjs::awaitFactBarrier、entry.test.mjs::waitCaptured 和 event-ceiling.js 都消费同一 journal observer。先查快照，再注册订阅，再查一次，避免检查与订阅之间漏掉事件；完成或失败后原子取消 waiter、timer 和监听。

保留低频、有界的物理兜底 refresh，以应对 fs.watch 丢失或合并通知。兜底只重查条件，不产生成功事实、不续期 watchdog。删掉 waitCaptured 的 setImmediate 忙循环，以及每次 wake 重建全部 watcher 的路径。

wakeOnJournal／wakeOnSignal 当前获胜后并未取消已经创建的 delay。修改 delay-port 使延时可取消，等待返回时清除败方；不要积累“早已输了、仍会触发”的 timer。优先复用现有可取消接口，不另造 Promise race 通用库。

### 9.2 区分期望受理与物理运行

provider 回答出现某工具，只证明模型生成了调用；ToolPart running 才证明 Host 已进入执行。awaitManagerJoinRunning 这类物理屏障必须保留。先注册屏障，再施加外部 user_message／abort，禁止 sleep 估计执行已开始。

awaitSessionsByAgent 的 GET /session 是身份来源，session.created 可以触发重查但不能自己替代快照。先快照、订阅后快照、按相关事件重查，最后保留短 guard；不需要 50ms 无差别持续请求。

SSE 仍只有一条观察连接。event-probe.js、event-probe-awaits.js、event-probe-queries.js 可按 type/session 建轻量订阅路由，避免每条事件唤醒所有无关 waiter。先量 waiter 数；没有瓶颈不加多层索引。任何路由优化都要保留全量计数和诊断 ring，不漏掉未知但有效的 Host 事件。

### 9.3 失败之后也必须结束干净

唯一结束入口接收 success、assertion failure、strict mock fatal、watchdog、event ceiling、SIGINT／SIGTERM。它记录第一故障，停止发新请求，取消等待，关闭 SSE／本地 mock，终止本次拥有的 Host 进程组，再等待 close。清理异常作为附加错误，不覆盖原始原因。

优先复用已有 process-lifecycle.js、spawn-ledger.js、reaper.mjs；不要新增另一套进程管理器。只能处理本次登记的 PID／进程组，禁止按名字杀所有 node、dotnet 或 opencode。

cleanup 必须有独立上限；主断言失败不能无限等 dispose。达到上限时先保留诊断，再对本次进程组升级强制回收。强制回收是失败，不把全断言曾通过解释为成功。SIGKILL 无法靠 JS finally 处理，保留独立 reaper／CI 容器边界的职责，不承诺任何情况下零残留。

本轮 E2E 日志保留了第一 mismatch 和事件尾部，但没有独立验证进程树完全回收。因此本次不声称失败后清理已经正确。

## 10. package：一次打包，证明真正交付的内容

### 10.1 哪些重复可以删

distribution/package 四份小文件目前检查的主要是工作区布局，而最终 verify-package 才执行真实 pack。终态把“入口与资源是不是在交付物里”统一放到真实包验证。目录扫描、资源文本内容的领域测试留在合适 owner，不再保留模拟安装的壳。

不要删除全部 packaging unit。成员校验器接受／拒绝什么、缺一份运行资源是否报错、字节不一致是否拦截，都应保留小型固定反例。但不直接对内部 normalize helper 堆几十个写法断言；主要通过 validateArtifact 的公开结果验证。

### 10.2 成员期望从本次真实输入派生

现有 REQUIRED_MEMBERS 只抽举了 manager、common-law 和一个 enforcer。它证明不了其他全部角色、全部语言、Sphinx 入口或任意新增资源都在包里。

令 D 为有效 build manifest 的全部 dist 文件；R 为本次发布快照中的 resources 文件；M 为 npm 固定包含、且项目允许的根文件（package.json、README、LICENSE，依实际 npm 规则核对）。期望是 D∪R∪M，而不是手抄几个例子。

实际归档成员必须与期望集合一致；每个普通文件的 bytes digest 也必须相同。资源目录被 .npmignore 意外排除、包里混入旧 JS、同路径内容被 pack lifecycle 改写，都应失败。

build-state 的 resources selector 当前使用 Git 跟踪集合。正式 release 必须拒绝运行时树里未纳入声明的额外文件，或显式把实际将打包的运行树绑定进快照；不能一边忽略 untracked 资源、一边让 npm 自动把它打进 tarball。

M 是 npm 语义边界，不是任意根目录文件都放行。不要把文档计划或内部报告加入包白名单。README/LICENSE 等自动包含行为按当前 npm 官方规则与固定 fixture 核实，而不是凭想象写枚举。

### 10.3 先验证归档，再提取

当前顺序是 tar -xzf 后 walkExtractedDirectory。解包后的文件树看不出原始归档是否有重复路径；walk 还会跳过 symlink。路径安全和归档语义不能等提取完才检查。

推荐使用一个维护中的 TAR 解析依赖，作为显式 devDependency，只用读取成员与受限提取接口；不要自己解析 512 字节 TAR header，也不要依赖 npm 私有传递依赖或解析 tar -tv 的人类输出。实施时固定兼容当前 Node 的版本，并按文档明确处理 strict、warning 与提取过滤行为。[N4]

第一遍流式校验成员的原始路径、类型、重复、完整性及内容摘要；拒绝逃出 package 根的路径、链接和非预期特殊条目。第二遍只向新建临时目录提取已验证归档。若库支持同一遍受限提取且能在写入前完整验证条目，可合成一遍，但不得先写再查。

成员集合可用 Set 比较，字节摘要流式计算；O(文件数+总字节数)，不按每个 REQUIRED_MEMBERS 重新扫描归档。归档来自本项目 npm pack，不需要再建设通用不可信文件上传防护系统；只守住分发所需的边界。

### 10.4 一份 tarball 的生命周期

verifyPackage 返回有效 artifactPath、tarball digest、build input/output digest 与内容统计。成功时不立即删掉它再返回路径。由 verify 的当前 run directory 持有 tarball，所有消费都引用同一份文件；失败时保留诊断和必要反例，非必要提取目录及时清理。

若将来接入真正 publish，发布的就是已验证 tarball，而不是重新运行 npm pack。当前仓库 private=true，本方案不自动 npm publish，不改仓库发布权限。

实际 pack 应使用一次 npm pack --json --ignore-scripts --pack-destination <runDir>，因为 compile 已由 build 唯一拥有。若项目将来确实需要打包 lifecycle，应明确移入构建阶段或在正式合同中声明，不能暗中重编被测试过的生产字节。JSON 缺失、进程被 signal 终止、输出不完整全部失败。[N3]

### 10.5 消费公共入口，而不是 namespace 类型

当前 importCode 只要 import 返回 object/function 就通过；ESM namespace 本来就是对象，即使没有合法 plugin export 也可通过。改为在非仓库目录用包名 import('wanxiangshu')，使 package.json exports 真正参与解析；验证公开 default export 的形状，并通过已有公开接口完成一个无额外 Host 的最小消费。

资源检查通过包内真实 resource loader 读取至少一个不同角色及两种语言；全资源完整性已经由成员集合与 digest 保证，不在消费阶段重新逐份加载全部语义资源。Sphinx 等独立入口做最小可启动／协议 smoke 时只验证其边界，不重放全部领域功能。

consumer 必须自然结束，不能立即 process.exit(0) 把 import 引入的泄漏 handle 藏掉。进程外监督器拥有退出上限、日志与清理；positive exit、signal、未清空 handle 分别报告。

### 10.6 依赖隔离：不要借整个开发环境

第一步即删除指向仓库完整 node_modules 的软链接，它会把 devDependency 偷渡给交付物。推荐准备一个仓库外的 runtime dependency root：复制当前 package.json 与 package-lock.json，由 npm ci --omit=dev --ignore-scripts --no-audit --no-fund 构造 runtime／peer 依赖树；复用 npm 下载缓存，不自写依赖闭包解析器。[N6]

将验证过的包提取到该 root 的 node_modules/wanxiangshu；另建 consumer/ 子目录，放置名称不同的最小 package.json 和消费脚本。消费脚本的 package scope 是 consumer，不是安装依赖时那份同名根 manifest，避免 Node self-reference 错误指向根目录未安装的 dist。所有目录均在仓库外，解析链不能回退到开发 node_modules。

消费环境显式隔离 HOME、USERPROFILE、XDG 配置路径，清除会预加载开发代码或改变模块解析的 NODE_OPTIONS／NODE_PATH；必要测试环境由调用者逐项传入。不能一边隔离目录，一边让继承环境把开发工具重新注入消费者。

peer 与 dev 同名条目的实际 omit 行为、native 依赖是否需要 lifecycle，先用固定 fixture 验证。若依赖确需安装脚本，在隔离的依赖准备阶段按项目允许的依赖执行并记录；不能静默跳过后宣称安装兼容。该准备证明的是 lock 下 runtime dependency 环境，不是任意 npm 用户环境的所有版本组合。

这个隔离消费只做一次，不为四个旧 package 小套件重复安装。先测 npm ci 的真实开销；需要缓存时只缓存按 lock、Node、平台、架构和必要 ABI 绑定的依赖材料，不缓存 plugin import 成功结果。缓存不完整就明确重建或失败，不能回退到开发树。

### 10.7 保持当前发布顺序，暂不把 package 与 E2E 绑成新框架

本轮推荐继续 clean→unit→integration→E2E→package。E2E 消费当前 dist，package 用完整内容 digest 证明交付字节与它相同。这样无需同时改写所有 E2E helper 的静态 dist import，也不需要在“瘦身”时引入庞大的 artifact 注入系统。

将来确有收益再让唯一 E2E Host 从解包目录加载 plugin；那属于一次明确的流程顺序和 productionRoot 变更，不能在本批偷偷加第二个 Host。无论顺序如何，E2E 已失败时正式 release 必须停止；本轮后续单独运行 package 只是调研它自己的路径，不构成一次成功 release。

## 11. coverage：删百分比治理，留下可信的查漏工具

### 11.1 本次不是覆盖率偏低，而是根本没跑起来

现有命令：node requirements/verification-system/tests/run.mjs --coverage。

本轮 0.458 秒退出 1，错误是 run-inner.mjs 动态导入了不存在的 requirements/scripts/lib/walk.mjs。625 个测试文件虽然已经被父进程发现，测试尚未开始；“0 passed, 0 failed”后面仍是 runner exited 1，不是通过。

修复前两个明确的路径落点：

1. tests/support/run-inner.mjs 到根 scripts/ 的相对路径应上溯四层，调查起点只有三层；7a347df02已改为四层。最终删掉预导入分支后这个动态 import 不再需要，不能只改路径就声称 coverage 正确。
2. tests/run.mjs 中 ROOT 原来上溯两层得到 requirements，而日志写的是根 artifacts/coverage；7a347df02已修正 ROOT。报告路径仍应由仓库根构造并交给子进程，用真实入口回归防止再次漂移。

本轮没有生成可信的覆盖率百分比，也没有测得完整 coverage 的实际开销。以下是实现设计，不是优化前后的测量结论。

### 11.2 不再靠预导入把模块塞进分母

现有 run-inner.mjs 在 run({ coverage: true }) 前调用 preImportModules，把全部 dist 生产模块导入当前 runner 进程。实际测试则由隔离的文件子进程执行。这种方法把“把文件列入分母”和“执行文件顶层代码”混为一件事；又没有真实 CLI 证明父进程预加载记录一定进入最终覆盖数据。

预加载还会提前执行模块初始化，可能启动资源、读配置或报错。一个根本不在测试路径上的生产模块，不应该为了统计而被运行一遍。即使导入成功，也不能解释为它的业务行为有覆盖。

终态删除 preImportModules、全量预导入失败 gate 和相应自证测试；保留整个生产文件集合的分母，以及未触达模块补零。VERIFICATION-SYSTEM-011 当前明确规定预导入，必须先修订机制条款，再落实现，不能直接删除代码违反现行合同。

### 11.3 选现成覆盖工具，不再维护一套 V8 报表引擎

推荐增加锁定版本的 c8 作为直接 devDependency，复用 Node 的 V8 覆盖记录与成熟报表转换。c8 提供 --all/--src、include/exclude、JSON 报表和独立临时目录；其 --all 可以把未触达源文件按零覆盖纳入，而不要求测试预导入。[N1][N2]

新增薄入口 scripts/coverage.mjs，package.json 增加 coverage 命令。它只做四件事：校验本次 build receipt；准备本次数据目录并启动一次现有 unit runner；核对报告分母和本次执行结果；输出报告位置。不要另写 node:test runner、Istanbul range merger 或 HTML 可视化系统。

coverage 的目标链是：

```text
npm run coverage                         新入口，尚未实现
  assertBuildFresh
  → 本地锁定 c8，以独立 raw/report 目录执行现有 unit runner 一次
  → 报告文件集合与 build receipt 核对
  → 再次确认输入／输出未变
  → 一行统计 + 报告路径
```

实际 c8 参数由脚本显式给出，不搜索用户 HOME 中的配置，不使用可能联网安装的 npx 自动补依赖。安装版本须验证支持 CI Node 20 和本地使用版本；本文不把维护者当前 main 分支等同于已经安装的 npm 版本。

不启用实验性 reporter，不增加第二套 coverage 配置文件。旧 run.mjs --coverage 在同一批终态删除；不长期保留两条看似等价的实现。

### 11.4 报告范围必须说人话

第一阶段只报告“unit 执行范围对生产 emitted JS 的覆盖”。分母是当前 dist 内项目生产 .js 文件，排除 Fable runtime 和依赖，不包含 tests、scripts。排除按路径段与明确规则处理，不能因文件名恰巧包含 fable_modules 字样就吞掉生产文件。

这不是 F# 源码行覆盖，也不是完整产品的 E2E／Host 覆盖。OpenCode 及其运行时的代码不属于本包生产分母。默认不为 coverage 再启动 integration、Long Stroke 或外部 Host，也不把它们的缺失数据描述成已测。

c8 会处理 source map。当前方案先固定 emitted JS 口径：实施时检查真实 Fable 输出与 c8 报告路径；若 source map 导致报告映射到 .fs，必须明确配置并用 fixture 验证，不能把 .fs 和 .js 两种分母混加。暂不建设 F# 源映射覆盖平台。[N1]

库默认 excludes 不能成为静默遗漏的理由。报告中的规范化文件集合必须与预先计算的生产集合一致；多出或缺失任何生产文件都说明报告口径有误，应退出非零。按路径去重，不按 basename；两个目录里的同名文件不是同一个模块。

### 11.5 原始数据只来自本次运行

每次使用独立的 .fable-build/coverage/<run-id>/raw 和 report，禁止把上次 JSON 混入当前报告。数据目录是生成物，不写进 requirements，也不进入 npm 包。

NODE_V8_COVERAGE 应只传给本次受监督执行树，不永久写入全局环境；Node 可以将这个变量传播给子进程，但项目自身重设 env 的分支仍要以实际 runner fixture 验证。[N2]

不要对各文件／进程百分比求平均；覆盖区间合并交给同一个 c8。不要因为 --all 能生成一堆零值文件，就允许“测试没有启动、没有一份有效运行记录”的报告通过。

对 runner 故意启动并杀死的负向测试夹具，分别记录“预期被杀的 fixture”和“应该正常完成的测试文件”。前者验证监督器能力，不要求它自己的覆盖进程优雅退出；后者丢失、超时或流未排空必须使本次 coverage 失败。不能把所有 signal 都无视，也不能把预期负向夹具的 signal 当成整个 suite 失败。

### 11.6 撤销全局 80% 阻塞，不撤销测试失败

推荐正式取消全局 80% lines 的硬门槛。保留 lines、branches、functions 和未覆盖文件报告，用来找缺口，不作为发布通行证。Fable 生成代码的一个百分比既不能代表恢复、身份和副作用边界已验证，也不值得倒逼作者写只为“执行到”的测试。

低覆盖率：显示真实数值和未触达模块，报告成功生成。测试断言失败、freshness 失败、coverage 数据缺失或损坏：退出非零。退出码表达“此次测试与报告是否正确完成”，不能把“报告生成了”冒充“测试通过了”。

这会改变现有验收判据，必须与 VERIFICATION-SYSTEM-010/011 的正式修订一并落地。修订前现有阈值不能悄悄降为 advisory。本方案只提出取消这项低收益治理，不宣称它已经取消。

coverage 目前就不在默认 verify:release 中，因此把它保持为显式入口不会节省当前 release 的一秒。收益在于减少无效预加载、避免今后把统计错误接入默认门禁，以及把维护精力还给真实行为回归。

### 11.7 必要的真实 coverage 回归

新增 requirements/verification-system/tests/coverage-runner.test.mjs，运行真正的 coverage 入口针对临时小项目，保留以下反例：

| 输入 | 必须看到的结果 |
|---|---|
| 一份被测文件、一份未导入文件 | 两者都在分母，未导入文件为零 |
| 未导入文件顶层抛错 | 不执行该顶层代码，仍作为未覆盖文件出现 |
| 同名文件分处两个目录 | 报表两条独立记录 |
| 主测试与一个正常完成的子进程覆盖不同分支 | 合并后的同一文件包含两条实际路径 |
| 测试断言失败 | 命令失败，即使已写出报告 |
| 内层 runner 在测试启动前崩溃 | 明确 infrastructure error，不接受全零报告当成功 |
| raw 数据为空、JSON 损坏或错误 run-id | 命令失败，不读取历史数据兜底 |
| source map 改变报告文件路径 | 按声明口径验收，不静默丢失生产分母 |
| 测试运行期间生产文件被改写 | INPUT_CHANGED，不授予本次报告当前性 |

它们必须调用真实入口和实际依赖，不只手造 {totals:{coveredLinePercent:80}} 测 parse/evaluate。这组小项目测试不要求在自己的测试中递归跑全仓 coverage。

## 12. 输出：一份事实，一个汇总属主

### 12.1 成功输出

沿用 CLEAN.md 对共享 result collector／compact reporter 的改造。本文不新增一份计数器，不把 E2E 或 package 的日志正则解析成 verdict。

发布输出目标示例，数字为占位，不是预测：

```text
verify release
format       PASS     …
check        PASS     …
build        PASS     …   clean, generation=…
unit         PASS     …   … tests
integration  PASS     …   adapters + compiler + repository + harness
e2e          PASS     …   one Host, main flow complete, cleanup complete
package      PASS     …   … members, artifact=…
PASS release …
```

默认不重复每个阶段的第二张结果表，不每组打印同样的“authoritative”解释，不每层都列 p90/median/top5。明细仍存在本次日志，profile 模式才显示慢项。

完整 integration、单独 E2E、单独 package 各有自己的简短结束行；被 verify 调用时它们返回结构化结果，由顶层负责最终展示。单独命令仍能用，不能为了安静让它们无结果可读。

### 12.2 失败输出

对本轮实际 E2E 故障，目标是先看到：

```text
FAIL e2e / preflow-humanroot
reason: no-declared-turn; requestKind=chat; step=0; roles=[system]; userMessage=missing
pending: main Orchestrator/Manager flow not completed
cleanup: <本次真实清理结果>
details: <本次日志路径>
```

候选脚本、原始事件尾部和必要关联 ID 留在 details。不能把“roles=[system]”解释成合法状态自动放行，也不能打印一个假 PASS 后再在日志末尾藏失败。

coverage 的本轮错误应先显示 COVERAGE_RUNNER_ERROR / module-not-found / tests-not-started，不让“0 failed”占据视觉中心。package 闭包错误优先指出具体 missing/extra/mismatched member；编译错误保留 Fable 的文件与定位；输入改变直接指出发生变化的路径，不只给两个无从比较的 hash。

### 12.3 日志和诊断不参与成功判定

scripts/verify.mjs 的实际 step runner（收尾源码中的 defaultRunStepFactory）捕获 stdout/stderr 到本次文件；生产测试的预期错误不直接刷到顶层。失败和明确 warning 应及时展示，不能一直静默到物理总超时。默认保留第一故障摘要和日志路径，verbose 才原样展开全部输出。

runStep、supervisor、E2E reaper 共用已有结束协议：以子进程 close、事件流完成、日志 flush 和清理结果判断结束。只调用 logStream.end() 并不能证明内容已经写完。用 Node 官方 stream 的完成／错误传播能力收口，不手写“等 100ms 再退出”。[N5]

TTY 可更新一条当前阶段进度；非 TTY 仅打印阶段变更和结果。进度动画、日志字节和 I/O 唤醒不得给因果 watchdog 续期。长时间真正无进展时仍要失败。

### 12.4 profile 只记录能指导行动的数字

以下为待实现选项，不是当前已有 CLI 参数：verify --profile 或等价显式开关。基础结果始终记录阶段墙钟；扩展数据按需输出。

| 层 | 扩展指标 | 用途 |
|---|---|---|
| clean | 编译器调用数、实际 compile items、parse/compile/derive/linkage 时间 | 找重复编译，不盯花哨终端输出 |
| integration | 每组墙钟、并发数、峰值资源、最慢文件 | 判断是算法贵还是资源争用 |
| journal | 读入字节、解析完整行数、refresh 次数、最大等待者数 | 确认不是每次查询重做旧数据 |
| E2E | setup/preflow/main/oracle/cleanup 的墙钟，物理投递与事实数 | 判断哪一支线真正拉长主链 |
| package | pack 次数、归档与提取字节、依赖准备／消费耗时 | 防止四次安装或重复打包 |
| coverage | 生产文件分母、实际记录文件、未覆盖模块、测试与报告耗时 | 区分测试贵、报表贵和数据漏收 |

不要把各测试 duration 相加当作用户等待时间。并发组汇总 duration 可能大于实际墙钟；package uncompressed bytes 与 .tgz 文件大小也必须分别命名。

### 12.5 日志保留与泄漏边界

每次运行独立目录，阶段 raw log 与 summary 放在同一 run 下。成功默认保留可审摘要；详细物理诊断按项目现有保留策略管理，不增加每日清理守护进程。出现失败时保存第一故障上下文，不把上次失败覆盖成空日志。

日志里禁止凭据、授权头、用户真实输入的无差别复制。测试生成的关联 ID 可以保留，实际敏感正文要做可追踪的脱敏。脱敏不是删除错误类型、事实 identity 或必要调用顺序。

## 13. 程序落点：复用现有属主，不另造平台

| 文件 | 修改责任 | 终态边界 |
|---|---|---|
| scripts/verify.mjs | 阶段范围、build receipt、结果输出、日志 flush | 固定日常／发布链；不解析人类日志 |
| scripts/build.mjs | 一次 clean、后置验证、最终 manifest | 唯一生产构建属主 |
| scripts/lib/build-state.mjs | 精确输入／输出内容检查、失败枚举 | 被构建、独立验证和发布共同调用 |
| scripts/lib/owner-compile.mjs | 解析／规划复用、小工程 CLI 支持 | 不另写发布成功 manifest |
| tests/support/integration-node-test-steps.mjs | 实际分组及 release 范围 | 文件恰归一个执行入口 |
| tests/integration/run.mjs | 少量组调度、版本前提、failure propagation | 不重复 warmup／pack |
| scripts/warmup-opencode.mjs | 去掉 --help 与宽松失败路径 | 若版本检查可合并，则最后删除入口和壳 |
| tests/e2e/entry.test.mjs | 移除顶部登记自证，复用事实等待 | 唯一物理 world 入口 |
| tests/e2e/scenarios/long-stroke.toml | 缩减冗余分支，保留真实 adversity 主线 | 不加第二个场景加载器或宽松默认响应 |
| tests/e2e/support/long-stroke-oracles.mjs | 按业务 identity 读取一次 snapshot | 不把辅助登记表当证据 |
| tests/e2e/support/journal-observer.js | 单次 world 的增量读取与查询 | 不重建产品状态机 |
| tests/e2e/support/scenario-driver.mjs | 精确可取消等待、结束协议 | 不轮询文本猜成功 |
| tests/e2e/support/event-ceiling.js | 共享计数、runaway 拦截 | 与业务 exactly-once 分开 |
| tests/e2e/support/scenario-paths.js | 显式 production root | 删除无消费者 variant 与 cwd 试探 |
| tests/e2e/support/{process-lifecycle.js,spawn-ledger.js,reaper.mjs} | 只回收本次物理资源 | 不新增第二套 supervisor |
| scripts/verify-package.mjs | 真实归档集合／字节、隔离消费、有效 artifact 生命周期 | 恰好一次 npm pack |
| scripts/coverage.mjs（新增） | 调用既有测试及 c8，核验分母／本次性 | 只做薄编排 |
| tests/support/coverage-policy.mjs | 缩为生产文件口径；删阈值／预导入引擎 | 不自己计算 V8 执行范围 |
| tests/support/run-inner.mjs、tests/run.mjs | 删除旧 coverage 分支、正确传递执行环境 | unit 仍是原 runner |
| package.json / package-lock.json | coverage 命令、锁定 TAR/c8 工具依赖 | 不改产品依赖或 peer 范围 |
| .github/workflows/ci.yml | 保持 release 主入口，收集失败诊断 | 不暗加 coverage 全仓重跑 |

表中 tests/ 未写包前缀时均指 requirements/verification-system/tests/。CLEAN.md 已安排的共享 reporter、统计属主和输入快照修复只实施一次；本文件消费那些接口，不另起同名替代方案。

新增文件原则上只有薄 coverage 入口和少量真实回归。archive reader、journal observer、取消等待都在实际拥有这些行为的既有模块中修改；不要把每个三行分支拆成一个 lib，也不要为同一个配置新增 schema／registry／baseline 三份文件。

## 14. 正式规范调整：取消的义务要写清楚

本文不具备 WHAT 权威。实施时先修订与方案冲突的条款，再同时修改代码和证明；没有冲突的算法与显示改进可直接闭环。不是每个局部优化都要新增一个 Change 文件。

| owner／条款 | 要保留的产品或验证含义 | 要撤销／澄清的实现义务 |
|---|---|---|
| verification-system：001 | 分层执行、每份必要证明恰执行一次、发布有 clean/E2E/真实包 | 不锁每个小组名字或旧 leaf 的永久父级；package 模拟 child 退役后更新归属描述 |
| verification-system：002/003 | 一个 Long Stroke，真实 Host 见证；双 provider failure 后成功等现行命题 | 删除覆盖清单自证，不删除相应物理命题；不得把缩脚本当正式语义取消 |
| verification-system：004/005/006 | verifier 可红、失败向上传播、因果监督及诊断 | 不强求每个内部 helper 都有独立测试；正文和成功日志不是 verdict |
| verification-system：008/010 | 当前生产字节、内容凭据、不能暗自放宽失败边界 | 取消全局 coverage 硬阈值必须明确授权修订，不与 freshness 红线一起取消 |
| verification-system：011 | 全生产文件分母、遗漏不可隐藏 | 删除“必须预导入全部模块”的具体方法，改为静态文件全集＋实际执行数据＋未触达补零 |
| distribution：001/003/004/005/007/008 | 一个真实自足包、main/exports 真实可用、完整资源、与已验证 dist 相同 | 从少量样本文件检查改为完整字节闭包；工作区 layout 不再作为已安装证明 |
| structured-workflow：011/012 | 独立声明闭包、签名与 compiler 边界、impact 正确 | 用小型实际编译证明工具行为，不把每次重编整个生产库写成义务 |
| degeneration-guard：DG-004 | 真实仓库语料和运行时 envelope 一致 | 不再要求同一派生结果由每个测试各算一遍；独立 oracle 仍保留 |
| cognitive-environment／provider-language／behavior-diagnosis | 资源语言、角色语义、规则身份和加载行为 | 删除比率、旧名、固定写法等测试前逐条确认是否真是产品合同；机械比率不作为语言质量替代 |

HOW 删除退役证明的旧链接并写明新的实际落点。纯工具布局更新无需重新给每个测试造一条业务 WHAT；也不重新建立上一轮已撤销的 proof-level、covered checklist 或 triple registry。

若 HumanRoot 红例暴露真实产品缺陷，先记录故障和回归落点，不把它塞进“规范取消”里消失。若某个 E2E 支线确实不再是产品需要，则由相应 owner 正式撤销命题，而不是因为耗时就删证明。

## 15. 分批施工：每批都有明确退出条件

以下批次是实施顺序，不是本次已经完成的工作。每批把规范、实际调用、测试和废弃路径一并收口；不要全部代码先改完，最后再发现原始红例已经无法复现。

### B0：保住基线，定位现有红例

保留本轮日志中的 HumanRoot system-only mismatch，建立低层可重放输入，按第 7.1 节区分生产消息丢失、request kind 分类和 session binding。coverage 路径失败登记为独立运行器缺陷，不与 E2E 混为一个问题。

确定本次要修订的 WHAT 条款，特别是 coverage 阈值和预导入义务。记录需要保留的物理命题，不新增一个机器执行的“所有用例登记表”。

退出条件：能够用正式回归重现当前 E2E 故障；没有用宽松 mock、跳过用例、放大窗口制造绿灯。只有定位清楚后才动 E2E 主脚本。

### B1：消费已落地的验证基础，核验剩余结束边界

第2.4节已有输入快照、可注入 verify、文件完成谓词和 drainTestStream，不重复实施。先运行这些新接口的正式回归，再补仍缺失的日志落盘／清理结束边界、统一摘要，以及独立 integration/E2E/package 的 freshness 与当前构建绑定。

退出条件：输入中途增加／删除／改写可以使真实调度器失败；子进程未启动、崩溃、漏完成或报告输出失败不能被算作 PASS；预期负向 fixture 的错误不会污染全局摘要。

### B2：消除三次生产全量 CLI 编译

修改 owner-impact-compile-cli.test.mjs 的输入，使用小型 fixture；保留已知 compiler-boundary 的真实 Fable 正反例。明确断言 plan.mode、实际输入闭包与 emit 内容，不再只匹配日志名称。

将 compiler 组仅放入发布范围，更新 integration 文件覆盖关系。纯图算法、删除 provider、签名变化影响等继续日常验证；改变编译工具本身时可单独运行整个 compiler 组。

退出条件：完整 integration 中不再因 CLI 行为测试重编整个真实生产库；发布仍进行一次生产 clean；漏声明、私有实现越界和错误 flat emit 布局照样被真实编译器抓住。

### B3：integration 合组与资源准备减重

合并普通 adapter 组，保留文件隔离；删除 warmup --help；资源加载测试重复 happy path 合并，机械语言比例／写法断言按规范修订退出。

不要在本批删除真实 worktree、fast-forward、restart、object identity 和 leave-unread 行为。harness 的旧治理扫描按 CLEAN.md 原计划移除，不为本次另设一套退役命名。

退出条件：旧有效行为集合仍能运行；新增 integration 文件不会静默漏接；清理干净；固定的并发候选比较能说明选值，不靠 timeout 上调适配过度并发。

### B4：journal 单观察器与等待原语

先补真实 journal-observer 回归，再做增量读取和精确 case 查询。迁移 waitFact、event ceiling、waitCaptured 和 oracle 批量快照，最后删除重复旧读取／监听路径。

取消机制先在 delay-port 与实际 waiter 中落实，再迁调用者；共享对象仅在一个 world 生命周期内存在，不跨用例缓存临时目录。

退出条件：正文事实名诱饵不能制造事实；JSON/UTF-8 完整行损坏必红；多 writer 与尾部追加正确；同一物理事实不重复计数；notify 不续期 watchdog；结束时所有 waiter/timer 都被释放。

### B5：缩 E2E 支线，保留物理主线

本批前先让 B0 的原始红例经因果修复转绿。移除顶部 typeof／covered 清单和无效 variant；按业务身份替换世界总量补偿；逐段删除已被低层完整接替的重复分支。

每次删一段都回答：谁接替它独有的错误探测能力？不存在独有能力时不强造替代测试；仍有真实 Host 差异时保留最小物理见证。

退出条件：唯一 Host 的主线完整完成，两个 provider failures、真实 active join interruption、冲突后发布、HumanRoot 与 AgentOwnerRoot 差异均有证明；场景没有未声明请求兜底；同 PID continuation 不冒充 process restart；清理结果实际可见。

### B6：真实包闭包替换工作区模拟

先用小归档 fixture 测实际 validator，再接一次真实 npm pack。增加完整成员／字节核对和受限提取；runtime dependency root 只准备一次；通过真正 package exports 在仓库外消费。

迁移 contents/layout/import/resources 中有独立行为的断言，然后删除重复模拟入口和集成 child。清理对应 HOW 引用。不得保留“暂时先链接仓库 node_modules”的成功旁路。

退出条件：未声明 runtime dependency 无法借 dev 环境成功；缺任意资源、旧 JS、exports 错误、内容变动可红；成功返回的 artifactPath 确实存在；同一 release 只 pack 一次。

### B7：coverage 改成显式查漏报告

规范修订先落地，再用真实小项目补 coverage-runner 回归。引入锁定 c8，新增薄 coverage 入口，删旧预导入与百分比阻塞分支。

退出条件：未导入且顶层抛错的文件没有被执行却仍在分母；实际子进程覆盖正确归并；测试失败／数据损坏／输入变化仍非零；报告口径与路径清楚；低覆盖率不会被称为产品验证通过。

### B8：输出与完整发布验收

清理重复阶段标题、每次请求打印、逐成功用例输出和永远展示的慢项榜单；失败细节写入一次，保证日志落盘。CI 保持正式 release 入口，coverage 按显式需要运行，不重复接入同一发布。

退出条件：在确定输入快照下完整 verify:release 通过一次；独立 coverage 完成且数据可信；Node 20 与项目实际本地版本上的 runner／包／coverage 小项目回归通过；没有旧入口、假成功壳和失效规范引用。

### 批次依赖

```text
B0 ─→ B1 ─→ B2 ─→ B3
 │      ├─→ B4 ─→ B5
 │      ├─→ B6
 └─规范修订─→ B7
             B2～B7 完成后 ─→ B8
```

B2、B4、B6、B7 的调查与独立测试可以并行；涉及同一个 verify/result 类型、package.json、lockfile 或正式 integration 清单的修改必须串行。先定共享边界，再迁消费者，最后删旧路径。

## 16. 永久验收矩阵

矩阵列的是必须能抓到的错误，不要求每行机械拆成一个 test 文件。优先扩充相应 owner 的既有测试；同一失败含义不重复在 unit、integration、E2E 各建一份自证。

### 16.1 编译、调度和字节交接

| 编号 | 反例／输入 | 必须断言 | 建议落点 |
|---|---|---|---|
| C1 | 生产 dist 预留一个已不存在的旧 JS | clean 后文件真实消失，不能进入包 | build-freshness.test.mjs + clean fixture |
| C2 | compiler 成功但 artifact 派生或 linkage 失败 | 无有效 manifest，release 停止 | build-freshness.test.mjs |
| C3 | 构建期间更改一个 tracked 文档 | INPUT_CHANGED，不能复用前半段证明 | 构建／verify 实际编排测试 |
| C4 | Git 语料枚举报错 | 明确失败，不把空语料当合法输入 | build-state 对应行为测试 |
| C5 | 修改公共源码导致影响超 threshold | 返回 full，不因“focused 测试”名称强行缩闭包 | owner-impact-compile 测试 |
| C6 | 小 fixture 省略一个真实 provider | 编译错误；更大 aggregate 存在该文件也救不了缺声明 | compiler-boundary 集成测试 |
| C7 | .fsi 隐藏符号、消费者仍访问 | 真 Fable 判红 | 已有 SignedRed fixture |
| C8 | CLI 第二轮修改小工程实现 | 实际 emit 变更，且不写发布 manifest | owner-impact-compile-cli.test.mjs |
| C9 | integration 新增文件但没有接线 | 入口集合检查失败 | integration-entry-coverage.test.mjs |
| C10 | 同文件重复接线，或 gate spawn 失败 | 非零；后续依赖阶段不执行 | 真实调度器回归 |

### 16.2 journal、物理等待和 Long Stroke

| 编号 | 反例／输入 | 必须断言 | 建议落点 |
|---|---|---|---|
| E1 | 只在 prose 出现某事实名 | 事实计数仍为零 | 新 journal-observer.test.mjs |
| E2 | 一条 UTF-8 NDJSON 分多次写完 | 写完前不计，写完后恰计一次 | 同上 |
| E3 | 完整行 JSON 损坏、重复 ID 不同 bytes | 立即错误，不跳过后续继续绿 | 同上 |
| E4 | 文件被替换、已消费前缀截断／原位修改 | 失败并指出 writer 与偏移 | 同上 |
| E5 | 检查与订阅之间恰好出现目标事实 | 等待不丢通知、不悬挂 | 实际 awaitFactBarrier 回归 |
| E6 | 无关 SSE、后台事实或日志持续输出 | 不给不相关业务等待续期 | 已有 verdict-feed／harness 行为回归 |
| E7 | waiter 的事件先到、timer 后到 | 败方被取消，不产生后续副作用 | delay/waiter 行为回归 |
| E8 | provider 生成 join，但 ToolPart 尚未 running | 不提前注入 user_message | physical-contract + 实际 E2E |
| E9 | HumanRoot 本轮 system-only 请求 | 正确 owner 回归可复现；无合法依据不得放行 | B0 确定的产品 owner 测试 |
| E10 | 两次不同 provider failure 后成功 | claim、physical acceptance、最终 join identity 精确 | 现有 temporal + 唯一 E2E |
| E11 | 前置 HumanRoot 事件数改变 | 主线按自己的 identity 判断，不被世界累计数误导 | scoped oracle 回归 |
| E12 | assertion／mock fatal／signal 后仍有 child 或 listener | 本次回收或明确失败，首因不被覆盖 | lifecycle/reaper 小型真实进程测试 |
| E13 | 全断言结束但资源不退出 | RESOURCE_LEAK；不能提前 process.exit(0) 伪通过 | supervisor 与 package consumer 测试 |

### 16.3 包与报告

| 编号 | 反例／输入 | 必须断言 | 建议落点 |
|---|---|---|---|
| P1 | 删除任意一个非样本资源文件 | 完整 closure 核对报 missing | distribution/pack-closure.test.mjs |
| P2 | 包里混入额外旧 JS 或开发文件 | extra member 必红 | 同上 |
| P3 | 文件名相同但实际 bytes 被替换 | digest mismatch | 同上 |
| P4 | 归档重复、非法路径或不允许的链接条目 | 在实际提取之前拒绝 | distribution/pack-artifact.test.mjs 的真实归档输入 |
| P5 | package exports 指向错误入口 | bare package import 失败 | distribution 的外部消费 fixture |
| P6 | runtime 导入仅在开发依赖中存在的包 | 隔离消费者失败，不借仓库 node_modules | 同上 |
| P7 | consumer import 返回 namespace，但无合法 plugin export | 公开契约断言失败 | 同上 |
| P8 | pack 后输入或 build generation 变化 | 该 tarball 不取得当前 release 证明 | verify-package 实际编排测试 |
| P9 | 返回 artifactPath 后调用者读取 | 文件仍存在，digest 与报告一致 | 同上 |
| V1 | 未执行模块、同名模块、子进程覆盖 | 第 11.7 节的实际 CLI 矩阵成立 | coverage-runner.test.mjs |
| V2 | coverage 在测试启动前崩溃 | 不是0失败即通过；输出 tests-not-started | 同上 |
| V3 | summary/raw 文件来自上次运行 | 拒绝，不拼出假分母 | 同上 |
| O1 | 正常、失败、skip/todo、取消、文件 wrapper | compact/verbose/父级统计一致 | CLEAN.md 的 reporter-supervision 回归 |
| O2 | 阶段返回后日志流写入失败 | 本次失败，不能发布未完成报告 | verify 的真实 I/O 结束回归 |

更改真实 Host、进程、Git 或安装边界时保留相应物理测试；纯 reader/parser 的组合反例先用小输入。禁止每次为了证明 verifier 可红而全仓启动一轮 mutation 或故意失败的 Long Stroke。

## 17. 执行命令与验收顺序

### 17.1 现有正式命令

以下命令当前已存在。它们用于定位各层，不代表可以分别运行后随意拼成一次 release 成功。

```bash
# 干净生产构建；不清依赖下载缓存
node scripts/build.mjs --clean

# 完整 integration，包含 release-only repository oracle
WXS_RELEASE=1 node requirements/verification-system/tests/integration/run.mjs

# 唯一物理世界
node requirements/verification-system/tests/e2e/entry.test.mjs

# 现有真实包验证
node scripts/verify-package.mjs

# 当前旧 coverage 入口；本轮已确认基础设施失败
node requirements/verification-system/tests/run.mjs --coverage

# 只看真实影响范围，不启动编译器
node scripts/compile-impact.mjs src/Wanxiangshu/Foundation/FatalProcess.fs --plan-only
```

工作区运行这些命令前确认无人正在修改同一份受测输入；确需并行开发时用用户明确授权的独立工作区。工具发现输入变更应失败，不把静默等待用户或自动循环重跑塞进验证脚本。

### 17.2 实施后的命令

coverage 是本方案拟新增的命令，当前不可当作已经存在。其余正式入口不改用户习惯：

```bash
npm run format-build-test       # 日常必要验证
npm run verify:release          # 一次完整发布验证
npm run coverage                # 新增；显式 unit/emitted-JS 查漏报告
```

单独修改一个 parser／waiter，先跑该 owner 的正式回归；修改跨层调度、构建或包时再跑对应集成层。完成整个方案后，必须在同一输入与有效 receipt 下完整运行 verify:release；这一次不能被拆开的绿色结果替代。

本轮 E2E 失败后单独跑 package 只是观察 package；没有把它列为“release 继续通过”。实现后的正式流水线仍在 E2E 失败时短路。

### 17.3 基准测量不增加常驻脚手架

现有阶段计时与一次性 profile 输出足够。保存同一输入下前后样本的运行参数与原始结果，不新建性能成绩数据库或测试数量红线。

需要比较并发值或冷机行为时，使用预先固定次数与同一输入的对照，所有失败都计入结果；它不是失败后随意继续跑直到一次成功。分别记录 warm dependencies 的 clean、真正 cold restore 和普通增量，不混成“构建耗时”。

最有依据的第一目标是去掉 integration 三次真实生产编译，compiler 小 canary 仍保留。新的 package 隔离依赖准备可能增加若干开销，要如实测量并与其修复的证据缺口一起说明，不虚称每个子阶段都必然更快。

E2E 成功基线和完整 coverage 基线本轮都没有取得，不能承诺它们下降某个百分比。Journal 读取量、临时进程数和重复验证次数可以先客观比较，墙钟改进以真正完整成功运行确认。

## 18. 本轮执行结果与未完成的验证

### 18.1 实测表

| 命令／阶段 | 退出码 | 墙钟 | 本次事实 |
|---|---:|---:|---|
| clean build，首次输入 | 1 | 47.856 秒 | Fable 编译成功，最终 generated inputs 校验拒绝；期间 AGENTS.md 外部改写 |
| clean build，新输入 | 0 | 50.798 秒 | 一次 clean 编译与后置验证通过，generation=1 |
| 完整 integration，WXS_RELEASE=1 | 0 | 152.471 秒 | 12 组、package child、harness child 均完成；不是日常子集 |
| 唯一 E2E | 1 | 8.920 秒 | HumanRoot 前置阶段 no-declared-turn；主线未完成 |
| package，独立调用 | 0 | 2.606 秒 | 现有 verifier 接受2082个文件、11,125,961字节，generation=1 |
| 旧 coverage 入口 | 1 | 0.458 秒 | 动态 import 路径错误，测试未开始，无可信百分比 |
| impact --plan-only | 0 | 不作性能基线 | FatalProcess.fs → full / impact-exceeds-full-threshold |

首次 clean 的原始失败没有被第二次覆盖。第二次成功发生在已观察到的新文档输入上；只能证明新快照构建成功，不能声称初始输入全过程都稳定。

完整 integration 中，compiler 组墙钟130.13秒，约占整层85.3%。其中两项 CLI 测试分别报告48.597秒与81.482秒；小型 compiler canary 的时间与它们有并发，不能把所有测试 duration 简单相加作为墙钟。这个实际占比足以决定优先级，但不是承诺能把130秒全部删掉。

repository envelope 一组墙钟4.65秒，测试本体4.409秒；工作区 package child 4个文件6项测试，墙钟0.43秒；harness 报279项通过。它们的统计单位不同，不拼成一个夸张的“总通过项数”。

### 18.2 真实日志位置

本轮生成的日志留在忽略目录中，不进入源码、规范或 npm 包：

- .fable-build/clean-e2e-integration.log
- .fable-build/clean-e2e-long-stroke.log
- .fable-build/clean-e2e-package.log
- .fable-build/clean-e2e-coverage.log
- .fable-build/clean-e2e-impact-plan.json

两次 clean 的原始输出由本次工具运行记录保留；没有另造一个不存在的 clean log 路径。此处列举的是诊断材料，不是新增长期登记机制。

### 18.3 不能宣称的结果

本轮没有运行完整 npm run verify:release，也没有重新执行 format/check/完整 unit。独立阶段观察并不组成一次端到端发布通行证。

表中耗时和失败属于调查时的输入快照，不是收尾HEAD 7a347df02的验证结果。该提交修复了本轮遇到的 coverage 路径错误，但没有在本次重新运行其完整 coverage；旧成功 manifest 也不能据此描述新脚本字节。

没有取得成功 Long Stroke 总耗时、可信完整 coverage 百分比、全新机器 cold build、隔离 runtime dependency 安装成本，或 Node 20 上本轮修改方案的兼容性结果。没有确认 E2E 失败后整个进程树都已回收。没有实现新的 TAR validator、journal observer、coverage 入口或修复 HumanRoot。

因此本文中的算法复杂度、重构接口、输出示例和批次退出条件都是设计，不是已经验证的实现。唯一已交付的源码树新增项是 CLEAN-E2E.md；实际执行过的构建产物和日志属于生成物。

## 19. 依据、引用与交付边界

### 19.1 仓库依据

调用与所有权以实际源码为准，重点读取了：

| 主题 | 直接依据 |
|---|---|
| 发布调度与现有范围 | package.json；scripts/verify.mjs；.github/workflows/ci.yml；integration/run.mjs；integration-node-test-steps.mjs |
| clean 与内容凭据 | scripts/build.mjs；scripts/lib/build-state.mjs；scripts/lib/owner-compile.mjs；scripts/compile-impact.mjs |
| 编译测试实际成本 | owner-project-compiler-boundary.test.mjs；owner-impact-compile-cli.test.mjs；本轮完整 integration 日志；正式 impact plan |
| E2E 主线／前置／证据 | entry.test.mjs；long-stroke.toml；journal-observer.js；scenario-driver.mjs；event-ceiling.js；scenario-paths.js；本轮 mismatch 日志 |
| 分发 | scripts/verify-package.mjs；distribution/tests/pack-artifact.test.mjs；distribution/WHAT.md；本轮 package 输出 |
| 覆盖率 | tests/run.mjs；tests/support/run-inner.mjs；coverage-policy.mjs；本轮 coverage 错误日志 |
| 正式边界 | verification-system/WHAT.md；distribution/WHAT.md；js-semantic-surface/WHAT.md；CLEAN.md 中已经核实的共享 runner 问题 |

仅检索定位但未逐断言精读的 integration 文件，在第4节只给责任边界与改造方向，不据文件名直接决定整份删除。实施时读取 owner WHAT 和实际断言，留下独立失败价值；不要把这张表变成无条件删文件脚本。

### 19.2 外部技术依据

- [N1] c8 维护者文档：https://github.com/bcoe/c8 。用于核对 --all/--src、报表、原始数据目录与 source-map 处理。锁定依赖版本和 Node 兼容性须在实施时验证。
- [N2] Node.js 官方 CLI 文档：https://nodejs.org/api/cli.html 。用于核对 NODE_V8_COVERAGE 的执行记录与子进程环境语义。调研页面为26.8.2，不代表本机26.5.0或CI20已经具有该页所有新选项。
- [N3] npm 官方 pack 文档：https://docs.npmjs.com/cli/v11/commands/npm-pack/ 。用于核对真实 pack、--json、--pack-destination 与 --ignore-scripts；不以 dry-run 代替真实归档。
- [N4] node-tar 维护者文档：https://github.com/isaacs/node-tar 。用于核对成员读取、类型信息、受限提取与错误传播；必须显式处理 warning/strict 行为，不能当所有坏输入默认都会抛错。
- [N5] Node.js 官方 Stream 文档：https://nodejs.org/api/stream.html 。用于核对流结束、背压与错误传播；实际 Node 20／本地版本上的持久 fixture 是本项目接线证明。
- [N6] npm 官方 ci 文档：https://docs.npmjs.com/cli/v11/commands/npm-ci/ 。用于核对 lock 安装、omit=dev 和安装脚本配置；本方案没有声称实际隔离安装已执行。

外部依据仅用于校准工具边界，不赋予本仓库未经运行的功能以“已支持”结论。方案主要来自当前源码和本轮实测，不以第三方最佳实践套话替代现场判断。

### 19.3 文档本身不要变成下一轮脚手架

不要把本文复制到 AGENTS.md 或每个 HOW，形成多份永远要同步的施工手册。实现后由源码、简短 HOW 和真正回归承载当前行为；本文只保留设计缘由与本轮基线，不持续追加每次运行的进度流水。

本次没有修改上一轮 CLEAN 方案或 AGENTS.md；收尾时 CLEAN.md 已由外部工作收起，AGENTS.md 和 runner 改动已被其他提交纳入历史，本次均未回退。本文纳入 Git 后可能进入当前 envelope 语料；下一次正式构建按内容凭据刷新，不手改 dist 或 manifest 让它看似新鲜。

这轮精简的完成标志不是“终端只剩 PASS”，而是三件事同时成立：不再重复编译和自证；必要物理行为仍能被真实反例击穿；失败、未完成和未测范围在输出中一眼可见。
