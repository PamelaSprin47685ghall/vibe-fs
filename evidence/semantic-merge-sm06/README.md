# SM-06 施工证据

本批范围READY，以[计划](../../proposals/extraction-kanban/UPSTREAM-SEMANTIC-MERGE-2026-10-19.md)为准。

依赖SM-04/05已达本批范围READY。先沿实际生成工具核对Advisor回答、私有/公开身份边界和派发恢复。席位与游标只承诺当前进程；GAP-140 B2和旧TODO不销项。主仓合并事务尚未提交，未部署。

- `baseline-native-01`：361项306pass/11fail/3skip/41TODO，包含父子测试计数。普通红灯来自旧状态根、旧工具名与已误删的实际writer关闭探针；执行中的B2 TODO仍保留，未改并发断言。
- Advisor `red-01/02`分别是未开Manager工作和误读工具数据层的夹具错误；`red-03`才是真实反例：第二次咨询在新终态前返回FIRST_ADVICE。实现改用future终态与本次TurnFormalText，取消历史WorkRecord/快照补值，并复用每个live Journal的dispatcher。generation16编译通过；`focused-native-01`为65pass/2fail/1skip/1TODO，Advisor剩余失败是测试重复使用同一ProviderRun被事件owner正确拒绝，随后改为确切不同run。
- 失败预算closed场景红灯的真实物理回执已保留：closeJournalWriter随旧Handoff表面被误删。本批只恢复它对实际Journal.Writer.Release的调用，继续验证物理写入前拒绝、零发送和冷预算；不恢复旧Handoff流程。
- generation16中旧身份with-cut/same-event恢复，mixed仍在旧Mailbox标记处提前截断。`old-mailbox-codec-red-01`直接证明缺Concern解码。只增加确切MailboxSubscribed的只读历史形状，fold不恢复任何邮箱状态，AgentJournal新写入拒绝；未知case与缺字段仍拒绝。原有三份旧NDJSON字节、摘要、非法Predictor准入和精确cut断言均保留。旧.git根仍leave-unread；测试显式把原件放入新根以单独检验格式读取，不是生产迁移。
- `build-02`因兼容包装的port被推断为泛型、与obj签名不符失败，显式类型修正后`build-03`通过generation17。静态检查02退出0。`native-02`全选集360项316pass/0fail/3skip/41TODO；B2执行中的TODO仍见两条物理事实，不删除或改断言。
- `formal-01`为22pass/0fail/1skip，完整排空但因schema集成跳过不接受；启用后`formal-02`九文件23/0/0skip/0TODO，9/9排空、回收accepted=true，包含实际注册Advisor、公开selector、旧日志和合法Engineer的同cut正控。
- `authority-formal-01`七文件55/0/0skip/4TODO，`recovery-grounding-formal-01`六文件85/0/1skip/3TODO，均排空、回收accepted=true；正式入口因旧TODO/skip退出1。真实物理writer关闭/append前失败/释放后不确定/正常路径、独立冷读、success根和同Journal去重均在选集中；不由此关闭B2。
- `grounding-integration-native-01`启用被跳过的原生Host及实际provider序列化，4/0/0skip/0TODO。三个原始旧身份文件摘要仍与基线完全相同。Advisor已有注册链证据及未覆盖边界在requirements/advisor/tests/README.md逐项列明，不冒充完整物理provider或耐久席位证明。
