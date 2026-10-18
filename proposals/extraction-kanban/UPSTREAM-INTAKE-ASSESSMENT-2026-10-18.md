# resources 与 time wait 接收评估

日期：2026-10-18，时区 Asia/Singapore。评估两个上游拆分样本，判断接收前需要做什么，并校准后续投入。只读生产源码，在临时目录构建、测试和安装候选；没有合并 upstream，没有修改生产代码、需求、依赖或持久数据。

**两个样本均暂缓直接接收，原因和工作量不同。** resources 的核心职责基本收敛，发现一项替换顺序回退，适合局部修正。time/wait 的基本行为测试通过，但公开接口、窄能力边界和旧实现退出尚未对齐，应先修正契约，再安排接线。不能据此推断所有后续模块都需要重修前四模块的工作量。

## 固定基线与样本

| 对象 | 固定版本及范围 |
|---|---|
| 本地主仓 | `25651051f`；评估期间另有 Process、Git、Change 等未提交施工，不作为本轮候选 |
| 上游主仓 | `705e1ea3b0946ea91edb3ce694e22f58228b5c19` |
| 上游固定库 | `e4ba7ab6fd7e1028a68b26dcc5fda2a6e2e51f78`，`wanxiangli@0.8.0` |
| 低耦合样本 | CUT-05 resources：语言解析、资源配对、模板替换。含进程级语言设置，不把它称为完全无状态的纯模块 |
| 真实接线样本 | CUT-21 time/wait：一张职责卡、两个导出，连接 Deadline、Host 定时器、诊断注册表和 Join mailbox；不等于两张独立产品卡 |

使用前次分析保留的源码快照，对本次读取的库侧 49 个文件、主仓 114 个文件逐一核对 Git blob，均与固定版本一致。库通过 `node scripts/build.mjs` 构建全部导出，随后离线 pack 并安装到独立消费者目录；13 个导出的 ESM 和声明文件均存在。该包是本轮按固定源码构建的评估产物，不冒称上游发布 tarball。

主仓实际调用链按固定源码核对；本轮没有构建、运行完整上游 Host，也没有把本地进行中产物拿来代证上游接线。Node/Bun 原生测试与正式测试监督分开记录。

## 实测结果

| 检查 | 结果 | 证明范围 |
|---|---|---|
| 库原有 resources、time、wait 测试 | Node 26.6.0、Bun 1.3.14 各 75 pass，0 fail、skip、TODO；分别为 8、30、37 项 | 这些库内用例在该源码生成物上成立 |
| 独立安装包的 13 组补充观察 | 两运行时均 8 组通过、5 组违反预期 | 5 组失败包括同一 resources 问题的两个入口，不计作 5 个独立根因 |
| TypeScript 正向消费 | 按声明把时钟结果当 string、把租约当有实例方法的对象，严格编译通过；运行时反例失败 | 声明检查通过不代表声明与实际产物一致 |
| mailbox 声明消费 | 调用实际存在的 `waitForSignal`，编译报 TS2339 | `CompletionMailbox` 的公开声明漏项 |
| 上游 `time-capability/tests/008.test.mjs` | 2 pass、1 TODO、0 fail | fsproj 依赖图检查通过；实际负向编译与能力注入仍为 TODO |

补充观察脚本会捕获反例并输出 JSON，因此脚本 exit 0 只表示观察完成，不能写成测试全绿。主仓原有 24/52 等历史成绩不作为本轮复验结果。

## resources 的接收条件

### 已成立的边界

库不依赖 Host SDK、进程执行、持久化或其他库模块。主仓 `ProviderResources` 和 `ProviderProse` 将定位、配对及替换交给库；业务正文仍在主仓资源目录，由显式根路径绑定。包外 cwd 下默认包根读取、缺任一语种即拒绝、占位符集合不一致拒绝、普通技术字面量保留，都取得了安装包反例或正控。

### R05 参数首尾字符被裁剪

上游 `Resources.render` 先替换参数，再对整个结果 `Trim()`。本地原 `ProviderProse.render` 的顺序是先读取并裁剪模板，再注入参数。因此模板自身的尾换行与参数自己的首尾字符被混为一谈。

反例：双语模板均为 `\n{{value}}\n`，参数为 `"  token\n"`。上游返回 `"token"`；按原顺序调用 `readText → renderTemplate` 返回完整参数。实际 `tool/horizon/latest-work/en.md` 的尾部是 `{{record}}`：传入 `"record line\n\n"` 后，渲染结果丢掉两枚尾换行。该实际资源经安装包调用复现；完整注册的 horizon 工具没有在本轮运行。源码中 `HorizonTool.workRecordText` 验证正文摘要后将原文直接传给这个模板，故这不是只有虚构模板才能到达的差异，也不据此宣称持久原件已被改写。

