# 暂停证据与恢复入口

这是用户要求提前暂停后的原始证据归档，不是完整长测通过证书。没有测试进程继续留在后台。

- `process/PAUSE-HANDOVER.md`：Process/Supervision与PTY的完整重跑命令、原始源码路径、固定包指纹、重建步骤与未完成断言。
- `process/pause-receipt.json`：两项实验的时长、字节、13个自建PID/FD不存在、PTY资源消失及HMAC回收核验。
- `process/`与`pty/`：原名harness、元数据、资源样本与日志；`pty/recovery`仅为已终止测试实例的回收证据。
- `park-interrupted/vibe-fs-mf01-long-park-report.md`：约19分46秒提前恢复、唯一回执与两个PID退出，原始harness因用户取消exit1，不是一小时通过。
- `exact-abort/READ-ME.md`：已停止的Host边界调查，受控SDK反例的有限证据；没有实际provider被中断的声明。

源码从原路径原字节归档，目录布局为便于保存而展平。不要直接运行这些归档脚本。恢复时按交接在新的临时目录重建`consumer`等布局，手工调整明确的root常量，安装真实包并记录新指纹。旧实验不能续跑或补计时。未跟踪tarball、node_modules、二进制或Bun cache；若原/tmp产物已消失，需从固定源码重建并明确这是新候选。

主仓当前恢复入口为`proposals/extraction-kanban/MODULE-CONSTRUCTION-2026-10-21.md#pause-20261021`。新head远端CI及PR描述更新留待恢复，不自动重新开工。
