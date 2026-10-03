# institutional-learning 测试与证明范围

GAP-181 的 BIRTH 链路已按方案 B 落地：调用方提供 candidate（语义抽象归模型），Enhancer 做机械准入（TipName 唯一、双语叶子完整、trigger/negative 非空），commitLearning 带 revision 合同（漂移重评一次、二次冲突明确失败零提交），InstitutionalRuleBorn 进入统一 durable substrate 并在投影层合流。通用语义提炼的质量仍需人工审阅；substring 匹配仍是回落路径，不是抽象 oracle。

| 条款 | 当前实际证据 | 尚缺证据 |
| --- | --- | --- |
| [001] | 真插件两种工具接受口语经验；空白拒绝不消费 occurrence，同一 occurrence 改为有效输入可成功 | 自然语言含义仍需人工审阅 |
| [002] | evaluator 三值结论含 BIRTH；learn 面证明一次漂移重评与二次冲突明确失败 | 真实 provider 调用次数（Enhancer 为纯函数，无 provider） |
| [003] | 改变传入规则名会改变 evaluator 输出 | 机制抽象与真实输入/能力隔离 |
| [004] | 真插件：合格候选 BIRTH 落 InstitutionalRuleBorn、重放冻结、TipName 冲突与双语缺失拒绝且 live rulebook 零变更 | 冲突拒绝的对外文案区分度（当前回落 DISCARD 文案） |
| [005] | trigger/negative 缺失的候选被拒绝；合格候选 BIRTH；局部路径/时间戳仍 DISCARD | 去重与长期价值的语义判断（归调用方，需人工审阅） |
| [006] | celebrate 与 regret 均可经合格 candidate 走 BIRTH；无 candidate 时诚实 DISCARD | — |
| [007] | 真插件返回学习收据后接暂缓项；regret 不弹出；冻结重放不消费新项；无新任务或 authority 变化 | 暂停 BIRTH 准入时验证学习未闭合不能提前弹出 |
| [008] | 纯投影的冻结/会话隔离/输入不变；真正插件关闭重开后恢复冻结收据和暂缓项消费 | staging、准入、持久提交及 revision 故障的原子性；单独事实名与实现的合同冲突 |

`Surface.commit` 直接应用合成事实，只证明投影。`008` 的重启用例关闭并重开真实插件，使用同一个 Git-private journal，覆盖持久恢复；它没有杀死进程或注入提交中断。`007` 的字符串位置断言只证明最终呈现顺序，不冒充 BIRTH 的中间时序证明。

缺口记录：GAP-180 为证明范围；GAP-181 已关闭（BIRTH 链路见上文，台账见 requirements/GAP.md）；GAP-182 / 49-D1 为 [008] 要求单独 `DeferredWorkResurfaced` 事实，而当前原子 projection 使用 `LearningDispositionCommitted.ResurfacedDeferredWorkIds` 的合同选择。没有为迎合实现删除原合同。

统一构建后可局部运行：

```sh
WXS_TIER_INTEGRATION=1 node --test requirements/institutional-learning/tests/*.test.mjs
```

正式交付通过 verification-system runner 选择全部八个编号测试，并联查 attention-regulation 的真实 celebrate/restart 用例。TODO 不计为完成，也不以“文件已整理”表示学习机制已经齐备。