依据：[库 render](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/e4ba7ab6fd7e1028a68b26dcc5fda2a6e2e51f78/src/resources/Resources.fs)、[本地原 render](https://github.com/dyx13/vibe-fs/blob/25651051f/src/Wanxiangshu/Participant/Provider/Prose.fs)、[上游实际消费者](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Execution/Session/OpenCode/HorizonTool.fs)。

**接收条件：** 区分模板格式与注入值，保持参数原值；补齐双语首尾空白、换行、BOM 和技术字面量的反例及普通模板正控。再以主仓实际 `ProviderProse` 入口、实际资源和隔离包验证配对、语言切换与包根定位。此项不需要 Journal 故障或跨重启矩阵。不得借机重写业务资源正文、工具布局或全局语言策略。

“禁止路径带后缀”“readText 统一 LF”等早期卡片描述与当前 WHAT、实现并不完全一致，不能机械转成新修复任务；开工以现行需求及真实消费者为准。原施工卡内容仍停在准备阶段，而看板写已完成并指向未出现的 §10，属于证据登记欠账，需随接收整理。

## time wait 的接收条件

### 已存在真实接线，但迁移范围不完整

源码中，`PluginBoot`/`HostSignalBootstrap` 创建主仓 `NodeTiming`，其物理实现委托给 `wanxiangli/time`；`PluginHostWiring` 取得进程内 `CausalWaitProcess.local()`，主仓注册表桥接到库；`Fork/Host/Join → ForkRuntime → CompletionMailbox` 的唤醒最终进入 `wanxiangli/wait`。不是只有库测试导入了新模块。

但主仓 `Wait/Await.fs` 仍保留原 `CausalAwait` 实现，`CausalWait.fs` 仍持有前沿算法。库也有对应实现。主仓 helper 消费窄 `IWaitObserver`，库公开 `awaitTask`、`untilSignalOrDeadline` 却要求完整 registry；直接改成转发会扩大能力。应该先对齐入口合同，再决定每段实现的唯一归属，不能仅因已有 Registry/Mailbox 桥就宣称整张卡已迁完。

依据：[真实 Join 调用](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Execution/Delegation/Fork/Host/Join.fs)、[主仓 Await](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/src/Wanxiangshu/Execution/Session/Wait/Await.fs)、[库公开 Wait](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/e4ba7ab6fd7e1028a68b26dcc5fda2a6e2e51f78/src/wait/Wait.fs)。

### T21 公开契约与产物不一致

| 问题 | 安装包观察 | 对接收的影响 |
|---|---|---|
| `IClockPort.UtcNow(): string` | 实际返回 JS Date | 按声明使用字符串方法会失败；主仓现有桥明确按 Date 承接，不能说它已因此失败 |
| `WaitLease.markExit/dispose` | 两个实例方法均不存在；调用 `Wait.markExit/Wait.dispose` 正常 | 应统一不透明租约的消费方式，不能用 `unknown` 或复制另一份实现掩盖差异 |
| `CompletionMailbox.waitForSignal` | 运行时存在且中断正控通过，声明中不存在 | 按真实能力修正公开声明，类型和运行时共同验收 |
| 输入 deadline 被写入 `cancelCount` | 符合公开 `Delay/Cancel` 形状的冻结对象，在即时读取成功前即抛 TypeError | 这是测试计数进入生产导出造成的附加可变要求；计数应由测试自己持有 |

前三项来自新增 JS/TypeScript 交付边界；第四项来自库公开包装器。本轮没有证明主仓既有桥正好触发上述全部错误。它们仍阻塞新公开合同的可靠接收。

### T21 分居降级尚未满足现行要求

`time-capability-008` 要求纯契约、物理适配器和虚拟实现分居，普通生产消费者不得带入虚拟时间。库却将三者放在同一个 `Time` 导出对象中；主仓纯 Deadline 分片通过 JS Import 引入该对象。库 AGENTS 明示“单 bundle 分居降级”，但主仓 WHAT 未相应裁决。现有 fsproj 图检查看不到这个 npm 导入后的完整依赖闭包，绿色结果不足以补齐实际编译与产物证明。

依据：[现行 WHAT](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/requirements/time-capability/WHAT.md)、[库明示的分居降级](https://github.com/PamelaSprin47685ghall/wanxiangli/blob/e4ba7ab6fd7e1028a68b26dcc5fda2a6e2e51f78/src/time/AGENTS.md)、[尚有 TODO 的主仓门禁](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/705e1ea3b0946ea91edb3ce694e22f58228b5c19/requirements/time-capability/tests/008.test.mjs)。

**接收条件：** 先确定纯时间计算、物理时基、虚拟时基、等待观察与 mailbox 的公开边界；让实际消费者获得最窄能力，记录每段旧算法的退出或明确保留理由。随后验证实际包的声明与运行时一致、冻结 deadline 可消费、取消不影响其他等待者、到期只结束等待、失败后租约释放。完成实际 Host 定时器/注册表绑定和 Join 中断链的定向验证，以及真实正反编译和 npm 产物依赖闭包检查，再判断职责迁移完成。不要在这个接收包里重做 PTY 监管、持久失败预算或 Host 崩溃恢复。

## 工作量校准

以下是后续取得施工授权后的排期预算，包含实现、定向验证和复核，不是本轮机器运行时长，也不是实际工时统计。假定只接收这两组职责，保留本地前四模块必要保证，不随包采用上游整个工具面或分发方式。等待其他施工释放公共接口的时间另计。

| 样本 | 已见范围的预算 | 估算依据与再评估点 |
|---|---|---|
| resources | **4–8 有效工程小时** | 替换顺序及反例 2–4 小时；真实资源入口、语言/配对/安装回归及证据整理 2–4 小时。若需改语言策略或业务正文，先重新确认范围 |
| time/wait | **12–24 有效工程小时** | 公开 API 与输入副作用 3–6 小时；职责边界及旧实现归属 4–8 小时；实际 Host/Join、正反编译和产物验证 5–10 小时。契约方案确定后先重估，再接线 |
| 两个样本合计 | **16–32 小时，约 2–4 个八小时工程日** | 有限候选范围的预算，不含整批 upstream 合并或未知产品变化 |

本轮把两个样本从“未知要修多久”缩小为上述具体边界，尚未实施修复，不能声称效率已经提升多少倍。前四模块现有 16 包的 2–4 周估算仍只是低置信度存量预算，不能由两个较轻样本验证，也不能外推为全部上游的工期。

后续按 **初审 → 契约与反例 → 修正与接线 → 相关验收与复核** 分别记录有效投入、等待原因和返工原因。接收前对照实际消费者重新分级：resources 属于小范围行为修正；time/wait 属于公共协议与接线修正；持久化、跨进程及物理义务另估。记录用于校准下一批，不以模块数、测试数或提交数评分。

近期尚未评估的 events、fs、grounding 和 identity 不预设为同等工作量。先核对各自真实消费者与高风险边界，再给预算；identity 的库就绪不代表主仓集成完成。上游固定批次与本地现有 16 包分开登记，新版本进入新一轮接收评估。

## 可复核证据

临时证据目录：`/private/tmp/vibe-fs-intake.Q0rjav/`。`node-probes.json`、`bun-probes.json` 保存逐项结果；`consumer/intake-probes.mjs` 保存反例和正控；两个 `.mts` 文件保存声明消费案例。基础套件与构建日志位于 `/private/tmp/vibe-fs-four-module-analysis.0EMCLD/intake-*.log`。报告中的版本、反例和结果是长期记录，临时目录不是唯一结论来源。

- 本轮完整库包 SHA-256：`1ddbac069bcdbcb9f692f966452d854d84f28892e61ca18401d76c25b21eb0c1`。
- 最终补充观察脚本 SHA-256：`a72fd98c2fbd2751a3ac693aa7f76300f05277bb1d42ed8cc382637b3f66a09f`。

复验时先从固定源码构建并安装包，再执行库内 21 个测试文件，以及包外消费者的反例。最小可区分案例为：替换值首尾空白；实际 horizon 资源末尾 record；时钟返回值的类型；租约实例方法；mailbox 中断方法的声明；冻结 deadline；正常窄租约释放和超时正控。原有套件通过不能代替这些案例。

新的拆分方法、分级验收和接收顺序落实在[现行计划](REMAINING-REPAIRS-2026-10-16.md#upstream-intake)，复盘结论补入[四模块报告](LOCAL-FIRST-FOUR-MODULES-REPORT-2026-10-18.md#continuous-upstream)。本轮仅完成评估，不关闭原拆分卡、修复卡或 GAP。
