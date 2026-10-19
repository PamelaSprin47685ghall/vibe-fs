# unverified-completion-claim — Lesson

## 评价

### 定义

当“完成”的措辞比证据本身更强时，就触发本规则。真正的缺陷不是抽象意义上的“没有跑测试”，而是**认识论越权**：参与者手里只有候选修改、局部贡献、意图或尚未观察的预期，却把它说成已经成立的结果。

### 支配原则

“完成”是在陈述世界，不是在表达你对 diff 的感觉。

编辑只能证明 bytes 变了。推理可以证明修改在逻辑上连贯。compiler 可以证明某类类型性质。test 可以建立一个行为区别。canary 可以建立一条 live path 的观察。它们谁也不会自动替其他层级作证。

本规则真正盯的是 provenance 被抹掉的那一刻——“我改了”悄悄升级成“它好了”，或者“我的职责范围已经完成”悄悄升级成“整个结果已经验证”。

最危险的往往不是草率，而是长时间实现之后那种非常有说服力的自信：投入越多，心理上的确定感越强，也正因此越需要一个有权说“不”的独立观察。

第二种危险形式是 truthful premature finality：participant 准确说出了仍然存在的 required work，却把这份 account 的诚实、已经完成的数量，或 session boundary 的方便，当成停止许可。Truth 只能阻止 overclaim；它不会解除被自己点名的工作。

### 何时触发

当参与者明示或暗示的完成级 claim 超过了当前最强相关 observation 实际能够建立的范围时触发。典型情形包括：

- 只编辑了源码，却直接写“bug 已修复”，没有任何观察建立行为结果；
- 一个窄 unit test 通过，就把它升级成 integration、deployment 或 whole-workflow claim；
- 缺失 observation 属于另一个 office，却写得像那个 observation 已经发生；
- 把旧 commit、旧环境、上一次 green run，或“should pass”式推测当成当前证据；
- 明知仍有 verification gap，却只在“全部完成”之后塞一个尾注式 caveat。
- 明确把 required in-scope work 推给 “next session” 或 “later”，同时 participant 现在仍能对它执行 useful authorized action，而周围 prose 却暗示当前 mission 可以结束；
- 把经过时间、commit 数、克服的困难、大量 progress、整洁 checkpoint 或 handoff readiness 当成 finality 的支撑，而不是仅仅当成 cost/progress evidence。

### 不应触发

- 参与者诚实地说明自己的 bounded contribution 已经完成，但没有声称整体 behavioral result 已验证。
- Completion claim 明确带边界，例如：“source mutation 已完成；runtime verification 尚未观察。”
- 一个 bounded office 只如实关闭自己的 contribution，同时真实协议已把下一 obligation 转交给另一个当前存在的正当 owner，而且没有暗示更大的 mission completion。
- 与 claim 相匹配的 evidence 已经真实取得，足够新、足够相关，并且在真实 defect 存在时确实可能失败。
- 当前工作只是 planning、analysis 或其他不需要 execution 才能成立的非行为 artifact。

不要惩罚角色纪律。Engineer 完成本次源码任务、把缺少的执行观察交回 Manager，就已经完成本次任务。
使命还欠验证，不是强迫 Engineer 执行、差遣 DevOps 或等待案例整理的理由。

DevOps 停在普通可修复失败前是另一回事：直接非架构级修复和重新验证本来就归它。
Manager 缺少必要运行证据就接受源码完成，也是另一回事。
判断实际责任人及其主张，不只寻找「完成」二字或免责声明。
新修复改变了对象，之前的运行结果即使真实，也不能验证修改后的状态。

### 与相邻规则区分

`tool-error-ignored` 表示已经存在反向 evidence，却被无视。`false-gate` 表示所谓 verification 根本没有可靠区分 success/failure 的能力。`release-ladder-skipped` 表示必须经过的 proof stages 被跳过。`guessed-not-verified` 更宽：某个具体事实仍停留在 guess。

Tie-break 看最后那次 speech act：如果核心病灶是把“当前知道的东西”升级成“complete”，优先使用本规则。

不要把 hypothetical future session 与 rightful owner 混为一谈。session boundary 不是 authority 已经移动的 evidence。

### 判定程序

把 completion sentence 改写成一个可证伪 proposition，然后逐项问：

1. 什么 observation 能够证明这个 proposition 是错的？
2. 这个 observation 是否真的针对当前 change、当前相关环境与 scope 被取得？
3. 如果没有，当前 participant 是否拥有取得它的 authority/capability？
4. 如果不拥有，是否守住了 role boundary，并让更大的 claim 保持 open？
5. 即使每句话都 truthful，participant 是否亲口指出了自己现在仍能继续推进的 required work？如果是，finality 就没有被赢得。

如果第 2 项答案是否定的，而 prose 仍然写得像 proposition 已经成立，本规则成立。

### 例子

- positive：“race 修好了，都好了。”实际只改了 patch，没有 concurrent reproduction，也没有相关 test observation。
- positive：“deployment healthy。”实际只跑了 local build。
- near-miss：“源码修改和 regression test source 都写完了。我没有执行测试；仍需 DevOps 建立行为观察。”这是诚实的 bounded completion。
- counterexample：相关 test 已经跑红，参与者看见失败后仍声称 success。此时 `tool-error-ignored` 往往是更锋利的诊断。

## 建议

### 现在该做什么

