# impure-core — Main 中文版

## 现在该做什么
把 observation 移到 shell，把 decision 留在 core。先读取 clock、random choice、configuration、storage result、provider response 等事实，再用明确数据调用 policy；core 返回 decision / command / event，由 shell 执行 effects。

## 为什么这很重要
隐藏 effect 最贵的后果是“无法完整重放理由”。同一 command 重放时，core 又读了一次新的世界，于是 replay 不是重放过去，而是在今天重新裁决昨天。

这也会污染测试：测试不再给 policy 输入，而是布置一个假的宇宙，祈祷 core 从里面读取到预期事实。

## 修复策略
1. 列出每个 decision 真正依赖的外部事实；
2. 判断哪些只需在入口观察一次，哪些确实需要 operation 内多次观察；
3. 一次性事实转成值输入；
4. 必须持续观察的能力做成窄 port，并把 effectful orchestration 留在 shell；
5. core 输出 typed decision，而不是自己执行 DB/network/process side effects；
6. 需要 replay 的随机/时间结果要持久化 provenance。

## 常见假修复
- 把 `Date.now()` 包成 `Clock.now()`，但 core 仍自己调用它；依赖只是换了名字。
- 给所有东西套 interface 就宣布 functional core 完成。
- 把 DB call 搬进 repository object，再让 domain object 持有 repository。
- 用 global dependency injection container 隐藏 ambient world。
- 为了追求“纯”而删除 domain 真正需要的实时事实；正确做法是显式建模，不是假装不存在。

## 验证
给 core 相同的完整显式输入，多次执行应得到相同 business outcome。改变 clock/config/random/storage observation 时，应通过改变输入来改变结果，而不是靠修改进程环境。

## 完成条件
policy 的 premise 能从函数输入与已声明 capability 中完整读出；外界负责提供事实，core 负责解释事实，两者不再由同一段代码偷偷混做。
