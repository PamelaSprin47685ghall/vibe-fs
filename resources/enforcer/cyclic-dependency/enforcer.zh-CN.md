# cyclic-dependency — Enforcer 中文版

## 定义
依赖环不是“图上出现了一个圈”这么浅。真正的病灶是：两个或更多 owner 互相需要对方先成立，结果没有任何一方能独立解释、构造或初始化自己。

环意味着知识主权失去方向。`A -> B` 本来是在说“A 的一部分定义建立在 B 之上”；当 B 又反过来依赖 A，系统事实上承认：关键事实没有独立 owner，只能靠双方互相借存在感。

## 何时触发
当模块、package、service 或 runtime component 出现以下情况时触发：

- A 必须 import / initialize B，B 又必须 import / initialize A；
- 必须靠 lazy、service locator、全局 registry、延迟绑定才能“把环藏起来”；
- 单独构造任一侧都缺关键事实，测试也被迫启动整张图；
- 初始化顺序、半初始化对象、空占位符成为正确性的组成部分；
- 某个决定看似属于 A，又看似属于 B，双方都不能在没有对方的情况下作出。

## 不要误判
以下情况不是本规则：

- 两个 domain peer 通过一个独立协议双向通信，但 compile-time ownership 仍有方向；
- A 发 command 给 B，B 通过 event 回给 A，消息的 contract 有独立 owner；
- lazy 仅用于性能，而依赖图本身仍是 DAG；
- test 依赖 production subject，不等于 production 反向依赖 test；
- 两个概念彼此相关，不等于它们必须互相定义。

## 刀口
问一句：**如果把其中一边拿走，另一边还能完整说明“我是谁、我拥有什么事实、我需要什么 contract”吗？**

如果不能，先别研究怎么用 DI container、forward declaration 或动态 import 把环跑起来。先找那个双方争抢、又没人真正拥有的事实。

## 与近邻区分
`boundary-collapse` 是不同 owner 越界读取彼此 internals；`cyclic-dependency` 更进一步：双方已经互相成为定义前提。

`implicit-control-flow` 可能表现为“启动顺序很玄学”；若玄学背后是 A/B 必须互相先初始化，根因仍是这里。

## 例子
- 正例：`OrderService` 依赖 `PaymentService` 判断是否可下单，`PaymentService` 又依赖 `OrderService` 判断支付是否有效；两者初始化互相注入。
- 近邻：Order 发 `ChargeRequested`，Payment 消费后发 `Charged`；事件 contract 独立存在，两者不互相 import。
- 反例：用 service locator 把直接 import 消掉，但运行时仍必须先塞 placeholder 再回填引用——环只是换了衣服。

## 提醒
不要问“怎样让环工作”。先问“哪个事实本来应该有一个第三方 owner，或者哪条依赖方向其实写反了”。
