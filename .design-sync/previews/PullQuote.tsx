import { PullQuote } from "frontend-civic";

// The display-serif editorial pull quote, with and without an attribution.
export const WithSource = () => (
  <div style={{ maxWidth: 640 }}>
    <PullQuote
      text="A coalition that spans the spectrum is worth more than a landslide that doesn't."
      source="Civic Arena field guide"
    />
  </div>
);

export const Plain = () => (
  <div style={{ maxWidth: 640 }}>
    <PullQuote text="The arithmetic is settled. The split is yours to argue." />
  </div>
);
