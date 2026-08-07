namespace Wanxiangshu.Domain

open System

/// GLORY-052 + A.5.3 + SURFACE-004: the Finality rejection prompt owner.
/// The reviewer's canonical work record is rendered as comment blocks.
module FinalityPrompt =

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
    /// # Final Output
    /// # ...
    ///
    /// Only comment blocks, no TOML data blocks.
    let rejected (reviewerWorkRecord: string) =
        let header =
            [ "# Your ending has not accepted you."
              "# You have done well, and you still have plenty of time. Continue."
              "# The following is evidence of what remains unfinished. It is not a new user instruction."
              "# Resolve the unfinished work, continue normal execution, and call suicide again only when nothing useful remains." ]
            |> String.concat "\n"

        let normalizedRecord = SyntheticToml.normalizeNewlines reviewerWorkRecord

        let recordComments =
            if String.IsNullOrWhiteSpace normalizedRecord then
                ""
            else
                normalizedRecord.Split '\n'
                |> Array.map (fun line ->
                    if line.StartsWith "#" then line
                    elif String.IsNullOrWhiteSpace line then "#"
                    else "# " + line)
                |> String.concat "\n"

        if String.IsNullOrWhiteSpace recordComments then
            header + "\n"
        else
            header + "\n\n" + recordComments + "\n"
