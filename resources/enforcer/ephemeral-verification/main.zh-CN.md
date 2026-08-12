# ephemeral-verification — Main

把临时 experiment 中真正有区分力的部分提炼成 repository-owned proof。

先抽掉调查噪声，只保留：stimulus、关键 setup、observable、以及什么 defect 会让它失败。然后把它放到最合适的永久层：unit/property、boundary contract、replay/recovery、integration、或真实 canary。

不是所有 scratch script 都值得保存。保存的是**发现的 invariant**，不是当时敲过的每一条命令。

常见假修复：

- 把 scratch 脚本原样丢进 `scripts/`，没人知道何时运行、expected output 是什么；
- 把 terminal output 粘进 comment/issue；
- README 写 “验证步骤”，却不接 standard check path；
- 保存一个 flaky stress reproduction，不固定真正 causal schedule；
- manual canary 只有作者知道 secret/setup；
- 为了自动化把一条小 contract probe 扩成巨大、昂贵、不稳定 E2E。

选择最窄 faithful boundary。纯 law 放 unit/property；producer/consumer agreement 放 contract；restart fact 放 replay；外部 undocumented behavior 放 canary。Evidence 层级应由 claim owner 决定。

验证 durable proof 自己：临时恢复 old defect 或破坏该 invariant，标准入口必须红；fix 后 green。另一个 clean checkout 应能按照 repository 自己拥有的 setup 复现，不依赖你的 terminal history。

若 probe 只是 investigation stepping stone、并没有形成长期 invariant，可以让它消失；RuleBook 不要求保存所有探索过程。

完成时“我亲眼验证过”被升级成“项目以后会自己验证”。下一次 regression 不需要某个人还记得当年怎么敲命令。

> Session confidence 会过期；executable memory 才会留下来。