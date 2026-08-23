# output-distillation — WHY

## 领域价值与核心矛盾

真实执行输出（日志、测试输出、追踪与转储）往往远超单个 participant horizon 的承载上限。系统必须将大规模物理输出有损但诚实地压缩为足以改变后续判断的 bounded observation。

核心矛盾在于：**超大输出通常意味着异常放大，不能把“输出更大”解释成“值得投入更多模型并发”**。蒸馏必须把资源成本锁死：只观察最近一个固定字节窗口，明确声明更早内容已截断，并用一个 Distiller 提炼该 bounded tail。严禁按文件大小自动拆成 N 个 Distiller，再用 reduce 继续派生更多 Distiller。

## 核心不变量

1. **固定成本**：任意 spool 只允许启动一个 Distiller；输入最多为最近 200 KiB。文件继续变大不得增加 Distiller 数、Blogger 数或 reduce 层级。
2. **截断谦逊**：最近 tail 中未出现错误不代表全局成功；只要更早字节被丢弃，结果必须明确声明观察范围不完整。
3. **诚实压缩与区分性保留**：在 bounded tail 内优先保留错误类型、带行号路径、失败断言、矛盾行与未决状态等能够改变判断的印记，剔除重复进度噪声。
4. **失败保留原始尾部**：唯一 Distiller 失败或不可恢复时，直接返回 bounded raw tail 与失败说明；不得伪造摘要。
5. **Distiller 是叶子 runtime**：Distiller 不拥有 Companion Blogger。蒸馏本身不得再派生持续观察/压缩会话。

## 破坏后果

- **并发风暴**：异常输出越大，自动 map/reduce 派生的 Distiller 越多，并连带创建 Blogger，资源消耗随错误规模正反馈放大。
- **静默吞并失败**：截断边界不可见，调用方把 tail 的沉默误判为整次执行成功。
- **定位断裂**：摘要丢失最近的文件路径与关键报错行，使后续排查失去依据。
