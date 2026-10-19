# status-announcement-noise — Lesson

## 评价

### 定义

Status-announcement noise 的病，不是“agent 话多”，而是沟通 channel 被**没有改变协作状态的信息**占满：重复说正在做什么、马上要做什么、刚刚做过什么，却没有新 finding、decision、blocker、evidence 或需要对方响应的分叉。

高频状态播报看似透明，实际会稀释真正重要的 interrupt。每条 message 都向 reader 收一次 context-switch 税；如果十条都是 “继续检查 / 还在跑 / 下一步看看 X”，真正出现 material blocker 时反而更难被看见。

### 支配原则

注意力是有限 channel。不携带新状态的消息与重要的消息消耗同样的 interrupt budget，把信噪比压低，直到重要信息更容易被错过。进度沟通在改变 receiver 对工作的 model 时才配得上它的位置，而不是仅仅又发生了一个内部步骤。

### 何时触发

以下情形触发：

- 每个 tool call 前后都发一条同义 update；
- announcement 只是复述上一条计划，没有新 fact；
- 长任务中按动作数量播报，而不是按 decision point；
- “已完成 A，接下来 B”反复出现，但用户无需据此选择任何东西；
- progress prose 比实际 findings 还多。

### 不应触发

不要误杀有价值的协作更新。长任务里出现首个 root cause、重大 scope choice、真实 blocker、外部状态变化、长时间沉默后的 milestone，都值得及时说；用户能够据此打断/纠偏时，update 就有价值。

### 与相邻规则区分

与 `unverified-completion-claim` 区分：status noise 不一定说错，只是信息增量太低。与 `comment-theater` 类似，两者都可能产生看似解释很多、实际没增加 reasoning 的 prose；一个发生在交互 channel，一个发生在代码。

### 判定程序

一个简单判定：**删掉这条 update，用户会失去哪一个能影响判断/协作的事实？** 如果答案是“没有，只是不知道我又调用了一个工具”，它就是 noise。

### 例子

- positive：agent 对每个内部 substep 都发“开始第 3 步”“还在工作”“第 3 步完成”，没有任何 decision、failure 或 changed fact。
- near-miss：长 migration 按文档化 phase 各报一次，带计数、剩余风险与下一步 required action。
- counterexample：一次失败调查留下的 `console.log`——那是 `debug-print-left`，不是 status-announcement-noise。

## 建议

### 现在该做什么

把 update cadence 绑定到**信息变化**，不要绑定到 tool-call 数量。

值得主动播报的节点通常只有几类：

- 新发现了 load-bearing fact/root cause；
- 原计划因 evidence 改方向；
- 出现真实 blocker/风险；
- 完成一个用户能理解的 milestone；
- 需要用户决定一个无法从现有 context 解决的分叉；
- 长任务已经经历一段显著工作，需要给协作者新的可打断点。

其他低层动作可以安静执行，最终在下一个 meaningful update 一起总结。

### 为什么重要

沟通 channel 的人类带宽有限。Low-information update 稀释 high-information 事件，训练 recipient 去 skim——这正是让后来的 failure 或 decision 容易被错过的行为。

### 修复策略

定义 meaningful phase boundaries，在这些 boundary 上报 concise evidence，优先报最终结果而不是叙述每个内部动作。对机器消费用 structured telemetry，而不是会话式噪声。

### 决策分支

如果 message 改变了一个 decision、fact、failure、uncertainty 或 required action，保留它。如果它只叙述 routine motion，删除它或聚合到下一个真实 phase boundary。

### 常见假修复

常见假修复：

- 把每条 update 缩短成一句，但频率完全不变；
- 用固定计时器每 N 秒发“仍在进行”；
- 每次 tool result 都换个措辞复述；
- 为显得透明，提前宣布所有即将调用的文件/命令；
- 反过来完全不更新，长时间任务直到 final 才暴露早已发现的 blocker。

### 验证

验证从 reader 角度看 transcript：每条 update 是否带来新的 finding、decision、risk、milestone 或 actionability？相邻几条若能无损合并，说明 cadence 仍太细。

对真正 material finding 要及时，不必等整项任务结束。让用户能够在方向尚可改变时纠偏，这是 update 的价值，不是“证明 agent 一直有在工作”。

### 完成条件

完成时 status channel 信噪比高：消息数量可能少，但每一条都足以改变协作者对当前状态的理解。

> 好进度更新不是频繁地证明工作存在，而是在工作产生新事实时把事实交给协作者。

### Nudge

> 透明度不是直播每一次手部动作，而是在事实、风险或方向发生变化时让协作者及时知道。
