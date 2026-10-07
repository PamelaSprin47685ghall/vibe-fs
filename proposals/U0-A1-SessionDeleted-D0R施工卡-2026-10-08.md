# D0-R：原 shared Store 的删除 cut 与后台拒绝

本卡接续已完成的D0-P/G，不重复原工作前提与合法归档。2026-10-08（新加坡）仅完成源码审计与方案，尚未实现、构建或执行本包，不授业务红或验收。完整审计原件另存archive。

## 先解决真正缺少的前提

原链只有一个`WorkspaceEventStore.SharedEntry.Store`：SpikePlugin原Boot取得Journal，删除时`PluginHostWiring.acquireFinalizationStore`再借同entry；原Scope后台等待Bookkeeper/Capture。已有payload decorator返回另一个端口，却不会改已登记的entry。手传另一个Store给helper只能证明helper，不能证明SessionDeleted。

仅在fs写入/Release抛错也不足：真实`PrepareLive`先生成`Prepared.Cuts`，之后才physical append、Current commit、Release。Casebook rule解码event payload，不读文件payload；合法Capture不能靠损坏后续磁盘字节冒充本次live cut。需让受控坏payload在原唯一Store的PrepareLive之前进入实际请求。

## 装配边界，先定契约再施工

1. 由`WorkspaceEventStore` owner创建原`deferredStore`一次；首次登记时只包装它的Append，登记为唯一entry，不能接纳任意外来Store/factory。默认acquire行为保持，原Scope/Boot/Journal/Casebook仍借同一引用。
2. 已存在entry时明确拒绝，不覆盖、不换指针、不隐式多借一个引用。装配失败不得留下半entry。wrapper构造不激活lazy writer；先以独立有限回归核这些边界，再进入删除场景。
3. 复用`EventStoreSurface.createAppendPayloadStoreWhen`共用体，选择器看真实`EventEnvelope list`；只选matching的singleton `EngineerCaseCaptured`第一个请求。记录matching ordinal与全Store ordinal、原EventId/identity/DTO和实际Append结果；非目标原样转发，mixed batch明确拒绝测试配置。不能硬编码全Store第N次就是Capture。
4. JS只拿不透明能力，不能改编译对象字段。probe只释放自己的一次借用，不循环release他人引用，不另造shared registry、Current或结算状态。opaque handle的Dispose不等于物理Store关闭，不能据此补发last-writer证书。
5. `withExecutablePlugin`先调用createHooks，之后才借fixture的额外Journal；D0-G的beforeCreate在原Boot前可安装entry。先写非空decoy并释放它的独立handle，再安装probe；不能在Boot后替换entry。

预计修改WorkspaceEventStore及其Surface的fs/fsi、EventStore Surface共用体及fs/fsi、必要owner project reference、正式022/support/law登记与状态文档。默认Store/Integrator/PluginBoot/HostSessionDeletion/Casebook/Scope算法不在本包范围。只读ProjectReference遍历未见durable runtime返回workspace owner的反向链（205项目），仍须以实际Fable/架构门禁确认，不把静态遍历当编译通过。

## 原物理故障只能按实际事件武装

沿已有进程局部fs接缝，先forward实际appendFileSync，核自己common-dir内writer的精确Capture EventId；再核实际open/fsync/close、锁路径及owner PID，最后该次Release完成rmSync后抛同一Error。WritePayload也持同锁，不能靠第几个rmSync猜归属。observer只保存真实结果，不在消费者接纳前抛测试断言遮住产品结果。

同一进程核原Error/Prepared/incident引用；跨进程传明确收据与完整DTO，不伪造引用相等。每叶沿D0-G原SDK barrier、实际js-bookkeeper和精确terminal；原完整工作产生draft，不stage/noteAnswer或重绑profile。

## 四叶正式矩阵

| 真实输入/结算 | 必须观察的结果 |
| --- | --- |
| 合法正常 | 一次Capture、Dispose fulfilled、index推进；hot/cold新案，非空decoy/terminal/旧bytes保全。 |
| 合法Release Unknown，空Cuts | 原StoreRelease/cause/Prepared/event保留、零semantic-cut report、零重投。故障在Current已commit之后，hot/cold均应有合法新案；index不能把失败settlement当成功推进。不可套pre-Current故障的hot旧案oracle。 |
| 坏payload，committed cut | 真实bad fact+matching Casebook cut-tail，旧Case/Journal事实保持，目标不进Case Current/index；原owner report一次、原后台失败令同Scope Dispose拒绝原typed incident。 |
| 坏payload，Release Unknown cut | 同上，incident保原CommitUnknown/Prepared/Cuts/cause；不能降为普通Unknown或成功，不能再派Bookkeeper或重投Capture。 |

所有叶先核物理前提再断言业务，独立OS cold核全部canonical byte映射、完整Case、head/heads、非空旧payload及实际bad/cut事实；Casebook ApplyCut保旧投影，不清空Journal。重复同Dispose须复用原拒绝/incident且零新append。已有保护直接绿就登记补证，不能虚构业务红；若实际链失守，先留正式红再精准修真正consumer。

## Fatal、身份及停止边界

原Boot callback会物理kill，之后owner才raise。原fixture已有`WANXIANGSHU_NO_FATAL_EXIT=1`：本包明确沿这项既有抑制，只证原report/typedthrow→RunBackground→Dispose拒绝，不授physical kill、kill等待完整Scope drain或全部phase顺序。无抑制physical child另包，不能相加冒充一条全链。

managed022仍写exact Inspector和五种commitment；现链归档Engineer并有PersistenceFailed，DropSessionIdentity又是no-op。必须先定条款所指取证carrier/历史身份合同；不得恢复Inspector、重建ActiveLogicalRun或造registry。先补拒绝传播也不因此关闭managed022/021、knowledge013、durable closure、private叶回收、public tool或installed Host。

若需换已活跃entry、注入另一Store、坏磁盘后reload冒充live Cuts、重绑Boot owner、按clock arm，或DAG要求复制整套装配，停止并缩小方案；不放大预算、不弱化oracle。一次冻结后按AGENTS跑Fable和对应有限套件，准确列pass/fail/skip/TODO并保全部失败原件。J3仍按自身整包卡施工，不与此接缝半迁移混在一起。
