# GAP-140 取证

本目录只验 G140-A 列明范围，G140-B 并发命令/发送与实际普通冷 Workflow 仍开放。R12-U1 未纳入。所有压缩档的大小及 SHA256 见 [archives.json](archives.json)。

| 归档 | 内容与适用版本 |
| --- | --- |
| `GAP140-ROOT-VERIFICATION-697-20261019.tgz` | 主代理基线/新合同首红、691–697构建、正式41/75/101文件原件、永久物理夹具首红/绿、源快照/patch、697精确包、实际并发和合法旧root原件。最终101文件693 pass/0普通fail/7skip/62TODO，exit1是部分证据，不是整套通过。 |
| `GAP140-INDEPENDENT-CJT-20261019.tgz` | 非作者695/697干净包、双Host C/J01、limit3/15、四新OS冷读、24/12与28/14真实fsi正负编译及同发射JS消费、合法跨root版本对照。含两次夹具误判原件、新目录真实便携复跑及247文件校验。 |
| `GAP140-HOST-WRITER-AND-WORKFLOW-692-20261019.tgz` | 非作者692 Node/Bun真实三故障/健康/12轮/冷读，初版Bun无效注入和并发两轮红均保留。 |
| `GAP140-HOST-FINAL697-20261019.tgz` | 最终697双Host十个producer/十个cold全通过；2076包文件逐字节核对，692→697实际产品差异及永久夹具复核。 |
| `gap140-692-legacy-word-evidence-20261019.tgz` | 缺Spec且Reason带legacy词的假设被真实Journal否证；当时direct FactCodec版本/限额校验仍缺，版本范围不抹平。 |
| `gap140-695-codec-evidence-20261019.tgz` | 补齐后Node22.13的17个合同/三codec共51观察通过；数字字符串额外假设首轮exit1单列review误判，沿用既有Thoth整数归一化。 |
| `gap140-concurrent-old-evidence-20261019.tgz` | 精确旧680包的raw ledger：顺序1fact、并发2facts、count1；不是Host发送证明。 |
| `gap140-old-consumer-concurrent-evidence-20261019.tgz` | 精确旧680与692相同消费者/脚本的唯一一次对照：facts2/send2/count1，8秒诊断原红、exit1、资源关闭。故并发发送也是修前既存；刻意绕开普通scheduler，不外推公开入口可达性。 |

最终分发包 `candidate-697.tgz` 位于主代理档内，SHA256 `21a3409fe09a626976189b47349122c2e94b405b941d4536a5ad7e837817346f`。包内2076文件的真实安装核对由两份最终非作者档分别保留。库仍为 `8bb1021 / 0.6.12`；未发布registry、未部署Host。

CJT档解压后可执行 `node probes/replay-runtime.mjs /absolute/archive-root /absolute/new-output`，不回退到主仓源码或活动dist。完整Fable编译原件在档内；这个便携运行不会冒充重新编译。其他探针的精确命令、HOME、原文件、PID、未省略清单及脚本在各自README/JSON中。
