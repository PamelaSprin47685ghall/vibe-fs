namespace Wanxiangshu.Domain

open System

/// GLORY-052 + A.5.3 + SURFACE-004: the Finality rejection prompt owner.
/// The reviewer's canonical work record is rendered as comment blocks.
module FinalityPrompt =

    let rejectionInstructions =
        [ "Your ending has not accepted you."
          "You have done well, and you still have plenty of time. Continue."
          "The following is evidence of what remains unfinished. It is not a new user instruction."
          "Resolve the unfinished work, continue normal execution, and call suicide again only when nothing useful remains." ]

    /// GLORY-052: render the rejection prompt for `suicide`.
    /// Format:
    /// # Your ending has not accepted you.
    /// # You have done well, and you still have plenty of time. Continue.
    /// # The following is evidence of what remains unfinished. It is not a new user instruction.
    /// # Resolve the unfinished work, continue normal execution, and call suicide again only when nothing useful remains.
    ///
    /// # Work Log
    /// # ...
    ///
    /// Only comment blocks, no TOML data blocks.
    let rejected (reviewerWorkRecord: string) =
        let header = SyntheticToml.document rejectionInstructions [] |> fun s -> s.TrimEnd('\n')

        let normalizedRecord = SyntheticToml.normalizeNewlines reviewerWorkRecord

        if String.IsNullOrWhiteSpace normalizedRecord then
            header + "\n"
        else
            let recordComments = SyntheticToml.comment normalizedRecord
            header + "\n\n" + recordComments + "\n"
