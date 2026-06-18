// Lightweight, day-scoped quest completion stored in localStorage.
//
// There is no quests API yet (see design_handoff_player_home/README §State).
// PlayerHome derives most quest done-states from real signals (today's reasoning
// XP, campaign news count). For quests with no server signal — "Read today's
// briefing" — we record completion locally so the checklist gives immediate,
// honest feedback (the row checks off and progress advances) when the player
// actually does the thing. The "+XP" is the visible reward here; persisting real
// XP server-side is a follow-up once a quests endpoint exists.

export type QuestKey = "briefing-read";

function todayKey(): string {
  // Local calendar day — quests reset at the player's midnight.
  return new Date().toLocaleDateString("en-CA"); // YYYY-MM-DD
}

function storageKey(key: QuestKey): string {
  return `civic:quest:${key}:${todayKey()}`;
}

export function markQuestDone(key: QuestKey): void {
  try {
    localStorage.setItem(storageKey(key), "1");
  } catch {
    // Private-mode / storage-disabled: degrade silently. The quest just won't
    // persist its done-state, which is harmless.
  }
}

export function isQuestDone(key: QuestKey): boolean {
  try {
    return localStorage.getItem(storageKey(key)) === "1";
  } catch {
    return false;
  }
}
