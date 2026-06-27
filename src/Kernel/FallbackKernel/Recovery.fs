module Wanxiangshu.Kernel.FallbackKernel.Recovery

open Wanxiangshu.Kernel.FallbackKernel.Types

/// Perfect-square test: n > 0 && floor(sqrt(n))^2 == n.
let isPerfectSquare (n: int) : bool =
    if n <= 0 then false
    else
        let r = int (System.Math.Sqrt(float n))
        r * r = n

/// Starting scan index.
///   isPerfectSquare failureCount → 0  (full-retry reset point)
///   otherwise                    → currentIndex  (resume from where we were)
let scanStartIndex (failureCount: int) (currentIndex: int) : int =
    if isPerfectSquare failureCount then 0 else currentIndex

/// Safe model lookup by zero-based index.
let selectModel (chain: FallbackChain) (index: int) : FallbackModel option =
    chain |> List.tryItem index

/// Update FailureCount after a scan round completes.
///   n' < n  → 0          (found a model earlier in the chain; fresh start)
///   n' > n  → k + 1      (advanced further; bump counter)
///   n' = n  → k          (stayed at the same position; keep current count)
let updateFailureCount (n': int) (n: int) (k: int) : int =
    if n' < n then 0
    elif n' > n then k + 1
    else k
