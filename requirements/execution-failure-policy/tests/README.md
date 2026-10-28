# execution-failure-policy 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录测试直接调用生产策略、Host 解码与真实 admission/settlement owner，验证封闭分类、互斥决策与 exact identity。

- 001/002/003/005/007/008/013：封闭输入、互斥决策、恢复条件、exact identity 与 commitment。
- 004：真实容量准入拒绝错误 physical identity 和过期 lease；策略返回的 fence 字符串只是测试翻译。
- 006：真实 pre-provider 结算先 terminal、再释放容量。
- 009/011：Host error 归类与真实 hook 原样抛出；未知异常不推定执行干净，单独的 messageID 字段不算末尾物理用户消息的证据。

## 运行

构建后，把本包 `001`–`009`、`011`、`013.test.mjs` 的实际路径传给 `requirements/verification-system/tests/run.mjs` 的 `TESTS_MJS_FILES`，启用所需 integration tier。正式 runner 隔离配置目录，测试不得读取或修改日常 OpenCode 配置。
