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

## M4 / M5

- `m4-build-06.log`：gen31，171表面/826模块。之前真实rg反例发现绝对路径校验误用文件API的根相对语义，已修；测试启动器遗漏Node参数分隔符会产生假阳性，补实际进入见证后重验。
- `m4-formal-04.log`：并行58/1，取消启动见证未及时到达，保留失败。`m4-formal-05.log`：串行59/0，5/5排空且回收accepted。实际搜索、根内外/侧文件/符号链接、配置继承、缺rg、JSON/UTF8损坏、NUL文件名、输出超量、同一绝对期限、预取消及TERM抵抗后的PID死亡；另含真实插件Engineer无Exec与Host配置接线。
- `m4-repository-node22.log`：最低Node全编程首轮289/5，58/58排空回收。五处公开合同旧断言和failureCatalog漏RG_FAILED已手工调整；复验待。日志保留，不计完整通过。
- 受控shell启动器不稳定，采用实际Node查询；期限测试仍要求第一条完成、第二条实际启动并被同一期限停止，10秒第二工作不能获得新预算。产品期限、5秒监督门与旧TODO均未放宽。早期失败日志保留。

剩余：统一候选物理/历史/包消费与发行入口；E05/E06按处置表留档。Blogger、.Herdchestra、库16入口及旧GAP范围保持。

## M6 进行中

- `m6-build-01.log`：gen32，171注册表面、826模块实际加载；`m6-format-01.log`、`m6-check-01.log`均退出0，report-only低置信度提示不记作销项。
- `m6-related-node22-01.log`：最低Node串行78文件611pass/1fail/8TODO，78/78排空且回收accepted=true。全编程旧断言修正、原物理集、历史身份009/010、Trace002、Attachment021、冻结前缀008、context-compression010/011、WorkRecord001、只读墓碑及发行入口约束均实际执行。唯一普通失败是rg取消夹具3秒启动窗口；8旧TODO完整保留，不接受为通过。
- `m4-cancel-independent-node22.log`仍在3秒窗口失败；`m4-cancel-observed-node22.log`使用已有有限物理等待后1/0，4363ms完成，实际进入见证后取消并确认PID死亡。预取消与活动取消分为独立插件实例；正式补验待。并未调整Host期限、5秒默认静默或实际关闭断言。
- `m6-build-02.log`：gen33。`m6-focused-node22-02.log`：最低Node正式80/0，10/10排空、回收accepted=true，有限物理等待已正式验收；公开合同修正、插件接线与真实搜索可用。各组选集重叠，不累加为完整发行通过。
- `m6-release-01*.log`：实际发行入口格式/静态/干净gen34通过，171表面/826模块；单元distribution007触发5000ms静默，260/870文件完成，无权威总计，回收accepted=true；未进入集成/e2e/package，发行不通过。
- `m6-contract-node22-03.log`：四处能力断言和005错误码补齐后40/1/2旧TODO，新增Grep使Manager只读示例被旧白名单排除；精准补Grep后`m6-build-04.log`为gen36，`m6-contract-node22-04.log`正式41/0/2旧TODO，4/4排空、回收accepted=true。未增加Write/Exec权限，旧TODO未接受。

## 最终记录

计划与代码冻结后，最终构建、发行入口、跨运行时物理/包消费、提交及普通推送分别写入同目录`delivery.json`；原始日志继续保存。该JSON是最后包状态与剩余边界的记录。原始日志的空白与失败不清洗；源码与需求改动另跑diff检查。
