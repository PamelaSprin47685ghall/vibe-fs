namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

/// GLORY-014/064 + SURFACE-004: the ONLY owner of the Birth and Reawakening
/// narrative texts. Tests read these constants; they must never be spelled
/// again at a call site.
module ManagerNarrative =

    /// GLORY-014: frozen planning tail.
    [<Literal>]
    let PlanningTail =
        "If I want to complete the request above, how should I work?\nHow should I define the final goal?\nYou may call several rounds of tools to investigate and research, but in the end simply output your answer as direct text. Do not perform any actual work. Do not call suicide."

    /// GLORY-064: frozen reawakening prefix.
    [<Literal>]
    let ReawakeningPrefix = "You awaken once more in the distant future."

    /// GLORY-015: the synthetic narrative source identity. Duplicate-injection
    /// detection keys on (sessionId, lifeId, messageId, source), never on text
    /// matching.
    [<Literal>]
    let BirthNarrativeSource = "manager-birth-planning-tail"

    /// GLORY-014: first-Life Birth rewrite.
    ///
    /// USER_TEXT_RAW is not trimmed and not translated; exactly two LF are
    /// appended after the raw text (SURFACE-001/002/003).
    let firstBirth (userTextRaw: string) = userTextRaw + "\n\n" + PlanningTail

    /// GLORY-064: reawakening rewrite after a completed Life.
    let reawakening (userTextRaw: string) =
        ReawakeningPrefix + "\n\n" + userTextRaw + "\n\n" + PlanningTail
