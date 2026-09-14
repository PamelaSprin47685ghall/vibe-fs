建议保留大家已经在用的 `npm run build`，换掉它背后的旧机制。不要再加一条“更快的新命令”，让人自己记着切换。

这次要淘汰的，不只是 `Wanxiangshu.fsproj` 这个文件，而是三件事：**总工程仍掌握源码清单和编译顺序；普通改动被扩大成近乎全仓；多个入口对产物是否有效各说各话。**

我检查了当前工作区。审计开始时是 `master / c7cd80060`，有大量未提交改动。本次没有修改文件、提交或推送；运行的是现有脚本的只读编译计划，没有运行真实编译或全仓测试。

## 一、当前问题已经能具体定位

### 1. 分片本身很小，增量计划却把它重新扩大了

本次实际运行的结果：

| 检查对象                                                 |     计划结果 | 工程数 | 编译输入数 |
| ---------------------------------------------------- | -------: | --: | ----: |
| 单独修改 `Foundation/FatalProcess.fs`                    |     full | 276 | 1,500 |
| 单独修改 `Persistence/EventStore/CanonicalEventCodec.fs` |     full | 276 | 1,500 |
| 单独修改 `OpenCode/Host/HostSignalBootstrap.fs`          |     full | 276 | 1,500 |
| 独立检查 FatalProcess 所属工程的编译闭包                          | owner 闭包 |   1 |     2 |

这里的 1,500 个输入包含 750 个 `.fs`。这些是计划规模，不是编译耗时。

我又把 FatalProcess 那次计划的阈值从默认 60% 调到 100%。结果虽然改叫 `focused`，仍然选入 **269 个工程、1,479 个编译输入**。

所以，调高阈值只是把“全量”改了名字，不能解决问题。

原因在 `scripts/lib/owner-compile.mjs:506–649`：每个已找到 owner 的变化文件，都进入 `addReverseConsumers(ownerProject)`，然后再求全部前向依赖闭包。没有区分普通 `.fs` 实现变化和 `.fsi` 契约变化。

### 2. 规范要求局部编译，测试却要求扩大编译

这不是单纯漏改了一行代码。

`requirements/structured-workflow/WHAT.md:59–63` 已经规定：普通 `.fs` 改动且 sibling `.fsi` 未变，不应纳入普通下游消费者。

但 `requirements/structured-workflow/tests/owner-impact-compile.test.mjs:79` 的测试名称就是：

```text
implementation changes reach reverse consumers
```

它明确断言这种改动应走 full。后面的生产计划测试也要求 FatalProcess 实现变化走 full。

**实现和测试站在一起，规范站在另一边。** 只改实现，测试会把旧行为“保护”回来。

### 3. 默认 build 已有增量外壳，但总工程仍是权威

`package.json` 的默认构建确实进入 `scripts/build.mjs`，后者也确实区分 `no-op / focused / clean`。不能把它说成“每次都直接运行旧单工程”。

但内部依然这样工作：

```text
读取 Wanxiangshu.fsproj 的完整清单和顺序
→ 读取 owner 工程
→ 按影响范围选择源码
→ 从总工程过滤出临时 flat 工程
→ 调 Fable
```

直接依赖位置包括：

| 位置                                                          | 仍依赖旧总工程做什么                  |
| ----------------------------------------------------------- | --------------------------- |
| `scripts/build.mjs:230`                                     | 固定总工程入口，收集构建输入              |
| `scripts/lib/owner-compile.mjs:210–352`                     | 校验 owner 闭包必须存在于总工程，沿用总工程顺序 |
| `scripts/lib/owner-compile.mjs:673–695`                     | 从总工程 XML 删减出临时工程            |
| `scripts/lib/compile-shards.mjs:121–200`                    | 总工程必须存在，且与所有分片并集相等          |
| `scripts/lib/build-state.mjs:144–170`                       | 从总工程派生编译输入                  |
| `scripts/checks/architecture.mjs`、`js-surface-manifest.mjs` | 仍直接读取总工程                    |

因此，现在直接删 `Wanxiangshu.fsproj`，新路径也会一起坏。必须先转移这些职责。

### 4. 有缓存标记，不等于复用了编译

`compileOwnerProject` 会检查 `.success`，但不会据此跳过后面的 Fable 调用；实际参数还固定包含 `--noCache`。位置在 `scripts/lib/owner-compile.mjs:909–969`。

