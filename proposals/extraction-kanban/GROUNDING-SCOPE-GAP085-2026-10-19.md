# GAP-085：整个自目录的规则校验

候选667已修正自覆盖漏判和字符类泄漏。最终正式回归14文件128通过、0失败、0TODO，文件全部完成、自有进程收尾确认通过。固定包SHA `b0de9b9770139ae4aa977fae3d8b8d59f2d558cf4ff4a41cf4a1289419743297`。三名非作者的规则、真实Host、安装包与冷恢复复验通过；本卡按当前计划列明的范围随本提交交付。

## 为什么修改

`GroundingCatalog.validateRule`原先只用`requirements/<package>/WHAT.md`试匹配。只声明自身tests、未来文件或包根的规则会通过，否定规则也有同样问题。这是修复前已有的校验缺陷；不是D1/D2引入。

复审候选666又发现旧matcher的字符类问题：首个`]`未转义，`/src/[]|]`生成的Regex能把`|`解释为类外分支，错误覆盖其他目录中以`]`结尾的路径。新目录判断曾假定字符类一定是单字符原子，未覆盖这个旧异常；666尚未提交便由非作者反例否证。667在共用解析器中把首个`]`正确留在字符类内，`[]|]`只匹配`]`或`|`，不再逸出。这是明确的旧行为纠错，不宣称所有旧Regex字节都等价。

## 最终职责与边界

- `Foundation/GlobMatch`只负责纯规则语义：同一套token解析供既有Regex matcher和目录交集查询使用，没有第二套glob解析器、文件枚举或新物理依赖。
- 目录查询消费固定目录前缀，再检查其自身或斜杠后缀是否可匹配；有限状态图按已见节点收敛。`**/`的零层入口与必须经过斜杠退出的循环态分开。字符类沿用实际无flags Regex，按UTF16单元判断，不把emoji改成单代码点。
- `GroundingCatalog`拥有规则政策：每条普通/否定规则都检查整个自目录，未来路径同样适用，后续排除不能抵消非法声明。非法配置在发现和材料化前拒绝。
- 路径域沿用现有`selfMatch`与`slash`输出的词法前缀，不对规则做`path.resolve`，不新增空段、点段或重复斜杠规范化。工作区别名、未来路径、逃逸和断链symlink规则不变；NUL和仍含反斜杠的字符串不属于该输出域。
- Runtime、Gate和持久化协议未改。Grounding失败仍只是知识补充失败，真实read/write及JS程序不因此延期、失败、回滚或重发。源文件实际读取仍可留下`ReadObserved`；不能把它误当非法规范材料的消费。

## 已完成验证

| 入口 | 结果与证据边界 |
|---|---|
| 正式requirement runner | requirement-grounding 001–012与repository-programming 008/009，共128通过、0失败、0TODO；14/14文件完成，owned accepted |
| 002自然语义面 | 原TODO转正式反例；未来文件、正/否定、包根、class/?/**、相邻包、UTF16、换行、NUL/反斜杠不可达匹配及真实symlink边界 |
| 007真实注册读取 | native read与js-manager的合法/非法正反控；原结果保留，非法包零Requested/Anchored且其sentinel不进入wire |
| 008真实修改 | controlled Host write参数引用/字段不变；实际js-engineer用rewrite追加ONCE，正/否定非法self配置下仍成功一次 |
| 共享glob消费者 | repository-programming 008/009实际枚举、ignore、grep与目录边界通过；首]成员及不跨prefix泄漏有正式反例 |
| 非作者纯规则 | Node/Bun各234对旧matcher实际路径等值、27组目录交集、6组首]纠错、实际Catalog正反规则与未创建future文件；仅首]例有意不同 |
| 非作者政策与材料 | 同一002原8body：663双Host各4通过/4真实断言失败，667双Host各8通过；59组具体witness/Runtime对照。Node非法材料read0、合法正控1、旧坏声明1；安装包各2904文件前后未变 |
| 非作者真实Host | Node/Bun各5测试：legal/self+/self-四工具、首]正反与两次真实Plugin incarnation冷重放。材料read合法read/js-manager各1、write/js-engineer各2，非法均0；零新Requested/Anchored，修改唯一目标且参数不变。旧wire逐字重放、规范read0 |
| 构建与静态 | Fable构建667：175注册面、839模块；局部format检查通过；check退出0，526项既有report-only、无新增control pyramid |

固定包与工作树/干净安装2076文件逐项同一，较D2仅GlobMatch.js/Catalog.js变化，库22文件未变；见`evidence/GAP-085/final-product-equality.json`。Node正式监督和Bun原生/独立消费分别记录，不把后者冒称CUT-00-R2已完成。主正式首轮124/0仅是补共享消费者反例前的阶段结果，不与最终128相加。

## 本轮错误与归属

1. 修复前已有问题：WHAT.md单点校验及首`]`类外分支，以上均已定位并修正；没有证据把它们归为D1/D2回退，具体引入提交未在本包追溯。
2. 本轮未交付实现问题：首次遗漏GlobMatch.fsi新声明，Fable构建拒绝；已补。666未考虑旧异常字符类，非作者真实反例失败；667已修。初版十处嵌套决策由静态门禁拒绝，已用同一有限图遍历去掉重复逻辑和嵌套，未改门禁。
3. 测试/review误判：两次把`al**/...`当合法包外规则，实际可命中alpha未来后代；夹具已改，未迎合错误断言改算法。新JS测试用write覆盖已有文件被正确拒绝，按repository-programming-010改成rewrite。无flags Regex末尾换行假设、字符类必为原子的假设均被实际证据否证，原红保留，不归为产品失败。独立夹具曾只写self规则却向src请求，不能证明旧材料消费；补同一`/src/**`正控后重跑两项。Bun错误命令与未隔离启动的MissingEstimate、不能拦截named import的零计数均另列，不冒称产品回退。

## 仍有边界

本包只关闭GAP-085约定的自覆盖与接线范围，不重做发现、不迁移用户配置、不宣称所有POSIX包名都已获得正确隐式覆盖。现有包名含反斜杠时的slash差异、`[!]]`等旧glob方言限制未在本轮改约；没有全gitignore方言或未测平台的完整证明。

GAP-159、GAP-140、Trace消费者链、Bootstrap及验证设施按原计划继续；R12-U1仍暂缓。没有registry发布或Host部署。
