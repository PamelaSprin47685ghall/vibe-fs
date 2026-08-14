# `distribution`

WHY: 运行时依赖的代码与 semantic resources 必须作为同一个可安装 artifact 交付；否则源码树本地绿并不能证明消费者安装后拥有同一个产品世界。

OWNS:
- package/install artifact 必须包含 production entrypoint、运行时所需 compiled code 与 runtime semantic resources。
- runtime resource lookup 必须独立于 caller current working directory。
- package manifest/exports 与实际 shipped paths 一致。
- package contents 明确 whitelist；不属于 consumer runtime 的开发/测试/legacy authority 不应因偶然目录打包进入 artifact。
- build/test 应消费与发布相同的 compiled production bytes，避免测试另一份实现。
- release proof 包含 build/package/packing/install/import/resource availability。

DOES NOT OWN:
- resources 内部 prose/rule 的业务意义。
- compiler/build tool 的具体实现。
- npm 必须永久是分发介质。
- 当前 `dist/` / `resources/` 路径名字必须永久保持；它们是现行 HOW/contract evidence。
- version bump/release cadence。
- release/package proof 强度；这是 verification-system 的横向治理，不是 distribution semantic dependency。

DEPENDS ON:
- 所有声明 runtime resource 的 semantic packages；这里只依赖“这些资源必须随 artifact 可得”，不获得其 semantic ownership。

PROVIDES: 安装后的 artifact 与仓库中被验收产品在 runtime code/resource closure 上一致的 guarantee。

FAILURE MEANING: RED = consumer 安装的 artifact 缺少运行所需 code/resource、依赖 cwd/源码树才能运行，或 tests 验的是一份与 shipped bytes 不同的实现。

INDEPENDENT CHANGE: 从 npm package 改为另一 bundle/install format，而 runtime closure/entry/resource guarantees 不变。

CURRENT EVIDENCE: `package.json` `files=[dist/,resources/]`；package integration `contents/install/import/resources`；`npm pack --dry-run`；VERIFY release proof；`PackageResources` fixed-relative-path test。
