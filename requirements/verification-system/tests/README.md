# verification-system 测试说明

本目录包含全仓测试运行调度器与验证系统自保测试。行为规范见 [WHAT.md](../WHAT.md)。

## 测试套件与运行入口

验证分为日常开发测试与发布验证两类，由 `scripts/verify.mjs` 统一编排，各层具备独立可执行入口：

1. **单元测试运行器（Unit Runner）**
   - 入口：`node requirements/verification-system/tests/run.mjs`
   - 职责：扫描全仓 `requirements/*/tests/*.test.mjs` 并由 `superviseNodeTest` 监督运行；启动前检查编译产物新鲜度，防止对过期代码产生误报。
   - 过滤运行：通过 `TESTS_MJS_FILES=path1,path2` 环境变量或 `node scripts/verify.mjs --suites=<pkg>` 聚焦运行指定套件。

2. **集成测试调度器（Integration Runner）**
   - 入口：`node requirements/verification-system/tests/integration/run.mjs`
   - 职责：调度跨进程、环境隔离与构建产物相关的集成测试套件。

3. **端到端发布验证（E2E / Long Stroke）**
   - 入口：`node requirements/verification-system/tests/014.test.mjs`（或经 `npm run verify:release` 调度）
   - 职责：在单个真实 OpenCode 进程中运行长程多轮交互场景，操作说明见 [e2e/README.md](e2e/README.md)。

## 常用运行命令

```bash
# 运行单元测试
node requirements/verification-system/tests/run.mjs

# 聚焦运行本包单元测试
TESTS_MJS_FILES="$(find requirements/verification-system/tests -maxdepth 1 -name '*.test.mjs' | paste -sd, -)" node requirements/verification-system/tests/run.mjs

# 运行集成测试
node requirements/verification-system/tests/integration/run.mjs

# 全量发布验证（含构建、单元、集成、E2E 与打包检验）
npm run verify:release
```
