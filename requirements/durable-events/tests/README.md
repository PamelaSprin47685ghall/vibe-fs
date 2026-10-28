# durable-events 测试说明

行为规范见 [WHAT.md](../WHAT.md)。测试文件 `NNN.test.mjs` 与 WHAT 条款编号一一对应；本文件只说明各编号覆盖什么、怎么跑。

## 覆盖点

- **001—005**：append-only 前缀字节、canonical 编码与往返、跨进程存储锁、段序列命名/滚动/64 MiB 上限与旧形态文件处理。
- **006—009**：提交结果分类（幂等去重、`CommitUnknown`、`AppendNotAttempted`）、损坏与缺失父事件 fail-closed、并发分支保留、旧格式 leave-unread 与迁移提示。
- **010—012**：`.Herdchestra` 状态根与旧 `.git` 数据隔离、零 Git 对象、payload 内嵌闭包。
- **013—015**：重启重放与在线积分一致、k-way 确定序、业务 fold 不变量拒绝。
- **016—018**：词汇表不依赖宿主、追加路径无 Git 能力、激活不装 hooks/refspecs（fixtures 里的 `*.sh`/`*.fs` 是反例输入，不是要安装的东西）。
- **019—021**：业务注册对 Current 的作用、Journal 延迟启动、语义截断落盘与下一代进程接续。
- **022—023**：编译闭包分层（契约/运行时单向、闭包有界）、codec 与领域 fold 分层。
- **024—025**：settle 先于 fatal 的次序与 fatal owner 唯一。025 当前只有 `test.todo` 占位。
- **027—028**：退役词汇 fail-closed（删行后可恢复装载）、状态根拓扑与旧数据隔离。

## 运行方式

仓库根目录先标准构建：

```sh
node scripts/build.mjs
```

单文件直跑：

```sh
node --test requirements/durable-events/tests/013.test.mjs
```

或经统一验证运行器跑整个包：

```sh
TESTS_MJS_FILES="$(rg --files requirements/durable-events/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```

含真实编译的集成用例需 `WXS_TIER_INTEGRATION=1`。
