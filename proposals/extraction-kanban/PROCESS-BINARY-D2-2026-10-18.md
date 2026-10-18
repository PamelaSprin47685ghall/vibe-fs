# D2：二进制Git与存储消费者

状态：声明范围验收完成，随本原子提交交付；D1停止输出完整性同时修正。基线主仓`54c7a9266`，库`df14f48 / 0.6.8`。最终库`8bb1021 / 0.6.12`已正常推送原修复分支；主仓候选663，包SHA`3c0afec4f5aee947cff92a5a2e633a41d4e9728daa515f27295e634ed9b324e0`，2076个文件与工作树逐字节一致。R12-U1继续暂缓。不改NDJSON事实协议或Git对象格式。

## 本轮结果

Git只走真实plumbing与唯一物理监管，不再有自写ODB或失败后另起写入。二进制回执分别保留exit/signal、StopRequest、两路原字节、完整性及fault；对象/tree读不到内容保持错误，引用只在确切缺失时为None。WriterStreamSync、Gateway、Hook均短路，Unknown不重发、不缓存成功、不继续发布。旧可选manifest入口与v1内容兼容保留。

库0.6.12把输入iterator换成显式读取泵：读尽缓冲、取得真正end并等最后写回调完成后才end；read期间禁止重入，出错后不再写，close不代EOF。目标拒收只退订转发，不销毁借用stdin。Bun成功回执的非重入边界独立保留，不把0.6.11局部成功当成尾17修复。原661/662红、EOF确证及所有候选都保留。

| 最终证据 | 结果与边界 |
|---|---|
| 原017完整顺序 | Node正式19/19、owned accepted；Bun原生19/19，非作者固定安装再19/19；实际32/64MiB+17、冷OS读取及真实Git对象一致 |
| D1及文本消费者回归 | 12/12文件229pass、0fail、0TODO、owned accepted；停止后不交付假完整输出 |
| 真实Host/provider链 | 27/27、owned accepted，包含真实请求与冷恢复 |
| 相关存储组 | 52pass/0fail、6/6、owned accepted；3项GAP-151/验证矩阵TODO使整组exit1，不算完整收敛验收 |
| 真实消费者与写入裁断 | 两Host各10组WSS/Gateway；四次实际写入前后丢回执均到cut，保持Unknown且零重发/零后续发布；真实Hook128及stderr透传 |
| 公开产物及类型 | 固定663干净安装，2904文件及锁前后不变；同一真实F#编译JS双Host通过；15份fsi未变，沿用原编译和5负控，新增编译计数0 |
| 库边界 | Node94项88pass/6个Bun专用skip，Bun94/94；独立owner2/2；实际包12个物理场景，非作者6个Worker检查点及2个真实拒收场景 |
| 旧职责退出与闭包 | GitObjectDatabase及raw CAS已删，GitRuntime只依赖窄Node适配器，业务Port/Model无Git或物理能力；最终663独立闭包20/20、2/2文件、owned accepted |

Git与tests代理未参与输入泵实现，承担非作者复审；host代理参与原型，其后续结果只列实际包与消费者验收。受控source事件不是原生OS读错，目录fd0的EISDIR发生在Worker初始化，不能记作泵读取分支；生成代码插入点探针仅用于固定候选重放，不增加公共test hook。

以下经过按原输入保留，段内“待验”属于当时状态；当前结论以上表和最终收据为准。

## 问题归属与误判

