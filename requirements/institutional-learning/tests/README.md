# institutional-learning 测试与证明范围

当前实现只按经验中的规则名匹配返回 ABSORB，否则返回 DISCARD。它没有实现通用机制提炼、BIRTH candidate、规则准入或 revision 冲突重评。旧测试把“不含某个源码名字”或一个 DISCARD 样例当作这些能力已经成立，现已移除这种宣称。

| 条款 | 当前实际证据 | 尚缺证据 |
| --- | --- | --- |
| [001] | 真插件两种工具接受口语经验；空白拒绝不消费 occurrence，同一 occurrence 改为有效输入可成功 | 自然语言含义仍需人工审阅 |
| [002] | 当前 evaluator 对明确规则名与无匹配文本返回单一结论 | BIRTH、有界 revision 重评、真实 Enhancer 调用次数 |
| [003] | 改变传入规则名会改变 evaluator 输出 | 机制抽象与真实输入/能力隔离 |
| [004] | 尚无 BIRTH 可执行路径 | 双语唯一规则通过 behavior-diagnosis 准入；失败零变更 |
| [005] | 一个未匹配的局部路径/时间戳例子被舍弃 | trigger、negative/distinction、去重与长期价值的有效正反例 |
| [006] | 真插件对成功、失败经验都诚实返回 DISCARD，不启动工作 | 可复用成功机制与代价机制拥有同等的 BIRTH 机会 |
| [007] | 真插件返回学习收据后接暂缓项；regret 不弹出；冻结重放不消费新项；无新任务或 authority 变化 | 暂停 BIRTH 准入时验证学习未闭合不能提前弹出 |
| [008] | 纯投影的冻结/会话隔离/输入不变；真正插件关闭重开后恢复冻结收据和暂缓项消费 | staging、准入、持久提交及 revision 故障的原子性；单独事实名与实现的合同冲突 |

`Surface.commit` 直接应用合成事实，只证明投影。`008` 的重启用例关闭并重开真实插件，使用同一个 Git-private journal，覆盖持久恢复；它没有杀死进程或注入提交中断。`007` 的字符串位置断言只证明最终呈现顺序，不冒充 BIRTH 的中间时序证明。

缺口记录：GAP-180 为证明范围；GAP-181 为当前保守 evaluator 与未实现 BIRTH 路径；GAP-182 / 49-D1 为 [008] 要求单独 `DeferredWorkResurfaced` 事实，而当前原子 projection 使用 `LearningDispositionCommitted.ResurfacedDeferredWorkIds` 的合同选择。没有为迎合实现删除原合同。

统一构建后可局部运行：

```sh
WXS_TIER_INTEGRATION=1 node --test requirements/institutional-learning/tests/*.test.mjs
```

正式交付通过 verification-system runner 选择全部八个编号测试，并联查 attention-regulation 的真实 celebrate/restart 用例。TODO 不计为完成，也不以“文件已整理”表示学习机制已经齐备。