Fable 官方说明，`--noCache` 会重编全部输入，包括来自包的源码；`--noRestore` 只是跳过 restore，两者不是一回事。([Fable][1])

另外，`materializeOwnerCompile` 把源码内容纳入临时工程目录指纹。改动源码就会换临时目录，restore 资产也位于这个指纹目录下面。当前设计优先保证隔离，但会损失跨修改复用机会。位置在同文件 `704–789`。

### 5. 文档还在教人走旧流程

`requirements/verification-system/HOW.md:14` 仍说 build 每次先删 dist、再真实编译；实际 `build.mjs` 已有 no-op 和 focused。

`README.md:187` 仍把日常命令描述成包含写盘格式化、打包和 Long Stroke 的整套流程；实际 `verify.mjs` 使用只读格式检查，且只在 release 时追加 `--clean` 和发布阶段。

CI 的实际入口则是 `.github/workflows/ci.yml` 中的 `npm run verify:release`。

这些说明必须同批修正。否则后来的人和 Agent 会继续按旧说明操作。

---

## 二、切换后的形态：一个构建入口，一份源码归属

目标应当是：

```text
分片 fsproj 中的 Compile + ProjectReference
                  ↓
        唯一的分片清单与依赖图
                  ↓
        根据上次成功构建计算变化
                  ↓
          得到确定的编译计划
                  ↓
       生成临时 Fable 编译输入
                  ↓
       验证产物，最后记录成功构建
```

不再出现：

```text
分片工程 → 向旧总工程查询清单、顺序和配置
```

这里有一个重要区别：

**可以保留“将本次选中的源码合并成一次 Fable 调用”，不能保留“全仓手写总工程作为真相来源”。**

你现有规范明确要求合并影响集合，避免逐分片重复编译依赖。没有必要为了证明“多工程”而启动 276 次 Fable。保留这个合理部分，把临时工程变成分片图的派生物即可。依据是 `structured-workflow/WHAT.md:63` 和 `verification-system/HOW.md:48–57`。

同样，发布时可以全量编译。但它必须是：

```text
从全部分片生成完整输入 → 全量验证
```

而不是：

```text
切回旧 Wanxiangshu.fsproj
```

**全量验证不等于恢复单工程旧路径。**

## 三、按六个工作包实施

### 工作包一：先修影响范围，让已有拆分产生实际收益

主要修改：

```text
scripts/lib/owner-compile.mjs
requirements/structured-workflow/WHAT.md
requirements/structured-workflow/tests/owner-impact-compile.test.mjs
requirements/structured-workflow/tests/owner-impact-compile.property.test.mjs
```

先改测试，再改实现。把变化分类写成一张明确的决策表：

| 变化                | 应选范围                         |
| ----------------- | ---------------------------- |
| 普通实现变化，签名和编译契约未变  | 所属分片及其必需前向依赖                 |
| `.fsi` 变化         | 所属分片、传递下游消费者，再补齐各自前向依赖       |
| 同批多个文件变化          | 先合并 root，再求一次闭包；输入去重         |
| 新增、删除、移动源码或修改工程关系 | 首批实现允许全量，但必须来自分片图            |
| 工具链、编译配置变化        | 全量，并给出具体原因                   |
| 只有文档、资源或测试变化      | 不调用 Fable；按实际输入关系重算派生产物、重跑检查 |
| 生产源码没有分片归属        | 报错，不用全量编译掩盖遗漏                |

“只有 `.fs` 变化”也不能机械地理解成“下游永远安全”。内联函数会进入调用方代码，仅凭 `.fsi` 没变不能证明调用方产物无需更新。Microsoft 的语言说明明确了内联代码进入调用位置这一点。([Microsoft Learn][2])

因此，这一步必须增加真实 Fable 小型回归，覆盖内联、编译期常量、导出命名和局部产物替换。不能为缩小数字而删除必要依赖，也不应另造一套全仓 FCS 扫描器。

退出条件：

普通非内联实现修改不再无条件追溯下游；签名变化仍能覆盖真实消费者；局部结果替换进完整产物后行为正确。FatalProcess 当前独立闭包只有两个输入，可作为首个明确验收对象，但最终判断还要经过真实产物回归。

### 工作包二：让分片图接管总工程的三个职责

总工程现在掌握三样东西：源码集合、全局顺序、公共编译配置。要分别移交，不能只改文件名。

