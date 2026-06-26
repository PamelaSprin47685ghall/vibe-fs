module VibeFs.Shell.ReviewReplaySync

open VibeFs.Kernel.ReviewReplayPolicy
open VibeFs.Shell.ReviewRuntime

let syncReviewFromTexts (store: ReviewStore) (sessionID: string) (texts: string seq) : unit =
    reviewTaskFromTexts texts |> syncReviewProjection store sessionID