| 归属 | 问题、修正与证据限度 |
|---|---|
| 原有实现，非D1引入 | async execFile未送stdin、exit默认1、失败stdout/成功stderr丢失、超时后仍运行；自写ODB误认权限/布局失败、Unicode编码和CAS假失败。旧源码摘要未变，基线反例保留。现在删除该执行与ODB路径 |
| 先前修复引入 | `ca238c0`把FileSink即时write计数当全量确认；0.6.9去掉错误门槛并等待flush，不恢复该guard |
| D1引入 | 停止后以scope/capture Closed交付假完整文本；收紧为无stop、无fault自然完成才交付。另replay根路径多退一级是验收脚本错误，已修，原实际日志不被覆盖 |
| 本轮D2迁移引入 | 固定64MiB cap、数组按长度比较UTF8名、relative repo重复绑定、Hook describe丢stderr、late Written后仍可送下一块均已修。0.6.9暴露pipe截断却无故障，0.6.10改iterator后又新增尾17漏交路径，最终0.6.12显式读取泵解决；不据旧管道已坏宣称新路径没有责任 |
| Review误判，已撤回 | exit0/missing/空stderr证明缺对象被pack权限反例否定；从ls-tree规范化mode重建raw size会误拒合法对象；早期猜stdout丢失被目标tee原件否定；FileSink重入未解释尾17，662红仍在，最终EOF针才定位iterator。目录fd0的EISDIR属于Worker初始化，不能冒称泵原生read-error覆盖 |
| 验证与原型缺口 | 首次沙箱ps拒绝不算owned通过；后在可观察自有进程的入口验收。原型漏read期间同步重入guard已补并独立验证。私有类型推断编译错误和三个控制金字塔已由门禁挡住，最终构建/检查通过；不把这些当已运行版本回退 |

不能把已关闭D1/D2扩大成整个Process、存储收敛或四模块全部验收。GAP-151、GAP-215、C/R7-A既有归因、R1/R2正式监督、Bootstrap与平台容量仍按现行计划；R12-U1暂缓。实际变化Hook延迟增加另列后续P2，见本报告成本段及施工计划。

## 已确认的旧问题

非作者在固定D1产物上分别用Node/Bun验证：async execFile的options.input没有送入stdin；真实exit7及失败stdout被转成1和空数组；成功stderr被丢弃；忽略TERM的命令可越过timeout继续写入自有marker后正常exit0。直接对象实现还会将权限/损坏错误当作None，将Unicode树名按Latin1编码，或在布局查询失败后另起写入。两份生产源摘要与D1开工前相同，归为原有问题，不归为D1回退。原始记录暂在`/private/tmp/d2-git-review-baseline.mkeYic`，交付前归档。

## 本包契约与施工顺序

1. 复用D1的ProgramEnd、EndEvidence、StopRequest、NoExecution；二进制回执另保留两路原始字节及完整性，执行故障不能覆盖已知终结。物理Git contract使用Result保留失败，对象/树必须取得内容才成功；引用另保留确切不存在。业务EventStore.Port/Model不取得Git或物理运行时能力。
2. 先验证复用现有Process物理适配器的binary输入、输出、有限deadline和成本。唯一监管owner不变；不另造execFile的终结/期限推断。Unknown写入不重发，输入按事先选定块长发送，不在Unknown后补写。
3. 删除自写GitObjectDatabase、失败布局缓存及没有生产消费者的raw CAS。真正Git plumbing负责对象、引用读取与规范编码，保留每实例不可变对象/树缓存及WriterStreamSync的stat缓存。现有local append早已退出Git路径，原“每次append24个spawn”理由不适用于当前代码。
4. 将WriterStreamSync、Gateway、Hook及验证fixture切到显式错误短路；任何未知读/写都不得假装缺对象或继续发布。cat-file missing也返回保留原回执的错误，不承诺确切缺失；有效ref的正常缺失另有正控。截断、非法OID/树行、权限错误和其他非零均拒绝。
5. 实际二进制写入与新OS进程冷读、Unicode/tab/LF树名、失败字节、信号、期限、启动拒绝和受控丢回执；真实Hook clean/单writer变化/多writer变化记录调用及耗时。对应WHAT、独立闭包、固定包干净安装、非作者复审与最终集成核验后才DONE。

## 原型中未通过与验收边界

SupervisedProcess原型Node已完成32MiB+17字节写入/冷读；普通Git测得median原生11.44ms、监督59.45ms。Bun的首个16MiB写入返回Unknown及ERR_STREAM_DESTROYED，只确认589824字节；该次没有补写，已请求关闭并清理。下一原型将在新调用前选择64KiB块长，不把16MiB失败抹掉或冒称传输成功。期限硬KILL可能丢目标回执，正确结果仍可为Unconfirmed，不能猜成SIGKILL；不借本包销R12。

