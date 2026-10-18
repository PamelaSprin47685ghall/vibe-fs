# CUT-02-R7-A：收齐逐路输出后才自然清组

输入主仓`863670d02`、库`ca238c0 / 0.6.6`。库最终`44a750e / 0.6.7`，固定tarball集成主仓；本包不改变PTY接管协议、持久事实或命令启动次数，R12-U1继续暂缓。Bun上游背压仍归R7-B，不由本包销项。

## 原因、归属与反证

| 记录 | 归属 | 已确认事实 |
|---|---|---|
| R7A-01 | 原有监管收尾缺口，本轮I/O变化放大触发 | 实际writeSync回执均134,217,728bytes、零短写，旧0.6.4的12场丢3场，0.6.6的12场丢7场，却仍exit0。不能以旧版也有问题宣称本轮没有回退，也不能归为生产者忽略短写。两项fs hook失效另属旧Bun named-binding夹具前提。 |
| R7A-02 | 已定位因果 | 无库原生12场完整。真实已交付Worker的closed出现后立即清组，旧/新各2场丢失；同一Worker按确切收到字节再清组，旧/新各2场完整且无超期。Worker零字节回调不足以证明Bun兼容写出缓冲已交付；自然KILL截断仍在Worker中的字节。 |
| R7A-03 | 本轮候选新增错误，已修 | 初版82项虽绿，EOF时先Closed/唤醒wait、后登记完整性失败，会让事先等待者得到failure=null。新增两个待决wait反例先红，再将错误登记移到结算之前，Node/Bun各82/82。 |
| R7A-04 | 排除的归因，不算缺陷 | read(0)丢返回值、原生FileSink直接丢失及若干未触发样本均未取得因果证据；不能用36场未触发撤销确定调度反例。独立探针的转义和EOF前提错误分别留档，没有为它们修改生产语义。 |

证据：`/private/tmp/process-c-independent-output-receipt-matrix.log`、`process-c-independent-native-output.log`、`process-c-independent-worker-drain.log`。同一包与安装字节前后不变。结论只针对这条已证因果，不从计数反推任意操作系统/进程树已安全终结。

## 最终实现与职责

Worker在实际转发处分别计数，等两路计数Transform的**readable end**以及目标close，才发布本实例不可变最终extent。它只说明目标管道关闭及交给转发队列的字节范围；删除零字节flush“已排空”的伪证明。计数不复制Capture正文、留尾或业务账本。

SupervisedProcess使用自己的实际Capture observed逐路**恰好相等**作为自然清组许可，随后仍等基础close与Capture EOF。缺字段、旧布尔、负数、小数、字符串及不安全整数均控制失败；超数/通道矛盾为OUTPUT_COUNT_MISMATCH。未消费的缓冲在EOF前不算短数；确切EOF不足为OUTPUT_INCOMPLETE。错误先登记，再结清Capture和全部待决wait。

启动失败、控制故障、Capture失败、显式discard及KILL保留原收尾路径，不因自然屏障卡住。主动KILL导致的不足单列OUTPUT_INTERRUPTED，不能冒称完整输出。主仓只在本次真实Requested KILL后把该后果与UNKNOWN_TARGET_EXIT交还既定取消/超时裁决，其余输出与控制错误照常失败，不掩盖COUNT_MISMATCH。

这是私有实例协议，没有增加公开通用服务、旁挂持久账本、重发或按时间等“足够久”。控制目录仍按既有同UID信任边界，不声称可以识别任意恶意伪造的合法extent。

## 验收证据

日志除主包路径外均在`/private/tmp`。重跑不相加，skip/TODO不作通过。

