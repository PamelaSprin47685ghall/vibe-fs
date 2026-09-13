# js-semantic-surface — WHAT

本文件是 `js-semantic-surface` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## JS-SEMANTIC-SURFACE-001: 所有 automated tests 使用 JavaScript

`requirements/**/tests/**/*.mjs` 是自动化语义证明的唯一有效载体。语义测试及其辅助夹具（support、fixtures、helpers、e2e、integration）必须统一采用 JavaScript（`.mjs`），严禁在测试层引入第二开发语言。生产代码采用 F#（`.fs`），测试世界采用 JavaScript（`.mjs`），实现语言边界的物理隔离。

## JS-SEMANTIC-SURFACE-002: JS semantic tests 只能调用正式 semantic surface

语义测试只能通过正式注册、稳定且具备 JS 原生接口（JS-native）的 Semantic Surface 进入系统。严禁在测试中深层导入内部 dist 模块，严禁通过符号前缀匹配、混淆名反射或遍历模块导出对象进行非授权调用，严禁直接消费编译器底层运行时模块。Surface 存在的唯一正当理由是所属领域组件拥有对外承诺的规范契约，严禁单纯因为测试便利而将内部实现暴露至公开接口。

## JS-SEMANTIC-SURFACE-003: 值得独立测试的 law 必须有独立 semantic owner + JS surface

任何值得独立验证的语义定理或业务规则，必须归属于明确的 package owner，且该 owner 必须为其提供 JS 原生的 Surface 承载。Surface 的门禁是物理边界存在性，不是执行语义证明：每条注册必须具备 module + owner + source + representation + kind；`laws` 所列每个 law id 必须真实存在于 owner（或 `lawOwners` 指向包）的 WHAT.md 标题中——`laws` 只是该 surface 承载哪些 WHAT 命题的静态描述，不授予任何 import 权限；生产 source 必须存在且被 `Wanxiangshu.fsproj` 编译；`dist/` 下必须存在已发射的 surface 模块；至少一个 `.test.mjs` 必须以静态 import 语法（`import … from '…dist/<module>'` 或 `import('…dist/<module>')`）引用该 surface。门禁只检查 import 语法的存在性：import binding 是否被实际执行、测试断言语义是否成立，由测试本身负责，门禁不评判。Surface 必须在所属领域边界处负责完成 JavaScript 原生数据表示与内部领域模型之间的双向适配与转换，严禁建立跨越所有业务领域的集中式上帝外观（god facade）。不存在 callback 可达执行闭包检查，不存在 consumer 包授权检查，不存在 `SURFACE_CONSUMERS` 登记：任何测试都可以 import 任何 surface，跨包依赖不需要显式授权。

## JS-SEMANTIC-SURFACE-004: 不拥有独立 law 的 helper 不直接测试

是否为 helper 单独编写测试用例，按该 helper 是否具备独立失败价值判断。不具备独立业务失败含义的内部辅助函数、局部夹具或中间工具，不得作为独立的测试对象直接编写测试用例：其行为与正确性由所属 Owner 契约面的公开行为端到端覆盖，重命名、内联或替换内部数据结构不得导致测试用例修改。具备独立失败含义的 helper 允许直接测试，门禁不要求 helper 挂靠任何 law。门禁保留对全语义测试区的 debt 扫描与 support→support 传递边可达性检查，只用于发现隐藏的内部实现依赖，不用于授予或撤销测试资格。

## JS-SEMANTIC-SURFACE-005: semantic data 跨边界必须是 JS-native representation

跨越 Semantic Surface 边界传递的数据必须完全采用 JavaScript 原生类型体系：`string`、`number`、`boolean`、`null`、`undefined`、标准数组、纯对象（plain object）、`Promise`、标准函数以及必要时的 `bigint` 与不透明资源句柄（opaque handle）。严禁向语义测试暴露编译器特有的链表、哈希表、集合、Option/Result 包装类、DU 运行时实例或类反射元数据。时间跨越边界必须归一化为 ISO-8601 字符串或 epoch 毫秒数。

## JS-SEMANTIC-SURFACE-006: Fable runtime representation 不属于 semantic contract

编译器的代码生成约定（包括模块名称前缀、DU 标签序数、内部反射元数据及修饰后的实例方法名）不属于产品语义契约的组成部分。语义测试中严禁出现针对底层运行时表示属性（如 `.tag`、`.fields`、`.cases()`）的硬编码感知与断言。只有专属的编译器产物校验测试（compiler verification quarantine）才具备直接消费底层编译产物的特殊资格。

Fable 发射成功不构成 JavaScript 模块链接成功。每次 production build 必须对完整 `dist/**/*.js` ESM 图执行 fail-closed linkage proof：所有相对 import 必须解析到 `dist/` 内真实模块；所有 named import 必须由目标模块真实 named-export；禁止生成依赖仓库源码树、未打包 sibling 或其它 `dist/` 外相对文件的运行时边。该 proof 属于 compiler/build quarantine，不把生成符号提升为产品 semantic contract。
