# N04 C-R0 产物四：逐格结论与停止线

调查时未启动任何mount/能力探针或构建测试；仅C-R1新测试源码完成语法/diff检查，等待主agent冻结后验收。

| 义务 | Darwin | Linux | 下一可验交付 |
| --- | --- | --- | --- |
| 经固定readonly namespace写/新增/删除 | 先前有限真实EROFS证据 | 当前公开入口消费前明确拒绝 | 本轮C-R1 paused actual consumer+四role20次操作原生/正式证明 |
| 内部rename、新symlink创建 | 未取得本轮新证书 | 明确拒绝 | 新用例已有合法writable正控与实际errno oracle，待统一验收 |
| 已有内部symlink替换 | SDK archive具备准入源码；全四prepared-role不支持 | 明确拒绝 | 真实archive owner与合法link库存的独立fixture；C-R1 partial保留 |
| registered consumer drain/原Error-null传播/owner归还 | N04-B/C3先前有限已证 | 未提供readonly scope | 保持原例，不重复计新工作完成 |
| raw backing内容不可改 | 未证；仅dev/ino与格式观察 | 未提供实现 | actual raw权限/内容消费独立探针；UDRO不能单独关项 |
| backing/ancestor持续替换拒绝+foreign保留 | 有拒绝/保留源码分支，未正式黑盒 | 未提供实现 | C-R2由独立actor owner/actual device先证明能力，再验拒绝与真实cleanup |
| 瞬时ABA | 未证 | 未提供实现 | actual changed read+最终同库存反例；选实际可强制OS能力 |
| 原FD/目录句柄绕过 | 未证完整FD闭包；路径复制与原FD不同对象 | 未提供实现 | 实际FD身份/字节证明；不拿原隐藏目录变化伪造snapshot corruption |
| consumer与同UID外部actor mount操作限制 | 未证 | 未提供实现 | actual authorization/namespace能力合同；不足时对应入口消费前拒绝 |
| Node/monitor/Git/native/OS完整执行来源 | 未完整选定/保护 | 未提供实现 | 显式加载/执行闭包；不由四role单工程证明推断 |
| format→package同candidate实际保护 | verify.mjs仍环境launcher+阶段后比较 | 同样未接readonly执行 | N08逐真实阶段接线；N09发布证明另验 |

“明确拒绝”是当前代码实际平台准入事实，不是证明Linux完全没有OS能力。Darwin未证格也不能写成已复现漏洞。新C-R1若green只关闭其四身份owner namespace子义务，不关闭N04-C/C-R1全矩阵、T418/T419、GAP-055或N08。

四份交付物位置：consumption-closure、actor-capabilities、formal-probes、conclusions（均`/private/tmp/vibe-fs-n04-c-r0-…-2026-10-06.md`）。三个前提须先定：受信coordinator/OS边界、consumer及同UID外部actor各有哪些真实能力、能力不足如何在消费前拒绝。不能由调查降格WHAT016或静默回退原可变workspace。
