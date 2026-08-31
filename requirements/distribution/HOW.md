# distribution — HOW

## 架构模型与执行流

`distribution` 保证系统从编译产物到用户交付包的物理闭包完整性：

```text
源码树与语义资源 (src/**, resources/**)
  ↓
Build Pipeline (清理旧 dist/ → Fable 编译 → 验证入口与资源完整性)
  ↓
Staleness Gate (确保测试运行消费最新 dist/ 字节)
  ↓
Package Layout (package.json 设置 main, exports, files 白名单)
  ↓
PackageResources (基于 import.meta.url 独立解析 resources/**)
  ↓
Release Proof (npm pack --dry-run 验证 tarball 清单)
```

## 核心机制

### 1. 产物配置与白名单管理 (Artifact Manifest & Whitelist)

- **Entrypoint 对齐**：`package.json` 中的 `main` 与 `exports["."]` 统一指向 `./dist/OpenCode/Plugin/Plugin.js`。
- **打包白名单**：通过 `files: ["dist/", "resources/"]` 精确限定打包范围，自动排除 `src/` 源码、测试用例与开发脚本，保证产物纯净。

### 2. 独立于 CWD 的资源解析 (CWD-Independent Resolution)

- 资源读取模块（`PackageResources`）通过 `import.meta.url` 计算模块自身路径并向上寻址三级到达包根目录，拼接目标资源相对路径。
- 严禁调用 `process.cwd()` 或进行候选路径盲目猜测；目标文件不存在时立即抛出 `package resource missing: <path>` 异常，杜绝静默失败。

### 3. 同源编译与防陈旧门禁 (Clean Build & Staleness Gate)

- 构建脚本在编译前彻底清空 `dist/` 目录，防止旧的未清理产物混入发布包。
- 测试运行器内嵌时间戳检查，当检测到源码较编译产物更新时拒绝执行，确保所有测试严格证明待发布字节的正确性。

### 4. 发布级闭包证明 (Release Proof & Packing Verification)

- 在发布前执行全流程验证，最终阶段调用 `npm pack --dry-run` 导出实际生成的 tarball 包含项清单，验证入口脚本与所有语义包的资源目录均被完整收录。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DISTRIBUTION-001 | `requirements/distribution/tests/pack-closure.test.mjs::WHAT[DISTRIBUTION-001] DISTRIBUTION_artifact_carries_compiled_code_and_runtime_resources_together` |
| DISTRIBUTION-002 | `requirements/distribution/tests/cwd-independent-resources.test.mjs::WHAT[DISTRIBUTION-002] DISTRIBUTION_resource_reads_resolve_under_package_root_regardless_of_cwd`；`requirements/distribution/tests/cwd-independent-resources.test.mjs::WHAT[DISTRIBUTION-002] DISTRIBUTION_fresh_process_with_foreign_cwd_imports_entry_and_reads_resources` |
| DISTRIBUTION-003 | `requirements/distribution/tests/pack-closure.test.mjs::WHAT[DISTRIBUTION-003] DISTRIBUTION_manifest_entry_matches_exports_and_shipped_path` |
| DISTRIBUTION-004 | `requirements/distribution/tests/pack-closure.test.mjs::WHAT[DISTRIBUTION-004] DISTRIBUTION_files_whitelist_is_explicit_and_excludes_dev_test_legacy` |
| DISTRIBUTION-005 | `requirements/distribution/tests/cwd-independent-resources.test.mjs::WHAT[DISTRIBUTION-005] DISTRIBUTION_lookup_is_single_fixed_relative_path_not_candidate_search` |
| DISTRIBUTION-006 | `requirements/distribution/tests/cwd-independent-resources.test.mjs::WHAT[DISTRIBUTION-006] DISTRIBUTION_resource_missing_fails_fast_no_fallback` |
| DISTRIBUTION-007 | `requirements/distribution/tests/pack-closure.test.mjs::WHAT[DISTRIBUTION-007] DISTRIBUTION_release_proof_covers_build_package_packing_and_artifact_checks` |
| DISTRIBUTION-008 | `requirements/distribution/tests/pack-closure.test.mjs::WHAT[DISTRIBUTION-008] DISTRIBUTION_enforcer_rulebook_closure_is_complete`；`requirements/distribution/tests/pack-closure.test.mjs::WHAT[DISTRIBUTION-008] DISTRIBUTION_provider_resource_closure_is_language_complete` |