64KiB新调用也复现同一计数误判，没有补写；非作者原生FileSink四正控证明flush后完整收到33554449字节。根侧在库只移除stdin即时计数门槛、仍等待flush及保留退出/断管Unknown，已以0.6.9固定包验收，提交`d50080c`正常推送库原有修复分支。包SHA `c0ed8fe889fe4e49cd19c7ae092c23892fdd9a73773c73ad40c8380ba409bd32`。Node/Bun各9项非作者故障边界、各15个原型session、类型/拆除5/5及源码相关套件和独立编译均通过。该guard归此前`ca238c0`修复引入，不能归原拆分或本轮D1；[FileSink API](https://bun.com/reference/bun/FileSink)及实际字节对照共同支持归因。Worker输入队列总内存仍未证明，不冒称Written代表目标消费。

首版D2复审又发现固定64MiB cap会拒绝合法64MiB及超限单事件段、Hook describe丢stderr、相对repo路径被重复-C，以及UTF8数组比较实际先按长度排序。均归本轮D2迁移问题，已修源码待回验。Raw请求现显式携带有限OutputLimit；Store先查确切metadata，再按size读取raw body，tree表示用int64推导上界后检查byte[]容量，避免把协议开销当产品内容上限。D1新发现停止后的完整性回退单列于[D1报告](PROCESS-TERMINATION-D1-2026-10-18.md)，不推给原拆分。

非作者进一步证实：不可读的.pack或.idx会让Git返回exit0、missing且stderr空，与真缺失不可区分。此前loose对象矩阵不能泛化为整个Git后端的缺失证明；只查stderr的建议已撤回。新原生证据83条查询、9项EACCES正控保存在`/private/tmp/d2-object-native-review-with-pack-evidence-20261018.tgz`，SHA `b36e62a16082fe823180489e10da614eedd2e587d52ef9407dcdd50468617b77`。ReadObject/ReadTree改为无option的Result，missing原样留在错误中；生产原先的None分支均立即拒绝，没有成功路径损失。旧历史缺manifest entry及v1内容兼容保留。ReadRef改用Git>=2.43的show-ref --exists：2才是缺失，quiet状态1不作证明；最低Git版本同步写入README。

659候选的正式Node二进制17/17、owned收尾，以及Bun原生17/17已通过；相关存储52/0但保留3项GAP-151 TODO，整组exit1。独立编译20/0的首轮受沙箱ps权限影响不算正式收尾通过，另在可观察自有进程的入口回验。

659又发现：同一真实fsi消费程序在Node通过，Bun首个真实Git alias exit7只收到1048576/2097169 stdout bytes，却为Stop=None、Fault=None、Complete=true。目标tee自有文件也截断；基础ProcessSession及原生Bun完整，排除只少收stdout。原始现场`/private/tmp/d2-typed-fixed-package-evidence-anu6Iw`、`/private/tmp/d2-typed-fixed-package-evidence-seaVR9`，随后封存于`D2-TYPED-659-BLOCKER-EVIDENCE.tgz`，不以17项局部通过掩盖。

## Worker输入顺序修正与660候选

私有最小桥证实：Bun source已发完2097169字节，原pipe只向目标write四次262144，随后final，最后一块根本没有进入写端。逐块await回调再end的原型完整；默认async iterator提前return又触发ABORT_ERR，故生产用destroyOnReturn:false保留借用source。目标拒收只停止转发，真实source读取异常独立报fault，不补写、不提前丢后续输出。旧0.6.8在同例为Unknown，0.6.9为Written且缺字节无故障，属于0.6.9的可观察回退；不能把它归原拆分、D1，或恢复错误计数guard来掩盖。

库0.6.10源码Node92项88pass/4个Bun专用skip、Bun92/92；新增真实随机tee、目标关输入后继续输出exit23，原32MiB digest及取消/背压反例均通过。固定库包SHA `13fbbfd5ca9b6d3542b8041373352fdb98117638bd76d52900e7fb5edf3e1725`。非作者已实际安装验证原9边界、类型/拆除5/5、独立源闭包2/2；第一跳待决时强停仍Unknown，既有0.6.9和659红保留。

主仓660固定包SHA `0bb7211c4b1e7eca77c72a59177073b221548233f78d2be97ab4d86d6b98b348`，干净npm安装后，原同F#程序及编译JS与659诊断红逐字节相同，只切换产物，Node/Bun均通过；真实fsi30输入正编译、五负编译、删除生产生成JS后只接包dist。正式017为18/18且owned accepted，Bun原生18/18。最终真实消费者、Hook与物理故障矩阵复核仍在进行；R12-U1不销项。

## 661复验的新阻断

661只把树行解析拆成纯函数，2076个包文件中仅`ProcessGitRawStore.js`改变；Host、监管库及二进制执行尾部逐字节与660一致。Node正式017为19/19、owned accepted；Bun原生为18/19：33554449字节写入返回`284160124c6f2e4694b0fee82bfb1fe73746b962`，独立Node crypto确认完整输入应为`73cd9847570c1a6413a50a7063f8f698b62c46cf`，前者恰为前33554432字节的对象编号。最后17字节未进入该对象，不能归为比较器误判，不能用其他通过样本关闭D2。

原始红保留在`/private/tmp/d2-017-661-bun.log`，分层取证中。库0.6.10已提交`f712acf`并正常推送原修复分支，但这不代表输入职责已通过最终验收。接下来保留对象与目标字节，区分Host分块、第一跳、Worker读取和目标写端；先取得因果证据，不恢复即时计数guard、不靠缩小分块掩盖、不在Unknown后补写。R12-U1继续暂缓。

单次完整顺序的私有留档运行再次失败，这次67108881字节的对象只有67108864字节，独立Node/Git冷读逐字节证实恰少末17；完整现场见`evidence/R6-D2/D2-LAST17-661-RETAINED-RED.tgz`。单例及逐块同步日志副本的绿不冲销这两个红；日志副本只取得完整传输的正控，尚未定位丢失所在跳。

0.6.11候选只将Bun成功输入回调移到原生微任务检查点之外，错误/exit仍即时仲裁。两个受控确认/退出通知反例先红后绿，库物理套件Node94项88pass/6个Bun专用skip、Bun94/94；但662主仓原017仍为Node19/19、Bun18/19，33554449字节再次少末17。因此该局部修正不能称为物理尾丢修复，D2继续DOING。FIFO备用原型仅单跳可行，双跳停滞，不接生产。下一对照改为内存累计、EOF一次落盘，保留原输入及对象，减少逐块日志的扰动。

低扰对照再次18/19，parent末17的write/flush计数和回调成功，Worker iterator却仅交出33554432字节。下一次预登记EOF余量针取得确证：parent于17:22:33.350Z结束输入，Worker于.353Z认为迭代结束，流状态却为readableLength=17、readableEnded=false、destroyed/closed=true；一次普通source.read完整取出hex`fa000102030405060708090a0b0c0d0e0f`，随后原生fd0读取0。该尾部与完整输入逐字节相等。第一跳已送达，当前损失在Bun stdin iterator交付，不在目标写端。诊断按登记故意不转发读出的尾部，不能拿这个破坏性运行算修复验收。[EOF确证档](evidence/R6-D2/D2-LAST17-662-EOF-READABLE.tgz)，SHA`e2703225a67de1e67378c3637561b60fa4cea2bb215011b8636d259814a70483`。先验证显式readable pump能否保住真实EOF，再确定生产修正；不凭close推断EOF。

## 性能代价

非作者在固定D1-652与659上用真实EventStore事实、私有bare remote和原生Git Trace2对照：Node clean108→268ms、单writer223→1073ms、双writer245→1116ms；Bun clean87→239ms、单220→1029ms、双223→1135ms。实际Git启动数分别3→5、11→24/25；变化场景hash-object仍为2/3，只重blob变化writer与manifest，稳定writer的OID复用保留。另一次原始样本也归档，不能只挑更快的一次。

这是删除自写ODB并复用真实物理监管的实测代价，不能宣称“无性能回退”。clean仍零transport/对象I/O，变化后的远端bytes与本地逐字节一致，冷读facts正确。此本机样本不代表跨平台或总容量证明；后续优化须保住错误、背压、冷读和Unknown边界，不恢复旧错误吞没路径。
