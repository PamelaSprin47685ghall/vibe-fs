# institutional-learning 测试与证明范围

GAP-181 已有部分 BIRTH 实现：调用方提供 candidate，Enhancer 检查 TipName 唯一、双语叶子完整及 trigger/negative 非空，InstitutionalRuleBorn 可以进入 durable substrate 并派生规则投影。这些机械检查不证明 WHAT[003] 要求的机制提炼与限定输入，也不证明 WHAT[005] 的语义价值；substring 匹配仍是回落路径，不是抽象 oracle。新机械用例与原语义 TODO 并存。

`Surface.learn` 用不同规则快照证明 revision 漂移时最多重评一次、再次冲突返回失败的纯计算分支。生产路径随后逐事实调用 Append，没有绑定预期 revision 的条件提交（CAS）。BIRTH 先追加 InstitutionalRuleBorn，再追加 LearningDispositionCommitted；后者失败时规则已经可见而 occurrence 尚无冻结结果。同一 occurrence 重试会重新评估，已存在的 TipName 使 candidate 回落为 ABSORB 或 DISCARD，然后可能提交该结论。不能把这段半状态称为原子提交或失败后零变更。

born rules 目前只在 InstitutionalLearningTools 的私有规则合流中使用，尚未接入 behavior-diagnosis 的统一 Blogger system prompt、Main 处置索引与 chronicle.tip 查找；不能据此宣称 live Rulebook 合同已经完成。

| 条款 | 当前实际证据 | 尚缺证据 |
| --- | --- | --- |
| [001] | 真插件两种工具接受口语经验；空白拒绝不消费 occurrence，同一 occurrence 改为有效输入可成功 | 自然语言含义仍需人工审阅 |
| [002] | evaluator 三值结论含 BIRTH；learn 面证明一次漂移重评与二次冲突失败的纯计算分支 | 生产提交时的 revision 冲突与零提交；实际并发下唯一结论 |
| [003] | 改变传入规则名会改变 evaluator 输出；外部 candidate 的非空检查有可执行用例 | 从经验提炼机制、仅经验与 canonical live Rulebook 输入、真实能力隔离 |
| [004] | 真插件返回 BIRTH 并冻结重放；冲突或双语缺失的候选不返回 BIRTH | 与 behavior-diagnosis 共用准入与规则索引；真实预期 revision 预检；现有返回字符串断言不直接证明规则全量投影不变 |
| [005] | 非空 trigger/negative 的机械检查；无 candidate 的局部路径/时间戳经验回落 DISCARD | 可泛化机制、可识别 trigger、防误诊区分、语义去重与长期价值/注意力成本 |
| [006] | celebrate 与 regret 均可经合格 candidate 走 BIRTH；无 candidate 时诚实 DISCARD | — |
| [007] | 真插件返回学习收据后接暂缓项；regret 不弹出；冻结重放不消费新项；无新任务或 authority 变化 | 暂停 BIRTH 准入时验证学习未闭合不能提前弹出 |
| [008] | 纯投影的冻结/会话隔离/输入不变；真正插件关闭重开后恢复冻结收据和暂缓项消费 | staging、准入、持久提交及 revision 故障的原子性；单独事实名与实现的合同冲突 |

`Surface.commit` 直接应用合成事实，只证明投影。`008` 的重启用例关闭并重开真实插件，使用同一个 Git-private journal，覆盖持久恢复；它没有杀死进程或注入提交中断。`007` 的字符串位置断言只证明最终呈现顺序，不冒充 BIRTH 的中间时序证明。

缺口记录：GAP-180 保留 staging、持久提交与 revision 故障的正式证明；GAP-181 为 PARTIAL（已实现与剩余义务见上文及 [台账](../../GAP.md)）；GAP-182 / 49-D1 为 [008] 要求单独 `DeferredWorkResurfaced` 事实，而当前 projection 使用 `LearningDispositionCommitted.ResurfacedDeferredWorkIds` 的合同选择。没有为迎合实现删除原合同。

统一构建后可局部运行：

```sh
WXS_TIER_INTEGRATION=1 node --test requirements/institutional-learning/tests/*.test.mjs
```

正式交付通过 verification-system runner 选择全部八个编号测试，并联查 attention-regulation 的真实 celebrate/restart 用例。TODO 不计为完成，也不以“文件已整理”表示学习机制已经齐备。
