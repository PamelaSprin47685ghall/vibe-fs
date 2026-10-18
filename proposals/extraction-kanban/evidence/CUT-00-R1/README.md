# CUT-00-R1 证据

完整范围、归因和剩余边界见[报告](../../FINITE-PHYSICAL-WAIT-R1-2026-10-19.md)。全部 13 份归档的字节数和 SHA256 见 [archives.json](archives.json)，总计约 1.2 MiB。

| 归档 | 原件及结论 |
| --- | --- |
| [最终689验收](CUT-00-R1-VERIFICATION-689-20261019.tgz) | 16文件源码快照、基线c385补丁及哈希；正式7文件321/0、构建/check、2904发行文件相等；基线两红、684/686/687/688各轮完整日志、原生事件顺序观察、针对性修正及Bun未监督兼容探针。SHA `fe7a83a0ff2b942412814fff94cb19ad024f6e71aec31879afa994cb5ca701d5`。 |
| [最低Node最终验收](cut-r1-689-node2213-evidence-20261019.tgz) | 官方Node22.13真实完整00684/0，原生事件/双summary、源码tar、稳定哈希、非作者结论。SHA `ca02c2cfe659cc84d6272afc91fcf5d3e959fd9a1a9d8575e3bfe77cf86689d7`。 |
| [输出及继承最终复验](CUT-00-R1-OUTPUT-GATED-FINAL-20261019.tgz) | 原首红夹具/断言不改，固定当前源码实际3/3通过；精确fork双路、七种输出、900ms许可及PID/socket/HOME收尾。SHA `52f829bf852c80fb1d9c8812842b3db72d3df22bb5dfd5e3731ada41cf4f1996`。 |
| [Node原生输出调查](R1-HOST-NATIVE-PARSER-BOUNDARY-20261019.tgz) | 实际22.13/26.6内置源码与二进制SHA、合法data分块、stdout回调不足、公开路由及canonicalConsole缓存的红绿对照。初次未coalesce及22缺观察门的结果独立保留，不混为有效分块证明；原型执行标记不是独立次数计数。 |
| [ACK独立反例](CUT-00-R1-ACK-FINAL-20261019.tgz) | 真Unix socket和TestContext的9场景；拒绝/丢失/迟到/错实例callback0，end丢失保留原异常。仅本地timeout调度冻结的晚ACK分支明确单列，原生I/O及monotonic clock真实；全PID/socket收尾。 |
| [施工期边界](R1-HOST-BOUNDARY-CONSTRUCTION-20261019.tgz)、[非作者状态机](CUT-00-R1-NARROW-NONAUTHOR-20261019.tgz) | null/UTF8原红和修后绿、context/dequeue、终止后拒授、普通nested及继承探针；不代最终整卡。 |
| [输出首轮](CUT-00-R1-OUTPUT-REVIEW-FIRST-20261019.tgz) | exact fork输出原红、旧新对照、双缓冲和真实close；旧preload副作用不误归新回退。 |
| [最低版本前置探针](cut-r1-node2213-evidence-20261019.tgz)及684/686/687/688最低Node档 | 原生身份/同名/顺序；68470/0、68679/0、687限定16/0及漏收stderr反例、68882/1的真实sink夹具遗漏。早期EPERM原件保留且不计产品失败；后续绿不覆盖原红。 |

解包最终689档后，`inputs.json` 与 `source.tgz` 对应当次固定输入，`candidate.patch` 基于主仓 c385。主正式命令及完整输出在 `cut-r1-formal-689.log`；先按仓库规定构建，再以同样 TESTS_MJS_FILES 与 NODE_TEST_CONCURRENCY=1 运行正式入口。运行需本机自有进程、PTY、socket及ps权限。直接原生模式、短窗适配器探针和正式入口结果分别记录。

本目录不含新的npm产品包；验证设施只随Git分发。发行字节相等不代替本卡真实入口、反例和非作者复验，也不关闭R2、R3、R12-U1或完整release验证。
