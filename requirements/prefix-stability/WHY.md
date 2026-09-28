# prefix-stability — WHY

已经呈现过的历史若被重新排序、改写或附上变化的标记，即使大意相同，也会破坏 provider 的前缀缓存和模型所依据的连续上下文。因此需要冻结的是实际呈现的字节，而不只是摘要中的含义。

压缩、重锚和待办检查点有时必须替换历史。用已提交事实划定冷边界，才能解释哪次请求开始采用新前缀；候选尚未成功时不能提前改写过去，提交之后也不能因后续失败撤销过去。

模型看到的材料与内部语义历史用途不同。重试和 guidance 可以影响当前呈现，却不应成为工作事实；精确身份、完整 turn 和统一 digest 让这种分离不至于误删原始内容。低信任工作记录提供连续性，不创造新的人类授权。

## DEPENDS ON

- `provider-projection`
- `context-compression`
- `provider-language`
- `participant-identity`
