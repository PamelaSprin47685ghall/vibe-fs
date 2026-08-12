# status-announcement-noise — Main

把 update cadence 绑定到**信息变化**，不要绑定到 tool-call 数量。

值得主动播报的节点通常只有几类：

- 新发现了 load-bearing fact/root cause；
- 原计划因 evidence 改方向；
- 出现真实 blocker/风险；
- 完成一个用户能理解的 milestone；
- 需要用户决定一个无法从现有 context 解决的分叉；
- 长任务已经经历一段显著工作，需要给协作者新的可打断点。

其他低层动作可以安静执行，最终在下一个 meaningful update 一起总结。

常见假修复：

- 把每条 update 缩短成一句，但频率完全不变；
- 用固定计时器每 N 秒发“仍在进行”；
- 每次 tool result 都换个措辞复述；
- 为显得透明，提前宣布所有即将调用的文件/命令；
- 反过来完全不更新，长时间任务直到 final 才暴露早已发现的 blocker。

验证从 reader 角度看 transcript：每条 update 是否带来新的 finding、decision、risk、milestone 或 actionability？相邻几条若能无损合并，说明 cadence 仍太细。

对真正 material finding 要及时，不必等整项任务结束。让用户能够在方向尚可改变时纠偏，这是 update 的价值，不是“证明 agent 一直有在工作”。

完成时 status channel 信噪比高：消息数量可能少，但每一条都足以改变协作者对当前状态的理解。

> 好进度更新不是频繁地证明工作存在，而是在工作产生新事实时把事实交给协作者。