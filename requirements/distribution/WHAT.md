# distribution — WHAT

## [001] 单一交付物

安装产物同时包含生产编译代码 `dist/**` 和全部 runtime semantic resources `resources/**`，作为同一个自足 artifact 交付，不分开发送代码与资源。

## [002] 资源定位

资源路径以模块自身的绝对位置（import.meta.url）上溯包根，再按固定相对路径解析。不依赖 caller CWD，不探测候选路径，不回退到 src 或 dist 内备用资源。

## [003] 生产入口

package.json 的 main 与 exports["."] 均指向 artifact 内实际存在的 `./dist/OpenCode/Plugin/Plugin.js`，消费者按 manifest 可正常加载。

## [004] 内容白名单

package.json 的 files 白名单固定为 `["dist/", "resources/"]`。不得打包源码、测试、内部工具、规范文档或非运行时开发资产。

## [005] 同一份 Production Bytes

dist 是唯一编译产物。发布须通过 `scripts/build.mjs --clean` 重建，日常使用增量构建；二者产物字节一致。测试直接消费这些产物，并以 manifest 摘要拒绝陈旧构建；发布真实 pack 同一份产物，不在 dist 复制资源。

## [006] 资源读取与缺失

语义资源读取和 I/O 统一归资源基础设施。缺失立即抛出致命错误中断，不用内置 fallback 清单静默降级；规则库元数据由实际目录决定，不以 catalog.json 建立第二权威。

## [007] 发布证明

verify:release 覆盖构建、真实 npm pack、解包和消费者验证。校验归档成员仅限 dist 与 resources，并在非仓库目录按生产入口导入；dry-run 不构成打包证明。

## [008] 资源闭合

所有声明 runtime resources 的语义包，其所需目录与双语文件均须完整进入发布产物。本包只负责交付，不决定资源的业务语义。

## [009] 归档校验

解析归档流时校验成员结构、完整性及 npm pack 结果的结构与包名。成员须为包根内安全相对路径，拒绝绝对路径、`..`、符号链接等非普通文件及开发/测试/源码/内部脚本/构建元数据；异常立即拒绝。

## [010] 活跃注册一致

产物的代码和资源须与现行活跃角色一致，不含废止角色的注册代码、死资源或旧工具映射。js-engineer、js-devops、js-bookkeeper 等现行工具在编译代码、surface manifest 和资源清单中保持完整一致，无孤立注册。
