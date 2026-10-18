# CUT-02-R7-B：真实上游背压

第二轮三小时窗口从2026-10-18 10:04:08 UTC开始，输入主仓`fa88bd65aac8`、库`e0246781bef9 / 0.6.7`，两仓干净。用户授权本轮非作者复审；R12-U1继续暂缓。本包先以物理原型推翻不可行方案，再接Process和Supervision。库`df14f48 / 0.6.8`已正常提交并推送原交付分支；主仓随本原子提交交付。实现、非作者审查与精确消费者均已完成，剩余边界见下文。

## 问题与实现边界

旧Bun原生pipe在Capture未消费时仍可吸收至少64MiB。R7-A逐路extent修复自然清组截断，没有兑现上游背压。旧0.6.7的两项公开入口压力反例为0pass/2fail，确切败于暂停时实际写者已写完；日志`/private/tmp/r7b-library-pressure-red.log`。不能用丢输出、杀进程、公开observed=0或单次RSS样本替代背压证明。

新Bun路径由唯一私有`native-process/BunProcess.fs`承接：在0700临时目录用绝对`/usr/bin/mkfifo`创建两路FIFO，先开独立非阻塞读端，再开阻塞写端；目标启动前删除路径及目录。真实Bun.spawn收到数值写fd，目标启动后关闭父方写fd。Node继续原生child_process，不依赖Bun、FIFO或新入口。

读端只按Readable需求及其有限预取作正长度非阻塞readSync。真实EAGAIN让出一次Immediate，再按1/2/4/5ms退避，成功后复位；每路最多一个重试，destroy撤销匹配的调度任务、先撤销fd身份再关闭一次。只有实际read返回零才是EOF。64KiB scratch私有复用，每次成功复制实际字节，避免覆写已交付块或为单字节块保留整片scratch。Readable高水位64KiB是软阈值：队列上界128KiB，另有64KiB scratch；不是整个Bun进程RSS上限。有限预取存在，observed=0不等于fd没有读取。

Worker出口也改用有界Writable：Bun FileSink对每块取得完整write计数及flush后才回调；借用的全局stdout/stderr不end、不关闭。R7-A逐路最终extent与外侧observed一致屏障保留，不能由出口flush独自证明接收方收齐。输入回执、单次启动、目标与监管范围的事实仍归原owner。

启动前创建/关闭失败不重试目标。关闭异常保留原启动原因并报告`output cleanup was not confirmed`，不能宣称资源已经释放；不盲目重关可能已复用的fd。目标已经启动后父写fd关闭失败，必须先发布真实Started/pid，再报告I/O失败，不得抛成“没有启动”而给重发许可。

这是POSIX能力，新增`/usr/bin/mkfifo`与FIFO可用临时文件系统依赖。macOS与Linux分别取得实测；不据此承诺Windows、PTY、SQLite、脱组后代或任意环境的整个进程内存上限。轮询有空闲CPU成本，调度退避上限5ms，实际唤醒受事件循环影响；32流验证只证明该并发规模的I/O可用性。PROCESS.md旧“本轮只以Linux实测”属首刀历史陈述，当前0.6.8范围以上述分平台证据为准；非作者建议后续统一归档澄清，不修改已冻结包。

## 原型反证与问题归属

| 分类 | 事实与处置 |
|---|---|
| 原有实现缺口 | 原生pipe/lazy均可提前缓存64MiB；最终改实际OS管道，不增加兼容缓存。 |
| 被否决原型 | Unix socket raw fd写到8192即EAGAIN，且依赖私有fd；FIFO+Bun.file(nonblocking fd).stream不推进；blocking fs.ReadStream在Bun32空闲流占住I/O资源，destroy不释放，只有写者退出后恢复。均未接生产。 |
| 本轮新增原型错误 | 用`highWaterMark-readableLength`禁止下一次读取，在暂停后部分读取的场景死锁；移交stream后finally遗漏destroy；forwarder杀目标后没等待退出。已分别修正，失败原件保留，不写成原拆分问题。 |
| 本轮新增实现错误 | 启动失败后destroy的close异常触发无人监听的error事件。非作者真实EIO注入复现，已加明确清理错误归属；原故障脚本转绿，刻意未关闭fd由探针最后回收，未冒称close成功。 |
| 测试/判断错误 | target导入assert会初始化stdio并改变非阻塞标志，原生pipe同样可EAGAIN；背压目标改用纯fs写者。Node flags前后可证，Bun额外fd探针未可靠解释，不套用Node内因。另有buffer观察器误读null、压缩大小误当展开大小，以及错误要求整组KILL后必有target信号回执，均只修验证前提，保留失败记录。 |

初次生产补丁被自动审批拒绝，理由是fd归属/清理风险及原型证明不足。拒绝后继续隔离补证并取得三名非作者审查；以原授权及具体证据从同一路径重申后获准，才修改生产。没有绕过拒绝，当前没有待批准动作。

原型日志保存在`/private/tmp/review-r7b-*`及`/private/tmp/r7b-*`。`r7b-fifo-nonblocking-final-forwarder.log`是45秒超时的失败候选；最终双跳每路64MiB成功是`r7b-fifo-nonblocking-final2-forwarder.log`，不能混记。冻结原型与当前0/1/2/4/5退避实现分别验收。

## 库与精确安装包验收

固定包`wanxiangli-0.6.8.tgz`，22files，压缩107,426bytes、展开432,206bytes，SHA-256 `7e5873932a9ddd4519115f5575eda46f09c0f37d50ad4e97def05dbaba76022b`。未覆盖旧候选；主仓vendor及lock使用同一字节。

