# ce / 13d 原 group 回收：只读事实与下一包

原件目录分别为 `/private/tmp/vibe-fs-ci-ce1bbdddc-37634223372/`、`/private/tmp/vibe-fs-ci-13dfaf2e4-37632696039/`。本调查没有改仓库、运行测试、发 CI 或 kill 任何进程。

## 已确定的事

两次都触发原 300000ms backstop，且没有全局 authoritative summary。ce 是 725 drained / 2 active / 97 queued；13d 是 697 / 2 / 125。不能据普通断言标记缺席或已完成 worker exit0 写全局 0 fail。

ce 原 unit7887–7889：原 group3131 在 inner exit 后第一次扫描仍有15244；随后原回收器报告 reclaimed，但 accepted=false。13d 原 unit7661–7663 同样为 group3065 残留15024、15264，随后 reclaimed，accepted=false。没有 inspection 抛错、无法解析 ps 或回收失败的错误文字。原错误 cause 为 undefined，未报告 `Could not complete owned runner termination`。

15244、15024 的 beforeEach/pre-import 均直接绑定 grounding/007 与对应原 inner parent。15264 缺少任何正式 PID 身份记录，只能记为原 group3065 residual 清单中的未知成员；不能因为 grounding 测试会 spawnSync 就说它必然是那个 child，更不能拿别处 CPU 数字中的15264当身份。

两次原件都没有成功 capture/freeze 的原 ps rows 或 captured other-group inventory。已知同 group 残留最终被原回收器处理；未知组是否存在、是否完整捕获不能补证。此前 source898 或本地 W0/W1 的 accepted=true 只适用于各自运行。

## 源码上的观察窗口

两个 exact source 与调查时 HEAD 的相关 blob 全相等：

- supervise-node-test.mjs：`ab395b1fc24a0fc51c4c422ae9d015799955a5c6`；
- time-budget.js：`557f37da79dcc872a19796da0a5352f22de359dd`；
- grounding007：`b2b4aa4f753f8e3c07862f95a2a41174d9197a03`；
- 正式006：`46c78b37630f2687c1b907566fe873ee9b1f8c8f`；
- supervised-tool-reclamation-tests：`5613437d97812c73f8a13e7cf09e86a1254356d4`。

当前 termination 的原顺序是：初次抓树 → SIGSTOP 原 group → 确认原 group 已停并记录 descendant 的不同 group → SIGKILL 原 group → 只等待捕获的不同 group → inner exit 与 termination 都结束后，verifyExitedGroup 对原 group 立即扫描。

`rememberDescendantGroups` 明确仅把 `group !== pgid` 放入集合。`awaitObservedGroups` 在该集合为空时立即返回；原同 group 的所有后代不在这一等待集合中。`verifyExitedGroup` 第一次看到非 zombie 成员就固定返回 false；即使随后在 SIGKILL_GRACE_MS 内成功回收，仍保持失败，这是代码明写的 fail-closed 行为。

因此“leader exit 已到，原同 group 成员尚在异步死亡过程中”是合理假说；已知 surviving worker 与约46ms内 reclaimed 相容。但没有原始 stat、信号发出时刻、首次/末次扫描 rows，不能唯一归因为该窗口，更不能证明存在不可杀进程、误归因 PID、捕获失败或未知 detached group 泄漏。

grounding/007 仍在实际工作：ce 直接 beforeEach 最后是 reversed-delivery wiring leaf；13d 是 reversed-calls leaf。它们的源码使用原 spawnSync Node mutation child、timeout30000，并完整断言 child status/信号及生产 composition 断言。以各自最后一条具有 elapsedMs 的外层启动记录为截止，同一 supervisor 时钟已观察文件占槽下界分别约29.313s/21.904s；这不是终态耗时，也未把 backstop callback 假定为恰好300000ms。本轮不足以认定 mutation leaf hang；ce 还在更后一个 mutation leaf，说明它此前确有推进。

## 下一有限调查与受控证明

1. **先定待证命题。** 区分“正常结束后留下活进程，应 fail-closed”与“已请求失败终止后，应在原 deadline 内排空整个已知 owned group 再向 caller 抛错”。保持 backstop/静默失败与 accepted=false 原件；不能把 reclaimed 后改成绿作为修复。
2. **先补 bounded 原观察而非重跑全量。** 在下一明确授权包里，优先复用现有 inspectProcessTree/native ps：只保存原 owner 与已捕获 descendant 的 PID/PPID/PGID/stat、phase、同一单调时钟、signal attempt/outcome、已知 group 集合、deadline 剩余值；不要建立第二 registry，也不要因缺信息扫描/kill 未知组。现日志缺这些，任何新的测量都必须标作新 evidence，不能回填旧 run。
3. **可确定执行的 native fail-closed 负控。** 正式006可用原 inner + 一个实际同 group、unref且stdio独立的 child：child 先通过原子 marker/IPC确认持有资源，再允许其父 worker 和 inner 正常退出；child 在确认后仍活着，原 supervisor 必须报告 residual、回收并非零拒绝。配 foreign group 存活正控，finally 只处理本 fixture 已有 creation/lineage 证据的 owner。这个可用 barrier 固定，不用 sleep；其证明是“leader退出不等于group空”，不是上面 SIGKILL 窗口的红。
4. **失败终止的 native正控。** 复用正式006 silence-caller-cleanup 与现有 supervised-tool-reclamation-tests。增加原同 group 的实际 worker+同步 mutation child、明确 ready barrier，再由原因果静默触发终止；caller catch 前独立 ps 核已知同 group/已捕获 detached groups均无非 zombie成员，原 HOME已清理，foreign group未受扰。保原 SIGKILL_GRACE_MS、不重跑选绿。若一次只能得到 clean termination，不得声称已重现 ce/13d。
5. **当前接口的确定性限制。** 现 inspectProcessTree 只覆盖 capture/freeze/不同-group drain；verifyExitedGroup 的 liveGroupMembers 是另一个直接 ps 调用。现有 API 没有原同 group 的 post-kill drain observation seam，所以不能仅靠现 API 保证每次 native SIGKILL 后第一次 ps 必仍有成员。不得通过 sleep、CPU压力、概率重跑或伪造当前 ps rows制造“原CI竞态红”。若必须做确定性 observer-level回归，先窄化同一原 processRecords port 贯穿 owned-group drain；受控 observation序列只能证明该 port 的等待/拒绝语义，需和独立实际 native正反控分层记录，不能冒称真实OS复现。
6. **候选最窄修复方向，仅在正式证据支持后实施。** 已请求 termination 的路径在同一个原 deadline/同一原捕获集合中等待 original pgid 与 captured other groups 排空，再进入原 post-exit验证；普通正常退出残留仍由原 verifyExitedGroup 拒绝。保 cause/双失败、unknown inventory不授许可、foreign保全、不新增超时或第二gate。当前只提出方向，不把本调查写成已证业务红或已完成修复。

正式验证应以006有限 suite与以上确定 barrier 正负控为主；不因额外等原 group 就放大300s或改 tier。实际最终处理仍以 WHAT[verification-system-005/006/008/010/021] 为边界。必须单独记录范围、skip/TODO、退出、原组回收与未捕获 inventory，不能将局部绿色写成官方全量、全 Host 或全清理闭合。
