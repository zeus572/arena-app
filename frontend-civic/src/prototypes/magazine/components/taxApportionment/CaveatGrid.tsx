// "Where the model gets fuzzy" — six caveat cards, content fixed verbatim (§7).
const CAVEATS: { title: string; body: string }[] = [
  {
    title: "Consumption — sales tax is an estimate",
    body: "Assumes a household spends a taxable 40% of income; real spending varies with income, and groceries/services/rent are often exempt.",
  },
  {
    title: "Housing — property tax assumes you own",
    body: "Imputes a home worth a multiple of income; renters pay indirectly through rent, so this overstates the renter burden.",
  },
  {
    title: "Local — city taxes mostly excluded",
    body: "NYC, Yonkers, and many PA/OH municipalities levy their own income taxes on top of the state.",
  },
  {
    title: "Capital — investment income ignored",
    body: "Model treats all income as wages; capital gains face different federal rates, escape FICA, and are taxed differently by states.",
  },
  {
    title: "Behavior — caps and credits simplified",
    body: "SALT cap, child tax credit, EITC, retirement exclusions, homestead exemptions all move real bills; omitted for legibility.",
  },
  {
    title: "Employer FICA — only your half shown",
    body: "Employer's matching 7.65% never appears on the stub; counting it would raise the federal share.",
  },
];

export function CaveatGrid() {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" data-testid="tax-caveats">
      {CAVEATS.map((c) => (
        <div key={c.title} className="border border-[var(--border)] bg-[var(--bg-elev)] p-5">
          <h3 className="display text-base font-semibold">{c.title}</h3>
          <p className="mt-2 text-sm leading-snug text-[var(--fg-soft)]">{c.body}</p>
        </div>
      ))}
    </div>
  );
}