- 七入口Fable构建通过：`/private/tmp/r7b-library-final-build.log`。
- 全库与scripts为354项：349pass/0fail/5skip/0TODO，`r7b-library-all.log`。skip为Node下四项Bun专用反例及macOS下一个Linux限定项，不计通过。物理Node88项84pass/4Bun专用skip；Bun11文件各正常退出，共88pass/0fail，`r7b-library-physical-{node,bun}.log`。
- 新增失败准备、实际启动后关fd失败、启动清理EIO的真实反例及公开大输出/32流空闲用例通过；最后001/006的Bun为34/34，`r7b-startup-capture-green.log`。非作者原EIO故障脚本独立转绿，`r7b-review-pre-spawn-cleanup-fixed-independent.log`。重复运行不相加。
- `review_process_a`对同一包离线安装，无源码/编译器/额外依赖，四个有界阶段exit0，`/private/tmp/r7b-exact-068-evidence-H1xQmq/summary.json`。32真实流时FS与独立写者可前进，discard恰关闭32个FIFO读fd；Process/Supervised×Node/Bun目标四场各双路128MiB总量，暂停确有背压，恢复后write回执、连续游标、hash、EOF完整。真实短读65,540次，队列及底层backing峰值73,728bytes；原chunk不变。调度观察取得0/1/2/4/5，Immediate及Timeout取消、同号fd复用100ms无误读/误关，pending=0。
- 同一独立包复验覆盖Node/Bun两种Host的R7-A自然extent、ENOENT及显式KILL。34个owned对象全Closed，22安装文件与archive摘要不变，scratch已清。匿名运行时FIFO计数1→2如实保留，文件系统FIFO0→0，不宣称所有运行时fd恢复初值。
- `review_bun_adapter_c`在Linux E5-2699A，以Node22.22.1/Bun1.4.2离线安装同一包，两Host各14场景及一项完整性断言通过：双API/双目标每路8MiB、早关输出、ENOENT、discard/KILL及leader先退出后的同组TERM→KILL。目标回执未取得时仍为UNKNOWN_TARGET_EXIT；未放宽成成功。安装文件/归档不变、fd前后一致、各场景finally收尾。短时连接失败后恢复，六个原始文件已导出到`/private/tmp/review-r7b-linux-0.6.8/`，逐个SHA与远端一致，provenance已更新；最终/proc自有执行进程为空。临时脚本和传输分片已删。剩余生成安装目录`/home/kunweiz/.r7b-review-c-20261018`的递归删除被远端工具要求额外人工确认，未执行或换路径绕过；不把该清理写成完成。未运行Host/PTY/SQLite。

## 主仓接线与验收

主仓安装同一vendor0.6.8，Fable构建629通过。Bun真实run/wire两文件26/26、128MiB spool1/1，日志`/private/tmp/r7b-main-bun-{bounded,spool}.log`。真实OpenCode Host1/1，`r7b-main-real-host-unsandboxed.log`；初次沙箱拒绝loopback监听，原失败另留`r7b-main-real-host.log`，不是产品结果。115文件相关组首轮也因沙箱本地控制通道拒绝中止，无权威完成总计。在允许本地通信的环境重跑796项：759pass/5普通fail/6skip/26TODO，`r7b-main-requirements-unsandboxed.log`；5fail全部work011，不能宣称扩大回归全绿。

非作者以同一原样work011在旧6cc27包、当前0d60包和工作区作三路对照，均为0pass/5fail/1TODO、exit1、无timeout，失败名称完全一致，`/private/tmp/work011-attribution-evidence-S5hPQP/summary.json`。排除只换0.6.8才引入这五项，未关闭既有评审绑定问题，后续另包诊断。GAP-092的549/256字节TODO仍实际失败，不计通过。

正式七文件runner为76pass/0fail/1integration skip/0TODO、7/7完成，owned收尾accepted=true，`r7b-main-formal.log`。format/check/diff通过；check中的523条shard-edge审计仍是现有report-only结果，不伪称架构问题清零。

精确主包generation629，2076members、展开13,399,152bytes，SHA-256 `0d60ceea580ccbe2c188a45ac6d36199736fc6f82f7b24823d09d0db00b38232`，`r7b-main-package.log`。外部实际安装后Node/Bun完整run/wire各26项、Bun128MiB五次各1pass/0fail、完整025为124/124且exit0。完整内层stdout/stderr及状态保存在`/private/tmp/r7b-main-package-evidence-TdwmPj/`，汇总`summary.json`；2076安装文件和archive字节不变，消费目录已清，`r7b-main-isolated.log`。本轮只打本地包，没有registry发布或用户Host部署。

本包只关闭R7-B所列背压与输出交接范围。完整Process、wire预算、Trace和Ownership父卡不随之关闭；旧PTY失败仍按下一节保留。

## 旧PTY异常与剩余范围

上轮6cc27精确主包的完整隔离025首轮失败继续保留。非作者同包完整复验124/124、exit0、无skip/timeout，证据`/private/tmp/cut-00-r3-evidence-5eB2A7/summary.json`；2076安装文件及archive字节不变，scratch已清。被动fixture观察器不注入生产owner。新绿不解释原失败、不算修复Unknown、不代替本包精确主包验收；C/R7-A及CUT-00-R3的边界继续登记。

R12-U1暂缓；完整wire预算B仍需单独施工。新输出路径已替代Bun无界兼容输出，未部署用户Host、未发布registry。
