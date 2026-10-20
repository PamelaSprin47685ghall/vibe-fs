# 增量接收证据

固定版本、进度与剩余范围见 [计划](../../proposals/extraction-kanban/UPSTREAM-INTAKE-2026-10-20.md)，语义边界见 [处置表](../../proposals/extraction-kanban/UPSTREAM-INTAKE-DISPOSITION-2026-10-20.md)。按包记录命令和真实结果；原始失败不覆盖为成功。

## M1

- 反例：`node --test --test-name-pattern='verification results retain' requirements/verification-system/tests/016.test.mjs`，1 fail：`candidateDigest` 为 undefined。未修改实现前真实运行。
- 定向复验：`node --test --test-name-pattern='verification results retain|inputs changed|mid-run|perturbation' requirements/verification-system/tests/016.test.mjs`，2 pass / 0 fail，覆盖成功/失败完整摘要与运行中输入改变拒绝。
- 完整 016 套件另记日志；摘要自身不证明整个发行或不可变环境已验收。

- `m1-verification-016.log`：Node26完整套件308项，260pass/27fail/20skip/1TODO，退出1；工具复制夹具的 Node 在新目录无法加载 `@rpath/libnode.147.dylib`，出现SIGABRT；不由此改安装器或扩大为运行时打包修复。Node22完整二进制复验另记。

## M2

- 动态预算反例在旧构建因缺少新接口失败。gen23正式Host015/Process013/015：24pass/0fail，排空及回收通过。
- 补充降低Host上限反例：`node --test --test-name-pattern='a lowered Host ceiling' requirements/process-execution/tests/015.test.mjs`真实失败，旧实现返回Succeeded而非Failed；其最小结果未纳入Host上限。已修正启动前门禁，保留完整结果编码和Unicode规则。
- `m2-build-02.log`：gen24，Fable126项增量、171注册表面、825模块真实加载。
- `m2-formal-02.log`：`TESTS_MJS_FILES=requirements/host-boundary/tests/015.test.mjs,requirements/process-execution/tests/013.test.mjs,requirements/process-execution/tests/015.test.mjs node requirements/verification-system/tests/run.mjs`，25pass/0fail/0TODO，3/3排空，回收accepted=true。真实标记证明小字节/行上限零启动；4096字节可用上限保留结构化截断结果。
- 剩余：生产config hook的完整Host入口待M5；M6统一候选重验；不代证call/sanity/rg或完整发行。
