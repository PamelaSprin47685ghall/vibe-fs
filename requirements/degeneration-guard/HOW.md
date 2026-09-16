# degeneration-guard — HOW

## 架构与核心机制

### 直接语料包络

- `LoopDetector` 继续使用 `o200k_base` token 与指数衰减加权相异度 $D_t$；状态只有 `Normal | TooRepetitive | TooRandom`。
- half-life 固定为 `256` 个 `o200k_base` token。该值定义 detector 的语言记忆尺度，不从源码物理行长、formatter 或文件布局推导。
- build 每次直接从 repository SSOT 派生：`loopDetectorRepositoryInputFiles`只用`git ls-files`选择tracked filesystem paths，不读取内容；`loadLoopDetectorRepositoryCorpusV1`拒绝root外路径并规范为canonical repository-relative identity，再把每个path交给`createTrackingReaderV1`，以raw bytes记录blob digest后才做fatal UTF-8 decode、generated marker过滤与语料连接。generator不再调用返回text的旁路helper。`writeLoopDetectorEnvelopeArtifact`以同一input rows与exact output bytes生成唯一artifact row，绑定`#wanxiangshu-loop-detector-envelope`、package target、generator/build/selector entry及traversal ID。corpus只接受正常人工可读source/document类型，拒绝vendor/dependency、generated、fixture/golden与结构化数据；多线程编码在安全换行边界（`\n`后紧跟可打印非`/` ASCII字符）分块，保证与整流BPE编码位等价。
- 令 $D_0=X$，一次 token/history replay 递推 $D_t(X)=\lambda^tX+b_t$ 并保存 $b_t$，由 $X=mean(D_t(X))$ 直接解出 self-consistent normal prior，再由前向投影序列 $D_t(X)=\lambda^t X + b_t$ 计算经验分位数（rank $= \lceil p \cdot n \rceil$，index $= \min(n-1, \text{rank}-1)$）：低侧 $p=0.025$ 得 `minimum`，高侧 $p=1.0$ 得 `maximum`。没有任意启动 seed，也没有第二次 token/history replay。生成文件只是 ephemeral runtime import，不是配置。
- 生产判定只比较 `D_t < minimum` 与 `D_t > maximum`。Beta 拟合、连续参数分布与运行期动态阈值全部删除。
- `lastSeen[token]` 是唯一算法 scratch；每 token 更新 $O(1)$，空间受 tokenizer vocabulary 上界约束。

### Sensor-owned interruption + continuation

`HostSignalBootstrap` 通过 `LoopSensor.create` 静态构造并调用 `scope.AttachLoopSensor`，reset／observe／drop 使用 `ILoopSensor` 合同；continuation 资源直接取自 `LoopSensor.continuationPath`。删除动态模块及 global fallback，缺失装配不再静默跳过保护。`host-boundary/tests/loop-sensor-wiring-owner.test.mjs` 经真实插件 fork 创建 managed child、投递订阅事件并观察 SDK abort：旧实现中断记录为空，新实现只中断该 child 一次，root 与 foreign session 均豁免。该回归替换原先仅匹配 source token 的伪证明；完整 reconcile 后 continuation 的细分语义仍由下表 sensor owner 测试证明。

`LoopEventCodec`在独立`loop-event-codec` contract中只消费`host-event-envelope`；`execution-session-loopdetector`因此不再获得完整Host signal、provider terminal或diagnostics实现。`LoopSensor`的诊断能力是Host composition必填注入的窄callback；sensor在自身边界吸收callback异常，诊断成败不改变arm、interrupt、consume或continuation。

1. `LoopSensor.Observe` 只消费 Host text/reasoning delta，并为每个 eligible session 持有 fresh detector。
2. 首次异常把 `DegenerationKind` 写入进程内 armed map，然后调用 `InterruptAttempt`；后续 delta 因 armed 状态被忽略。
3. Host 原有 reconciliation 在 `TurnAborted` 分类点调用 sensor 的 consume operation。该 operation 原子取走 anomaly，并由 sensor 自己调用注入的 continuation port；因此 continuation 的时间位置与旧 abort classification 点一致。
4. continuation port 只负责物理发送：`TooRepetitive`/`TooRandom` 映射到各自 provider-language resource，并以 `PromptAuthority.DegenerationGuard` 发送同一 LogicalRun continuation。
5. consume 返回 typed `AbortCause.DegenerationGuard`；Application/Fission 收到后只 yield/no-op。没有 fallback ledger 写入，没有 nudge/AABB 第二恢复者。
6. abort 或 continuation 物理调用失败只回滚/记录当前 guard 的进程内状态；不得改道另一恢复协议。

## 依赖关系

DEPENDS ON:
- `host-boundary`
- `interaction-authority`
- `dispatch-protocol`
