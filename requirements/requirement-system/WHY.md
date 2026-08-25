# requirement-system — WHY

## 不可替代的存在理由

万象术的知识同时存在于五个物理载体：正式规范、静态门禁、可执行测试、代码实现与历史记录。只要没有一条规则明确界定「某个产品事实由谁拥有、谁能定义、谁能证明」，这五个载体就会各自长出平行的语义法域：

```text
docs 说 A，gate 说 B，test 证明 C，code 实现 D —— 四者同时为真，互相覆盖
```

当多个载体各自宣称权威时，仓库就会失去单一可裁决的「当前系统是什么」；门禁的扫描范围与测试的断言边界也会因为缺乏明确的责任主体而形同虚设。

`requirement-system` 的核心存在理由是：**把「谁拥有什么」本身变成一条被统一拥有的元规则。**

## 核心张力与元规则定位

领域包（如 `durable-events`、`review-assurance`）拥有具体的产品与业务事实，回答「世界是什么」；而 `requirement-system` 不拥有任何具体业务领域断言，它拥有的是跨包语义治理的元规则：

```text
这条命题归哪个包拥有？
这个可执行证明归谁负责？
这个包允许消费哪些依赖保障？
哪个文档是唯一规范合同，哪些只是辅助导航？
```

这些元规则必须由单一包集中拥有，并且该包自身也严格遵守「无裸规范权威」——治理规则自身也必须是 WHAT.md 中的正式条款，杜绝散落在散文或非规范文件中。

## 为什么独立于 verification-system

- `requirement-system` 解决**所有权与规范合同**问题（唯一 owner、显式依赖闭包、断言级 proof ownership、WHAT 唯一性）。
- `verification-system` 解决**证明资格与验证技术**问题（证据分层、可红性、fail-closed、时间确定性）。

两者具有清晰的独立变化边界：更改元规范的存储与表现结构（如 manifest 格式演进或文件布局重排），只影响 `requirement-system`，不影响 `verification-system` 的测试阶梯；替换物理 Canary 运行器或适配器，只影响 `verification-system`，不影响 `requirement-system` 的所有权边界。

## 核心不变量与违约状态（RED）

仓库处于 RED 状态，当且仅当出现以下任一破坏语义完整性的违约：
1. 存在无 owner、双重 owner、互相矛盾或无法独立验收的 normative 命题。
2. 规范命题脱离正式编号与落点，散落于非规范散文中。
3. `requirements/` 目录下出现未经索引登记的外来包，或包文档结构残缺。
4. 包声明了超出依赖骨架定义的非法依赖关系。
5. WHAT.md 命题在 HOW.md 证明表中缺失，或引用的测试文件不存在。
6. migration ledger 出现 11 类非法状态中任一：PENDING 冒充成功、READY 无 owner/证明、DONE 无闭环、分类/结果错配、提交非祖先、变更缺失、证明门禁缺失、闭合依赖未 DONE、覆盖无归属、基线增长。

## 为什么需要 migration ledger 门禁

`scripts/checks/migration-ledger.json` 是 63 节点 DAG 的施工事实，不是架构文档。没有机械门禁时，PENDING 节点可写 GREEN 证据冒充完成，READY 节点可无 owner 合同却宣称就绪，DONE 节点可无实现提交、无生产变更、无证明门禁却标记完成，分类与结果可错配，closure 依赖可指向未 DONE 节点，覆盖可无归属，基线可静默增长——每种都会让 ledger 从“可执行的施工图”退化为“文字报告”。门禁把 ledger 的状态机（PENDING→READY→DONE）、分类机（KEEP→PROVEN-KEEP 等）、证据机（verified/complete/GREEN 禁止）、证明机（proofs/gates）、提交机（HEAD 祖先）、变更机（touched_paths）、依赖机（closure DONE）、覆盖机（owner graph）、基线机（不得增长）全部变成可红可绿的机械断言，使每一次 ledger 变更都经历与代码同等的 fail-closed 校验。
