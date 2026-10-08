# C-R2：同UID非force detach能力收据

这是已授权的私有/tmp OS能力调查，不是production helper业务红绿，也不是N04-C验收。正式新增case源码交root统一native/build/正式运行。

脚本：`/private/tmp/vibe-fs-n04-c-r2-detach-probe-2026-10-06.mjs`。

实际host：macOS26.6.2、build25G83、Darwin25.6.0；Node26.6.0。版本为探针后本机只读命令核对，不推定其他Darwin/Linux host具备相同权限。

1. sandbox首轮hdiutil create exit1，stderr `Device not configured`；未attach、未进detach，cleanupProven=true、私有root已回收。日志`/private/tmp/vibe-fs-n04-c-r2-detach-probe-2026-10-06.log`。此结果只属于准备能力拒绝。
2. 已授权native exec第二轮exit0。日志`/private/tmp/vibe-fs-n04-c-r2-detach-probe-2026-10-06-native.log`。coordinator PID85082、UID501；实际consumer PID85265、UID501先经target mount读取`target image bytes`，关闭文件FD后仍存活。实际独立Node actor PID85269、UID501启动`/usr/bin/hdiutil` PID85270，非force detach exact实际receipt/info所得`/dev/disk4s1`，exit0/signal=null，stdout `disk4 ejected`。随后info确认target image不再attached。
3. 同一consumer释放后经同一绝对路径读到`target hidden original bytes`，同时原mount dev/ino恢复。这证明本host、本actor权限与关闭input FD的生命周期下，readonly namespace可被持续撤下。不能推广为所有持有文件/目录handle或不同授权状态都能detach。
4. 独立foreign UDRO实际`/dev/disk5s1`，image dev/ino `16777231/155259065`、size1800603、mount dev/ino `16777239/2`及`foreign image bytes`保持，直到它自己的owner执行cleanup。actor没有按设备basename猜目标，也未操作其他磁盘。
5. actor/command/consumer三PID均实证ESRCH；两个镜像不再attached、原mount身份恢复、自有root`/private/tmp/vibe-fs-n04-c-r2-detach-probe-QgA2R8`已回收，completed=true/cleanupProven=true。

## 正式小包的有限结论目标

新增正式case仅证明：actual successful consumer与合法output不充分；lost mount的production scope必须拒绝发布，并保留独立foreign device。consumer报告四role实际字节及mount根身份，退出后原scope返回精确root失配/owned mount消失错误。production对此外部干预有意保留image artifacts，fixture只按预记录private parent/root/两artifact身份与hash、actual无attached image证据恢复这些自有资源。

新测试没有新造SDK/Fable receipt；不是原子读取保护、FD/backing/瞬时ABA、unknown attach或foreign同挂载点接管的证明，也不升级为完整C-R2/WHAT016/GAP-055闭合。

## 后续samepoint replacement另包

需要独立actor的identity-owned alternate image，内容含原四role目录名和可辨认新member；target detach后真实attach到同一inputs mountpoint，记录实际foreign image/device/root身份，再由actual Node读到foreign内容。scope必须拒绝，并在返回时foreign仍attached且原inode/bytes保持；actor此后只detach自己设备，再核原四owner恢复。当前正式小包不含此路径。

不得用假plist、替换hdiutil、相同basename或手工改生产view registry来建夹具。unknown attach仍要真实daemon结果/取消切点控制能力，和samepoint replacement分开认领。
