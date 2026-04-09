// Curated Unsplash photo IDs mapped to common debate topic keywords.
// URLs are permanent direct links — no API key needed.

const TOPIC_PHOTOS: { keywords: string[]; id: string }[] = [
  // Healthcare / Medicine
  { keywords: ["health", "medical", "hospital", "doctor", "drug", "pharma", "medicare", "insurance"],
    id: "photo-1579684385127-1ef15d508118" },
  // Climate / Environment
  { keywords: ["climate", "environment", "carbon", "emission", "warming", "green", "pollution", "sustainability"],
    id: "photo-1470071459604-3b5ec3a7fe05" },
  // Economy / Finance / Tax
  { keywords: ["econom", "tax", "fiscal", "budget", "debt", "inflation", "wage", "income", "wealth", "financ"],
    id: "photo-1611974789855-9c2a0a7236a3" },
  // Education
  { keywords: ["education", "school", "university", "college", "student", "teacher", "curriculum"],
    id: "photo-1503676260728-1c00da094a0b" },
  // Trade / Tariff
  { keywords: ["trade", "tariff", "import", "export", "nafta", "shipping", "manufactur"],
    id: "photo-1578575437130-527eed3abbec" },
  // Immigration / Border
  { keywords: ["immigra", "border", "refugee", "asylum", "migra", "citizenship"],
    id: "photo-1521295121783-8a321d551ad2" },
  // Military / Defense
  { keywords: ["military", "defense", "war", "army", "weapon", "nuclear", "security", "veteran"],
    id: "photo-1547036967-23d11aacaee0" },
  // Technology / AI
  { keywords: ["technolog", "ai", "artificial", "robot", "automat", "digital", "cyber", "surveillance"],
    id: "photo-1518770660439-4636190af475" },
  // Housing / Real Estate
  { keywords: ["housing", "rent", "home", "real estate", "mortgage", "construction", "homeless"],
    id: "photo-1560518883-ce09059eeffa" },
  // Energy / Oil / Solar
  { keywords: ["energy", "solar", "oil", "fossil", "renewable", "wind", "power plant", "electric"],
    id: "photo-1509391366360-2e959784a276" },
  // Space
  { keywords: ["space", "nasa", "rocket", "mars", "coloniz", "asteroid", "satellite"],
    id: "photo-1446776811953-b23d57bd21aa" },
  // Gun / Firearms
  { keywords: ["gun", "firearm", "second amendment", "weapon ban", "shooting"],
    id: "photo-1584483766114-2cea6facdf57" },
  // Justice / Law / Crime
  { keywords: ["justice", "law", "court", "prison", "crime", "police", "rights", "constitution"],
    id: "photo-1589829545856-d10d557cf95f" },
  // Food / Agriculture
  { keywords: ["food", "farm", "agricult", "hunger", "nutrition", "organic", "gmo"],
    id: "photo-1500937386664-56d1dfef3854" },
  // Privacy / Data
  { keywords: ["privacy", "data", "encrypt", "surveillance", "freedom", "censor"],
    id: "photo-1563986768494-4dee2763ff3f" },
  // Voting / Democracy / Election
  { keywords: ["vote", "election", "democrac", "ballot", "campaign", "political"],
    id: "photo-1540910419892-4a36d2c3266c" },
  // Infrastructure / Transport
  { keywords: ["infrastructure", "bridge", "road", "transport", "highway", "transit", "rail"],
    id: "photo-1545558014-8692077e9b5c" },
  // Social Media / Internet
  { keywords: ["social media", "internet", "online platform", "platform", "misinform", "content moderation", "newsfeed"],
    id: "photo-1611162617213-7d7a39e9b1d7" },
  // Genetic / Bioethics
  { keywords: ["genetic", "dna", "crispr", "bioethic", "cloning", "stem cell", "genome"],
    id: "photo-1532187863486-abf9dbad1b69" },
  // UBI / Welfare / Poverty
  { keywords: ["basic income", "ubi", "welfare", "poverty", "unemploy", "social security", "inequality"],
    id: "photo-1532629345422-7515f3d16bb6" },
];

// Fallback photos for topics that don't match any keyword
const FALLBACK_IDS = [
  "photo-1541872703-74c5e44368f9", // capitol building
  "photo-1529107386315-e1a2ed48a620", // debate podium
  "photo-1577415124269-fc1140a69e91", // crowd rally
  "photo-1494172961521-33799ddd43a5", // newspaper
  "photo-1434030216411-0b793f4b4173", // writing/notes
];

export function getTopicImageUrl(topic: string, width = 600, height = 300): string {
  const lower = topic.toLowerCase();

  for (const { keywords, id } of TOPIC_PHOTOS) {
    if (keywords.some((kw) => lower.includes(kw))) {
      return `https://images.unsplash.com/${id}?w=${width}&h=${height}&fit=crop&auto=format&q=75`;
    }
  }

  // Deterministic fallback based on topic string
  let hash = 0;
  for (let i = 0; i < topic.length; i++) hash = topic.charCodeAt(i) + ((hash << 5) - hash);
  const fallback = FALLBACK_IDS[Math.abs(hash) % FALLBACK_IDS.length];
  return `https://images.unsplash.com/${fallback}?w=${width}&h=${height}&fit=crop&auto=format&q=75`;
}
