# js-semantic-surface — WHAT

本文件是 `js-semantic-surface` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## JS-SEMANTIC-SURFACE-001: 所有 automated tests 使用 JavaScript

`requirements/**/tests/**/*.mjs` 是自动化语义证明的唯一有效载体。语义测试及其辅助夹具（support、fixtures、helpers、e2e、integration）必须统一采用 JavaScript（`.mjs`），严禁在测试层引入第二开发语言。生产代码采用 F#（`.fs`），测试世界采用 JavaScript（`.mjs`），实现语言边界的物理隔离。

## JS-SEMANTIC-SURFACE-002: JS semantic tests 只能调用正式 semantic surface

语义测试只能通过正式注册、稳定且具备 JS 原生接口（JS-native）的 Semantic Surface 进入系统。严禁在测试中深层导入内部 dist 模块，严禁通过符号前缀匹配、混淆名反射或遍历模块导出对象进行非授权调用，严禁直接消费编译器底层运行时模块。Surface 存在的唯一正当理由是所属领域组件拥有对外承诺的规范契约，严禁单纯因为测试便利而将内部实现暴露至公开接口。

## JS-SEMANTIC-SURFACE-003: 值得独立测试的 law 必须有独立 semantic owner + JS surface

任何值得独立验证的语义定理或业务规则，必须归属于明确的 package owner，且该 owner 必须为其提供 JS 原生的 Surface 承载。Surface proof authority 只来自 active primary WHAT callback 内对该 Surface import binding 的直接、可达、terminal use；shadow、dead helper、其他 law callback、非 terminal alias 与 assignment target 均不得制造因果证据。Surface 必须在所属领域边界处负责完成 JavaScript 原生数据表示与内部领域模型之间的双向适配与转换，严禁建立跨越所有业务领域的集中式上帝外观（god facade）。

## JS-SEMANTIC-SURFACE-004: 不拥有独立 law 的 helper 不直接测试

不具备独立业务失败含义的内部辅助函数、局部夹具或中间工具，严禁作为独立的测试对象直接编写测试用例。内部实现的行为与正确性必须完全通过其所属 Owner 契约面的公开行为进行端到端证明。重命名、内联或替换内部数据结构不得导致测试用例修改。

## JS-SEMANTIC-SURFACE-005: semantic data 跨边界必须是 JS-native representation

跨越 Semantic Surface 边界传递的数据必须完全采用 JavaScript 原生类型体系：`string`、`number`、`boolean`、`null`、`undefined`、标准数组、纯对象（plain object）、`Promise`、标准函数以及必要时的 `bigint` 与不透明资源句柄（opaque handle）。严禁向语义测试暴露编译器特有的链表、哈希表、集合、Option/Result 包装类、DU 运行时实例或类反射元数据。时间跨越边界必须归一化为 ISO-8601 字符串或 epoch 毫秒数。

## JS-SEMANTIC-SURFACE-006: Fable runtime representation 不属于 semantic contract

编译器的代码生成约定（包括模块名称前缀、DU 标签序数、内部反射元数据及修饰后的实例方法名）不属于产品语义契约的组成部分。语义测试中严禁出现针对底层运行时表示属性（如 `.tag`、`.fields`、`.cases()`）的硬编码感知与断言。只有专属的编译器产物校验测试（compiler verification quarantine）才具备直接消费底层编译产物的特殊资格。
