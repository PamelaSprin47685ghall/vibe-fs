
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { SessionId_$reflection } from "../Kernel/Identity.js";
import { lambda_type, bool_type, record_type, class_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Deadline_$reflection } from "../Process/Deadline.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { throwIfCancellationRequested } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { JsTcs$1__get_Task, JsTcs$1__TrySetResult_2B595, JsTcs$1_$ctor } from "../Process/ProcessPump.js";
import { SessionCommandError, SessionInboxEvent, SessionCommand, SessionCommandResult } from "../Session/Inbox.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";

export class ToolContext extends Record {
    constructor(SessionId, Workspace, Cancellation, Deadline, Session) {
        super();
        this.SessionId = SessionId;
        this.Workspace = Workspace;
        this.Cancellation = Cancellation;
        this.Deadline = Deadline;
        this.Session = Session;
    }
}

export function ToolContext_$reflection() {
    return record_type("Wanxiangshu.Next.Tools.ToolContext", [], ToolContext, () => [["SessionId", SessionId_$reflection()], ["Workspace", string_type], ["Cancellation", class_type("System.Threading.CancellationToken")], ["Deadline", Deadline_$reflection()], ["Session", class_type("Wanxiangshu.Next.Tools.SessionCommandPort")]]);
}

export class ToolInput extends Record {
    constructor(Payload) {
        super();
        this.Payload = Payload;
    }
}

export function ToolInput_$reflection() {
    return record_type("Wanxiangshu.Next.Tools.ToolInput", [], ToolInput, () => [["Payload", string_type]]);
}

export class ToolOutput extends Record {
    constructor(Result, Truncated) {
        super();
        this.Result = Result;
        this.Truncated = Truncated;
    }
}

export function ToolOutput_$reflection() {
    return record_type("Wanxiangshu.Next.Tools.ToolOutput", [], ToolOutput, () => [["Result", string_type], ["Truncated", bool_type]]);
}

export class Tool extends Record {
    constructor(Name, Description, SchemaJson, Execute) {
        super();
        this.Name = Name;
        this.Description = Description;
        this.SchemaJson = SchemaJson;
        this.Execute = Execute;
    }
}

export function Tool_$reflection() {
    return record_type("Wanxiangshu.Next.Tools.Tool", [], Tool, () => [["Name", string_type], ["Description", string_type], ["SchemaJson", string_type], ["Execute", lambda_type(ToolContext_$reflection(), lambda_type(ToolInput_$reflection(), class_type("System.Threading.Tasks.Task`1", [ToolOutput_$reflection()])))]]);
}

export class SessionInboxCommandPort {
    constructor(inbox) {
        this.inbox = inbox;
    }
    Request(command, cancellation, deadline) {
        const _ = this;
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            throwIfCancellationRequested(cancellation);
            const tcs = JsTcs$1_$ctor();
            const cmdWithReply = (command.tag === 1) ? (new SessionCommand(1, [(snap_1) => {
                command.fields[0](snap_1);
                JsTcs$1__TrySetResult_2B595(tcs, new FSharpResult$2(0, [new SessionCommandResult(1, [snap_1])]));
            }])) : ((command.tag === 2) ? (new SessionCommand(2, [command.fields[0], (res_1) => {
                JsTcs$1__TrySetResult_2B595(tcs, res_1);
            }])) : ((command.tag === 3) ? (new SessionCommand(3, [command.fields[0], (res_2) => {
                JsTcs$1__TrySetResult_2B595(tcs, res_2);
            }])) : (new SessionCommand(0, [command.fields[0], (res) => {
                JsTcs$1__TrySetResult_2B595(tcs, res);
            }]))));
            const matchValue = _.inbox.TryPost(new SessionInboxEvent(4, [cmdWithReply]));
            return (matchValue.tag === 0) ? builder$0040.Using(cancellation.register(() => {
                JsTcs$1__TrySetResult_2B595(tcs, new FSharpResult$2(1, [new SessionCommandError(1, ["cancelled"])]));
            }), (_arg) => builder$0040.ReturnFrom(JsTcs$1__get_Task(tcs))) : builder$0040.Return(new FSharpResult$2(1, [new SessionCommandError(0, [])]));
        }));
    }
}

export function SessionInboxCommandPort_$reflection() {
    return class_type("Wanxiangshu.Next.Tools.SessionInboxCommandPort", undefined, SessionInboxCommandPort);
}

export function SessionInboxCommandPort_$ctor_Z4592CC48(inbox) {
    return new SessionInboxCommandPort(inbox);
}

