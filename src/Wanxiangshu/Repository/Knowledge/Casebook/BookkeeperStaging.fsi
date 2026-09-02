namespace Wanxiangshu.Repository.Knowledge.Casebook

/// Process-local staged Case for one Bookkeeper transaction.
/// The provider sees one atomic question/answer object through js-bookkeeper;
/// no filesystem-shaped Q.md/A.md surface exists here.
module BookkeeperStaging =

    /// Begin a new staged transaction with the given question and answer.
    val beginTransaction: txId: string -> question: string -> answer: string -> unit

    /// Read the staged question and answer for a transaction without removing it.
    val snapshot: txId: string -> Result<string * string, string>

    /// Apply optional question/answer patches to a staged transaction.
    val apply: txId: string -> question: string option -> answer: string option -> Result<unit, string>

    /// Remove and return the staged question and answer for a transaction.
    val take: txId: string -> Result<string * string, string>

    /// Remove a staged transaction without returning its contents.
    val abort: txId: string -> unit