**源码集合：归分片工程。**

扩充现有 `scripts/lib/compile-shards.mjs`，让构建、门禁、Surface 检查和输入指纹都消费同一份 inventory。不要在另一个 JSON 中重新手写全仓源码列表。

这个 inventory 必须拒绝重复归属、遗漏归属、断开的 `.fs/.fsi` 配对、缺失引用和循环依赖。生产目录中新出现的文件也要检查，不能只检查已经登记的文件。

**编译顺序：由声明的依赖和分片内部顺序产生。**

跨分片采用确定性的拓扑顺序，分片内部保留 `<Compile>` 顺序，保证 `.fsi` 在对应 `.fs` 之前。

这一变化不能只做集合相等检查。还要检查输出命名、导入路径及模块初始化行为。若原先依赖某个隐含顺序，应把真实依赖表达出来，或改为显式初始化；不要为了复刻偶然顺序而补出一张接近全连接的依赖图。

**公共配置：归共享 props。**

将总工程中的包引用和必要编译属性移入共享配置，由分片和生成的临时工程共同使用。特别注意当前 `src/Wanxiangshu/Directory.Build.props` 的包引用带有 `Wanxiangshu.Owner.*` 名称条件，生成工程不能因为名字不同漏掉配置。

随后一起迁移这些消费者：

```text
scripts/build.mjs
scripts/lib/build-state.mjs
scripts/lib/owner-compile.mjs
scripts/lib/check-context.mjs
scripts/checks/architecture.mjs
scripts/checks/js-surface-manifest.mjs
scripts/owner-impact-report.mjs
相关 fixture 和门禁测试
```

退出条件：

构建和检查不再读取总工程；在隔离验证环境中移除总工程后，新路径仍能完成完整编译、模块链接检查和对应测试。

这一步完成后，才具备删除旧文件的条件。

### 工作包三：收拢成功凭证，再处理缓存

建议保留 `scripts/build.mjs` 作为唯一能够宣布“正式 dist 已有效构建”的入口。

你现在已有两个不同语义：

```text
build-manifest-v1：正式构建结果
owner-compile-v3 / .success：编译辅助状态
```

`detectChangedFiles` 按后者校验，而默认路径又指向正式构建 manifest。独立 compile CLI 还刻意不提交正式 manifest。相关代码在 `owner-compile.mjs:1129–1374`，正式格式在 `build-state.mjs:12–13`；这种 compile-only 契约也被 integration 测试明确锁定。

不要通过“让每个 CLI 都写一份成功 manifest”解决它。那会增加权威来源。

应当改成：

* 正式构建状态只由 `build.mjs` 写入，测试和打包只认它。
* 编译器只接受明确计划，返回编译结果，不自行宣布整仓新鲜。
* scratch 编译允许有自己的性能缓存，但不能把它当作正式 dist 凭证。

缓存键也要拆开：

```text
工程/restore 身份
= 工具链 + 公共配置 + 包依赖 + 工程结构

产物身份
= 工程身份 + 有序源码内容 + 实际编译选项
```

普通函数体变化不应迫使 restore 身份跟着变化；但产物缓存必须随源码字节变化而失效。`global.json`、实际工具身份及所有影响构建的脚本也应进入相应输入集合。

**不要在这一包里直接删掉 `--noCache` 就宣布完成。** 先让局部闭包正确，再用真实回归恢复编译器缓存。若缓存不能通过“内容改变但 mtime 不变”等测试，先在收窄后的闭包内保留 `--noCache`；不能拿可能陈旧的结果换速度。

退出条件：

连续两次无变化正式构建，第二次 Fable 编译调用为零；普通实现变化能得到局部计划；缓存损坏、工具变化、源码删除和编译失败都不会被认作成功。

### 工作包四：封住绕过正式构建的写入入口

当前 `compile-owner.mjs` 和 `compile-impact.mjs` 都允许指定输出目录。它们不应成为另一条写入正式 dist 的路。

建议最终保留以下职责：

| 入口                          | 最终职责                 |
| --------------------------- | -------------------- |
| `npm run build`             | 默认增量构建，唯一正式产物写入者     |
| `npm run format-build-test` | 日常完整验证，复用默认构建        |
| `npm run verify:release`    | 同一实现上的发布级干净验证        |
| `npm run build:clean`       | 显式重建，用于诊断或恢复         |
| `compile-owner.mjs`         | 隔离的分片边界证明，只写 scratch |

