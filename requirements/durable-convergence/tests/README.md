# durable-convergence 的测试与证明范围

WHAT 定义收敛合同；本文件只说明现有证据。先标准构建，再由 `requirements/verification-system/tests/run.mjs` 选本目录编号文件运行；涉及 Hook 子进程的用例需 `WXS_TIER_INTEGRATION=1`。所有远端都是测试临时目录内的普通 bare Git，不访问用户远端。TODO 不算通过。

## 实际执行

- 001/002/006 调用生产 k-way merge。002 生成含逆字典序因果边的 DAG，独立检查事件集合与父子顺序；置换与重复输入比较完整事件，结合律样本限定于独立流。没有跨机器证明，也不能靠源码名证明全体入口使用唯一算法。
- 003 实际建立 Git 对象、传递完整 writer blob、重复合并并比较根 OID，拒绝分叉 writer 及非法 UTF-8。尚未以受控并发和崩溃切点证明本地集合替换原子性；原 lock 名称扫描已撤下。
- 004/005 观察实际 EventStore 的 structural heads：共同父节点的合法分叉不作存储损坏，部分覆盖仍有两个 heads，全部覆盖才唯一。未观察各业务投影的 typed DomainConflict/裁决，不能越级宣称已经证明这些状态。
- 007 以非空 Structural、Strength、Casebook、JsTransaction 事实比较写入后的 Current 与同进程关闭、重开后的 Current。尚不涵盖全部注册 oracle，也不等于独立副本双向同步后的业务收敛。
- 008/009 运行独立 Hook 进程，以两个独立本地仓库和普通 bare 远端双向收敛。008 真正调用 reference-transaction，009 调用 pre-push；随后重开两边验证完整事实和 frontier，重复后根不变。测试没有安装产品服务端，不把 fixture 源码不含某些单词当作远端证据。
- 008 的 ensure 用临时 Git 仓库验证两个 remote 各补 store/heads、保留自定义行的顺序、修复 store-only 状态及重复调用不新增行；这些是配置行为，不证明真实插件的激活时机。
- 009 的受控 Git 对象端口记录实际操作，比较给定与返回的身份，并以非法远端根为反例。删除了固定 WriteTree/ReadObject 调用序列要求；合同不规定这些内部调用的确切次数和排列。
- 010 用透传 Git 观察器记录实际 transport：本地 clean 且 tracking 未变时，即使远端推进也不 fetch/push；新增本地事实后同一观察器确实看到 transport，收敛保留远端事实。payload-read 仍只是生产判别函数的局部矩阵，变动文件读写成本尚待整链证据。
- 010 真正执行安装器和自有 SSH wrapper，保留身份选项、尊重用户 multiplex、迁移旧路径、清除 socket 目录后重建为0700并向受控 SSH 传参。没有真实 SSH server，因此不声称已经观察连接复用。测试产物路径只用于找到当前安装器输出，不增加命名合同；测试自己的 socket 目录在 finally 清理。
- 011 显式注入截止时间，覆盖 TTL 等号与后一毫秒、整条含旧事实的 writer、Journal 活动优先于 mtime、旧远端不复活、manifest v2 缺项/多项/重复/错 OID/非法时间，以及真实删除与新活动重新出现。

## 保留的缺口

GAP-151 记录全体入口一致性、全部业务 Current、激活不修改配置/不后台同步、并发 CAS/原子替换、真实变动成本及 SSH 连接复用的证明缺口。无需扩大合同即可补这些证据；不把缺证自动变成需要用户选择的事项。

GAP-152 单列真正的语义分岔：retained 集合中不存在的 parent 是否直接视为窗口外，还是必须有其过期证据。当前实现采用前者；测试中未暗加新的持久索引要求，也不以此声称任意缺失 parent 都已被识别。
