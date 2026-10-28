# session-ontology 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录验证会话容器分类、关联维护与上下文复用边界。

## 测试覆盖重点

- **003/004**：验证生产会话分类函数，确保不同类型的会话（如工作会话与伴侣会话）具有明确归属。
- **005/007—010**：验证会话间关联投影的双向维护、冲突拒绝与安全解散。008 覆盖防止重复挂接伴侣会话的回归测试。
- **006**：验证持久化父子会话关联的投影。
- **012**：验证类型化事务附件在会话生命周期中的传递。

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/session-ontology/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
