
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { throwIfCancellationRequested } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { string, field, fromString } from "../fable_modules/Thoth.Json.10.5.1/Decode.fs.js";
import { SessionCommand, SessionCommandResult } from "../Session/Inbox.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { Tool, ToolOutput } from "./ToolContext.js";

export function submitReviewTool() {
    return new Tool("submit_review", "Submit review task result.", "{\"type\":\"object\",\"properties\":{\"report\":{\"type\":\"string\"}},\"required\":[\"report\"]}", (ctx, input) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            throwIfCancellationRequested(ctx.Cancellation);
            let reportText;
            try {
                const matchValue = fromString((path_1, value_1) => field("report", string, path_1, value_1), input.Payload);
                reportText = ((matchValue.tag === 1) ? input.Payload : matchValue.fields[0]);
            }
            catch (matchValue_1) {
                reportText = input.Payload;
            }
            let replyVal = new FSharpResult$2(0, [new SessionCommandResult(2, [])]);
            return builder$0040.Bind(ctx.Session.Request(new SessionCommand(2, [reportText, (r_1) => {
                replyVal = r_1;
            }]), ctx.Cancellation, ctx.Deadline), (_arg) => {
                const res = _arg;
                return (res.tag === 1) ? builder$0040.Return(new ToolOutput(toText(printf("Failed to record review submission: %A"))(res.fields[0]), false)) : ((res.fields[0].tag === 2) ? builder$0040.Return(new ToolOutput("Review submitted and structured fact recorded", false)) : builder$0040.Return(new ToolOutput("Review submitted", false)));
            });
        }));
    });
}

export function returnReviewerTool() {
    return new Tool("return_reviewer", "Return verdict from reviewer.", "{\"type\":\"object\",\"properties\":{\"verdict\":{\"type\":\"string\"}},\"required\":[\"verdict\"]}", (ctx, input) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            throwIfCancellationRequested(ctx.Cancellation);
            let verdictText;
            try {
                const matchValue = fromString((path_1, value_1) => field("verdict", string, path_1, value_1), input.Payload);
                verdictText = ((matchValue.tag === 1) ? input.Payload : matchValue.fields[0]);
            }
            catch (matchValue_1) {
                verdictText = input.Payload;
            }
            let replyVal = new FSharpResult$2(0, [new SessionCommandResult(3, [])]);
            return builder$0040.Bind(ctx.Session.Request(new SessionCommand(3, [verdictText, (r) => {
                replyVal = r;
            }]), ctx.Cancellation, ctx.Deadline), (_arg) => {
                const res = _arg;
                return (res.tag === 1) ? builder$0040.Return(new ToolOutput(toText(printf("Failed to record reviewer verdict: %A"))(res.fields[0]), false)) : ((res.fields[0].tag === 3) ? builder$0040.Return(new ToolOutput(toText(printf("Reviewer verdict returned: %s"))(verdictText), false)) : builder$0040.Return(new ToolOutput(toText(printf("Reviewer verdict returned: %s"))(verdictText), false)));
            });
        }));
    });
}

