# distribution — WHAT

## DISTRIBUTION-001: 编译代码与 Runtime Semantic Resources 单一 Artifact 交付

安装产物（npm tarball 或已安装包）必须同时包含生产入口的编译代码（`dist/**`）与全部运行时语义资源（`resources/**`）。二者必须作为同一个自足的 artifact 一并交付，不存在代码与资源分离分发的机制。

## DISTRIBUTION-002: Runtime Resource 定位独立于 Caller CWD

运行时加载 package resource 必须基于模块文件自身的绝对位置（通过 `import.meta.url` 上溯定位包根目录）进行固定相对路径解析，严禁依赖 `process.cwd()`、严禁执行候选路径探测、严禁回退至 `src/` 或 `dist/` 内部的备用副本。

## DISTRIBUTION-003: Manifest Entry 与实际 Shipped Paths 一致

`package.json` 中的 `main` 与 `exports["."]` 必须精确指向同一个实际存在于 artifact 内部的文件（`./dist/OpenCode/Plugin/Plugin.js`），保证消费者与宿主工具链按 manifest 解析入口时能够正常加载。

## DISTRIBUTION-004: Package 内容由显式 Whitelist 决定

打包产物的内容必须且仅能由 `package.json` 的 `files` 白名单显式指定（`["dist/", "resources/"]`）。不属于运行时所需的源码文件（`src/`、`.fs`、`.fsproj`）、测试文件（`tests/`）、内部工具（`scripts/`）、规范文档（`requirements/`）或已废止资产严禁打包进入交付物。

## DISTRIBUTION-005: 编译、测试与发布消费同一份 Production Bytes

`dist/**` 是系统唯一的编译产物。构建过程必须先清理旧目录再进行编译；测试套件必须直接消费该编译产物并配备防陈旧门禁（`dist/` 产物早于源码时拒绝运行）；发布流程直接打包同一份产物，严禁在 `dist/` 中引入资源的双副本。

## DISTRIBUTION-006: 资源加载收口与缺失 Fail-Fast

所有语义资源的读取与 I/O 操作必须严格收口于资源基础设施模块。当请求的资源不存在时，必须立即抛出致命错误中断流程，严禁使用代码内置的 fallback 清单进行静默降级；规则库元数据不以 `catalog.json` 为第二真源，以实际目录结构为唯一依据。

## DISTRIBUTION-007: Release Proof 覆盖 Artifact Closure

发布级证明流程（`format-build-test`）必须涵盖构建、打包模拟与解包验证全链条，通过执行 `npm pack --dry-run` 严格校验实际打包清单，确保交付物包含完整的入口代码与资源闭包。

## DISTRIBUTION-008: 语义包 Runtime Resources 完整可得

所有声明了运行时资源的语义包（包括诊断规则库、角色法典、通用法典及工具描述等），其对应的资源目录与双语文件必须在已发布产物中完整可得。本包保证这些资源随 artifact 一同交付，不拥有资源内容的具体业务语义。
