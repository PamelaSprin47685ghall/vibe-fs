# obligation-ledger 测试证明范围

规则由 WHAT 定义。本目录验证输入、义务投影、Prepared/Accepted、T1、恢复及工具边界；不以自然语言关键词替代义务质量判断。

- `001–003、006、008`：实际编码、Host 解码与纯身份检查；`008` 分别改变 Incumbency、输入摘要、base 摘要和 ordinal。身份字段使用实际接缝的 `incumbencyId`，旧 `managerLifeId` 并未进入该检查。
- `007`：批量准入允许多个调用；尚未以并发真实 hooks 证明顺序 base 与单在途事务。
- `010–019、025–026`：保留真实 journal、生产 membrane 和 projection 的有效覆盖。`018` 关闭并重新打开 journal，证明 Prepared 恢复；不等于所有提交中断点均已恢复。`010` 的 Host 成功接缝使用实际 before/after，随后是同进程 prepare 重放，不是进程崩溃恢复。
- `021`：实际 cutoff/evidence 纯函数。locator 存在不能证明所有热路径 O(1)。`024` 验证生产 definition 同步更新两份 schema；安装版 Host 的消费与 callback 时序尚未证明。
- `028`：实际 before hook 拒绝缺少 journal、snapshot 或调用身份。原测试自造 fatal 描述符再自行验证，没有执行终止，已改为明确待证。

义务是否可闭环、承诺后是否仍是交付债务、渐进细化是否完整，需要对照实际任务与证据人工审阅。文案含词、源码含函数名、测试复制的 Host 算法均不计为行为证明。相关缺口见 GAP-190 施工记录；TODO 不计入通过。

局部运行：`node --test requirements/obligation-ledger/tests/*.test.mjs`。交付时使用 verification-system 正式入口及新构建产物。
