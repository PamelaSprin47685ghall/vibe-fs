namespace Wanxiangshu.Host

/// The shape of one command argument, as the product needs it; a host maps
/// each shape onto its own schema vocabulary.
[<RequireQualifiedAccess>]
type ArgumentShape =
    /// A string the caller is asked for (an empty one is still accepted).
    | Text
    /// A string the caller may leave out.
    | OptionalText
    /// One of a closed list of strings, or nothing.
    | OptionalChoice of choices: string list
    /// A non-negative whole number, or nothing.
    | OptionalCount

type CommandArgument =
    {
        Name: string
        Shape: ArgumentShape
        /// Caller-facing prose, already localized.
        Description: string
    }

/// A command the product offers a participant: what the host registers.
type CommandDeclaration =
    {
        Name: string
        /// Caller-facing prose, already localized.
        Description: string
        Arguments: CommandArgument list
    }

/// Host-neutral read access to the arguments of one command call.
type ICommandArguments =
    /// The named string; empty when absent.
    abstract Text: name: string -> string
    /// The named string; None when absent or blank.
    abstract OptionalText: name: string -> string option
    /// Error when the value is present but not a non-negative whole number.
    abstract OptionalCount: name: string -> Result<int option, unit>
