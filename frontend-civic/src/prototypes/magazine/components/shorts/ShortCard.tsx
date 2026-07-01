import type { ShortItem } from "@/lib/shortsFeed";
import { CoalitionShortCard } from "./CoalitionShortCard";
import { ThinkDeeperShortCard } from "./ThinkDeeperShortCard";
import { NewsShortCard } from "./NewsShortCard";
import { BudgetFactShortCard } from "./BudgetFactShortCard";

/** Renders one feed item by kind. Each sub-card fills the snap viewport. */
export function ShortCard({ item }: { item: ShortItem }) {
  switch (item.kind) {
    case "coalition":
      return <CoalitionShortCard provision={item.provision} />;
    case "thinkDeeper":
      return <ThinkDeeperShortCard briefing={item.briefing} />;
    case "news":
      return <NewsShortCard briefing={item.briefing} />;
    case "budget":
      return <BudgetFactShortCard fact={item.fact} />;
  }
}
