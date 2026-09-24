# cognitive-workspace — WHAT

## [001] 单一隐式 owner 画板与隔离作用域

每个认知 owner 在同一时刻恰有一份当前画板。owner 由已验证的工具上下文与当前 authority 解析为 `SessionId + 当前逻辑 Life/Incumbency 身份`，不是进程全局变量，也不是单独可复用的物理 SessionId。不同 owner 的画板互不可见、互不污染。子会话的声明只能写入子会话自己的画板。新 Life 从空画板开始；跨 Life 继承只允许显式、可审计的 bootstrap，禁止从宿主 UI 或上一个 Life 的内存状态偷偷继承。

## [002] jq 恰好一个输出，失败前不改变语义状态

`update` 是标准 jq 程序，以当前画板为 `.`，必须且只能输出一个 JSON value。零输出、多输出、jq 编译失败或运行失败都只拒绝本次调用：画板、todos、阶段序号与 epoch 均不得变化。工具红字向 provider 说明原因，不解释为系统故障。

## [003] 画板、todos 与阶段同属一个 committed snapshot

一次成功调用原子产生一份 `AssumeSnapshot`（完整画板 + 规范化 todos）与一条 `AssumePhaseCommitted`。todos 属于同一提交 envelope，但不强塞进自由画板内部的必填字段；模型仍可自由设计画板结构，工具不要求它在画板里手抄一份待办。不存在「画板已生效但 todos 未生效」或相反的中态。

## [004] exact ToolCallId 幂等与输入冲突分型

相同 `ToolCallId` 与相同输入的重放返回该调用第一次已提交的结果，不再次执行 jq、不增加阶段序号。相同 `ToolCallId` 与不同输入属于 typed identity conflict，交由现有不变量故障策略处理，不允许猜测哪个输入为真。不同 `ToolCallId` 的完全相同输入是两个真实调用，允许两个阶段，不靠文本相同隐式合并。

## [005] owner 串行、跨实例一致、不同 owner 可并行

同一 owner 的多个已物化 `assume` 按宿主工具调用顺序执行，后一次基于前一次的提交结果。跨 plugin instance 必须共用同一实际 owner 的串行 admission，不允许每个实例各建一把锁。不同 owner 不共享画板，也不共享无必要的全局执行队列。输入顺序与执行权来自 canonical Host identity，不来自异步回调返回次序。

## [006] 先持久后 fold，崩溃恢复不重放 jq

快照 Blob 必须先落盘并完成规定的耐久性动作，才允许 append `AssumePhaseCommitted`；append 成功后才 fold 更新内存投影。Blob 已持久而 Journal 未 append 时不存在提交，孤立 Blob 不成为新版本。Journal 已 append 而 fold 未完成时 Boot Fold 重建新版本，绝不再执行一次 jq。`now` 等非确定 jq 也只能在同一已提交调用上执行一次；幂等依据是已冻结的提交结果。

## [007] 全量、无损、冻结结果交付与输入资源 admission

成功调用返回更新后的完整画板表示。表示必须无损：`null`、混合数组、特殊键、换行与根标量都不得被悄悄丢弃或改写。同一 committed snapshot 的已返回结果采用冻结字节，不因 renderer 升级而改写历史回放。提交前必须做大小 admission：不能完整交付的画板在语义提交前被明确拒绝，不允许出现「成功写入但返回被截断」的假全量。

## [008] 阶段以成功提交建立，边界是 canonical semantic turn

阶段序号只由成功提交增加；失败调用不产生阶段。阶段边界是包含该调用的完整 semantic turn 的起始位置，使用 canonical XTrace generation 与 stable Host identity 表示，绝不是 provider 消息数组下标。同一 semantic turn 内可以有多个顺序提交，它们的边界可以相等。

## [009] 重锚恢复、代际隔离与当前快照可见性证据

Host compaction 后旧 generation 的阶段边界不得参与当前 cutoff 计算；新 generation 中尚无合法阶段边界时不给出新的阶段 cutoff。latest-only 退休的前置条件是当前画板有真实可见载体：最新成功调用的结果将被物化在当前请求，或冷重锚已安排一个明确的恢复快照。没有新载体证明就清空旧结果是禁止的。

## [010] 画板不接管权限、执行资源、完成判断与质量评审

画板是模型工作记忆的写入口，不是系统所有事实的数据库。它不授予角色权限，不推断进程是否活着，不证明测试通过，不替代 assessment 结论。`todos=[]`、画板里的「完成」或 `status=completed` 都不是质量证书或可退休证明。
