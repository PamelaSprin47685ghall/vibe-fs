
import { tryFind } from "../fable_modules/fable-library-js.5.6.0/Map.js";
import { singleton, empty } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { TodoFact, Fact, ReviewFact, ReviewVerdict, TodoSnapshot } from "../Kernel/Fact.js";
import { defaultArg } from "../fable_modules/fable-library-js.5.6.0/Option.js";
import { StreamId } from "../Journal/Envelope.js";
import { SessionCommandError, SessionCommandResult } from "./Inbox.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { compare } from "../fable_modules/fable-library-js.5.6.0/String.js";

export function dispatchCommand(gateway, sessionId, cmd) {
    let arg_5, arg_8, arg_2;
    switch (cmd.tag) {
        case 1: {
            const reply_1 = cmd.fields[0];
            const matchValue = tryFind(sessionId, gateway.ProjectionSet.SessionProjections);
            if (matchValue == null) {
                reply_1(new TodoSnapshot(empty()));
            }
            else {
                reply_1(defaultArg(matchValue.Todos, new TodoSnapshot(empty())));
            }
            break;
        }
        case 2: {
            const reply_2 = cmd.fields[1];
            if (((arg_5 = (new Fact(4, [new ReviewFact({
                Round: 1,
                Verdict: new ReviewVerdict(1, [singleton(cmd.fields[0])]),
            })])), gateway.Append(new StreamId(1, [sessionId]), undefined, arg_5))).tag === 0) {
                reply_2(new FSharpResult$2(0, [new SessionCommandResult(2, [])]));
            }
            else {
                reply_2(new FSharpResult$2(1, [new SessionCommandError(0, [])]));
            }
            break;
        }
        case 3: {
            const verdictText = cmd.fields[0];
            const reply_3 = cmd.fields[1];
            if (((arg_8 = (new Fact(4, [new ReviewFact({
                Round: 1,
                Verdict: (compare(verdictText, "Passed", 5) === 0) ? (new ReviewVerdict(0, [])) : (new ReviewVerdict(1, [singleton(verdictText)])),
            })])), gateway.Append(new StreamId(1, [sessionId]), undefined, arg_8))).tag === 0) {
                reply_3(new FSharpResult$2(0, [new SessionCommandResult(3, [])]));
            }
            else {
                reply_3(new FSharpResult$2(1, [new SessionCommandError(0, [])]));
            }
            break;
        }
        default: {
            const reply = cmd.fields[1];
            if (((arg_2 = (new Fact(2, [new TodoFact({
                Snapshot: cmd.fields[0],
            })])), gateway.Append(new StreamId(1, [sessionId]), undefined, arg_2))).tag === 0) {
                reply(new FSharpResult$2(0, [new SessionCommandResult(0, [])]));
            }
            else {
                reply(new FSharpResult$2(1, [new SessionCommandError(0, [])]));
            }
        }
    }
}

