# SM-04 施工证据

本批范围 READY。状态以[计划](../../proposals/extraction-kanban/UPSTREAM-SEMANTIC-MERGE-2026-10-19.md)为准，不代表全部历史 GAP、提交或部署。日志使用 `vibe-fs-sm04-` 前缀；红灯原件不覆盖。

- `physical-red-01` 的三个真实反例均失败：拆块 UTF-8、signal 与退出码混淆、前台 EOF；`physical-green-01` 在 generation3 为 3/0。
- `build-01` 为 generation3 构建通过；`build-02/03` 为接口/Fable 支持差异的编译红，`build-04/05` 为 generation4/5 构建通过。`build-06` 是测试 surface 编译名错误，`build-07` 为 generation6 构建通过。
- `budget-native-01`、`control-native-01`、`cancel-native-01` 保留 null 结束路径检查错误造成的红。修正后 `budget-native-02` 为 28/1，剩余目标程序用了会截断未 flush 输出的 process.exit；随后修正目标为 exitCode，不放宽原文断言。`control-native-02` 为 15/3，其中两项缺 wget，一项把 KILL 后缺原生回执误期望为正常完成。
- `callback-budget-red-01`、`watch-budget-red-01` 分别证明回调观察可绕过预算；`dispose-red-01` 证明真实插件退出未取消进程。`callback-cpu-red-01` 的三个隔离进程证明同步 sanity/next/watch 可越过 VM 等待；父测试有限等待并清理确切新建进程组。`park-deadline-red-01` 证明 park 会暂停原先的物理期限检查。
- generation6 的 `budget-native-03` 为 30/0，`control-native-03` 为 18/0。后者实际驱动 GNU wget 下载/恢复/404 与 park/unpark。`lifetime-native-02` 为 1/2，揭示 Task.IsCompleted 在 Fable 运行时无实现；后续改为观察资源表及关闭完成，不计作通过。
- `build-08` 是缺少类型标注的编译红；`build-09/10/11/12` 分别通过 generation7/8/9/10。generation7 的 `native-04` 为 82/1，唯一失败误要求同步 watch 超时必须有 SIGTERM；现断言保留真实 signal 或明确 endUnconfirmed，不伪造终结。`control-native-04` 为 44/0/2TODO，旧 TODO 未关闭。
- generation8 的 `process-native-05` 合跑 process 全集与 managed-session-lifecycle-025，386 项为 321pass/64fail/1TODO，64 红均在后者；process 普通失败为零，含真实 OpenCode PTY、隔离 Fable 正负边界及最低输出预算下的真实采集故障。仍不能把这次合跑称为全绿。
- PTY 旧夹具迁到 `.Herdchestra`，测试端口补齐生产实际使用的 sticky terminal 订阅。generation9 的 `pty-native-06` 为 89/34；剩余红证明上游通用异常恢复把尚未收尾的资源转换成了业务完成。`pty-diagnostic-03` 实际观测 finished=true 同时 blocker 尚存。generation10 将异常恢复限制在已认领完成之后，保留认领后的结果兑现；收尾失败交由原有 owned-work 失败传播，业务继续阻塞。其复验尚在进行。
- generation9 的 `formal-01` 正式选集为 109pass/0fail/0skip/0TODO，12/12 文件排空，进程组回收 accepted=true。该候选早于上述完成时机修正，不代证 generation10。
- `pty-diagnostic-01/02/03` 保留逐动作定位；本轮旧路径失败遗留的 16 个测试 custodian 经确切 PID、完整命令、测试目录核对后 TERM 清理，`test-custodian-cleanup` 为清理后空清单。没有处理生产或其他会话进程。

临时 GNU wget 输入为官方 `https://ftp.gnu.org/gnu/wget/wget-1.25.0.tar.gz`，SHA-256 `766e48423e79359ea31e41db9e5c289675947a7fcf2efdcedb726ac9d0da3784`。1479 个归档项经过路径检查，无链接；仅在 `/tmp/wget-1.25.0` 构建，配置禁用 TLS 等非本地 HTTP 测试依赖。不安装系统，不打入产品。`vibe-fs-wget-configure.log`、`vibe-fs-wget-build.log` 记录配置与编译。

- generation10 的 `pty-native-07` 为122/1；唯一剩余冷夹具向旧状态位置与非本轮scope预留。迁到真实共享状态根及确切AdmittedWork轮次后，`pty-cold-08`为1/0，仍检查blocker保留、未完成及未交付。
- generation11 的 `build-13` 通过；`formal-02`正式13文件232pass/0fail/0skip/0TODO，13/13文件排空，进程组回收accepted=true。该组包含完整managed-session-lifecycle-025、实际插件进程归属和取消、编码最低预算与故障、真实GNU wget及park/unpark。与上述分次测试有重叠，不相加计算覆盖数量。

剩余：发行与真实 Host 切换归SM-07。格式检查发现26个路径未通过，尚未处理，不记门禁通过。同步 VM 回调与有界异步等待不代证通用异步 CPU 隔离或整个 Long Stroke。旧 GAP/TODO 保持原状态；Blogger 全退役不在本包。