| 范围 | 结果与日志 |
|---|---|
| 确定公开反例 | 同一暂停消费测试，0.6.6 Node/Bun各0/1，败于已提前KILL的Draining；0.6.7各1/1。`process-r7-independent-paused-{before,after}-{node,bun}.log`，不是用schema变化制造红例。 |
| 库物理全部 | Node/Bun各82/82，`process-r7-20261018-{node,bun}-final.log`。待决wait初版红2/2见`waiter-red.log`，原日志保留。 |
| 库全套与构建 | Fable七入口构建通过；348项347pass/0fail/1Linux限定skip/0TODO，`process-r7-20261018-library-all.log`。本机未执行的Linux限定范围仍未证明。 |
| 精确库包 | 0.6.7、22files/104,269bytes，SHA-256 `9fbab564f40ba85be78197e8e510c3c4928f7d9379f97e7718af9e29eae733a5`。0.6.6由Git历史保留，不覆盖同版本包。 |
| 非作者实际库包 | `review-r7-final-installed-complete.log`：Node/Bun各13项共26/26；环境、ENOENT、双路原字节、同组TERM/KILL、extent暂停/逐路收齐、零输出、Discard/显式Kill/控制失败、坏schema/换路、短EOF与事先wait一致。离线安装无源码/工具链，22files与archive摘要不变、目录清理。 |
| 独立源码及旁路 | 两名非作者检查最终实现，未确认新阻断问题；`process-r7-independent-node.log`15/15，Bun faults12/12和extent3/3；`review-r7-escape-paths-{node,bun}.log`各3/3，`process-r7-independent-boundaries-{node,bun}-verified.log`各3/3。不重复计入包的26项。 |
| 主仓相关128文件 | `process-r7-20261018-main-related.log`：997项900pass/0普通fail/25skip/72TODO。原549/256完整结果预算仍为失败TODO，不因本包变绿。 |
| 精确主包 | generation624、2076files/13,387,322bytes，SHA-256 `6cc27a69236fa29da2012fbec29ae448d278da27eb859bfb69ad327b5fda7762`，实际pack/隔离默认入口，`process-r7-20261018-main-package.log`。 |
| 主仓真实Host | `process-r7-20261018-real-host.log`1/1，实际网络provider请求、原件捕获和新OS冷读。 |

非作者精确0.6.7公开SupervisedProcess矩阵12/12：Bun宿主、Node/Bun目标各串行3场和并发3场，每场实际回执134,217,728bytes、逐路范围/游标/hash一致，自然清组；`process-r7-independent-package-verified.log`。首次11场通过后外部receipt缺失的诊断失败原样保留，原因未定；独立回执路径、fsync及producer本地回读后才取得12场完整证据，没有放宽字节或hash断言，不把两轮相加。

精确主包隔离安装的Node/Bun完整011/014各26项通过；Bun真实128MiB spool字节及hash补强5次，每次明确1pass/0fail，日志`process-r7-20261018-main-isolated-large-verified.log`。安装2076files及archive摘要不变，目录清理。补强脚本当时复制的PTY总标志过宽，后已改成明确scope；该large-only日志绝不证明完整PTY通过。

完整隔离025的首轮exit1，至少显示`local late-not-started (bun)`失败；外层异常打印截短了内层stdout，未取得完整可读权威汇总，不能写成124/124或猜123/1，日志`process-r7-20261018-main-isolated.log`。同一精确包对该完整用例单独补验1/1（`main-isolated-pty-local.log`）不覆盖原失败，不据此宣称整个矩阵通过。工作区完整128文件内的025已通过；两种环境分别记账。此项只登记待分层取证，不修改Unknown或归为已证R7回退，R12-U1仍暂缓。

正式Node定向七文件76pass/0fail/1integration skip，7/7完成、owned收尾accepted=true（`process-r7-20261018-formal.log`）；format/check与diff检查通过，未扩大任何豁免。库代码提交44a750e，后续仅规范证据提交e024678，两者正常推送原交付分支；主仓随本原子提交正常推送master。没有把本包有限验收写成全项目通过。

## 剩余边界

R7-A只修自然清组截断输出。R7-B仍须用实际OS管道/套接字或更合适的运行时原型兑现上游背压及内存边界；不能用更大缓存、maxBuffer杀进程或一次RSS样本替代。B的完整wire预算、GAP-085/159/140、Trace产品链、实际Bootstrap和脱组/平台矩阵仍按计划。Node正式包装器有界等待归CUT-00-R1，Bun正式监督/已有故障夹具证明归CUT-00-R2，不因本轮原生通过关闭。

提交成功、验证通过与部署运行分别报告；本包不部署用户Host。
