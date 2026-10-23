# 上一次请求的 token 经济，按 provider 的实际计费：{{cached}} token 命中缓存前缀（便宜），{{uncached}} token 被重新计算（原价）。
# prefixUntil 保留编号严格小于它的历史区间，便于继续复用缓存前缀。carry 从前缀之后组织材料；删去不再需要的内容，避免每回合重复付费。
