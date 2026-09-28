# host-provider-failure-ownership 测试说明

[WHAT](../WHAT.md) 定义规则。本目录区分配置写入、Host 实际消费与最终界面呈现，三者不能互相代替。

| 文件 | 实际证据 | 未覆盖 |
|---|---|---|
| 001 | 实际插件 config hook 覆盖既有值与旧环境变量 | Host 是否消费该字段 |
| 002 | 安装版 Host、生产配置投影、本机受控 503 服务及实际请求计数 | 完整插件的持久恢复、授权新 run、正常 provider 体验 |
| 003/006 | 现存纯分类器区分 Recovery/Final/Ignore | 分类器未发现生产调用；不能证明真实提示、终态写入、停止准入或恰好一次 |
| 004 | 实际配置入口将旧角色配置错误交回调用方 | 其余错误类别与原生 UI 可见性 |
| 005 | 既有 retry-owner 源码启发式 | 全部真实调用者的授权与唯一写入 |
| 007 | 依赖声明的精确版本 | consumer 与 producer→SDK→Desktop/CLI 全链漂移门禁 |

002 的观察插件调用生产配置投影，并记录 Host 的公开事件；它不是完整 Wanxiangshu 插件。固定会话标题排除后台标题请求，一条物理用户消息只启动一个 assistant run。端点返回受控 `503` 和 `retry-after-ms: 1`，后者只是缩短故障夹具的等待，不产生第二次请求。发现第二次请求便结束场景；未提前出现重复时等待失败终态和 idle。报告保留请求总数，不去重掩盖重试。清理会中止该会话、停止 Host、关闭端口并删除自己的隔离目录，不使用私人配置或真实凭证。

2026-09-26 实测 OpenCode/plugin 1.18.29：配置为 0、1 个 assistant run、2 次 provider request。该用例保留原合同断言，以**可执行 TODO**记录 GAP-144；不是通过用例。中止前未到最终 session.error/terminal 不代表 Host 永不报错，不能据此评价呈现。根因与兼容性待决见[核查记录](../../../proposals/38-Host重试兼容性核查-2026-09-26.md)。其余整链缺口见 GAP-143。

先运行 `node scripts/build.mjs`，再将本目录的 `001.test.mjs` 至 `007.test.mjs` 交给 `requirements/verification-system/tests/run.mjs` 的 `TESTS_MJS_FILES`。002 需要本机回环监听与安装的 OpenCode 可执行文件；缺少这些条件应报告环境阻塞，不计通过。TODO 阻断验收。
