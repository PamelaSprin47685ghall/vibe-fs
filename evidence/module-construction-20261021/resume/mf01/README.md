# MF-01：独立 guest 原型证据

这里保存恢复施工后已结束实验的原始源码、实际依赖、反例与日志。它们是隔离原型，不是主仓实现、公开包入口或完整 MF-01 接收。Host fs 仅操作各实验自己的 scratch；没有生产授权、ReadOutput、Prepared/Committed ledger、真实 shell owner 或实际内存硬限额。

## 当前可核对的结论

- `a-base-red/` 是第一次冻结的七份输入。独立测试在 Node22/Bun 都证明：真实 guest 的结果已到达输出缓冲，但 exact Host owner 先进入 Closing 后，仍错误发布 Result。原红证未改写。
- `a-fixed/` 只修业务终局准入，并增加两个终局测试。Node22/Bun 原12项及独立8项分别全绿；Closing 竞争使用真实 ProcessSession 的真实 chunk，结果后重复帧的直接 dispatch 则只算状态检查。详见 `review/vibe-fs-mf01-a-terminal-independent-report.md`。
- `b-callable/` 增加 guest 内 Callable registry、同 proof receiver、留在 guest 的 thunk 参数与真实 finally。Node22/Bun 原16项和独立7项分别全绿；函数、undefined、稀疏数组和引用身份未经 JSON 往返。该结论仅限这里的端口机制。
- B 的生产接收兼容反例在两个 runtime 都为 **0/2、exit1**：class 命令被通用 passive 转换拒绝；普通命令的 toJSON 被执行、命令被改写，还多写了一次文件。旧主仓 compiled Workflow 的实际执行正控为 Node22 2/0、exit0，toJSON 调用数为0。**此 P1 尚未修，B 不接入生产。** 详见 `review/vibe-fs-mf01-callable-independent-report.md`。
- A 的可选“结果后未 await continuation 再发 RPC”实验未建立执行前提，不能计作通过或产品失败。该审查 fixture 的外层超时未取消内部轮询；原源和 exact harness 回收日志均保留，不应无人监督重跑。

原作者报告保留其写作时点的“待验/尚未实现”；后来的独立报告单列新事实，不覆盖旧证据。B 报告里“旧 membrane 把合法对象交到解释边界”一句不精确：现主仓 JsSandbox 直接把 raw 回调对象交给 parser；实际 class/toJSON 正反控结论不受此表述影响。

## 输入与复核

`review/` 保留原文件名，SHA 清单仍指向原 `/private/tmp` 布局。`a-base-red/`、`a-fixed/` 各保留5份手写输入和2份实际消费的 bundle；B 保留7份手写输入和同2份 bundle。完整原始清单分别为 `vibe-fs-mf01-a-gates-boundary-inputs.sha256`、`vibe-fs-mf01-a-terminal-inputs.sha256`、`vibe-fs-mf01-callable-pre-run.sha256`。两份 bundle 不能用之后临时目录的构建物冒充。

ProcessSession 来源为公开库45849f5，源码仍可从主仓确切 gitlink 获取。`legacy-source/` 保存未发布的 Sandbox F# 源、Fable 项目和声明，源 SHA256 为 `e8750dc9a7439e16084da329222215796e3e4a95b0e8ced40f75f003846c8497`。消费 bundle SHA256 为 `9157f06c7b4ca850c1e5f0ba709db4dcd0ad1e0c0ab9e92bdf34c192e0952eec`；ProcessSession bundle 为 `57ecdb37c1c7c00cca0b735853fb563c85b42efd9ca3290261a332cd0f199054`。膜仍有旧 guest epoch 和60000ms invocation 上界；A/B 未关闭此问题。

独立脚本使用相邻 `vibe-fs-mf01-vertical-a-20261021/`、`vibe-fs-mf01-vertical-a-callable-20261021/` 导入；旧 Workflow 正控还绑定当时的主仓 dist，关键3输入 hash 在清单中。归档布局为便于保存而分组，**不要从本证据目录直接运行**。重新验证时在新的独立 scratch 布局中手工恢复明确路径，保留原始来源，记录新输入 hash 和实际运行命令；不得覆盖旧日志，或声称只重建3个入口就复原了整个旧依赖闭包。

原始日志可能包含测试框架的行末空白；逐字保存，不经格式化。`.log` 不代表自动通过，兼容红例和早期 fixture 失败也在其中。没有 node_modules、二进制、活动 socket 或可复用的运行凭证。

## C期限候选与后续边界

`c-fixed/` 保存最终15输入和作者收据，F#膜由正常FableJS/esbuild构建，未手改生成代码。`c-node-round02/`保存作者严格串行Node42/0对应的旧fixture/首次bundle；最终fixture只补70秒单测上界、内部有限轮询和启动PID日志，F#只修注释，重建bundle仅生成PID注释正规化后相同。两个版本不能偷换为同一次完整验收。`c-baseline-red01/`、`c-baseline-red02/`分别保留首次不完整正向前提和修正后的公平旧B反例源码；两者的原exit1日志均在review。

C最终源已独立通过Node22/Bun各16项及真实缓冲offer/reply过期准入各2项，四条命令分别exit0。真实61秒async cat后同步write、归还原剩余预算和正常退出成立；这只证明长借期跨旧epoch，去掉60000ms同步invocation cap仍是源码diff证据。36个记录PID均ESRCH、私有目录消失，逐例assert nativeClosed、accepted排空、FIFO/FD关闭；15输入前后与含原B的24输入清单都通过。详见 `review/vibe-fs-mf01-c-independent-report.md`。prepared-unrun中的三项lease/T组合未运行、不计数。

用户随后要求流程优先、小问题只记录；D仅有 `review/vibe-fs-mf01-d-protocol-static-20261021.md`，未创建候选、未写源码。无效trusted resume预算、queued旧offer等候选边缘先记录。即使C期限机制通过，B的按用途投影、完整sanity、生产Host/resource接线、启动预算和实际内存硬限额仍需后续完成，MF-01没有整体验收。C/D未接入生产，不阻断本批未受其影响的主仓流程。
