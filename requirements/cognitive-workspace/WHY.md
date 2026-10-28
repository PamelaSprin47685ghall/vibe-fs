# cognitive-workspace — WHY

`assume` 是「已笃定」的认知承诺点：模型宣布某个判断已定型、可以据此行动。
这个动作要轻——它只写一条事实，不携带画板、待办或压缩状态。把工作判断、
待办 UI、压缩 checkpoint 绑在同一个工具上，会让一次笃定顺带改写待办列表
和上下文边界，调用方无法预测一次 assume 到底改了什么。

所以三件事各归其位：

- `assume` 只负责认知承诺点；
- 编程面 `todowrite-for-<role>` 负责当前待办；
- context-compression 由显式 `resetScope` 宿主工具屏障管理上下文清理。

历史 journal 里的旧 Cognition 事实仍可读，是为了诚实恢复旧数据；fold 对它
停在 no-op，不重新生成任何画板投影。
