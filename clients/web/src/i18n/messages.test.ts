import { describe, expect, it } from "vitest";
import { messages, messagesWithValues, plainMessages } from "./messages";

/**
 * `messages` merges two objects, so a name in both would silently replace one message
 * with the other: the Habits pillar once showed an explanation's raw ICU text this way.
 */
describe("messages", () => {
  it("never use a name twice", () => {
    const shared = Object.keys(plainMessages).filter((name) => name in messagesWithValues);
    expect(shared).toEqual([]);
  });

  it("have unique ids, since translations are keyed by them", () => {
    const ids = Object.values(messages).map((message) => message.id);
    const repeated = ids.filter((id, index) => ids.indexOf(id) !== index);
    expect(repeated).toEqual([]);
  });
});
