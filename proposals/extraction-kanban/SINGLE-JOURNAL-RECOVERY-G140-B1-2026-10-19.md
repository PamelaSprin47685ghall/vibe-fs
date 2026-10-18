# G140-B1：同一 Journal 的恢复发送

基线 `8b5dd03f7`，库保持 `8bb1021 / 0.6.12`。G140-B1按下列范围验收；最终产品候选为706，包SHA256 `8572c309c3c5e64011e51972a70058c044b5cfd1478a650a460e64f2de4d2be2`。先前700固定包仍保留，SHA256 `2a7a80267723c9be3fe03cc526257e6585b5f446b5474d6919d3fb17316e5231`。701–704只改验证设施、夹具与文档；705–706把长期夹具接到正式F#注册边界。GAP-140整卡继续DOING，R12-U1继续暂缓。

## 合同与实现

本包只处理 non-idle `HostSessionNudge → SendGateNudge` 在同一 live AgentJournal 内的并发发送。按 Journal 对象以 WeakMap 持有共享 dispatcher runtime；资源消失后可回收。每次 active profile、pending claim 和真实落地判断仍读取 Journal，不缓存权限、预算或投影。flight 只覆盖同一 session、logical run、origin 和 gate/terminal occasion 的在途发送，完成即释放。

加入者等待同一 PromptKey 和传输结果。成功后，它通过原有 `ObserveGateNudgeAcceptance` 重读 exact profile 与 claim，按既有独立 lease 接管物理通知。登记抛错只影响该加入者，不毒化共享发送，不重发。传输拒绝不给加入者登记；Unknown 的 durable Pending 保留，同一 occasion 不重发；明确 Abandoned 后才可开启下一 sequence。

原有 physical callback 是单 owner：后登记的 lease 接管通知，旧 lease 的释放不能移除新 owner。本包保留这个合同，未改成广播。被替换的 Physical 等待者仍可能待决，是已在697原样复现的边界；本包不把它写成所有调用方都能收到物理通知。

idle/admission 使用 `SendIdleGateNudge` 和 quiescence permit，没有因为共享 runtime 自动经过本 flight；该路径的并发 claim 唯一性未由本包证明。其余显式构造 dispatcher 的只读或其他发送路径保持各自用途，不宣称系统只有一个 Runtime。

## 先红与独立验证

相同真实 SyncDelegate HandleTurn 消费者刻意绕过普通按 session 调度器，原始强断言完全保留：

| 输入与版本 | 实际恢复发送 | FailureRecorded | 两次观察 |
| --- | ---: | ---: | --- |
| 固定697，Node/Bun | 2 | 2 | 一次完成，另一次等待至8秒诊断 |
| 首代隔离原型，Node/Bun | 1 | 2 | 均及时完成 |
| 固定700，Node/Bun | 1 | 2 | 均及时完成；迟到重复仍不新增发送 |

原消费者仍因 `facts 2 != 1` 保持红，不能把它改写为整体验收通过。G140-B2 的领域命令仲裁尚未实现；普通公开 Host 调度是否可达绕行场景也未证明。旧680已有相同并发发送缺陷的证据仍在 G140-A 档，不冒充本包重新运行了旧680。

- 隔离原型的同 owner、不同 session/gate/terminal、合法关闭A再接受B、错误与冷重开反控均通过。它只证明原型，不能替生产包验收。
- 最终700在两个干净安装中核对全部2076包文件，前后字节不变。非作者 Node/Bun 22项通过，第二个独立安装的便携复跑同样22项通过；重复运行不重复计语义用例。
- 最终真实 writer 的 StoreRelease 丢回执：实际 append、fsync、释放锁后抛错，恢复0发送，重复0发送/0追加，独立 cold count1。健康正控实际发送1、cold count1。不能据此宣称所有 Workflow 或无 Host-stop 的普通冷恢复已验。
- 六项永久回归已加入003/008：真实 delegate 并发发送及迟到重复；物理接收晚于或早于回执；加入者登记抛错；明确拒绝；Unknown。同两文件正式18 pass / 0普通fail / 2 TODO，owned 收尾通过，exit1为部分证据。003的事实仲裁 TODO、008的完整 post-flight TODO 都未删除。

## 扩大验证发现的问题

700的142个相关文件首次为607 pass / 3普通fail / 6skip / 93TODO，全部完成排空，owned 收尾通过。三项原红保留，逐项归因：

1. **先前R1引入的监督竞争。** 上一用例 end ACK 已返回，但 native complete 尚未交付；下一真实 TestContext 的 socket begin 先到，旧 owner 误拒为重复申请。现只允许一个新 token 等待，不授予临时额度；旧截止保留到匹配 complete，下一次真实 dequeue 才授权。两项永久确定性反例（含同名下一用例）先红转绿。非作者 Node26和实际Node22.13真实 node:test/socket 受控交付对照确认：旧版第二 action 不执行，新版两个 action 均执行；未伪造 native 事件。702完整原R1七文件323 pass / 0fail / 0skip / 0TODO。详见 R1 报告追加记录。
2. **既有005夹具改写原件。** 697/700同输入实际都报材料身份冲突。夹具把同一物理用户消息正文从 provider request 改成 tool result，违反原件不可变；不能拿它要求 ProviderStarted 接受改写。现于首次呈现前保留原始 Host 请求副本，明确断言改写拒绝，再用原副本验证后续 assistant 复用 phase、仍只有一条 ProviderStarted 且没有替换发送。它不代证真实 tool-call/result carrier 全链。
3. **021夹具与先前review归因不准确。** 021试图经新 live append 建立旧 Inspector 历史，被严格 decoder 拒绝；697/700同输入都失败，仅证明不是B1新增。最终回溯发现667该输入仍通过：它写原件并自动cut；680的有意收紧改为拒绝不可解码的新Journal写入。真实667写盘原件在706新OS可完整raw审计、字节不改，所以先前“历史审计reader缺失”的归因撤回。永久用例现读真实旧物理文件，保留原cut，并新增当前live append拒绝旧身份且零写的反例。FactCodec历史类型解码TODO仍在；读取原件不等于恢复活跃权限。

