# execution-model-routing 测试说明

行为规范见 [../WHAT.md](../WHAT.md)。本目录验证执行模型路由的准入顺序、租约归属与容量结算。

## 各用例覆盖范围

| 文件 | 看什么 |
|---|---|
| 001—002 | 模板文件不再存在；调度器 ABI 的形状与协议版本正反例。 |
| 003、006 | 进程内共享的 multiset 容量真相、多 plugin 实例、exact retry 与 previous 续用；追加材料在 step 结束后交接 credit，取消时清理自己的保留。 |
| 004—005 | pending 队列的等待、取代与唤醒；更换调度输入同时改变 target 与容量策略。 |
| 007—009 | 物理终结与单步终结的区分、只读释放、Host decoder 与 SDK adapter、`chat.message` 保持宿主模型、`chat.params` 只观测不建租约。 |
| 010 | token 账簿与借贷：显式 lender、单调序号公平等待、transform 入口抢占召回、有界随机序列。 |
| 011—012 | capacity fence 身份、旧 epoch 拒绝、跨 owner 拒绝、单次结算。 |
| 013—015 | 队列上限与不丢弃、FIFO 唤醒、immutable snapshot 只观察不修复、诊断投影复用同一份 snapshot。 |
| 016 | 当前为 TODO：routing fatal 的注入与结算顺序尚无自动化覆盖。 |
| 017—018 | 失败后的原目标保留或驱逐、角色集合准入与旧角色拒绝。 |
| 019 | 本地已有 DevOps 绑定时拒绝冲突改绑，未记录绑定的会话仍走普通调度。 |
| 020 | 调度只按角色池回答理论容量。 |

`support/process-shared-routing.mjs` 提供跨进程用例的环境构造（临时 HOME、plugin 实例创建与共享消息形状），不是独立断言。它仍会写一份 `~/.config/opencode/wanxiangshu.mjs`；现行程序不加载该文件，这份夹具不参与任何断言，可清理。

## 运行方式

先执行标准构建，再使用验证系统运行器：

```sh
TESTS_MJS_FILES="$(rg --files requirements/execution-model-routing/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
