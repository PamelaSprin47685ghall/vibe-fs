# durable-events — README

本包是本机持久化事件底座的操作说明；规则正文见 [WHAT.md](WHAT.md)，动机见 [WHY.md](WHY.md)。

## 它服务总流程的哪一步

总流程（用户提出需求 → 交互调查 → 稳妥修改代码 → 真实做成结束）的每一步都产生事实：会话开始、工具调用、委托、账本变更、语义截断。本底座负责把这些事实追加进 `.Herdchestra/wanxiangshu/events/` 下的 ndjson 段文件，并在进程重启或插件重载时按同一套积分程序重建各业务的 `Current` 状态。崩溃恢复、会话续接、委托账目对得上，都靠这条重放链。

日常使用不需要手工操作这个底座：业务代码通过 Journal/EventStore 接口追加事实，查询读 Integrator 的 Current。需要人工介入的场景只有两类——排查「重启后状态不对」和确认磁盘上的事实流形态。

## 本地复现一条重放

1. 标准构建（编译 F# 到 JS）：

   ```sh
   node scripts/build.mjs
   ```

2. 跑重放相关测试（任选其一）：

   ```sh
   node --test requirements/durable-events/tests/013.test.mjs   # 重启重放 + 同一积分程序
   node --test requirements/durable-events/tests/007.test.mjs   # 损坏历史 fail-closed
   ```

   或经统一验证运行器跑整个包：

   ```sh
   TESTS_MJS_FILES="$(rg --files requirements/durable-events/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
   ```

3. 直接看事实流：打开任意工作区的 `.Herdchestra/wanxiangshu/events/`，每个 `<WriterId>-<seq>.ndjson` 是一次进程的追加段，每行一个 canonical JSON 事件。用任意 JSON 行解析器按序读取，就是该 writer 的逻辑流；多条逻辑流按 durable-convergence 的 k-way 合并规则归并后交给 Integrator，即得与运行时一致的 Current。

4. 想验证「重放等于在线」：先跑一次产生事实的测试（如 013），再对同一状态根冷启动重放，比较两次的 Current。测试 013/015/007 已固化这条对照。

涉及真实编译的用例（如 022 的闭包编译）需 `WXS_TIER_INTEGRATION=1`。测试清单与各编号覆盖点见 [tests/README.md](tests/README.md)。
