# 增量接收证据

固定版本、进度与剩余范围见 [计划](../../proposals/extraction-kanban/UPSTREAM-INTAKE-2026-10-20.md)，语义边界见 [处置表](../../proposals/extraction-kanban/UPSTREAM-INTAKE-DISPOSITION-2026-10-20.md)。按包记录命令和真实结果；原始失败不覆盖为成功。

## M1

- 反例：`node --test --test-name-pattern='verification results retain' requirements/verification-system/tests/016.test.mjs`，1 fail：`candidateDigest` 为 undefined。未修改实现前真实运行。
- 定向复验：`node --test --test-name-pattern='verification results retain|inputs changed|mid-run|perturbation' requirements/verification-system/tests/016.test.mjs`，2 pass / 0 fail，覆盖成功/失败完整摘要与运行中输入改变拒绝。
- 完整 016 套件另记日志；摘要自身不证明整个发行或不可变环境已验收。

- `m1-verification-016.log`：Node26完整套件308项，260pass/27fail/20skip/1TODO，退出1；工具复制夹具的 Node 在新目录无法加载 `@rpath/libnode.147.dylib`，出现SIGABRT；不由此改安装器或扩大为运行时打包修复。Node22完整二进制复验另记。
- `m1-verification-016-node22.log`：已有官方Node22.13完整二进制运行同一套件，309项288pass/0fail/20skip/1TODO，退出0；TODO的真实红仍在日志中，不能据退出0当作该旧命题通过。未修改工具安装器或运行时归档实现。

## M2

- 动态预算反例在旧构建因缺少新接口失败。gen23正式Host015/Process013/015：24pass/0fail，排空及回收通过。
- 补充降低Host上限反例：`node --test --test-name-pattern='a lowered Host ceiling' requirements/process-execution/tests/015.test.mjs`真实失败，旧实现返回Succeeded而非Failed；其最小结果未纳入Host上限。已修正启动前门禁，保留完整结果编码和Unicode规则。
- `m2-build-02.log`：gen24，Fable126项增量、171注册表面、825模块真实加载。
- `m2-formal-02.log`：`TESTS_MJS_FILES=requirements/host-boundary/tests/015.test.mjs,requirements/process-execution/tests/013.test.mjs,requirements/process-execution/tests/015.test.mjs node requirements/verification-system/tests/run.mjs`，25pass/0fail/0TODO，3/3排空，回收accepted=true。真实标记证明小字节/行上限零启动；4096字节可用上限保留结构化截断结果。
- 剩余：生产config hook的完整Host入口待M5；M6统一候选重验；不代证call/sanity/rg或完整发行。

## M3

- `m3-protocol-red.log`：call/Callable 与旧空证明反例在旧构建2fail，确认新协议尚不存在。
- `m3-build-01.log` / `m3-build-02.log`：Fable gen25/26，171注册表面、825模块实际加载；执行内核保留本地期限、取消、Capture、scope关闭与owner归属。
- `m3-formal-01.log`：14文件234pass/7fail，全部排空且回收accepted=true；完整managed-session-lifecycle-025进程退出0。七红为名称断言2处、旧示例关键词1处、缺已构建wget2处、基类声明断言2处；均未改产品期限或物理断言。
- `m3-stale-refusal.log`：构建未完成时正式入口正确拒绝旧manifest；不计产品失败或通过。
- `m3-formal-03.log`：gen26正式14文件120pass/0fail/0TODO，14/14排空、回收accepted=true。PATH只补已有 `/tmp/wget-1.25.0/src` 的真实GNU wget；无新下载/系统安装。覆盖公开能力、前后台/wget/send、伪造apply、参数数组、真实next/watch忙循环、未完成Promise、证明期间取消、并发借期、park期限、双路原字节、spool故障与未确认关闭归属。
- 另有小范围手工格式整理与描述词汇修正，后续统一构建需覆盖这些最终字节；不将gen26证据冒称最终提交字节已验。M6完整物理和历史回归仍待。
- `m3-build-03.log`：最终手工格式和描述修正后gen27，171表面/825模块加载通过。`m3-formal-04.log`：公开001/002、sanity050、取消006四文件50/0，4/4排空、回收accepted=true。三个格式预览仅写/tmp；依预览手工编辑后check通过，控制嵌套检查0违规。M3限定范围READY，完整发行、真实Host配置与M6统一候选未完成。
