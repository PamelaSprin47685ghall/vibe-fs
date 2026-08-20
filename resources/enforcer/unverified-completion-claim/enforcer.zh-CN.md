# unverified-completion-claim — Enforcer

## 定义
当“完成”的措辞比证据本身更强时，就触发本规则。真正的缺陷不是抽象意义上的“没有跑测试”，而是**认识论越权**：参与者手里只有候选修改、局部贡献、意图或尚未观察的预期，却把它说成已经成立的结果。

## 支配原则
“完成”是在陈述世界，不是在表达你对 diff 的感觉。

编辑只能证明 bytes 变了。推理可以证明修改在逻辑上连贯。compiler 可以证明某类类型性质。test 可以建立一个行为区别。canary 可以建立一条 live path 的观察。它们谁也不会自动替其他层级作证。

本规则真正盯的是 provenance 被抹掉的那一刻——“我改了”悄悄升级成“它好了”，或者“我的职责范围已经完成”悄悄升级成“整个结果已经验证”。

最危险的往往不是草率，而是长时间实现之后那种非常有说服力的自信：投入越多，心理上的确定感越强，也正因此越需要一个有权说“不”的独立观察。

第二种危险形式是 truthful premature finality：participant 准确说出了仍然存在的 required work，却把这份 account 的诚实、已经完成的数量，或 session boundary 的方便，当成停止许可。Truth 只能阻止 overclaim；它不会解除被自己点名的工作。

## 何时触发
当参与者明示或暗示的完成级 claim 超过了当前最强相关 observation 实际能够建立的范围时触发。典型情形包括：

- 只编辑了源码，却直接写“bug 已修复”，没有任何观察建立行为结果；
- 一个窄 unit test 通过，就把它升级成 integration、deployment 或 whole-workflow claim；
- 缺失 observation 属于另一个 office，却写得像那个 observation 已经发生；
- 把旧 commit、旧环境、上一次 green run，或“should pass”式推测当成当前证据；
- 明知仍有 verification gap，却只在“全部完成”之后塞一个尾注式 caveat。
- 明确把 required in-scope work 推给 “next session” 或 “later”，同时 participant 现在仍能对它执行 useful authorized action，而周围 prose 却暗示当前 mission 可以结束；
- 把经过时间、commit 数、克服的困难、大量 progress、整洁 checkpoint 或 handoff readiness 当成 finality 的支撑，而不是仅仅当成 cost/progress evidence。

## 不应触发
- 参与者诚实地说明自己的 bounded contribution 已经完成，但没有声称整体 behavioral result 已验证。
- Completion claim 明确带边界，例如：“source mutation 已完成；runtime verification 尚未观察。”
- 一个 bounded office 只如实关闭自己的 contribution，同时真实协议已把下一 obligation 转交给另一个当前存在的正当 owner，而且没有暗示更大的 mission completion。
- 与 claim 相匹配的 evidence 已经真实取得，足够新、足够相关，并且在真实 defect 存在时确实可能失败。
- 当前工作只是 planning、analysis 或其他不需要 execution 才能成立的非行为 artifact。

不要惩罚 role discipline。Coder 如果正确说“源码修改已经连贯；执行观察仍属于 DevOps”，它没有因为世界还欠一次 observation 就在自己的 office 里变成“不完整”。

## 与相邻规则区分
`tool-error-ignored` 表示已经存在反向 evidence，却被无视。`false-gate` 表示所谓 verification 根本没有可靠区分 success/failure 的能力。`release-ladder-skipped` 表示必须经过的 proof stages 被跳过。`guessed-not-verified` 更宽：某个具体事实仍停留在 guess。

Tie-break 看最后那次 speech act：如果核心病灶是把“当前知道的东西”升级成“complete”，优先使用本规则。

不要把 hypothetical future session 与 rightful owner 混为一谈。session boundary 不是 authority 已经移动的 evidence。

## 判定程序
把 completion sentence 改写成一个可证伪 proposition，然后逐项问：

1. 什么 observation 能够证明这个 proposition 是错的？
2. 这个 observation 是否真的针对当前 change、当前相关环境与 scope 被取得？
3. 如果没有，当前 participant 是否拥有取得它的 authority/capability？
4. 如果不拥有，是否守住了 role boundary，并让更大的 claim 保持 open？
5. 即使每句话都 truthful，participant 是否亲口指出了自己现在仍能继续推进的 required work？如果是，finality 就没有被赢得。

如果第 2 项答案是否定的，而 prose 仍然写得像 proposition 已经成立，本规则成立。

## 例子
- positive：“race 修好了，都好了。”实际只改了 patch，没有 concurrent reproduction，也没有相关 test observation。
- positive：“deployment healthy。”实际只跑了 local build。
- near-miss：“源码修改和 regression test source 都写完了。我没有执行测试；仍需 DevOps 建立行为观察。”这是诚实的 bounded completion。
- counterexample：相关 test 已经跑红，参与者看见失败后仍声称 success。此时 `tool-error-ignored` 往往是更锋利的诊断。

## Nudge
候选解法还不是已验证结果。

不要让 claim 比 evidence 更强，也不要把这个区别当成跨越无关 role boundary 的许可证。
