# spike-not-cleaned — Enforcer 中文版

## 定义
Spike 的成功只证明“这条路可能走得通”，不证明“这就是应该长期维护的 architecture”。Spike-not-cleaned 是把为学习速度而允许的 hardcode、global state、fake boundary、缺失 failure/recovery contract 直接接进 production。

Prototype 是 epistemic instrument；production 是长期承诺。把前者直接晋升，等于让实验时期的偶然假设因为“demo 成功了”获得永久地位。

## 何时触发
- POC 代码直接成为 shipping implementation；
- hardcoded/in-memory/fake transport 只被换成真实 endpoint，其余假设未重审；
- failure/cancellation/recovery/ownership 在 spike 中被跳过，production 仍跳过；
- “先让它跑起来”的 shortcut 没有被重新证明；
- prototype file 名没了，但 prototype model 原封不动进入正式层。

## 不要误判
- spike 保持隔离、不可进入 release path；
- 探索后发现最小设计本来就足够，且所有 production contracts 已明确验证；
- 临时辅助脚本遗留在正式实现旁，应归 `leftover-scaffolding`；
- 某一局部 workaround 不是学习 prototype，应归 `dirty-hack`。

## 刀口
列出 spike 阶段曾允许的所有假设。**哪些只是为了快速回答 feasibility，哪些已被生产证据重新证明？** 未证明的 assumption 不能因为代码已经存在就自动毕业。

## 提醒
Demo green 证明 idea 有可能，不证明 prototype 有资格成为 architecture。
