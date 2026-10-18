# 八小时施工与收尾

授权窗口：2026-10-18 14:36:38–22:36:38 UTC（新加坡时间10月19日06:36:38结束）。用户允许继续施工及非作者复审。本文记录本窗口各独立交付，不把窗口结束写成整个修复计划完成。R12-U1继续暂缓。

## 已交付的范围

| 工作包 | 结果、提交与证据 |
| --- | --- |
| D1 / D2 | 主进程结果、停止后输出完整性及真实binary Git接线；`54c7a9266`、`5157c4915`。库`8bb1021 / 0.6.12`修正真实stdin泵的末尾交付。候选663：D1相关229/0、原017两Host各19/0、独立闭包20/0、Host27/0；存储3TODO与P2延迟边界另留。[报告](PROCESS-BINARY-D2-2026-10-18.md) |
| GAP085列明子式 | `4c771cadd`；整目录和未来路径自覆盖、字符类首`]`纠错，正式14文件128/0/0TODO及实际Plugin/冷读通过。完整GAP085仍PARTIAL。[报告](GROUNDING-SCOPE-GAP085-2026-10-19.md) |
| GAP159列明子式 | `c385b8fab`；Predictor私有身份、配置与公开准入一致，保留正常Engineer/Replica。35文件327/0普通fail、15既存TODO；旧原件、确切失败事件cut、真实Host与冷读通过。[报告](PREDICTOR-OFFICE-GAP159-2026-10-19.md) |
| CUT00R1 | `0a7a53418`及B1补修；有限物理等待只归真实TestContext/native生命周期。完整原七文件最终323/0/0TODO，最低Node22.13真实运行和受控跨通道反例通过。Bun正式监督及旧PTY失败取证仍另包。[报告](FINITE-PHYSICAL-WAIT-R1-2026-10-19.md) |
| G140-A | `8b5dd03f7`；持久预算、耗尽与完整身份保留、成功的确切root边界。固定697真实故障/冷读、预算3/15及第12次零发送通过；101文件693/0普通fail、7skip/62TODO。[报告](DURABLE-FAILURE-BUDGET-GAP140-2026-10-19.md) |
| G140-B1 | `3e7d1f53a`；同live Journal non-idle恢复发送2→1，加入者接管真实观察；失败事实仍2。固定706、独立安装/消费者和永久回归通过；父GAP140仍DOING。[报告](SINGLE-JOURNAL-RECOVERY-G140-B1-2026-10-19.md) |

## 修复回退与review误判

各包原始红绿保留在对应归档。本代理的施工问题单列：D2早期stdin尾17丢失；GAP159早期670空reset与673错事件cut释放；R1 endACK/下一begin竞争；B1原型遗漏加入者观察登记、长期夹具读取DU内部形状。上述已在本窗口交付前修正或在后续原子提交补修，不能归到原拆分。

005夹具改写同一用户原件、B1 Physical广播假设、021用新live append造旧历史等属于夹具或review前提问题。尤其“raw历史reader缺失”的表述已撤回：真667写盘包含原事件及实际Journal cut，706在新OS中完整读出，904字节原件及文件集合不变；现改用冻结旧原件测试，并反证新live append仍拒绝旧身份且零写。FactCodec历史类型解码TODO保留，不将raw审计等同活跃准入。

021新增负控的两处测试笔误也已纠正：结构化error不能作字符串匹配，events目录须显式建立后才能断言文件集合为空。后者保留709首红；二者都没有要求修改产品规则。GAP067/085/159过期登记同步已验子式，父项保持PARTIAL。

## 最终验证

- 710正式相关选集：**148文件655通过、0普通失败、6skip、95TODO**；148/148文件完成、owned进程收尾accepted。exit1是待证阻断，不能写成整个套件全绿。更早706的653/1与709的夹具首红原日志保留。
- 710发行包SHA256 `8572c309c3c5e64011e51972a70058c044b5cfd1478a650a460e64f2de4d2be2`，与706连tar字节也相同；2076包文件零增删改。收尾只改测试和记录。
- 三名非作者分别复核D1/D2/库边界、产物与归档一致性、真实旧原件冷读取证。十份B1档共43,639,168字节，外档及1454条文件manifest全部匹配。限定复审没有新增通过数，也不替代各包实际消费者证据。
- 021真实NDJSON已force-add并从暂存树完整导出；独立快照消费固定706安装得到原生3pass/1TODO。原件/测试摘要同一，不以复用安装冒称新npm安装，不重复累计测试数。
- 最初沙箱阻止Unix socket与ps，0/148文件执行、无权威计数；核实自有进程已退后，在授权环境重跑。该次不是产品失败或测试通过。npm pack改用私有临时cache，没有改个人cache权限。

本次旧历史原件、复审原文、测试选集、首红/最终日志、包同一性和干净快照消费记录见[evidence/FINAL-8H-20261019](evidence/FINAL-8H-20261019/README.md)。构建、提交与普通推送分别留证；未发布registry，未部署Host。

## 窗口末尾的前置取证

B2补了一个有效反例：真实EventStore两个writer，A写50、B写60(parent50)后，stale A合法追加10(parent50)，其物理文件只追加、旧字节不变，规范顺序却由`[50,60]`变为`[50,10,60]`。fresh C追加10(parent60)的正控保持`[50,60,10]`。固定706在Node26/Bun1.3.14及非作者原样复跑一致；公开heads额外证明A仍见50、B已见60。它说明已知writer也可能改变canonical前缀，不是merge算法错误。新handle同OS、实际writer流经注册MergeSurface的观察，不冒称业务Current、跨OS或B2原子命令原型验收。没有产品修改，B2继续DOING。

R3限定只读复核确认原首红使用原生`node --test`、返回1且signal null；R1正式监督的5秒窗口和ACK竞争没有同现场因果证据。已知Bun late-not-started失败，未知是缺失的内部断言/物理原因。原日志及同包后续完整124/124证据已长期归档；保留的脚本后来改过选项，明确不把它的当前SHA称为首轮脚本。未跑新PTY测试，R3 READY、C/R7-A REVIEW及R12暂缓均不变。

## 接下来的顺序

1. **G140-B2，P1且较难。** 真跨OS反例已成立：同身份写两条；不同身份各写count1，冷积分只保留last-good1。先做已暖writer的锁内fresh Current与纯决定reference原型，再解决canonical frontier及重排/增量成本；当前ReloadLocal全重放不能直接放进每轮生产命令。随后迁移领域原子命令、生命周期及Unknown结算，再验durable跨OS发送权和缺Host-stop的普通冷Workflow。不得用进程内锁或预算结果冒充跨OS发送许可。
2. **Trace产品链。** CUT03R7 Opening范围→CUT06R5真实消费者→CUT03R8压缩/重锚→CUT03R9真实Fission汇聚；每包只关自己的材料和范围义务。
3. **Bootstrap及物理边界。** CUT04R7真实启动/崩溃恢复；主动脱组先R8-A有限原型，再按肯定结论决定R8-B生产接线；声明平台和容量归R9。
4. **独立P2。** Bun正式verdict监督R2、旧精确PTY失败取证R3、Git changedHook延迟；C/R7-A保持REVIEW。R12-U1暂缓，两个上游接收候选继续只读暂缓。

完整合同、关键反例和验收边界沿用[当前施工计划](REMAINING-REPAIRS-2026-10-16.md#current-plan)，状态沿用[看板](BOARD.md#source-audit-repairs)。本轮没有把上述剩余包标成完成。
