export function PullQuote({
  text,
  source,
}: {
  text: string;
  source?: string;
}) {
  return (
    <figure className="my-10 border-l-4 border-[var(--accent)] py-2 pl-6">
      <blockquote className="display text-2xl leading-tight text-[var(--fg)] md:text-3xl">
        “{text}”
      </blockquote>
      {source && (
        <figcaption className="mt-3 text-xs font-semibold uppercase tracking-wider text-[var(--muted)]">
          — {source}
        </figcaption>
      )}
    </figure>
  );
}
