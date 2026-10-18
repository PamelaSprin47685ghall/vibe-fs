# 上一次请求的 token 经济，按 provider 的实际计费：{{cached}} token 命中缓存前缀（便宜），{{uncached}} token 被重新计算（原价）。
# 便宜的那部分就是你在 prefixUntil 之下保住的前缀；其余每回合都要重新付账。调大 prefixUntil 能把更多内容移进这段前缀；既不需要、又没有摘要的段，每一回合都在计费。
