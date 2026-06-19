import { CampaignPostCard } from "frontend-civic";

// A compound campaign-feed post: candidate header with avatar + office line, a
// tone/intensity chip and disclaimer badge, the post body, optional
// "responding to" briefing link and cited source, and a vote/heat-map footer.

const chen = {
  id: "cand-maria-chen",
  slug: "maria-chen",
  name: "Maria Chen",
  office: "Senate",
  state: "CA",
  district: null,
  party: "Democrat",
  isIncumbent: true,
  bio: "Two-term senator focused on housing and infrastructure.",
  archetypeKey: "pragmatic-reformer",
  defaultTone: "Hopeful",
  defaultIntensity: 3,
  avatarBaseUrl: "",
  isFictional: true,
};

const okafor = {
  id: "cand-james-okafor",
  slug: "james-okafor",
  name: "James Okafor",
  office: "House",
  state: "OH",
  district: 11,
  party: "Republican",
  isIncumbent: false,
  bio: "Small-business owner running on tax simplification.",
  archetypeKey: "fiscal-hawk",
  defaultTone: "Stern",
  defaultIntensity: 4,
  avatarBaseUrl: "",
  isFictional: true,
};

// Body with two highlightable fragments. start/end are character offsets into
// `body`, so the heat-map spans line up exactly.
const hopefulBody =
  "The farm bill the Senate just passed renews crop insurance for family growers and keeps food assistance whole. That is what a working majority looks like — and we should be proud of it.";

const hopefulPost = {
  id: "post-chen-farm-bill",
  body: hopefulBody,
  tone: "Hopeful",
  toneLabel: "Hopeful",
  intensity: 3,
  intensityLabel: "Engaged",
  issueTags: ["agriculture", "SNAP"],
  trigger: "briefing",
  triggerBriefingSlug: "senate-passes-farm-bill",
  triggerBriefingHeadline: "Senate Passes the 2026 Farm Bill After Months of Deadlock",
  triggerBriefingSummary:
    "The Senate cleared a five-year farm bill renewing crop insurance and reauthorizing SNAP.",
  triggerPostId: null,
  citedReference: "Congressional Record, June 17, 2026",
  up: 218,
  down: 37,
  createdAt: new Date(Date.now() - 42 * 60 * 1000).toISOString(),
  candidate: chen,
  fragments: [
    {
      id: "frag-1",
      text: "renews crop insurance for family growers",
      start: hopefulBody.indexOf("renews crop insurance for family growers"),
      end:
        hopefulBody.indexOf("renews crop insurance for family growers") +
        "renews crop insurance for family growers".length,
      order: 0,
      up: 64,
      down: 9,
    },
    {
      id: "frag-2",
      text: "what a working majority looks like",
      start: hopefulBody.indexOf("what a working majority looks like"),
      end:
        hopefulBody.indexOf("what a working majority looks like") +
        "what a working majority looks like".length,
      order: 1,
      up: 12,
      down: 41,
    },
  ],
};

// A high-intensity post with no trigger briefing and no fragments — exercises
// the thicker intensity border, the flame icon, and the bare-body path.
const sternPost = {
  id: "post-okafor-spending",
  body:
    "Another trillion in spending and not one honest word about how we pay for it. Ohio families balance a budget every month. Washington should try it.",
  tone: "Stern",
  toneLabel: "Stern",
  intensity: 5,
  intensityLabel: "Fired up",
  issueTags: ["budget", "deficit"],
  trigger: "manual",
  triggerBriefingSlug: null,
  triggerBriefingHeadline: null,
  triggerBriefingSummary: null,
  triggerPostId: null,
  citedReference: null,
  up: 91,
  down: 73,
  createdAt: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString(),
  candidate: okafor,
  fragments: [],
};

export const HopefulResponse = () => (
  <div style={{ maxWidth: 680 }}>
    <CampaignPostCard post={hopefulPost as any} />
  </div>
);

export const SternHighIntensity = () => (
  <div style={{ maxWidth: 680 }}>
    <CampaignPostCard post={sternPost as any} />
  </div>
);
