module VibeFs.Shell.ReviewReplaySync

open VibeFs.Kernel.ReviewReplayPolicy
open VibeFs.Shell.ReviewRuntime

let replayReviewAlwaysSync (store: ReviewStore) (sessionID: string) (texts: string seq) : unit =
    reviewTaskFromTexts texts |> syncReviewProjection store sessionID

let replayReviewIfStoreEmpty (store: ReviewStore) (sessionID: string) (texts: string seq) : unit =
    if sessionID = "" then ()
    else
        match store.getReviewState sessionID with
        | Some _ -> ()
        | None -> replayReviewAlwaysSync store sessionID texts