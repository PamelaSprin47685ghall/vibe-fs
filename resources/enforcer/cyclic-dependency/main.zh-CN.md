# cyclic-dependency — Main 中文版

## 现在该做什么
不要用更多 indirection 把环藏起来。先画出真正的 knowledge / ownership graph，找出那个被双方共同需要却没有独立 owner 的事实，再把依赖改成有向关系。

常见修法只有几种：

- 抽出独立 contract / value / protocol，让双方都依赖它，而不是互相依赖；
- 把真正拥有决策的 policy 移回一个 owner，另一方变成 caller；
- 若是双向 runtime communication，保留消息往返，但让 compile-time ownership 仍单向；
- 若“互相需要”来自初始化，拆 construction 与 operation，避免半初始化对象互相续命。

## 为什么这很重要
环最贵的地方不是编译器报错，而是它把局部推理毁掉。任何一侧都必须带着另一侧的知识才能解释自己，测试需要整张图，初始化需要顺序魔法，故障会表现成“偶尔拿到空引用”“启动时序不稳定”“某个 registry 还没填完”。

这些不是独立 bug，而是同一个事实：系统没有清楚回答谁先定义谁。

## 分支判断
- 如果双方共享的是一个稳定概念，抽成第三个独立 owner。
- 如果双方都在做同一个决定，选一个 owner，另一方只提供事实或请求。
- 如果只是 runtime 双向消息，不要为了“去环”禁止业务往返；去掉的是定义环，不是通信本身。
- 如果环仅靠 lazy/service locator 被掩盖，先恢复显式依赖图，再修 ownership。

## 常见假修复
- 用 interface 把 A→B→A 改成 A→IB→IA；名字变了，环没变。
- 用 global registry/service locator 延迟取对象；依赖从 compile time 逃到了 runtime。
- 靠 initialization order、nullable placeholder、`lateinit` 让两边互相回填。
- 把共享类型复制两份，结果两个版本继续同步演化。
- 单纯把某个 import 移到函数内部，仍然没有改变谁定义谁。

## 验证
修复后应能做到：

- production dependency graph 对该区域可拓扑排序；
- 每个 owner 可在不构造其 former peer implementation 的情况下独立测试核心规则；
- 启动正确性不依赖“先 new A，再半 new B，再回填 A”；
- 双向业务通信若仍需要，发生在明确 contract 上，而不是通过互相暴露 internals。

## 完成条件
一个新读者能从依赖方向直接看出谁拥有事实、谁消费事实；系统不再需要 runtime 技巧来伪造一个本不存在的依赖方向。
