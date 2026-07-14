import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Landmark, ArrowRight, Compass } from "lucide-react";
import { listBills, type BillSummary } from "@/api/bills";

function statusStyle(status: string): { label: string; cls: string } {
  switch (status) {
    case "Enacted":
      return { label: "Enacted", cls: "bg-emerald-100 text-emerald-700" };
    case "PassedBothChambers":
      return { label: "Passed Congress", cls: "bg-emerald-50 text-emerald-700" };
    case "PassedOneChamber":
      return { label: "Passed one chamber", cls: "bg-amber-100 text-amber-700" };
    case "InCommittee":
      return { label: "In committee", cls: "bg-[var(--bg-elev)] text-[var(--fg-soft)]" };
    case "Failed":
      return { label: "Failed", cls: "bg-rose-100 text-rose-700" };
    default:
      return { label: "Introduced", cls: "bg-[var(--bg-elev)] text-[var(--fg-soft)]" };
  }
}

function fmtDate(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  return d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

function BillRow({ b }: { b: BillSummary }) {
  const st = statusStyle(b.status);
  return (
    <Link
      to={`/bills/${b.id}`}
      className="group block border border-[var(--border)] bg-[var(--bg-elev)] p-5 transition hover:border-[var(--accent)]"
      data-testid={`bill-row-${b.externalId}`}
    >
      <div className="flex items-center justify-between gap-3">
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">{b.identifier}</p>
        <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${st.cls}`}>{st.label}</span>
      </div>
      <h2 className="display mt-2 text-xl leading-snug group-hover:text-[var(--accent)]">
        {b.shortTitle || b.title}
      </h2>
      <p className="mt-2 line-clamp-3 text-sm leading-relaxed text-[var(--fg-soft)]">{b.teaser}</p>
      <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-[var(--muted)]">
        <span>{b.sponsor}{b.party ? ` (${b.party})` : ""}</span>
        {b.latestActionDate && <span>· {fmtDate(b.latestActionDate)}</span>}
        <span className="inline-flex items-center gap-1">
          <Compass className="h-3.5 w-3.5" /> {b.axisCount} value{b.axisCount === 1 ? "" : "s"}
        </span>
        <span className="ml-auto inline-flex items-center gap-1 font-semibold text-[var(--accent)] opacity-0 transition group-hover:opacity-100">
          See how it maps <ArrowRight className="h-3.5 w-3.5" />
        </span>
      </div>
    </Link>
  );
}

export default function MagazineBills() {
  const [bills, setBills] = useState<BillSummary[]>([]);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    void listBills()
      .then(setBills)
      .finally(() => setLoaded(true));
  }, []);

  return (
    <article data-testid="magazine-bills">
      <header className="mb-8">
        <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.3em] text-[var(--accent)]">
          <Landmark className="h-4 w-4" /> Bills &amp; your compass
        </p>
        <h1 className="display mt-2 text-5xl leading-tight">Where does each bill sit on your values?</h1>
        <p className="mt-4 max-w-2xl text-lg leading-relaxed text-[var(--fg-soft)]">
          Real bills in Congress, each mapped onto the value axes that make up your Civic Compass. Open one
          to see the bill at the center and the values around it — signed in, we overlay your own compass.
        </p>
      </header>

      {!loaded ? (
        <p className="py-12 text-sm text-[var(--muted)]" data-testid="bills-loading">
          Loading bills…
        </p>
      ) : bills.length === 0 ? (
        <p className="py-12 text-base text-[var(--muted)]" data-testid="bills-empty">
          No bills have been analyzed yet. Check back once synthesis has run.
        </p>
      ) : (
        <div className="grid gap-4" data-testid="bills-list">
          {bills.map((b) => (
            <BillRow key={b.id} b={b} />
          ))}
        </div>
      )}
    </article>
  );
}
