# WHY — 为什么 distribution 必须独立存在

## 不可替代的存在理由

> 源码树本地绿 ≠ 消费者安装后绿。运行时依赖的代码与 semantic resources 必须作为**同一个可安装 artifact** 交付；否则你在仓库里验证的「产品世界」与用户安装后运行的世界不是同一个世界。

具体到本产品：Wanxiangshu 是 OpenCode 插件，运行时同时依赖

- **编译代码**：`dist/**`（Fable 编译产物，入口 `dist/Infrastructure/OpenCode/Plugin/Plugin.js`）；
- **semantic resources**：`resources/provider/**`（Common Law / Role Law / Library 双语散文）、`resources/enforcer/**`（rulebook tip 双文件）。

这两者缺一，安装后的插件就无法启动（fail fast）或提供错误世界。它们的**同时、同包、同版本到达**由本包保证。

## 为什么不能并入其它包

- 它不拥有任何业务语义：resource 散文的**内容意义**归 `provider-language`（双语契约）、`office-capability`（office 后果）、`cognitive-environment`（知识组织）、`behavior-diagnosis`（tip 诊断边界）等；本包只保证「这些资源**在 artifact 里可得**」。
- 它不是编译/构建机制：Fable、fantomas、`scripts/build.mjs` 的具体做法是 HOW，可整体替换。
- 它不是 verification 治理：证明阶梯的强度归 `verification-system`；本包只要求 release proof **覆盖** closure（`DISTRIBUTION-007`）。

独立 change 测试：把分发介质从 npm package 换成另一种 bundle/install format（tar、单文件 bundle、registry 之外的渠道），只要 runtime closure/entry/resource guarantees 不变，本包命题全部成立——这证明它的 WHY 与介质无关。

## RED 是什么样（失败模式）

```text
RED = consumer 安装的 artifact 缺少运行所需 code/resource
    ∨ 依赖 cwd/源码树才能运行
    ∨ tests 验证的是一份与 shipped bytes 不同的实现
```

具体可观察形态：

| 形态 | 违反 |
|---|---|
| 安装 tarball 后 `import "wanxiangshu"` 失败（`dist/.../Plugin.js` 不在包里） | DISTRIBUTION-001/003 |
| 从非仓库目录启动，插件找不到 `resources/`（查找依赖 `process.cwd()`） | DISTRIBUTION-002 |
| `main` 与 `exports["."]` 指向不同文件，或指向未打包路径 | DISTRIBUTION-003 |
| tarball 意外含 `src/`、`tests/`、`scripts/`、`docs/` 或 `.fs` 源文件 | DISTRIBUTION-004 |
| `dist/` 里混入源文件副本（双副本），或测试跑一份、发布另一份 | DISTRIBUTION-005 |
| 资源缺失但启动静默继续（代码内 fallback catalog 掩盖坏包） | DISTRIBUTION-006 |
| release 发布前没有跑过 build/package/packing/install/import/resource 检查 | DISTRIBUTION-007 |
| 某语义包新增资源目录，但 `files` whitelist 或闭包校验没把它带进 artifact | DISTRIBUTION-008 |

## 历史考古（为什么长这样）

`changes/completed/` 中无 distribution 专属 completed change（CHANGES-AUDIT 结论：36 份 completed 均无 distribution 行）。本包语义的 WHY 考古来自 `docs/why/enforcer.md` 的分发裁决：

> **分发：单一打包 vs dist 双副本/代码 fallback。** 拒双副本：掩盖打包错误；拒代码内 fallback catalog：让坏的打包静默成功。resource 随 npm pack 单份发布。

即：历史上确实考虑过「把资源复制进 dist/ 双份」与「代码内嵌 fallback 清单」两种方案，均被拒——前者掩盖打包错误（测试可能读 src 副本而消费者拿到坏 dist），后者让缺资源的包静默运行。当前实现=单份发布 + 目录即清单 + fail fast，正是这两次拒绝的正面形态。`catalog.json` 作为 rulebook 元数据第二真相也被废止（`docs/why/enforcer.md`、ENFORCER-002/072/073 → GARBAGE）。

## 世界什么时候变绿（guarantee 成立）

- artifact（tarball/已安装包）包含 entrypoint、全部编译代码、全部 runtime semantic resources；
- 任何 cwd 下资源查找都成功，缺资源则启动失败（不静默）；
- manifest（`main`/`exports`）与实际路径一致；
- 打包内容由明确 whitelist 决定，开发/测试/legacy 资产不进入；
- build 产物是测试与发布共同消费的唯一编译字节；
- release proof（`format-build-test` 末级 `npm pack --dry-run`）覆盖上述每一条。
