# D2固定交付与复审证据

问题、结果和归属见[报告](../../PROCESS-BINARY-D2-2026-10-18.md)；所有归档SHA见`receipt.json`。固定主候选663 SHA`3c0afec4f5aee947cff92a5a2e633a41d4e9728daa515f27295e634ed9b324e0`，固定库0.6.12 SHA`a039ba669299c6919776196fd9b462624bcbe515d03593804596dbc86f43ac56`。`final-product-equality.json`逐项记录2076个包文件与工作树的同一性。

最终暂存态构建664通过；`integration.json`与`integration.log`记录再次核对全部2076个包文件、差异为零。此构建承接候选663的验收，不另计测试次数。

## 最终验收

- `root-verification-663.tgz`：143份原始日志、固定主tar、npm pack清单、安装锁及逐文件manifest。包括首轮沙箱/编译/静态门禁失败、661/662原19红、663正式原19两Host绿、D1相关229/0、独立闭包20/0、真实Host27/0；存储52/0另3TODO导致exit1，不能算全组通过。
- `D2-FINAL-663-INDEPENDENT.tgz`：非作者干净安装2904文件前后不变、Bun原19完整顺序、同一真实F#编译JS双Host、15份原fsi及原编译和5负控。新增编译计数0。
- `d2-raw-final-review-663-evidence-20261019.tgz`：非作者raw11组、树协议10组各双Host；真实Hook、冷读、661→663主模块映射及输入泵静态复审。
- `d2-host-review-663-consumers-20261019.tgz`：真实WSS/Gateway各Host10组、四次确实到达写入前后cut、Hook原生128/stderr及11043安装文件/锁不变。执行者参与原型，此档不算输入泵算法的非作者复审。
- `D2-LIB-0612-CHECKPOINTS.tgz`：非作者6个真实Worker受控状态分支及2个真实早拒收。source事件为受控注入，不能冒称原生OS读错；插入点依赖固定生成代码，不能冒称稳定正式语义测试。
- `d2-library-host-0612-acceptance-compact-20261019.tgz`：精确库22文件、两Host共12个物理场景及清理。参与原型者进行包验收；省略的成功可重生大bytes逐项列size/hash/重生依据，失败未省略。

## 必须保留的反证

- `D2-TYPED-659-BLOCKER-EVIDENCE.tgz`及`d2-worker-input-bridge-evidence-20261019.tgz`：目标tee原件证实旧pipe背压/EOF截断，不是只丢stdout。
- `D2-LAST17-661-RETAINED-RED.tgz`、`D2-LAST17-662-LOW-TRACE.tgz`、`D2-LAST17-662-EOF-READABLE.tgz`：真实对象少尾17、两跳计数及source.read取得仍留Readable缓冲的完整尾17。EOF针按登记故意不forward余量，不算修复验收；0.6.11非重入修正没有解释/解决此红。
- `d2-eventpump-private-prototype-662-20261019.tgz`：原19绿及两组拒收门控；原型缺read期间防重入、目录fd0在Worker初始化失败的误判均保留。后者不算泵原生读错。
- `d2-stopped-capture-baseline-20261018.tgz`：D1固定652停止后少57344字节仍交假完整输出；660修后证据及663字节承接见host660/663档，不覆盖原D1收据。
- native object/ref/tree档：pack不可读与missing同形，拒绝用stderr推断确切不存在；规范化mode不能重建raw size。所有被否证的建议和夹具前提错误单列，不归到产品。

其余660/661、0.6.9/10/11档只证明其固定输入和阶段，不代证最终候选或解释后来的红。两个compact档的原巨大成功字节全档仍在各自README列出的私有路径，SHA保留；长期提交省略只限明确等值可重生的成功数据。

## 重放

先按锁安装并`node scripts/build.mjs`。`node proposals/extraction-kanban/evidence/R6-D2/replay.mjs binary|storage|closure|host`分别运行原正式组；Bun原生用`WXS_TIER_INTEGRATION=1 bun test --timeout 120000 requirements/process-execution/tests/017.test.mjs`，不是Bun正式监督。D1用其修正后的`replay.mjs changed`。所有受限入口需允许观察自有进程；ps EPERM须失败，不能当目标消失。

固定包复验先解档核对manifest、原tar和安装锁，只换明确的安装路径，不用工作树dist代替。私有插入点漂移须停下重新审查，不静默跳过。R12-U1、GAP-151/215、完整Process/存储收敛、未测平台和P2延迟成本不随本证据关闭；没有registry发布或Host部署。
