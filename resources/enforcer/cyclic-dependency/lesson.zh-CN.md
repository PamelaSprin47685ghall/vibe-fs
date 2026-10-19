# cyclic-dependency — Lesson

## 评价

### 定义

依赖环不是“图上出现了一个圈”这么浅。真正的病灶是：两个或更多 owner 互相需要对方先成立，结果没有任何一方能独立解释、构造或初始化自己。

环意味着知识主权失去方向。`A -> B` 本来是在说“A 的一部分定义建立在 B 之上”；当 B 又反过来依赖 A，系统事实上承认：关键事实没有独立 owner，只能靠双方互相借存在感。

### 支配原则

一条依赖边是一支知识之箭：A → B 表示 A 的一部分定义建立在 B 之上。环因此意味着：每一方都必须已经存在，另一方才能被定义。运行时可以用 lazy initialization 或 service locator 打断物理上的环，但概念上的环仍然存在，稍后表现为 ordering 约束、半初始化状态和必须拉起整张图的测试。

### 何时触发

当模块、package、service 或 runtime component 出现以下情况时触发：

- A 必须 import / initialize B，B 又必须 import / initialize A；
- 必须靠 lazy、service locator、全局 registry、延迟绑定才能“把环藏起来”；
- 单独构造任一侧都缺关键事实，测试也被迫启动整张图；
- 初始化顺序、半初始化对象、空占位符成为正确性的组成部分；
- 某个决定看似属于 A，又看似属于 B，双方都不能在没有对方的情况下作出。

### 不应触发

以下情况不是本规则：

- 两个 domain peer 通过一个独立协议双向通信，但 compile-time ownership 仍有方向；
- A 发 command 给 B，B 通过 event 回给 A，消息的 contract 有独立 owner；
- lazy 仅用于性能，而依赖图本身仍是 DAG；
- test 依赖 production subject，不等于 production 反向依赖 test；
- 两个概念彼此相关，不等于它们必须互相定义。

### 与相邻规则区分

`boundary-collapse` 是不同 owner 越界读取彼此 internals；`cyclic-dependency` 更进一步：双方已经互相成为定义前提。

`implicit-control-flow` 可能表现为“启动顺序很玄学”；若玄学背后是 A/B 必须互相先初始化，根因仍是这里。

### 判定程序

问一句：**如果把其中一边拿走，另一边还能完整说明“我是谁、我拥有什么事实、我需要什么 contract”吗？**

如果不能，先别研究怎么用 DI container、forward declaration 或动态 import 把环跑起来。先找那个双方争抢、又没人真正拥有的事实。

### 例子

- 正例：`OrderService` 依赖 `PaymentService` 判断是否可下单，`PaymentService` 又依赖 `OrderService` 判断支付是否有效；两者初始化互相注入。
- 近邻：Order 发 `ChargeRequested`，Payment 消费后发 `Charged`；事件 contract 独立存在，两者不互相 import。
- 反例：用 service locator 把直接 import 消掉，但运行时仍必须先塞 placeholder 再回填引用——环只是换了衣服。

## 建议

### 现在该做什么

不要用更多 indirection 把环藏起来。先画出真正的 knowledge / ownership graph，找出那个被双方共同需要却没有独立 owner 的事实，再把依赖改成有向关系。

常见修法只有几种：

- 抽出独立 contract / value / protocol，让双方都依赖它，而不是互相依赖；
- 把真正拥有决策的 policy 移回一个 owner，另一方变成 caller；
- 若是双向 runtime communication，保留消息往返，但让 compile-time ownership 仍单向；
- 若“互相需要”来自初始化，拆 construction 与 operation，避免半初始化对象互相续命。

### 为什么重要

环最贵的地方不是编译器报错，而是它把局部推理毁掉。任何一侧都必须带着另一侧的知识才能解释自己，测试需要整张图，初始化需要顺序魔法，故障会表现成“偶尔拿到空引用”“启动时序不稳定”“某个 registry 还没填完”。

这些不是独立 bug，而是同一个事实：系统没有清楚回答谁先定义谁。

### 修复策略

按语义画依赖箭头，不要按文件引用。决定哪个方向反映 authority。把共享抽象移向稳定地基，或引入 message/data contracts，让对等方不必互相 import 就能通信。

### 决策分支

- 如果双方共享的是一个稳定概念，抽成第三个独立 owner。
- 如果双方都在做同一个决定，选一个 owner，另一方只提供事实或请求。
- 如果只是 runtime 双向消息，不要为了“去环”禁止业务往返；去掉的是定义环，不是通信本身。
- 如果环仅靠 lazy/service locator 被掩盖，先恢复显式依赖图，再修 ownership。

### 常见假修复

- 用 interface 把 A→B→A 改成 A→IB→IA；名字变了，环没变。
- 用 global registry/service locator 延迟取对象；依赖从 compile time 逃到了 runtime。
- 靠 initialization order、nullable placeholder、`lateinit` 让两边互相回填。
- 把共享类型复制两份，结果两个版本继续同步演化。
- 单纯把某个 import 移到函数内部，仍然没有改变谁定义谁。

### 验证

修复后应能做到：

- production dependency graph 对该区域可拓扑排序；
- 每个 owner 可在不构造其 former peer implementation 的情况下独立测试核心规则；
- 启动正确性不依赖“先 new A，再半 new B，再回填 A”；
- 双向业务通信若仍需要，发生在明确 contract 上，而不是通过互相暴露 internals。

### 完成条件

一个新读者能从依赖方向直接看出谁拥有事实、谁消费事实；系统不再需要 runtime 技巧来伪造一个本不存在的依赖方向。

### Nudge

不要问“怎样让环工作”。先问“哪个事实本来应该有一个第三方 owner，或者哪条依赖方向其实写反了”。
