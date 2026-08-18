# WHY — requirement-grounding

## 问题不是“Agent 会不会读文档”，而是什么时候读

仓库已经把产品真理整理进 `requirements/<package>/`，仍然可能发生最普通的一种失败：Agent
先打开源码、形成旧模型、甚至已经提交修改，最后才想起去看对应 requirement。此时文档即使完全
正确，也只能充当事后纠偏材料。代码已经沿错误语义定型，代价从“先读几页”变成返工。

依靠 AGENTS.md 写一句“改代码前先看 requirements”也不够。长任务会压缩上下文，不同子任务
会各自进入仓库，工具表会演进，模型会替换。流程纪律无法回答更具体的问题：**当前正在触碰的
这个路径，到底受哪些 package 约束？这些 package 当前版本的规范和测试，当前执行者真的看过吗？**

## 为什么必须由路径建立 grounding

源码路径是最早能稳定观察到的局部事实。项目可以在每个 package 旁边声明其包外适用路径，
而 package 自己的目录天然属于自己。这样“代码 → requirement”不依赖模型猜关键词，也不依赖
中央维护一张容易漂移的大映射表。

路径关联不是 semantic ownership 的替代品。一个文件可以同时承载多个 package 的实现；命中
多个范围就应同时 grounding 多个包，而不是强迫“一个文件只能归一个包”。

## 为什么测试源码也要进入上下文

WHAT 告诉执行者“什么必须为真”，HOW 告诉它当前实现结构，PROOF 告诉它证据落点；真正的测试
源码则展示仓库怎样把这些边界变成可红行为。只读散文而不读测试，Agent 很容易写出“语义看似
合理、实际破坏既有 oracle”的实现。反过来只读测试，也可能把偶然实现细节误当产品真理。

因此 grounding material set 必须把 package 文档与 package-local test source 作为一个整体纳入自动读取。

## 为什么第一次修改必须停在 effect 前

读取与修改有本质差别。第一次 `read` 可以在工具结果回到 provider 时，补做相关 requirement 文件的
普通 `read`，模型在形成下一步判断前已经拥有两者。这里不需要发明“grounding 消息”：模型应该像
自己主动逐个 read 这些文件一样看到它们。

第一次 `edit/write/rm/mv` 不行。模型发出修改调用的那一刻，它还没看见刚发现的 requirement。
如果 Host 先执行修改再读取规范，grounding 已经失去约束这次修改的机会。因此未 grounding 的
首次 mutation 必须被**延期而不是执行**：先补做普通 read，下一次新的明确调用才有资格触碰
真实文件。不能自动重放旧调用，因为新上下文本应允许模型改变主意。

## 为什么不能每次都重复读取

把完整 WHY/WHAT/HOW/PROOF/tests 每次读文件都自动 read 一遍，会迅速淹没上下文，也破坏稳定前缀。
但简单按包名永久去重又会产生另一个错误：任务进行中 package 内容真的发生变化，旧 grounding
继续冒充当前版本。

因此去重对象不是“包名”，而是 `(workspace, package, content digest)`。同一上下文已经收到同一
digest 就不重复；内容变化形成新 grounding identity，允许重新交付。

## 为什么要做成永久投影，而不是每轮重新注入

模型第一次真正读到 requirement 后，这些 read 已经是历史事实。后续每轮若重新读取当前文件、重新
组装 tool result 或重新决定插入位置，哪怕语义内容相同，也可能改动已发送 prefix 的字节，直接击穿
provider KV cache。正确做法与现有 pair-programming guideline 一样：第一次发生时把普通 read 的 exact
call/result bytes 与 transcript gap 锚定，之后只从 durable fact 原位 replay。

这也解决了“规范后来变了怎么办”：历史那次 read 不应该被未来文件内容反向改写。package digest 改变时，
当前尾部追加一组新的普通 read，旧 wire 仍是新 wire 的前缀。若当前尾部同时有 pair-programming 伪
`skill`，先放已有的伪 skill，再放 requirement reads；顺序一旦形成永久不变。

Cursor 是唯一需要保留 provider 差异的地方。它和现有 pair-programming 一样把 synthetic 内容拼在真实
terminal result 后面，因此没有 read call 可以携带 `filePath`。如果只拼正文，连续读 WHY/WHAT/HOW/tests
后模型无法知道每段来自哪里。正确补偿不是发明新的 grounding protocol，而是让 result 自身带最小来源
source-path attribute：只说“读的是哪个 workspace-relative 文件”，正文仍是 read 的原始结果。这样 ordinary provider
保留完整 call provenance，Cursor 用 result-local provenance 补齐同一事实。

## 为什么它必须是万象术能力，而不是本仓脚本

这个问题并不属于万象术源码本身。任何把当前产品语义整理成 `requirements/<package>/` 并愿意
提供路径范围的项目，都有同样需要。实现必须从当前 OpenCode workspace 发现 requirements 树，
不能 hard-code `src/Wanxiangshu`、48/49 个固定包名或本仓专有测试布局。

万象术提供的是一条开发策略：**代码一旦进入视野，与它相关的本地规范也同时进入视野。**

## DOES NOT OWN

- package 的正式产品语义与唯一 owner 规则 → `requirement-system`。
- provider 能看到哪些事实的一般准入法则 → `participant-horizon`。
- semantic intent 如何成为 provider bytes → `provider-projection`。
- user-shaped message 是否有 authority → `interaction-authority`。
- OpenCode 原始 hook / tool 物理能力 → `host-boundary`。
- repository programming 的事务与 sandbox 语义 → `repository-programming`。
- 原始 participant history 的 durable representation → `semantic-trace`。
- provider wire 的 append-only prefix 与历史 synthetic occurrence 原位 replay → `prefix-stability`。

## DEPENDS ON

`requirement-system`, `host-boundary`, `participant-horizon`, `provider-projection`,
`interaction-authority`, `semantic-trace`, `prefix-stability`, `repository-programming`。

