
import { PromptOutcome } from "./Fact.js";

export function toPromptOutcome(sendOutcome) {
    switch (sendOutcome.tag) {
        case 1:
            return new PromptOutcome(1, [sendOutcome.fields[0]]);
        case 2:
            return new PromptOutcome(2, [sendOutcome.fields[0], sendOutcome.fields[1]]);
        case 3:
            return new PromptOutcome(3, [sendOutcome.fields[0]]);
        default:
            return new PromptOutcome(0, [sendOutcome.fields[0]]);
    }
}

