# 分发测试及证明范围

WHAT 定义合同。先构建，再通过正式 runner 运行编号文件。完整 artifact 验证入口是 `node scripts/verify-package.mjs`；完整发布入口是 `npm run verify:release`。前者通过也不能代替后者的全部测试/E2E。

- 001/003/004/008/010 检查当前仓库的产物、manifest、资源或注册投影，不是假设已经安装。旧003把目录复制到临时位置并传入 mock.tgz，现已撤下这项“真实包”称谓；独立消费者由实际 pack 验证入口负责。
- 002 在新进程和外部 CWD 读取真实资源、导入仓库产物，证明 CWD 独立；不是独立安装。
- 005 保留 manifest 缺失/损坏/产物陈旧反例和真实清理。full build暂存旧dist，在复制输出至manifest提交之间真实进程exit23后，恢复必须保旧完整字节与manifest；目标非空不足以证明已提交。七类不可用manifest/不完整output identity拒绝与合法提交正控见[24c合并修复](../../../proposals/archive/2026-10-06/Upstream增量与施工续接-2026-10-06.md)。此有限恢复证据不证明clean/incremental字节一致，目录相等不证明loader没有其他fallback。
- 006 实际读取器缺失抛错及存在资源正例。源码字符串扫描不能证明全部 I/O 已收口，改为明确待证。
- 007 的受控步骤记录只证明发布调度顺序；真实合规/破坏归档由同一个归档校验器处理，覆盖缺项、多项、digest错及链接。真实 npm pack/解包/消费者不能由这组合成归档替代。
- 009 保留真实归档流与 pack JSON 解析的结构正反例。

GAP-210记录尚未闭合的完整发布证据。GAP-211记录原[007]“成员仅限dist/resources”与[003]必需manifest及npm根文件之间的文字冲突；当前checker允许package.json/README.md/LICENSE，没有把其便利白名单偷偷变成新合同。历史角色词汇允许用于解码，不等于活跃注册；不使用扫描所有代码禁词作为[010]证明。

2026-09-28 迁移至 upstream `1450f49d`：保留真实 role/tool 注册投影，撤下通过 module 路径猜角色的检查及其旧 Sphinx 特例。路径名称不能证明某模块属于废止角色，新的 sphinx-v2 也不能靠沿用旧路径豁免来处理。上游误提交的 `010.test.mjs:84-` 只是同一段断言的残片，既没有导入也不是可运行测试，已删除；正式 [010] 覆盖及安装场景 TODO 保留。
