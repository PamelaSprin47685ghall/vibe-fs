# 判决输送与 Host 参数 canary 修复（2026-10-03）

这是本批实施记录，不是新任务书。施工以 `e20b338be` 为起点，按用户批准的第一优先项处理 verification-006 静默与 S02 Host canary。后续排工从[现行总计划](../../TODO施工总计划-2026-10-03.md)与[分册01](../../TODO施工分册-2026-10-03/01-Host与验证.md)开始。

## 为什么修改

八文件官方运行在5000ms原窗口下中断，上游与本分支均出现032未排空。实测Node22原生032完整执行59 passed、0 failed，各叶通常200—380ms；C01判决在1137ms到达，但C05的247ms工作对应判决直到13564ms才到，后续结果集中排空。连续同步操作与Promise微任务让原生reporter物理输送得不到调度，父监督器无法观察已发生的进展。最后到达的判决和累计background诊断不能用于定位业务挂死。

旧全量集成的取消超时没有TRIGGER_CANCEL provider请求。canary按session idle到达序号推进，并用Manager请求计数选择业务阶段；自动Guard/恢复请求可抢阶段，迟到的旧idle不能证明当前物理消息完成。

## 修改与正式反例

- `run-inner`为测试child预加载公开`afterEach` hook，在叶完成后一次`setImmediate`，让Node原生reporter输送结果。不发送进展，不更改5000ms、并发或判决/背景分类。006真实子进程反例证明连续8项同步工作跨越静默窗口正常排空；末尾挂起并打印仍超时；after hook异常仍按失败传播。
- 032 canary改为按最新物理prompt和实际call历史路由，未知/structured最新user不回扫旧业务。SDK完成屏障绑定exact assistant.parentID、完成状态与当前Host状态。正常路径的自然provider后续步骤证明同call历史及schema稳定；业务错误与取消各有独立session/physical目标，在同一个真实Host生命周期执行。before同时检查session与physical parent，拒绝Guard/retry替身。取消仍先真实running、再外部abort、终态error与后续provider历史原值/键序；25秒预算不变。
- 旧idle判定、请求计数、回扫旧marker分别有先红后绿的正式反例。没有修改产品WHAT或共享生产实现；真实executor throw自动after的T180保持TODO。

## 验证范围

Node22与26的输送三项正式回归均3 passed、0 failed；相邻005/006/021两版均62 passed、0 failed、0 skipped、0 TODO。正式构建通过，165个Surface、822个模块链接加载；format/check通过。

| 正式入口 | 结果与边界 |
|---|---|
| 原八文件默认层，[Node22](baselines/2026-10-03-s02-eight-node22.log)、[Node26](baselines/2026-10-03-s02-eight-node26.log) | 两版均103 passed、0 failed、14 skipped、7 TODO；8/8排空、无WATCHDOG、进程清理accepted=true。因未完成证据退出1，不是完整验收。 |
| [完整integration](baselines/2026-10-03-s02-integration.log) | 34/34排空，364 passed、1 failed、15 TODO；032真实canary约19.5秒通过。唯一失败是speculative-investigation013 resident Predictor首次Host项目健康检查，服务已监听但/path未在原启动预算内给出响应；尚未分类根因，不以隔离绿色覆盖整套失败。 |
| 同一完整入口的子门禁 | harness278 passed、0 failed；distribution3 passed、0 failed、1 TODO。suites/package进程清理accepted=true；完整入口仍退出1。 |

本批没有重跑全量来碰绿色，没有调整5000ms静默、25秒canary或Host启动预算。一次[独立启动诊断](baselines/2026-10-03-s02-resident-startup-diagnostic.log)执行resident原driver，两次项目健康分别1424/1439ms，独立场景成功；不覆盖全量失败。installed1.18.29的InstanceStore通过Deferred共享初始化，不能假设每次HTTP abort重启plugin。健康采集器吞每次异常且任意HTTP状态都算响应，当前失败只证明窗口内没取得响应头；下次先补失败原因/初始化阶段观察，再安排受控并发反例，不先归因预算或放宽就绪。启动失败留作现有VS就绪审阅债，本次证据不证明它与输送补丁有因果关系。

## 独立发现与剩余范围

本批中间诊断夹具复现自动ManagerGuard与下一用户消息交错：旧Guard provider读取无exact committed lease，并出现恢复消息；另一次新用户`/message`返回HTTP500。[原始诊断](baselines/host032-guard-user-race-2026-10-03.log)保存中间版本的失败，不是最终canary或整仓验收结果。

这登记为GAP-223 OPEN，归W3独立施工。新H替代旧G租约是容量owner既定行为，不能在只读hook重建G租约。下一步受控安排G已接受/尚未发送两种调度，证明H的租约与执行保全、旧G按supersession正式结算、错误重试不污染H。参数canary隔离不同目标不是这项产品兼容性修复。

GAP-054仍缺全阶段监督审阅；GAP-055/T418/T419不可变候选未施工；GAP-217/T180真实执行器抛异常后的自动恢复未施工；其余424 TODO未由本批关闭。
