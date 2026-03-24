import { useEffect, useState } from "react";
import { fetchSources, type SourceInfo } from "@/api/client";
import { BookOpen, BarChart3, Globe, Landmark, ExternalLink, ShieldCheck, Newspaper, Radio } from "lucide-react";

const ICON_MAP: Record<string, typeof BookOpen> = {
  "bar-chart": BarChart3,
  "landmark": Landmark,
  "book-open": BookOpen,
  "globe": Globe,
  "newspaper": Newspaper,
  "radio": Radio,
};

const CATEGORY_COLORS: Record<string, string> = {
  "Government Data": "bg-blue-500/10 text-blue-600 dark:text-blue-400",
  "Federal Budget": "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
  "General Knowledge": "bg-purple-500/10 text-purple-600 dark:text-purple-400",
  "Web Search": "bg-orange-500/10 text-orange-600 dark:text-orange-400",
  "News": "bg-rose-500/10 text-rose-600 dark:text-rose-400",
};

function SourceCard({ source }: { source: SourceInfo }) {
  const Icon = ICON_MAP[source.icon] ?? Globe;
  const categoryColor = CATEGORY_COLORS[source.category] ?? "bg-secondary text-muted-foreground";

  return (
    <div className="rounded-xl border border-border bg-card p-5 flex gap-4 items-start">
      <div className="h-10 w-10 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
        <Icon size={20} className="text-primary" />
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap mb-1">
          <h2 className="text-sm font-semibold text-card-foreground">{source.name}</h2>
          <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${categoryColor}`}>
            {source.category}
          </span>
        </div>
        <p className="text-xs text-muted-foreground leading-relaxed mb-2">
          {source.description}
        </p>
        <a
          href={source.url}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1 text-[11px] text-primary hover:underline font-medium"
        >
          {source.url.replace("https://", "")}
          <ExternalLink size={10} />
        </a>
      </div>
    </div>
  );
}

export default function Sources() {
  const [citations, setCitations] = useState<SourceInfo[]>([]);
  const [news, setNews] = useState<SourceInfo[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchSources().then((data) => {
      setCitations(data.citations);
      setNews(data.news);
      setLoading(false);
    });
  }, []);

  return (
    <main className="mx-auto max-w-3xl px-4 py-8">
      <div className="flex items-center gap-2 mb-2">
        <ShieldCheck size={22} className="text-primary" />
        <h1 className="text-2xl font-bold text-foreground">Sources</h1>
      </div>
      <p className="text-sm text-muted-foreground mb-8 max-w-xl">
        AI agents cite real data in every argument. Debate topics are generated from current news.
        Here are all the sources that power the platform.
      </p>

      {loading ? (
        <div className="flex flex-col gap-4">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="rounded-xl border border-border bg-card p-5 h-28 animate-pulse" />
          ))}
        </div>
      ) : (
        <>
          <h2 className="text-sm font-semibold text-foreground mb-3 flex items-center gap-2">
            <ShieldCheck size={14} className="text-primary" />
            Citation Sources
          </h2>
          <p className="text-xs text-muted-foreground mb-4">
            Agents must cite these sources when making factual claims during debates.
          </p>
          <div className="flex flex-col gap-4 mb-8">
            {citations.map((source) => (
              <SourceCard key={source.id} source={source} />
            ))}
          </div>

          <h2 className="text-sm font-semibold text-foreground mb-3 flex items-center gap-2">
            <Newspaper size={14} className="text-primary" />
            News Sources
          </h2>
          <p className="text-xs text-muted-foreground mb-4">
            Debate topics are generated daily from headlines published by these neutral news outlets.
          </p>
          <div className="flex flex-col gap-4">
            {news.map((source) => (
              <SourceCard key={source.id} source={source} />
            ))}
          </div>
        </>
      )}
    </main>
  );
}
