# host-boundary 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录验证与宿主（Host）交互的边界，包括参数编解码、Hook 挂载、工具调用拦截与时序对齐。

## 测试覆盖重点

- **信号与参数转换**：验证多字节文本、大单行 Unicode、键序保持及参数隐藏（before/after 阶段恢复原键序与原始对象）。
- **工具调用与取消**：
  - 验证粗粒度中断与精确取消的区分，新用户输入到达时不抢先发出破坏性中断。
  - 032 验证工具参数所有权与键序恢复；覆盖正常返回、业务错误与取消三条路径。
  - 033 验证新用户输入的非中断合同（新输入进入下一次请求，不打断正在进行的无害计算与工具执行）。
- **进程与终止防御**：
  - 026 验证 Fable 局部编译依赖完整性。
  - 029 验证致命异常时子进程的确定性硬退出。

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

执行本包测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/host-boundary/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
对于需要启动实际宿主适配的用例（如 032、033），需在集成测试模式下配合真实环境运行。