不要让 completion claim 比 evidence 更强。

也不要把 truthful incompleteness 当成工作的替代品。如果你能够准确说出仍然存在的 required work，而且自己仍拥有一个能推进它的 useful action，就去执行那个 action。描述得诚实，不赚取任何 finality credit。

如果取得缺失 observation 属于你的 office，就现在取得它。使用最窄但忠实的 check——它必须真的有能力证明你的 claim 是错的。

如果那个 observation 属于另一个 office，**不要为了制造一个绿色结尾而跨越 role boundary**。把 candidate work 留在可被观察的状态，明确写出什么仍未被观察，并让整体 completion claim 保持 open。

最终宣告结果 complete 的 participant，必须拥有一种真实 evidence：如果实现错了，它有能力让这个声明失败。

### 为什么重要

工程里最常见的谎言通常不是故意造假，而是**语法膨胀**。

“我写了 patch”变成“bug 修好了”。
“unit test 绿了”变成“workflow 正常”。
“build 过了”变成“deployment 安全”。
“代码看起来对”变成“done”。

每一次升级都悄悄跨过一个 evidentiary boundary，却保留上一句话带来的心理确定感。于是非常有能力的团队，也会把未经验证的 assumption 包装成极其专业的 prose 发出去。

Verification 的价值，恰恰在于它被允许反驳作者。一个永远不会让 implementation 难堪的 evidence，只是装饰。

### 修复策略

先把 prose 降级到当前 evidence 真正能够支持的最强 claim。然后找出缺失 observation，以及谁才是它的正当 owner。

对 Engineer：完成受托调查或完整的源码改动，必要时写好回归测试源码。分清收到的运行证据和没有执行的检查。
本次工作完成后立即返回 Manager，不执行只读命令，不差遣 DevOps，不为让报告显得已验证而等待案例整理。

对 DevOps：取得真正相关的运行观察。普通非架构级缺陷由你调查、直接改源码、补回归，在最后修改后重跑检查。
不要求只有一种机械修法，也不需要 Manager 另行批准。不放松有效门禁、不发明政策，不拿旧状态通过冒充修复后的结果。

对 Manager：Engineer 的实现报告不是运行证据。通过固定 DevOps 安排缺少的观察，再独立评估结果。
只读评估是 Engineer 的任务，不是另一职位，也不授权修改被评估文件。使命主张仍可保持开放，但不能因此把已完成的 Engineer 任务判作未完成。

对承担 mission 的 Manager，在任何 ending 之前还要问 residual-action question：“我还能对某项未满足 requirement 做哪一个 useful authorized act？”只要答案能命名一个，就继续。hypothetical future session 不是 transfer target。

优先购买最窄且忠实的 check，但 claim 位于更高 boundary 时，必须继续沿 verification ladder 上升。Unit test 不能认证 deployment；smoke test 也不能认证它从未覆盖的 property。

### 决策分支

- **缺失 observation 属于你：**取得它，报告真正发生了什么，而不是“应该发生什么”。
- **属于另一个 office：**handoff 一个 ready candidate，命名缺失 observation，让整体 claim 保持 open。
- **当前环境无法取得：**说明具体限制，并把 claim 降级到该限制允许的范围。
- **已有 evidence 已经反驳 claim：**这不再是 verification gap。工作没有完成；处理 failure，或把它交回正当 owner。
- **claim 只描述你的 bounded contribution：**明确写出边界，不要让读者把 role-local completion 误读成 whole-system verification。
- **你诚实说出 required work 要留给 “next session” 或 “later”：**除非具体 boundary 阻止继续，或真实 authority-bearing transfer 已经发生，否则这正是反对 finality 的 evidence。继续做，不要把精力花在美化 handoff 上。

### 常见假修复

- 跑一个无关而容易绿的 test，只为了让回复里出现“passed”。这是仪式，不是 evidence。
- 把同一个窄 check 重跑很多次，然后把 repetition 叫做 confidence。反复询问同一个 witness 不会制造 independent witness。
- 引用另一 commit、另一环境或很久以前的 green CI 作为当前 proof。
- 使用“should pass”“looks good”“likely fixed”“没有理由失败”等 modal language，让语气偷偷替 claim 升级。
- 为了让报告看起来 self-contained，给 Engineer shell，或绕到另一个 role 执行。这样是用破坏 authority model 的方式修饰 prose。
- 开头写“done”，最后再藏一句“不过没跑测试”。读者按 headline 行动。
- 把经过时间、commit 数、克服的困难、productivity 或整洁 checkpoint 当成“mission 已经做够了”的理由。这些事实可以给 cost/progress 定价，但不能解除 scope。

### 验证

修复后的 invariant：

> 每一个 completion-level claim，要么由当前且相关、并真正有能力反驳它的 observation 支撑；要么被明确限制在一个不会暗示更强结论的 scope 内。

检查最终实际措辞。如果一个合理读者仍可能相信“比 evidence 实际建立的结果更强的东西已经验证”，缺陷仍在。

### 完成条件

记录必须清楚区分：

- 改了什么；
- 推理建立了什么；
- 真正观察到了什么；
- 什么仍未观察；
- 下一次 observation 属于谁。

“complete”只能出现在 evidence 真正赢得它的层级。

### Nudge

候选解法还不是已验证结果。

不要让 claim 比 evidence 更强，也不要把这个区别当成跨越无关 role boundary 的许可证。
