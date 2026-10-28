# participant-identity 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含参与者身份（Participant Identity）、角色投影与生命周期状态的测试。

## 测试覆盖重点

- 验证参与者唯一标识符（Participant ID）与逻辑角色名称的映射。
- 验证角色准入矩阵与跨会话身份传递的正确性。
- 验证历史角色与未授权名字无法伪造身份进入活跃调度路径（历史标签只在隔离解码边界可用）。

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/participant-identity/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
