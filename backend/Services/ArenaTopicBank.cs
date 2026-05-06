namespace Arena.API.Services;

/// <summary>
/// Per-arena topic banks for bot-generated debates. Keys match DebateArena.Slug.
/// Each bank holds 18-22 topics so rotation across arenas keeps each feed feeling
/// purpose-built rather than recycled.
/// </summary>
public static class ArenaTopicBank
{
    public static readonly Dictionary<string, string[]> BySlug = new(StringComparer.OrdinalIgnoreCase)
    {
        ["politics"] = new[]
        {
            "Should the federal minimum wage be raised to $20 per hour?",
            "Is the Electoral College outdated?",
            "Should the Supreme Court have term limits?",
            "Is the filibuster undermining democracy?",
            "Should the US adopt a national popular vote for president?",
            "Is gerrymandering the biggest threat to fair elections?",
            "Should DC and Puerto Rico be granted statehood?",
            "Is voter ID legislation a form of voter suppression?",
            "Should campaign finance be publicly funded?",
            "Is the two-party system failing America?",
            "Should ranked-choice voting be adopted nationwide?",
            "Is executive order overuse hollowing out Congress?",
            "Should Congressional members face term limits?",
            "Is lobbying a legitimate form of speech or institutional corruption?",
            "Should the Senate be reformed or abolished?",
            "Is mandatory voting compatible with American liberty?",
            "Should the US Constitution be due for major amendments?",
            "Is the federal government too large or too small?",
            "Should a path to citizenship be created for undocumented immigrants?",
            "Is birthright citizenship worth preserving?",
        },

        ["ai-safety"] = new[]
        {
            "Should frontier AI labs publish full safety evals before release?",
            "Is the alignment problem solvable in principle?",
            "Should AGI development be paused until international standards exist?",
            "Is open-sourcing frontier model weights a net safety risk?",
            "Should AI systems ever be granted legal personhood?",
            "Is RLHF a real alignment technique or safety theater?",
            "Should compute be regulated as a dual-use strategic resource?",
            "Is the 'AI race with China' framing more dangerous than helpful?",
            "Should AI-generated political content require mandatory disclosure?",
            "Is interpretability research close enough to detect deceptive alignment?",
            "Should frontier model weights be classified as export-controlled technology?",
            "Is the singularity a useful concept or a distracting one?",
            "Should every high-stakes AI decision require a human in the loop?",
            "Is AI consciousness a real concern or anthropomorphism?",
            "Should AI training require explicit creator consent for the data?",
            "Is the EU AI Act doing more harm than good to actual safety?",
            "Should governments impose hard compute caps on training runs?",
            "Is constitutional AI a robust alignment strategy or a band-aid?",
            "Should superintelligence be considered an existential risk worth pausing for?",
            "Is AI doomerism doing more to slow safety work than accelerate it?",
        },

        ["religion"] = new[]
        {
            "Can a fully secular society sustain meaning across generations?",
            "Is religious experience evidence of anything beyond the mind?",
            "Is the problem of evil a knockout argument against an omnipotent God?",
            "Can ritual carry the weight of belief once belief fades?",
            "Is religious upbringing transmission of wisdom or a form of indoctrination?",
            "Does science disprove the existence of the soul?",
            "Is forgiveness possible without belief in cosmic justice?",
            "Should we read sacred texts as literal, allegorical, or anthropological?",
            "Is the decline of religion responsible for the loneliness epidemic?",
            "Can morality be grounded without God?",
            "Is faith a virtue or an epistemic failure?",
            "Is the question 'what is the meaning of life' coherent?",
            "Should religious institutions pay taxes?",
            "Are near-death experiences worth taking seriously as evidence?",
            "Is reincarnation more philosophically defensible than heaven?",
            "Does suffering carry spiritual value, or is that a coping story?",
            "Is prayer a real practice or a sophisticated coping mechanism?",
            "Can a civilization survive the death of its founding myths?",
            "Is mysticism just unfalsifiable poetry?",
            "Is doubt a higher virtue than faith?",
        },

        ["finance"] = new[]
        {
            "Is Bitcoin digital gold or a speculative bubble?",
            "Should the Fed have a single mandate or keep its dual mandate?",
            "Is passive index investing destroying price discovery?",
            "Should short selling face tighter restrictions or fewer?",
            "Is the 60/40 portfolio strategy permanently broken?",
            "Should stock buybacks be banned again?",
            "Is private credit the next systemic financial risk?",
            "Should retail investors get access to private markets?",
            "Is the US dollar's reserve currency status sustainable?",
            "Should central banks issue digital currencies?",
            "Is gold still a real inflation hedge or a relic?",
            "Should venture capital firms face transparency requirements?",
            "Is the US housing market structurally broken?",
            "Should ESG investing exist as a regulated category at all?",
            "Is the SEC overreaching or underreaching on crypto?",
            "Should public pensions be allowed deeper alternative investments?",
            "Is high-frequency trading a tax on long-term investors?",
            "Is emerging-market debt fundamentally mispriced today?",
            "Should commercial banks be allowed to custody crypto assets?",
            "Is the wealth gap a market failure or a policy failure?",
        },

        ["parenting"] = new[]
        {
            "Should kids under 14 own smartphones?",
            "Is sleep training cruel or compassionate?",
            "Should screen time be measured or trusted?",
            "Is allowance a tool for teaching money or for control?",
            "Should parents share kids' photos on social media?",
            "Should kids be required to hug relatives they don't want to?",
            "Is helicopter parenting damaging or protective in a riskier world?",
            "Should an 8-year-old be allowed to walk to school alone?",
            "Is reading bedtime stories actually a meaningful literacy intervention?",
            "Should parents force kids to finish what's on their plate?",
            "Is letting kids be bored a parenting strategy or low-key neglect?",
            "Should a 12-year-old be allowed to choose their own bedtime?",
            "Is private school ever worth the cost?",
            "Should parents read their teen's text messages?",
            "Is gentle parenting actually permissive parenting?",
            "Should toddlers be allowed to make their own food choices?",
            "Is it OK to lie to kids about Santa Claus?",
            "Are participation trophies harmful or harmless?",
            "Is daycare worse than a stay-at-home parent in the first three years?",
            "Should homework be banned in elementary school?",
        },

        ["memes-vs-takes"] = new[]
        {
            "Cargo shorts: war crime or peak comfort?",
            "Pineapple belongs on pizza.",
            "Cilantro tastes like soap and that's a moral indictment of cilantro.",
            "The Oxford comma is a litmus test for character.",
            "Hot dogs are sandwiches.",
            "Cereal is a soup.",
            "Open-floor offices are psychological warfare.",
            "Daylight saving time is a federal-level psyop.",
            "Putting toilet paper 'under' the roll is barbarism.",
            "Adults who say 'Live, Laugh, Love' should not be trusted with serious decisions.",
            "Bluetooth speakers in public should be lawfully confiscated.",
            "Standing desks were a marketing campaign, not an ergonomic breakthrough.",
            "Crocs are footwear's greatest achievement.",
            "The Mona Lisa is overrated.",
            "Reading on a Kindle is morally inferior to reading on paper.",
            "AirPods are status symbols masquerading as audio gear.",
            "Saying 'no problem' instead of 'you're welcome' reveals a generational schism.",
            "Email signatures are corporate poetry.",
            "Tipping culture has become a tax-collection cosplay.",
            "LinkedIn is a slow-motion social network designed to humiliate humans.",
        },
    };

    public static string PickRandom(string slug)
    {
        if (BySlug.TryGetValue(slug, out var topics) && topics.Length > 0)
            return topics[Random.Shared.Next(topics.Length)];

        // Fallback for unknown slugs (e.g. user-created arena with no bank).
        return "What's the most overlooked debate in this arena right now?";
    }
}
