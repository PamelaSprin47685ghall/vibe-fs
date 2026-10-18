# GAP-085验收证据

问题与归属见[修复报告](../../GROUNDING-SCOPE-GAP085-2026-10-19.md)。固定候选667，包SHA `b0de9b9770139ae4aa977fae3d8b8d59f2d558cf4ff4a41cf4a1289419743297`；2076个包文件与工作树和实际安装逐项一致，较D2仅GlobMatch.js、Catalog.js两文件变化，库0.6.12未变。最终暂存态构建与逐文件复核由`integration.json`记录。

- `root-verification.tgz`：根执行者的固定包、npm清单、安装锁、构建/静态/正式回归原日志、原红及逐文件manifest。13文件124绿是补共享grep反例前的阶段结果；最终14文件结果单列，不能相加冒充独立用例数量。
- `non-author-rules.tgz`：Node/Bun实际Regex对照、目录交集、字符类纠错、真正glob/ignore/grep消费者和666原反例。
- `non-author-policy.tgz`：正式002原body的663红/667绿、具体self witness、实际Runtime与材料读取正反控、两个安装包前后2904文件未变。初次命令和旧材料夹具错误如实保留。
- `non-author-host.tgz`：npm根真实Plugin注册入口，两Host的合法/self+/self-四工具、字符类泄漏红绿、两次真实Plugin incarnation的旧occurrence重放；规范读取观察透传真实FS且有同Host合法正控。
- `character-class-666-red.tgz`：未交付666中实际point匹配与目录查询不一致的原红及固定包，不能用最终绿替换。
- `design-initial.tgz`与`design-domain.tgz`：开工前反例、既有词法路径域、Node/Bun真实差异、被否证的Regex与夹具假设。只是调查，不能代证生产验收。
- `scope-addendum.tgz`：按当前计划逐项澄清卡的边界，保留663/667对反斜杠包名同样漏隐式覆盖的实测；本卡通过不等于完整WHAT002所有合法POSIX名已正确。

全部归档SHA与计数见`receipt.json`。解档后先校验manifest和固定包，再用干净安装重放；不要让裸`import('wanxiangshu')`经主仓helper回到工作树dist。Bun的FS观察方式与Node不同，必须先证实正控，不能把未拦到调用的0解释成未读材料。

本仓重放：先`node scripts/build.mjs`，再`node proposals/extraction-kanban/evidence/GAP-085/replay.mjs`。该入口使用现有正式监督，不改timeout。Bun独立消费不代表正式Bun监督已交付。R12-U1、其他修复包、未测平台以及报告列明的旧glob/异常包名边界不随本包关闭；未发布registry、未部署Host。
