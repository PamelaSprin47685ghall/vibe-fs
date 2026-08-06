# LOOP — 目标实现

## LOOP-003：判定指标

### 观测单位

先从字符流中丢弃空白与减号，再在**过滤后的字符流**上生成重叠 4-gram：

```text
IGNORED = { ' ', '\t', '\r', '\n', '-' }

原始：
  "if (x > 0) {\n\treturn x;\n}"

过滤后：
  "if(x>0){returnx;}"

4-gram：
  "if(x" "f(x>" "(x>0" "x>0)" ...
```

其它标点与字符原样进入 4-gram，不做 NFKC / 大小写折叠 / 数字折叠。被忽略字符既不形成 4-gram，也不推进指数衰减。

### 指数混合权重

对已观察到的 4-gram 流，用固定个慢指数核逼近 Zipf 型无限历史：

$$
w(d)=\sum_{j=1}^{K}a_j\lambda_j^{d},\qquad d=0\text{ 为最新 4-gram}
$$

混合权重给出：

$$
Z=\sum_j a_j T_j,\qquad
Q=\sum_{j,k}a_ja_k R_{jk},\qquad
\mathrm{HHI}=\frac{Q}{Z^{2}},\qquad
N_{\mathrm{eff}}=\frac{1}{\mathrm{HHI}}
$$

**物理量是 \(N_{\mathrm{eff}}\)**（inverse Simpson / Hill number of order 2），表示「相当于多少个不同 4-gram」。阈值在 \(N_{\mathrm{eff}}\) 空间取中点，不在 HHI 空间取中点。

### 正常代码先验（无罪推定）

过滤空白后，正常代码的 4-gram 多样性明显高于「空白也计数」时。检测器创建时注入一段虚拟正常代码历史，不构造真实 4-gram，只设定：

```text
T_j = 1 / (1 - λ_j)          // 各指数核稳态总质量
R_jk = HHI_normal · T_j · T_k
HHI_normal = 1 / 256
```

于是初始：

```text
HHI = 1/256
N_eff = 256
state = NORMAL
```

不需要 `MIN_NGRAMS` 预热窗。过滤后不足 4 个有效字符时保持该先验判定。

真实输出进入后，先验按指数核淡出；循环持续足够久，\(N_{\mathrm{eff}}\) 下降越过阈值才强杀。

### 触发

物理量判定：

```text
N_eff <= LOOP_EFFECTIVE_COUNT   → LOOP
否则                            → NORMAL
```

等价 HHI 写法（同一阈值）：

```text
HHI >= LOOP_HHI = 1 / LOOP_EFFECTIVE_COUNT   → LOOP
```

无连续命中、无迟滞。单次越阈即 LOOP。

---

## LOOP-004：固定参数（KISS）

参数是规范常量，不得按模型、角色、上下文长度、自然语言 vs 代码动态改写（CTX-001）。全部按**过滤空白后的代码**估算。

```text
NGRAM_SIZE              = 4
HASH_BUCKETS            = 4096
K                       = 3

IGNORED_CHARACTERS      = { ' ', '\t', '\r', '\n', '-' }

HALF_LIFE               = [8, 64, 512]   // 单位：过滤后 4-gram

LAMBDA = [
  0.9170040432,   // 2^(-1/8)
  0.9892280132,   // 2^(-1/64)
  0.9986471129    // 2^(-1/512)
]

COEF = [
  0.15,
  0.25,
  0.60
]

// 正常代码先验（过滤空白后重估）
NORMAL_EFFECTIVE_COUNT  = 256
NORMAL_HHI              = 1 / 256       // 0.00390625

// 典型垃圾循环基准（约 24 个非空白字符周期）
GARBAGE_EFFECTIVE_COUNT = 24
GARBAGE_HHI             = 1 / 24        // ≈ 0.0416667

// 「一半垃圾」在 N_eff 空间取中点（不是 HHI 中点）
LOOP_EFFECTIVE_COUNT    = (256 + 24) / 2
                        = 140
LOOP_HHI                = 1 / 140       // ≈ 0.007142857
```

一般式：若典型垃圾周期等效 4-gram 数为 \(P\)，则

$$
N_{\mathrm{loop}}=\frac{256+P}{2},\qquad
\mathrm{HHI}_{\mathrm{loop}}=\frac{2}{256+P}
$$

权重：

$$
w(d)=
0.15\lambda_0^{d}+
0.25\lambda_1^{d}+
0.60\lambda_2^{d}
$$

时间尺度（相对权重，单位为过滤后 4-gram）：

```text
最近 64 个 4-gram  ≈ 11%
最近 256 个       ≈ 34%
最近 472 个       ≈ 50%   // 「平均一半是垃圾」的经验尺度
最近 1024 个      ≈ 76%
```

代码中约 25%～35% 字符为被忽略空白，同一组半衰期按原始输出字符计时又略慢于「空白也计数」版本；半衰期本身不再拉长。

---

## LOOP-005：O(1) 递推与固定内存

### 状态

```fsharp
type LoopDetector =
    { mutable Step: int              // 已处理的 4-gram 数
      Prefix: char[]                 // 最近不足 4 或用于滑动的前缀（仅非空白）
      mutable PrefixLength: int
      Value: float[][]               // [HASH_BUCKETS][K]
      LastStep: int[]                // [HASH_BUCKETS]
      Total: float[]                 // [K]，初始 1/(1-λ_j)
      Cross: float[][] }             // [K][K]，初始 HHI_normal·T_j·T_k
```

不保存无限增长的 4-gram Map；固定哈希桶。

### 每字符

```text
if character ∈ { ' ', '\t', '\r', '\n', '-' }:
    return 当前评价（不改状态）

prefix ← append character
if |prefix| < 4:
    return 先验评价（NORMAL, N_eff=256, HHI=1/256）

gram = prefix 中最近 4 字符
bucket = stable_hash(gram) mod HASH_BUCKETS
惰性 materialize(bucket)
用旧向量 old 更新：
  Cross[j][k] ← λj·λk·Cross[j][k] + λj·old[j] + λk·old[k] + 1
  Total[j]    ← λj·Total[j] + 1      // 稳态初始化下近似保持常数
  Value[b][j] ← λj·old[j] + 1
Step ← Step + 1
滑动：丢掉最旧字符，保留后 3 个
return evaluate（N_eff ≤ 140 → LOOP）
```

### 复杂度

```text
时间：每有效字符 O(K²) = O(1)；被忽略字符 O(1) 跳过
内存：O(HASH_BUCKETS · K)，不随流长度增长
```

哈希碰撞使 HHI 略偏高（更敏感）；禁止为「更准」改回无限 Map。

### 生命周期

```text
每个 provider attempt 一个全新 LoopDetector（带代码先验）
强杀、turn 结束、session 删除 → 丢弃
禁止跨 attempt 复用检测器状态
```

---

## LOOP-010：诊断

允许日志字段（HOST-007 / CTX-014）：

```text
session_id
operation = "loop-kill"
effective_character_count   // 此处承载 N_eff（过滤后 4-gram 等效数）
detector_step               // 已处理 4-gram 数
result = armed | aborted | ignored-duplicate | continue-sent | budget-exhausted | abort-failed
duration
provider_error              // 仅 abort 失败原因
```

禁止把完整循环正文写入日志；禁止用 HHI / N_eff 驱动 Fallback 之外的业务分支。

---
