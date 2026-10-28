# js-semantic-surface 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本说明解释 JavaScript 语义暴露面、契约检查器与边界门禁的测试范围。

## 条款与测试重点

| 条款 | 验证内容 | 说明 |
|---|---|---|
| **001** | 测试载体与辅助代码检查 | 检查辅助文件规范及执行边界。 |
| **002** | 语义边界门禁 | 验证门禁接受已注册的公开入口、拒绝直接访问内部实现；覆盖字面量别名逃逸防御。 |
| **003** | 清单与登记核对 | 验证登记模块确实发射、源码在编译清单内；畸形条目与重复模块被拒绝。 |
| **004** | 辅助模块传递依赖 | 检查辅助文件间的依赖环与孤儿辅助文件。 |
| **005** | JS 原生值与句柄结构 | 验证公开方法只返回原生值、安全 Promise 或不透明句柄。 |
| **006** | 产物链接与完整性 | 验证模块加载、字面量导入及缺失导出的检测。 |

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/js-semantic-surface/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
