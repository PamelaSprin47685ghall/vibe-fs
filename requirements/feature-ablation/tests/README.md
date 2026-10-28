# feature-ablation 测试说明

规则以 [WHAT](../WHAT.md) 为准。本目录是消融开关与 DAG 校验的自保测试。

## 测试覆盖重点

| 文件 | 验证内容 |
|---|---|
| `001.test.mjs` | 主节点与包一一对应；加载器拒绝主节点缺失、重复及错误父节点 |
| `002.test.mjs` | 未配置时默认全 active；工具执行、schema、角色和事实写入的三态结果及借用面 |
| `003.test.mjs` | 加载器拒绝两类边形成的环；枚举两类边各九种状态组合 |
| `004.test.mjs` | 全部当前配置集实际加载；合法覆盖生效；非法覆盖和未知配置被拒绝 |

反例在临时包副本内修改清单，再调用编译产物 `Ablation/Surface.js`。正式资源文件不被改写；临时目录在用例结束时清理，环境变量在用例后恢复，模块缓存清空。

三态用例只覆盖开关判定本身；某包 `active` 下的完整业务行为由各业务包自己的测试验证。

## 运行方式

先执行标准构建，再使用验证系统运行器执行：
```sh
TESTS_MJS_FILES="$(rg --files requirements/feature-ablation/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
