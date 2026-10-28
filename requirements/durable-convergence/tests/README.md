# durable-convergence 测试说明

行为规范见 [WHAT.md](../WHAT.md)。测试文件 `NNN.test.mjs` 与条款编号对应；本文件说明各编号覆盖什么、怎么跑。`tests/support/` 提供事件构造与断言辅助（`events.mjs` 被 001/004/005/006 引用）。

## 覆盖点

- **001**：合并保留不同并发事实、只去掉完全重复；一个 `WriterId` 命名两条流时 fail-closed 而非丢分支。
- **002**：跨 writer 依赖先于 child；writer 流置换与重复输入下合并结果不变；独立流合并满足结合律。
- **003**：标识碰撞 fail-closed（不是后写覆盖）；同 writer 的段文件先聚合为单条逻辑流再进 k-way。
- **004/005**：共同父节点的合法分叉保留两个 structural heads，不作存储损坏；部分覆盖父集时 heads 继续并存，全部覆盖才唯一。
- **006**：revision 与墙钟取值相反时竞争事实仍然全部保留。
- **007**：保留窗口内的完整历史重建出与在线运行一致的 production Current。
- **011**：真实读取链保留今天/昨天 writer、更早的整条丢弃；非有限截止时刻被拒绝；活动时间由尾部 Journal `ObservedAt` 决定而非文件时间；尾部 cut-tail 链跨段回溯到 Journal 观测；其他尾行回退到最新文件时间。

## 运行方式

仓库根目录先标准构建：

```sh
node scripts/build.mjs
```

单文件直跑：

```sh
node --test requirements/durable-convergence/tests/007.test.mjs
```

或经统一验证运行器跑整个包：

```sh
TESTS_MJS_FILES="$(rg --files requirements/durable-convergence/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
