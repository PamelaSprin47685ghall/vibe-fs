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

Adapter 是动态世界的海关：字段缺失、MessageOrigin、UserMessageId 和 parentID 在这里收敛为内部 Record/DU。越过边界后，Kernel 不得再次猜 `obj` 字段，也不得把字符串文案当协议状态。

Adapter 是动态世界的海关：所有字段缺失、消息来源、宿主 MessageID 和 parentID 在这里收敛成内部 Record/DU。越过边界后，Kernel 不得再次猜测 `obj` 的字段含义，也不得把字符串文案当协议状态解析。

---

## 二、Hook 分类 [NORMATIVE]

同步变换 · 生命周期 TryPost · 有界 Tool/CommandPort · Dispose 关本侧 Driver。  
TryPost 满：明确错误 + Fail Closed；不静默 drop。

decode **必须** MessageOrigin（Human / PluginGenerated / HostInternal）。  
Phase 0 spike 固化 synthetic 识别。

典型路径：

```
onMessageTransform: decode → pure transform → encode → return
onSessionEvent:    decode/origin → Inbox.TryPost → ack → return
onToolAfter:       decode → CommandPort（若改 Session）→ 有界等待 → encode
```

任何等待未来 Hook 才能完成的动作都不能占住 Driver 锁；FIFO Inbox 会在同一 Driver 等 terminal 时继续消费控制事件。

典型路径：

```
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

- 本进程 RuntimeId 与 Journal 文件 CreateNew  
- 启动时 KISS-04 BootSnapshot（枚举他日志只读前缀）  
- TryPost / 只读 Snapshot  
- 初始 Host turn 终态后激活 **本 Runtime** Session Driver  
- Dispose：Cancel Drivers；flush；**不**写他人 ndjson  

禁止 Service Locator / 动态 Registry / 业务 EventBus / workspace 总锁。

Gateway 启动顺序：生成 RuntimeId；CreateNew 自己的 Journal；读取其他日志固定 Frontier；归并 BootSnapshot；发布只读快照；首轮 Host turn 终态后按需激活本 Runtime Driver。关闭只取消本侧 Driver、等待有界清理并 flush 自己文件，永不接管他人日志。

Gateway 的启动顺序：

1. 生成新的 RuntimeId；
2. `CreateNew` 当前 Runtime Journal 文件；
3. 枚举并读取其他 Runtime 文件的固定 Frontier；
4. 稳定归并，按 Stream 构建 BootSnapshot；
5. 发布只读快照；
6. 仅在首轮 Host turn 终态后为需要执行的 Session 建 Driver。

Gateway 关闭只负责本进程：停止接收新 Hook、取消 Driver、等待有界清理、flush 自己的文件。它没有权限也没有理由“接管”仍存活进程的日志。

---

## 四、Codec

强类型 DTO；UserMessageId/parentID 映射；契约测。

Decode 错误应带 Hook 与字段路径；Encode 不把内部失败伪装成成功宿主对象。原地修改是 Adapter 细节，不得泄漏进 Domain。

Decode 错误应带字段路径和 Hook 类型；Encode 不把内部错误重新伪装成成功的宿主对象。原地修改是宿主适配细节，不得泄漏进 Domain 类型。

---

## 五、错误

Decode 错误结构化；OCE 取消；意外 → 本 Runtime 诊断 + Fail Closed。

---

## 六、旧版

不 import 旧生产。Oracle 仅行为。

---

*KISS-03 终。*