将 impact 的计划展示能力收进 `build.mjs --plan`。这是建议新增的接口，目前还不存在。调用方迁完后，删除独立 `compile-impact.mjs` CLI，避免再保留一套自动变化检测和输出参数。

正式写入过程继续使用一把构建锁。写入开始前使旧成功凭证失效；编译、生成资源、链接检查、输入复核全部成功后，最后写入新凭证。中断时宁可明确要求重建，也不能留下半新半旧、却自称有效的 dist。

局部更新还必须清除已退役输出。首批可以让源码增删触发受控全量清理，之后再优化成精确删除，不必一次做复杂。

退出条件：

辅助 CLI 不能写正式 dist；并发构建会串行；失败后测试和打包拒绝陈旧产物；删除源码不会把旧 JS 带进包。

### 工作包五：一次性切换默认路径，删除旧总工程

在前面条件满足后，同一合并批次完成：

```text
删除 src/Wanxiangshu/Wanxiangshu.fsproj
删除 DEFAULT_AGGREGATE_PATH 等默认项
删除生产入口上的 --aggregate 参数
删除“从总工程过滤输入”的实现
迁完仍读取总工程的检查和报告
更新直接使用该文件的测试 fixture
```

不要把旧总工程改名成 `Legacy.fsproj` 留在源码目录，也不要保留“新路径失败就退回总工程”的分支。

过渡比较只存在于迁移分支：旧输出和新输出分别在隔离位置生成，用于验证。主分支完成切换后，不再同时维护两套可用构建制度。

这里也不建议依赖一个“禁止构建”的 MSBuild Target 来挡住所有 Fable 用法。最可靠的退役方式仍是：调用方迁完，然后文件确实不存在。

退出条件：

原有默认命令自然进入新实现；旧总工程路径直接失败；任何新构建错误都原样返回，不触发静默全量旧路。

### 工作包六：修改说明，并加防回退门禁

同批更新：

```text
AGENTS.md 中的构建与验证约定
README.md 的开发、测试、发布说明
requirements/structured-workflow/WHAT.md、HOW.md
requirements/verification-system/WHAT.md、HOW.md
requirements/verification-system/tests/proof-ladder.test.mjs
.github/workflows/ci.yml
```

历史拆分提案应明确标为历史材料，不能再让其中的操作步骤承担当前构建指引。

门禁扩展现有 `scripts/check.mjs` 体系即可，不另建 runner。它至少要能发现：旧总工程被重新加入；活动构建代码重新读取它；日常入口重新加上无条件 clean；辅助编译绕过正式写入入口；未知参数被忽略后仍继续构建。

再为这些门禁加入反例测试。把一个旧入口重新放回 fixture，测试必须变红。只检查文件里有没有某个字串，不能证明整条调用路径受控。

退出条件：

新同事只看 README、Agent 只读 AGENTS，都能走到同一条默认路径；有人把旧行为带回来，标准检查会失败。

## 四、验收不能只看“编译成功”

建议把以下场景放进现有正式测试体系。临时命令输出可以帮助调查，不能充当切换完成的凭据。

| 场景                                | 必须观察到的结果               |
| --------------------------------- | ---------------------- |
| 无任何输入变化，再构建一次                     | Fable 编译调用为 0，正式产物不被重写 |
| 普通实现变化，契约未变                       | 只选必要闭包，不无条件带入下游        |
| 签名变化                              | 所有真实受影响消费者被纳入          |
| 内联实现变化，签名文本未变                     | 调用方产物更新，不执行旧内联代码       |
| 同批改多个分片                           | 闭包先合并，重复源码只输入一次        |
| 只改文档或 JS 测试                       | Fable 编译为 0；必要的派生产物仍更新 |
| 新增未归属的生产源码                        | 明确失败，不 fallback        |
| 删除、重命名源码                          | 旧 JS 消失，打包中不存在幽灵文件     |
| 修改源码但恢复原 mtime                    | 内容变化仍被检测到              |
| 修改工具链或编译配置                        | 正确失效，并打印全量原因           |
| 构建期间输入发生变化                        | 本次不写成功凭证               |
| Fable 非零退出或被终止                    | 错误上传，不能通过测试新鲜度检查       |
| 两个正式构建同时运行                        | 同一把锁串行，不相互覆盖           |
| 在 fixture 删除一个必要 ProjectReference | 该分片独立编译失败              |
| 移除旧总工程                            | 默认构建、门禁、发布均不依赖它        |

