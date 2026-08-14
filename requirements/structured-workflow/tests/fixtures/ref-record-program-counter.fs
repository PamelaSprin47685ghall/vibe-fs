namespace Fixture

type Cell =
    { State: OtherNamespace.RunState ref
      Return: ReturnInfo option ref
      Handoff: bool ref
      Final: FinalInfo option ref
    }
