# CI 四位 PID 夹具修复

CI 原日志 `/private/tmp/vibe-fs-ci-87c997-failed.log`：unit authoritative 为 4421 pass、6 fail、207 skip、277 TODO，757/757 files completed。六个失败均为 durable-events005 的真实 native crash（append-first、fsync-first、append-unicode-second、before-unlink、after-unlink、resume-copied-fsync）。`missing.fixture.mjs` / `broken.fixture.mjs`、js-module-linkage FAILED 输出来自验证系统负控，不是另外失败。unit 失败后不代表后续未执行的 release 阶段已通过。

真实规范：durable-events WHAT005 禁止新 WriterId 以 `-` 加四位数字结尾；生产 `ProcessEventLog.fs:392-414` 解析段序号并验证新身份。旧 support cold reader 用 `cold-reader-${process.pid}`，Linux 四位 PID 因此合法地被拒。mac 五位 PID 遮蔽了夹具错误。

仅修改：

- `requirements/durable-events/tests/support/native-crash.mjs`：公开测试内 helper `coldReaderWriterId(pid)` 并让真实 cold worker 使用它；格式为 `cold-reader-p${pid}`。
- `requirements/durable-events/tests/005.test.mjs`：新增确定性 PID 边界用例，经真实 EventStore.create + heads + dispose 验证 1/999/1000/4242/9999/10000/4194304；确认旧 -0000/-0001/-0999/-1000/-4242/-9999 名称仍被生产验证拒绝。

`base-red.log`：先把旧构造原样抽成同一 helper，并加上述实际 create 反例，Node22 0 pass/1 fail，错误正是 WriterId 禁尾。之后只把 helper 改成带 p 前缀。

`gen102-raw-green.log`：Node v22.13.0，现有 gen102 dist，直接 `node --test requirements/durable-events/tests/005.test.mjs`，31 pass / 0 fail / 0 skip / 0 TODO，5717.9ms，exit 0。保留全部原断言，六个真实 OS crash 各有 owner PID、SIGKILL、冷读/恢复 PID 证据；既有 helper 校验全部子进程已退出及恢复幂等。

没有改生产解析、Host预算、runner、门禁或其他主仓文件。未构建、stage、commit；两文件 scoped `git diff --check` exit 0。主仓 root 正同时修改 permit 接线，未触碰该集合。此为 raw 行为验证，正式验收仍须 root gen103 重新构建及正式 run.mjs。
