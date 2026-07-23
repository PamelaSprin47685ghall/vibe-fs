# OpenCode 宿主桥

承 KISS-02、KISS-Driver、KISS-Tools、**KISS-04**。

Adapter = 唯一见原始 OpenCode `obj` 的边界。decode → TryPost 或同步变换 → encode。`Task` 为异步货币。

---

## 一、位置

```
OpenCode Host
  → Adapter (Origin 分类)
  → 同步 Transform | Inbox.TryPost
  → 本 Runtime Driver
  → 本 Runtime Journal 文件
```

禁止 Hook 长跑 SessionFlow。禁止领域接触宿主 obj。

Adapter 是动态世界的海关：所有字段缺失、消息来源、宿主 MessageID 和 parentID 在这里收敛成内部 Record/DU。越过边界后，Kernel 不得再次猜测 `obj` 的字段含义，也不得把字符串文案当协议状态解析。

---

## 二、Hook 分类 [NORMATIVE]

同步变换 · 生命周期 TryPost · 有界 Tool/CommandPort · Dispose 关本侧 Driver。  
TryPost 满：明确错误 + Fail Closed；不静默 drop。

decode **必须** MessageOrigin（Human / PluginGenerated / HostInternal）。  
Phase 0 spike 固化 synthetic 识别。

典型路径：

```
onMessageTransform:
  decode snapshot/messages → pure transform → encode → return

onSessionEvent:
  decode event/origin → SessionInbox.TryPost → encode ack → return

onToolAfter:
  decode → 若只需工具结果则本地处理；若改 Session 投影则 CommandPort → 有界等待 → encode
```

任何“等待未来 Hook 才能完成”的动作都不能占住当前 Hook 的 Driver 锁；FIFO Inbox 会在同一 Driver 的等待循环中继续消费后续控制事件。

---

## 三、Plugin / Gateway [NORMATIVE]

```
WanxiangshuPlugin(config, gateway)
```

Gateway 职责：

- 生成本进程 `RuntimeId`
- 枚举并捕获其他日志固定 `Frontier`
- 读取固定前缀并稳定归并 `BootSnapshot`
- 通过 `CreateNew` 排他创建本 Runtime Journal 文件（跨平台 `FileShare` 与写排他契约引用 **KISS-04**）
- 写入首条 Fact：`RuntimeStarted`
- 发布只读快照并在首轮 Host turn 终态后开放 Hook 与激活本 Runtime Session Driver
- Dispose：Cancel Drivers；flush；**不**写他人 ndjson  

禁止 Service Locator / 动态 Registry / 业务 EventBus / workspace 总锁。

Gateway 启动顺序 [NORMATIVE]：

1. **生成 RuntimeId**：生成本进程唯一的 `RuntimeId`；
2. **枚举与捕获 Frontier**：只读枚举 `.wanxiangshu-next/runtimes/*.ndjson`，捕获各并发日志的固定字节前沿；
3. **读取归并 BootSnapshot**：读取各源 `[0, ByteLength)` 稳定前缀，按时间序合并为 BootSnapshot；
4. **CreateNew Journal 文件**：通过 `CreateNew` 排他创建属于本 Runtime 的 Journal 文件（跨平台 `FileShare` 与文件排他建文件契约引用 **KISS-04**）；
5. **写入 RuntimeStarted**：日志文件创建完成后，写入首条成功 Fact `RuntimeStarted`；
6. **开放 Hook / 激活 Driver**：发布 BootSnapshot 只读快照，开放 Hook，并在首轮 Host turn 终态后按需激活本 Runtime Session Driver。

Gateway 关闭只负责本进程：停止接收新 Hook、取消 Driver、等待有界清理并 flush 自己的文件。它没有权限也没有理由“接管”仍存活进程的日志。

---

## 四、Codec

强类型 DTO；UserMessageId/parentID 映射；契约测。

Decode 错误应带字段路径和 Hook 类型；Encode 不把内部错误重新伪装成成功的宿主对象。原地修改是宿主适配细节，不得泄漏进 Domain 类型。

---

## 五、错误

Decode 错误结构化；OCE 取消；意外 → 本 Runtime 诊断 + Fail Closed。

---

## 六、旧版

不 import 旧生产。Oracle 仅行为。

---

*KISS-03 终。*
