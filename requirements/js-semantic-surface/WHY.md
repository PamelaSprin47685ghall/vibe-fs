# js-semantic-surface — WHY

## 不可替代的存在理由

当测试能够随意触碰实现内部时，测试实际上在验证「代码当前是怎么偶然写出的」，而不是「系统对外承诺了什么语义契约」。这种实现与测试的过度耦合会导致严重的脆弱性：
- 内部辅助函数重命名或内联重构，导致无关测试大面积崩溃。
- 底层集合类型或数据结构微调（如将 Map 替换为 Dictionary），破坏测试中手写的内部遍历。
- 联合类型（DU）扩充内部实现分支，迫使外部测试修改无业务含义的枚举断言。
- 编译工具链或代码生成器版本升级改变输出符号名，导致测试对符号的探测整体失效。

`js-semantic-surface` 的核心存在理由是：**在实现世界与测试世界之间建立不可逾越的机器化隔离边界。** 生产代码（F#）拥有实现自由，测试代码（JavaScript）通过稳定、原生（JS-native）、显式声明的 Semantic Surface 访问系统并验证语义不变量。

测试中的生产调用不必都写在 `test` callback 的第一层：共享的局部 helper 与 property callback 会在同一 proof 的实际执行链上。只承认第一层会逼使作者添加无语义的装饰性直接调用；只搜索文件中是否出现调用，又会让 dead helper 或其他 WHAT 借出虚假证据。因此门禁必须在每个 active primary WHAT 的独立执行闭包内归因 Surface use。

## 核心张力与元规则定位

业务产品包（如 `managed-session-lifecycle`、`delegation`）拥有各自领域的具体产品语义，并有责任对外暴露对应的 Semantic Surface。`js-semantic-surface` 不拥有具体业务契约，它拥有的是**测试边界与数据表示的元规则**：
- 确定什么形态的入口才具备作为正式 Surface 的资格；
- 限制哪些数据表示可以跨越边界传递，禁止将编译器内部结构泄漏至测试；
- 确立「不拥有独立语义命题的内部 helper 严禁直接测试」的最小化验证原则。

## 依赖关系

**DEPENDS ON** `requirement-system`, `verification-system`

- 依赖 `requirement-system` 提供的断言级所有权规则与规范唯一性保证：每个 Surface 必须挂载在明确的 Owner 包及其合法命题之下。
- 依赖 `verification-system` 提供的门禁执行机制与契约面语言边界规范。

## 核心不变量与违约状态（RED）

仓库处于 RED 状态，当且仅当出现以下任一破坏边界隔离的违约：
1. 语义测试直接深度导入（deep-import）内部 dist 模块，而非使用正式注册的 Surface。
2. 语义测试读取编译器内部字段（如 `.tag`、`.fields`、`.cases()`）或直接构造编译器特有运行时类型。
3. 语义测试依赖混淆的导出符号名称进行动态查找或调用。
4. 为了满足测试的访问便利，而在生产代码中随意将内部实现导出为 public。
5. 新增 Surface 但未在 Manifest 中完成注册，或缺少对应的契约测试对其公开接口进行完整锁定。
6. Fable 类型检查与发射成功，但生成的 ESM consumer named-import 一个 provider 实际未导出的符号，或生成相对 import 逃出 npm package 的 `dist/` 闭包；这种产物直到真实 Node 加载才爆炸，说明“编译成功”不能替代模块链接证明。
7. 门禁拒绝 WHAT 真实调用的可达 helper/property callback，或让 dead helper、其他 WHAT 的调用、未执行 callback 为当前 WHAT 制造 Surface proof authority。
