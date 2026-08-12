# Magic Todo — 理由

把 `todowrite` 提升为 Manager 生命周期的因果 checkpoint，而不是再叠一层 Todo/Review/Compression 阶段机。控制点来自真实工具事实，非法状态尽量不可表达；恢复只认 durable facts。

Manager BlindPlan：Pre-T1 = Planning Table（替他人规划）；第一次 accepted `todowrite` = T1 commitment；canonical result 揭示「路是你的」。交托在 tool result，不在 system prompt 切换。同一 Life 内 office prompt 永不因 T1 改字节。

## 备选与被拒

**生命周期：BlindPlan 持续 checkpoint vs planning→Activation 两阶段。**  
拒两阶段并存：旧 stage 机 + 新 Todo stage 会横向爆炸；Planning→Working 换 system prompt 破坏 prefix cache 并泄露「你已携带任务」（TODO-001）。删除生产 Activation；Opening 保护改由结构性 `WorkRecordStart`。T1 帷幕只在计划无法因「知道谁来扛」而改写之后掀开。

**BlindPlan 揭示：conversation T1 result vs Activation / `WorkActivated` / system identity 切换。**  
拒身份切换：office 不变，entrustment 可变。Planning Table → validate → durable `TodoWriteAccepted(T1)` → 含 revelation 的 canonical provider-visible result。generic `lifecycle.activation` 若作语义资产，只指「新 owner 获 responsibility」，不得承载 Manager phase 或触发 prompt 替换。

**Identity：tagged `kind` vs optional `id`。**  
拒缺字段猜新旧与 content 猜 id：恢复与并发会静默错配（TODO-002）。

**完成门禁：强制 `reviewing→completed` vs 任意跳 completed。**  
拒任意跳：过程真实性失去 Host 可执行边界（TODO-003）。其它转移放宽，真实性交给 process review，避免第二套枚举真理。

**Admission：同 message 全拒 vs ordinal winner；V2 fail-closed vs 裸奔。**  
拒 winner 仲裁：多一个不必要的排序面，且与 lag-1 单链冲突（TODO-004）。拒 V2 裸奔：双语义长期分叉。

**Settlement：PERFECT 替换 + REVISE min-merge vs 总 merge / 总替换。**  
拒总 merge：PERFECT 失去「以提交表为准」。拒总替换：REVISE 会丢掉仍应保留的 old 进度。content/priority 在 REVISE 上 proposed 赢、status 迟滞——明确协议而非启发式（TODO-005）。

**评审节拍：lag-1 1:1 vs 同次等待自己的 Rk / 无阻塞多飞。**  
拒同次等待 Rk：Manager 无法在评审期间做独立工作。拒无阻塞多飞：结算链失去单链公式（TODO-006）。  
**可消费结论：Concluded≡record-ready LWR vs verdict 即消费。**  
拒「只有 verdict、尚无 report」中间态挤进同一 fact：下一 checkpoint 会消费空报告或竞态半态。`VerdictKnown` 复用 Reviewer 域，不造 Magic bool/Stage。

**真相源：Journal canonical vs Host TodoTable。**  
拒 Host 表当 canonical：无稳定 id、整表 DELETE+INSERT、无法承载 reviewing/merge（TODO-007）。sink 可显示 working Pk，但 REVISE 消费后必须 reconcile；repair 不作 checkpoint。

**证据：bounded LWR + coverage 分型 vs 纯 Y / session head / 第二 renderer。**  
拒纯 Y：frontier 前合法 RawGap 会丢证据。拒 session head：串台历史污染单次 Rk/Finality。拒第二 renderer：双源漂移（TODO-008）。Prefix 只认 PrefixCoverage 可证 Y；LWR 只认 RecordCoverage。四段标题固定为 `Opening / Chronicle / Recent work / Closing report`；Closing = prose claim，拒固定报告 schema。

**Dedicated：首次 enlist + ordinary graduate + process 留到 LifeCompleted vs 永不 graduate / 每轮强制回流。**  
拒永不 graduate 特例：破坏既有 Finality 毕业语义。拒 Blessing 即释放 process session：后续 checkpoint/二次 suicide 无人可审（TODO-008/010）。process PERFECT 不计 terminal dual-PERFECT。

**Rebase commit：next-attempt seal 前 vs todowrite after 立即 commit / provider 成功才 commit。**  
拒 after 立即 commit：Y 可能未 ready，且与 attempt 绑定竞态。拒 provider 成功条件：失败会诱惑回滚已 seal epoch（TODO-009）。desired ≠ committed。  
**Cutoff：Before(previous call) vs After(previous result)。**  
拒 After(result)：刚返回的 review/settled/preview 可能立刻被压掉。

**Finality 未完成项：process PERFECT/REVISE vs 机械 terminal-todo gate。**  
拒机械全 completed 门：与用户过程评审需求无关且与 REVISE 续命冲突。仍要求 first unblessed suicide 至少一次 `TodoWriteAccepted`，防止零 checkpoint 绕过协议（TODO-010）。尾 drain 只能是 suicide，不能再 todowrite flush。T1 本身即该 Life 的第一次 accepted commitment，计入协议入口。

**Legacy：升级前一次 seed vs 每 Life 从 Host 表 adopt / 空表忽略旧项。**  
拒每 Life adopt：session 级 TodoTable 会污染同 session 新 Life。拒等首轮 todowrite 再分配 id：模型未见 id 无法 `existing`（TODO-011）。

**控制状态：facts + CE vs TodoStage/AwaitingReview。**  
拒程序计数器：与「君子不立危墙」及 crash 恢复模型冲突（TODO-012）。

**Manager 表面：报告可见、编排隐藏 vs 全藏 / 全露；guideline 局部 vs 并入全局 HOST-013 文案。**  
拒全藏：Manager 无法根据过程结论改计划。拒全露：session/barrier/2N 泄漏破坏隐藏 reviewer 与 Finality 代数（TODO-013）。拒并入全局 pair 文案：会污染非 Manager / Blogger 合同。LWR 不 regex 清洗；不能证明安全则 fail closed。

**条款所有权：what 单源 vs 各域复制合同。**  
拒复制：跨层漂移无对账点（TODO-014）。shape/how/proof 只挂机械与证明；跨域只交叉引用。
