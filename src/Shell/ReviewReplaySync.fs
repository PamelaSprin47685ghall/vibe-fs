module Wanxiangshu.Shell.ReviewReplaySync

open Wanxiangshu.Kernel.ReviewReplayPolicy
open Wanxiangshu.Shell.ReviewRuntime

let syncReviewFromTexts (store: ReviewStore) (sessionID: string) (texts: string seq) : unit =
    reviewTaskFromTexts texts |> syncReviewProjection store sessionID