704同142文件当时为 **609 pass / 1普通fail / 6skip / 93TODO**，142/142完成排空，owned收尾通过；唯一普通失败是上述021的历史构造前提，整套不是绿色。R1竞争与005夹具均转绿；005的同一修正还在固定697/700、Node/Bun四格实际通过，证明未靠产品放宽原件规则。所有skip/TODO不算通过，不能以本包范围验收抹去它们。

最后静态门禁查出本代理把隔离研究夹具原样长期化时，五处读取Fable union内部形状，违反JS边界规则。未豁免或隐藏：现由既有注册的DispatchSurface调用真实HostSessionNudge，F#模式匹配后只返回 `{kind,key,error}`；typed Journal与observer均留在F#，lease也经现有Surface释放。补入008法律映射及Surface owner的直接项目依赖，没有另造发送实现。

706与700的2076包文件逐一对比，仅DispatchSurface.js变化，其余2075文件字节相同。正式加入js-semantic-surface六文件时为 **148文件653 pass / 1个021夹具普通fail / 6skip / 95TODO**，148/148完成排空、owned收尾通过；原日志保留，不把这次红改写成通过。收尾已校正021的物理历史前提，后续结果另见[八小时收尾记录](EIGHT-HOUR-CONSTRUCTION-2026-10-19.md)。构建、八个改动F#文件的格式及静态门禁通过；report-only提示未当作关闭。700原型内部DU探针保留研究身份，不冒称迁移后的长期JSON合同证据。

最终706的非作者长期夹具10项及两个Host的Surface-only范围程序通过；真实第二次干净安装与便携复跑通过。只用JournalSurface和DispatchSurface，未读取Identity/Model/Nudge、内部Journal或DU形状。另验无active root/非法kind零发送、零lease及零写，不同session/gate/terminal独立，真实接收的latest owner、完成后与重开拒重及重开新occasion正常发送。24回执和PID包含两轮同程序，不重复计语义用例。

八小时收尾的710选集为 **148文件655 pass / 0普通fail / 6skip / 95TODO**，148/148完成排空、owned accepted；因既存待证项仍退出1。021新增负控先错误假定events目录存在，只有该夹具断言红，显式建立空目录后转绿；review也提前发现并修正结构化error误当字符串的测试笔误。710发行包SHA与706完全相同，2076文件零变化。raw审计已验，FactCodec历史解码TODO未移除；详见八小时收尾记录和真实旧原件档。

首代原型还静默丢掉加入者的观察者登记，虽与旧版同为一个 Physical 等待者待决，却改变了接管归属；非作者发现后已在交付前修正。原型首次增第三参数漏括号的编译红、永久并发夹具未给 unresolved top-level await 保留诊断出口的首红分别归施工与夹具，不归产品缺陷。物理通知广播的额外断言在697也为红，属于过强 review 假设，不据此改通用 callback 合同。

## 下一包与验收边界

G140-B2 先在现有 StoreLock 内做私有正确性原型：fresh canonical Current → 同步纯领域决策 → fresh parents/envelope → append、Current commit → release。Host发送在锁外，budget owner 独占决策，不在 Journal 或 Store 复制去重算法。

生产前先解决 canonical freshness 合同。现 `ReloadLocal` 会验证、k-way merge 并从初态全量重放；解码缓存不等于增量 Current，直接接进每次命令违反 durable-events[017]。新 writer 流还能插入已有规范序列之前，不能把陌生 suffix 随意接到末尾。原型可明确用全重放作参考，不能据此直接上线。

按下列次序推进，每步各自留证：

1. 私有 reference 原型先用已暖 writer，真实两 Journal、两OS在相同 stale快照后并发：同身份只一事实；不同身份合法count1/2；冲突reason拒绝但writer健康；冷读无cut。此范围不偷带 lazy init 已解结论。
2. canonical owner 明确持久 frontier、锁内 freshness 核验、合法增量及重排/保留期反例，验017内容I/O与历史成本。未定合同前不批量传播接口。
3. 迁移纯领域命令、writer接纳/关闭/poison、NoWrite与revision。父边必须在fresh锁窗内选择；不能只移动count判断。lazy RuntimeStarted保留初始化失败时业务未尝试的合同，不递归获取同一物理锁。
4. NoWriteReleaseFailed、提交前故障和提交后Unknown保持原阶段/原因；领域拒绝不伪装为存储故障。跨Journal/OS的单次发送仍由durable prompt claim owner决定，重复失败返回预算结果不等于发送权。
5. 实际普通冷 Workflow 在缺当前进程 Host-stop 证据时零发送，连同真实消费者、故障、冷OS、固定包与非作者复核后，再判断GAP-140整卡是否完成。

已补B2修前真实跨OS反例：固定700，Node22.13与Bun各一次，两worker在相同root/logicalRun、count0处ready后释放。同身份两端均RetryAuthorized、物理事实2、第三冷OS count1；不同身份也各写count1，冷OS正常boot但仅保留last-good1，透明转发的实际Journal积分规则记录另一条FailureRecorded校验拒绝，原件没有ProjectionCutTail。不能把boot成功写成正确接纳两次失败。16个子进程正常退出无残组，未测Host发送；这是B2确认红，不是修复验收。

证据与确切哈希见 `evidence/G140-B1`。未发布registry，未部署Host。