文档变化那一行尤其要注意：本仓的 LoopDetectorEnvelope 会使用仓库语料。**“不重编 F#”不等于“什么也不做”。** 当前代码已经区分 compiler、generated、artifact 输入，应该保留这个分层，而不是把所有变化重新塞进全量编译。对应实现是 `build-state.mjs` 的三组输入收集和 `build.mjs` 的构建决策。

另外，需要做一次正式的产物等价验收：

```text
同一源码状态
→ 从零构建完整产物 A

另一个隔离环境
→ 构建基线
→ 应用正式测试中的变更序列
→ 得到增量产物 B
```

比较完整文件集合、可执行 JS、导入/导出关系、资源和行为测试结果。非确定性诊断字段可以单独处理，但不能靠“忽略所有差异”取得等价。

这项测试证明的是“增量构建可以替代干净构建”，不是“某次编译进程退出了 0”。

## 五、开发者具体怎么用

### 现在就能运行的只读诊断

下面两条都是现有命令，不写构建产物：

```bash
cd /home/kunweiz/Desktop/vibe/wanxiangshu

node scripts/compile-impact.mjs \
  src/Wanxiangshu/Foundation/FatalProcess.fs \
  --plan-only
```

```bash
node scripts/compile-owner.mjs \
  src/Wanxiangshu/Wanxiangshu.Owner.host-boundary.host-fatal-effect.fsproj \
  --plan-only
```

前者是当前增量计划，后者是分片独立闭包。两者的差距就是首批修复要消除的额外范围，不能拿后者的 scratch 编译替代正式 build。

### 切换后，日常习惯不用改变

修改生产代码后：

```bash
npm run build
```

再通过现有标准 runner 运行相关测试，例如：

```bash
TESTS_MJS_FILES=requirements/structured-workflow/tests/owner-impact-compile.test.mjs,requirements/verification-system/tests/build-freshness.test.mjs \
node requirements/verification-system/tests/run.mjs
```

日常完整验证仍然是：

```bash
npm run format-build-test
```

发布验收仍然是：

```bash
npm run verify:release
```

不要求开发者手工提供所有变化文件，正式 build 应根据上次成功构建的内容快照识别变化。显式指定文件主要用于诊断，不能成为绕过其他已修改文件的新漏洞。

`build:clean` 保留，但不能写成“遇到问题先清一下”。出现意外 full 时，先看原因；只有明确需要重建时才使用。

建议新增的 `--plan` 输出至少包含：

```text
mode
reason
changedInputs
selectedShards
compileItems
fableCompileInvocations
```

不要只输出一个 `focused`。本次已经证明，名叫 focused 的计划也可能包含全仓 98% 以上的输入。

## 六、回退方式和最终删除条件

回退分两个层次。

构建缓存有问题，但分片图和新构建结果可信：运行同一条新路径的 clean，重建产物。不能启用旧总工程。

切换本身存在正确性问题：回退完整切换提交，在隔离的旧版本上重建。不要只恢复一个旧 fsproj，与新脚本、新 manifest 混搭。

当前工作区已有其他任务改动，实施时应先把它们与构建切换分开保存或闭环，再建立明确基线。不要为取得“干净工作区”而执行 `reset --hard` 或批量清理。

旧路径的删除条件可以压成四项：

1. 所有活动消费者已经改读分片 inventory。
2. 局部编译与完整构建的产物等价验收通过。
3. 旧总工程不存在时，默认构建和发布验证通过。
4. 把旧入口重新放回测试 fixture，门禁确实变红。

第一笔修改，应当落在 `owner-impact-compile.test.mjs` 和 `planImpactFromInventory`，纠正普通实现变化被无条件放大的规则。

最后一笔修改，应当是删除 `Wanxiangshu.fsproj` 及其生产读取依赖。

这样切完，大家继续敲原来的命令，走的却只能是新路径。无需靠记忆，也没有可随手退回去的旧入口。

[1]: https://fable.io/docs/getting-started/cli.html "Fable · Fable CLI"
[2]: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/functions/inline-functions?utm_source=chatgpt.com "Inline Functions - F# | Microsoft Learn"
