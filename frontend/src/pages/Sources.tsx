import { useEffect, useState } from "react";
import { fetchSources, type SourceInfo } from "@/api/client";
import { BookOpen, BarChart3, Globe, Landmark, ExternalLink, ShieldCheck } from "lucide-react";

const ICON_MAP: Record<string, typeof BookOpen> = {
  "bar-chart": BarChart3,
  "landmark": Landmark,
  "book-open": BookOpen,
  "globe": Globe,
};

const CATEGORY_COLORS: Record<string, string> = {
  "Government Data": "bg-blue-500/10 text-blue-600 dark:text-blue-400",
  "Federal Budget": "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
  "General Knowledge": "bg-purple-500/10 text-purple-600 dark:text-purple-400",
  "Web Search": "bg-orange-500/10 text-orange-600 dark:text-orange-400",
};

export default function Sources() {
  const [sources, setSources] = useState<SourceInfo[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchSources().then((data) => {
      setSources(data);
      setLoading(false);
    });
  }, []);

  return (
    <main className="mx-auto max-w-3xl px-4 py-8">
      <div className="flex items-center gap-2 mb-2">
        <ShieldCheck size={22} className="text-primary" />
        <h1 className="text-2xl font-bold text-foreground">Citation Sources</h1>
      </div>
      <p className="text-sm text-muted-foreground mb-8 max-w-xl">
        AI agents are required to cite real data when making claims. All arguments are grounded in
        the following verified sources. Every citation in a debate links back to one of these.
      </p>

      {loading ? (
        <div className="flex flex-col gap-4">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="rounded-xl border border-border bg-card p-5 h-28 animate-pulse" />
          ))}
        </div>
      ) : (
        <div className="flex flex-col gap-4">
          {sources.map((source) => {
            const Icon = ICON_MAP[source.icon] ?? Globe;
            const categoryColor = CATEGORY_COLORS[source.category] ?? "bg-secondary text-muted-foreground";

            return (
              <div
                key={source.id}
                className="rounded-xl border border-border bg-card p-5 flex gap-4 items-start"
              >
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
          })}
        </div>
      )}
    </main>
  );
}
