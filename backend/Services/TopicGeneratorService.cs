using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

public class TopicGeneratorService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Random _rng = new();

    public TopicGeneratorService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<string> PickRandomTopicAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArenaDbContext>();

        // 1. Prefer highly-upvoted topic proposals (net votes > 0, not yet debated)
        var topProposal = await db.TopicProposals
            .Where(t => t.Status == TopicStatus.Proposed && (t.UpvoteCount - t.DownvoteCount) > 0)
            .OrderByDescending(t => t.UpvoteCount - t.DownvoteCount)
            .FirstOrDefaultAsync();

        if (topProposal is not null)
        {
            topProposal.Status = TopicStatus.Debated;
            topProposal.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return topProposal.Title;
        }

        // 2. Fall back to unused generated topics (news preferred)
        var dbTopic = await db.GeneratedTopics
            .Where(t => !t.Used)
            .OrderByDescending(t => t.Source == "news" ? 1 : 0)
            .ThenBy(_ => EF.Functions.Random())
            .FirstOrDefaultAsync();

        if (dbTopic is not null)
        {
            dbTopic.Used = true;
            await db.SaveChangesAsync();
            return dbTopic.Title;
        }

        // 3. Fall back to static topics
        return StaticTopics[_rng.Next(StaticTopics.Length)];
    }

    // Sync version for backwards compat — picks from static only
    public string PickRandomTopic()
    {
        return StaticTopics[_rng.Next(StaticTopics.Length)];
    }

    public (Guid proponentId, Guid opponentId) PickAgentPair(List<Agent> agents)
    {
        var eligible = agents.Where(a => !a.IsWildcard && !a.IsCommentator).ToList();
        if (eligible.Count < 2)
            throw new InvalidOperationException("Need at least 2 eligible debate agents.");

        var shuffled = eligible.OrderBy(_ => _rng.Next()).ToList();
        return (shuffled[0].Id, shuffled[1].Id);
    }

    public async Task SeedStaticTopicsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArenaDbContext>();

        var existingCount = await db.GeneratedTopics.CountAsync(t => t.Source == "static");
        if (existingCount >= StaticTopics.Length) return;

        var existing = await db.GeneratedTopics
            .Where(t => t.Source == "static")
            .Select(t => t.Title.ToLower())
            .ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        foreach (var topic in StaticTopics)
        {
            if (existingSet.Contains(topic)) continue;
            db.GeneratedTopics.Add(new GeneratedTopic
            {
                Id = Guid.NewGuid(),
                Title = topic,
                Source = "static",
            });
        }

        await db.SaveChangesAsync();
    }

    private static readonly string[] StaticTopics =
    {
        // Economy & Fiscal Policy
        "Should the federal minimum wage be raised to $20 per hour?",
        "Is a flat tax fairer than a progressive tax system?",
        "Should the US adopt a wealth tax on billionaires?",
        "Can universal basic income replace the current welfare system?",
        "Should corporate tax rates be increased to fund infrastructure?",
        "Is the national debt a crisis or a manageable economic tool?",
        "Should the Federal Reserve be audited or reformed?",
        "Is free trade better for American workers than protectionism?",
        "Should student loan debt be forgiven by the government?",
        "Is inflation primarily caused by government spending?",
        "Should there be a cap on CEO-to-worker pay ratios?",
        "Is cryptocurrency a threat or opportunity for the US dollar?",
        "Should Social Security be privatized?",
        "Is the gig economy exploiting workers or empowering them?",
        "Should the US return to the gold standard?",
        "Is rent control an effective housing policy?",
        "Should capital gains be taxed the same as income?",
        "Is economic inequality the biggest threat to democracy?",
        "Should the US implement a value-added tax?",
        "Is trickle-down economics a proven failure?",
        "Should public employee pensions be reformed?",
        "Is the stock market a fair measure of economic health?",
        "Should the US renegotiate all existing trade agreements?",
        "Is automation a greater threat to jobs than immigration?",
        "Should the US create a sovereign wealth fund?",

        // Healthcare
        "Should the US adopt Medicare for All?",
        "Is single-payer healthcare more efficient than private insurance?",
        "Should prescription drug prices be regulated by the government?",
        "Is mental health funding adequate in the United States?",
        "Should the government negotiate drug prices directly?",
        "Is the Affordable Care Act a success or failure?",
        "Should emergency room visits be free for all citizens?",
        "Is the US healthcare system fundamentally broken?",
        "Should vaccines be mandatory for public school attendance?",
        "Is telemedicine the future of healthcare delivery?",
        "Should the FDA approval process be streamlined?",
        "Is obesity a public health crisis requiring government intervention?",
        "Should the US invest more in preventive healthcare?",
        "Is the opioid crisis a law enforcement or public health issue?",
        "Should healthcare workers receive hazard pay permanently?",
        "Is dental care a human right that should be covered by insurance?",
        "Should the US allow drug importation from Canada?",
        "Is the pharmaceutical industry too profitable?",
        "Should end-of-life care decisions be left solely to families?",
        "Is the US prepared for the next pandemic?",

        // Education
        "Should college education be free at public universities?",
        "Is standardized testing an effective measure of student ability?",
        "Should school choice and vouchers be expanded nationwide?",
        "Is homeschooling better than public education?",
        "Should teachers be paid based on performance?",
        "Is the Department of Education necessary?",
        "Should civics education be mandatory in all high schools?",
        "Is critical thinking being adequately taught in schools?",
        "Should trade schools receive equal funding to universities?",
        "Is the student loan system predatory?",
        "Should religious education receive public funding?",
        "Is tenure protecting bad teachers?",
        "Should coding be a required subject in K-12 education?",
        "Is the achievement gap primarily an economic issue?",
        "Should parents have more say in school curricula?",
        "Is online learning as effective as in-person education?",
        "Should the US adopt year-round schooling?",
        "Is higher education becoming less valuable?",
        "Should athletic scholarships be reformed?",
        "Is bilingual education beneficial for all students?",

        // Climate & Environment
        "Should the US rejoin or strengthen the Paris Climate Agreement?",
        "Is nuclear energy essential for meeting climate goals?",
        "Should fossil fuel subsidies be eliminated immediately?",
        "Is a carbon tax the most effective climate policy?",
        "Should the US ban fracking nationwide?",
        "Is the Green New Deal economically feasible?",
        "Should electric vehicle mandates be accelerated?",
        "Is climate change the greatest threat to national security?",
        "Should plastic production be heavily regulated?",
        "Is renewable energy ready to replace fossil fuels entirely?",
        "Should the US invest in direct air carbon capture?",
        "Is factory farming an environmental crisis?",
        "Should water be treated as a public good, not a commodity?",
        "Is geoengineering a viable climate solution?",
        "Should the US prioritize climate adaptation over mitigation?",
        "Is deforestation a greater climate threat than emissions?",
        "Should coastal cities receive federal climate resilience funding?",
        "Is the environmental movement too focused on individual action?",
        "Should nuclear waste storage be a federal responsibility?",
        "Is sustainable agriculture possible at industrial scale?",
        "Should the US lead a global ban on deep-sea mining?",
        "Is the electric grid ready for 100% renewable energy?",
        "Should environmental crimes carry harsher penalties?",
        "Is space colonization a distraction from fixing Earth?",
        "Should the EPA have more enforcement power?",

        // National Security & Defense
        "Should the US reduce its military spending?",
        "Is the US military-industrial complex a threat to democracy?",
        "Should the US maintain military bases worldwide?",
        "Is cyber warfare more dangerous than conventional warfare?",
        "Should the draft be reinstated for national emergencies?",
        "Is NATO still necessary for US security?",
        "Should autonomous weapons be banned by international law?",
        "Is the US spending enough on cybersecurity?",
        "Should the intelligence community be more transparent?",
        "Is terrorism still the top national security threat?",
        "Should the US reduce its nuclear arsenal?",
        "Is the Space Force a necessary branch of the military?",
        "Should the US continue military aid to allied nations?",
        "Is digital surveillance justified for national security?",
        "Should veterans receive more comprehensive benefits?",
        "Is the US prepared for a conflict with China?",
        "Should the defense budget be subject to annual audits?",
        "Is the US over-reliant on military solutions to global problems?",
        "Should private military contractors be banned?",
        "Is the US border a national security issue?",

        // Immigration
        "Should the US create a path to citizenship for undocumented immigrants?",
        "Is a border wall an effective immigration policy?",
        "Should the US increase legal immigration quotas?",
        "Is sanctuary city policy good for public safety?",
        "Should DACA be made permanent through legislation?",
        "Is immigration a net positive for the US economy?",
        "Should asylum seekers be processed at the border or abroad?",
        "Is the H-1B visa program helping or hurting American workers?",
        "Should birthright citizenship be reconsidered?",
        "Is the US immigration court system fundamentally broken?",
        "Should employers face harsher penalties for hiring undocumented workers?",
        "Is family-based immigration the right approach?",
        "Should the US accept more climate refugees?",
        "Is immigration enforcement a federal or state responsibility?",
        "Should English be the official language of the United States?",
        "Is the diversity visa lottery a fair system?",
        "Should the US adopt a points-based immigration system?",
        "Is deportation an effective immigration deterrent?",
        "Should undocumented immigrants have access to public services?",
        "Is the US doing enough to combat human trafficking at the border?",

        // Criminal Justice
        "Should the death penalty be abolished nationwide?",
        "Is mass incarceration a form of systemic racism?",
        "Should marijuana be legalized at the federal level?",
        "Is police reform more effective than defunding?",
        "Should cash bail be eliminated?",
        "Is the prison system focused enough on rehabilitation?",
        "Should felons have their voting rights restored?",
        "Is qualified immunity protecting bad police officers?",
        "Should the US invest more in community-based policing?",
        "Is the war on drugs a failed policy?",
        "Should private prisons be banned?",
        "Is restorative justice a viable alternative to incarceration?",
        "Should all police officers wear body cameras?",
        "Is the juvenile justice system too punitive?",
        "Should non-violent drug offenders be released from prison?",
        "Is the three-strikes law just or excessive?",
        "Should mental health professionals respond to crisis calls instead of police?",
        "Is the US court system biased against minorities?",
        "Should solitary confinement be banned?",
        "Is gun violence a criminal justice issue or a public health issue?",

        // Technology & AI
        "Should AI systems be heavily regulated by governments?",
        "Is social media doing more harm than good to society?",
        "Should tech companies be broken up as monopolies?",
        "Is data privacy a fundamental human right?",
        "Should facial recognition technology be banned in public spaces?",
        "Is AI-generated content a threat to democracy?",
        "Should social media companies be liable for user content?",
        "Is the digital divide a civil rights issue?",
        "Should children be banned from social media?",
        "Is open-source AI safer than proprietary AI?",
        "Should the government regulate algorithmic decision-making?",
        "Is remote work better for productivity than office work?",
        "Should deepfakes be criminalized?",
        "Is the right to repair a consumer rights issue?",
        "Should the US create a federal data privacy law?",
        "Is quantum computing a national security priority?",
        "Should autonomous vehicles be allowed on all public roads?",
        "Is the metaverse a technological revolution or a fad?",
        "Should AI be used in judicial sentencing?",
        "Is intellectual property law stifling innovation?",
        "Should the government fund AI safety research?",
        "Is Big Tech censorship a free speech issue?",
        "Should algorithms be subject to public audit?",
        "Is the US falling behind China in AI development?",
        "Should there be a global AI governance body?",

        // Foreign Policy
        "Should the US prioritize diplomacy over military intervention?",
        "Is the US doing enough to counter China's global influence?",
        "Should the US reduce foreign aid spending?",
        "Is the United Nations effective at maintaining global peace?",
        "Should the US support regime change in authoritarian countries?",
        "Is the US-Israel relationship beneficial for Middle East peace?",
        "Should the US engage more with Africa economically?",
        "Is American exceptionalism a valid foreign policy framework?",
        "Should the US lead on global climate negotiations?",
        "Is the US too involved in European security?",
        "Should trade sanctions be the primary tool against adversaries?",
        "Is the US losing its global leadership position?",
        "Should the US recognize Taiwan as an independent nation?",
        "Is humanitarian intervention ever justified?",
        "Should the US close Guantanamo Bay detention center?",
        "Is the US-Mexico relationship being managed effectively?",
        "Should the US rethink its approach to North Korea?",
        "Is multilateralism more effective than unilateral action?",
        "Should the US invest more in soft power diplomacy?",
        "Is the current world order sustainable for the next decade?",

        // Civil Rights & Social Issues
        "Should affirmative action be used in college admissions?",
        "Is systemic racism a real phenomenon in modern America?",
        "Should the Second Amendment be reinterpreted for modern times?",
        "Is freedom of speech being threatened on college campuses?",
        "Should reparations be paid to descendants of enslaved people?",
        "Is cancel culture a threat to free expression?",
        "Should transgender athletes compete in their identified gender category?",
        "Is the gender pay gap primarily due to discrimination?",
        "Should hate speech laws be strengthened?",
        "Is religious freedom being used to justify discrimination?",
        "Should the voting age be lowered to 16?",
        "Is the Electoral College outdated?",
        "Should gerrymandering be banned through federal legislation?",
        "Is voter ID legislation a form of voter suppression?",
        "Should the Supreme Court have term limits?",
        "Is the filibuster undermining democracy?",
        "Should statehood be granted to Washington DC and Puerto Rico?",
        "Is ranked-choice voting better than the current system?",
        "Should campaign finance be publicly funded?",
        "Is the two-party system failing America?",

        // Housing & Urban Policy
        "Should the US build more public housing?",
        "Is zoning reform the key to solving the housing crisis?",
        "Should rent control be expanded nationwide?",
        "Is homeownership still the American Dream?",
        "Should the US invest more in public transportation?",
        "Is suburbanization bad for the environment and society?",
        "Should homeless encampments be allowed on public land?",
        "Is gentrification a net positive or negative for communities?",
        "Should the mortgage interest deduction be eliminated?",
        "Is the US building enough affordable housing?",
        "Should cities ban single-family zoning?",
        "Is NIMBYism the biggest obstacle to housing reform?",
        "Should the US create a federal housing guarantee?",
        "Is mixed-income housing the solution to segregation?",
        "Should the government subsidize first-time homebuyers?",

        // Labor & Workers' Rights
        "Should the US strengthen union protections?",
        "Is the four-day work week viable for the American economy?",
        "Should gig workers be classified as employees?",
        "Is the right to strike being eroded in America?",
        "Should the US mandate paid family leave?",
        "Is workplace surveillance by employers acceptable?",
        "Should tipped minimum wage be abolished?",
        "Is the labor shortage a wage problem or a workforce problem?",
        "Should non-compete agreements be banned?",
        "Is organized labor still relevant in the 21st century?",

        // Media & Information
        "Should the government regulate misinformation online?",
        "Is local journalism dying, and should the government save it?",
        "Should political advertising be banned on social media?",
        "Is the media too biased to be trusted?",
        "Should public broadcasting receive more federal funding?",
        "Is the First Amendment adequate for the digital age?",
        "Should news organizations be required to disclose funding sources?",
        "Is media consolidation bad for democracy?",
        "Should the government fund media literacy education?",
        "Is the 24-hour news cycle harming public discourse?",

        // Science & Ethics
        "Should genetic engineering of humans be permitted?",
        "Is gain-of-function research too dangerous to continue?",
        "Should the US invest more in space exploration?",
        "Is animal testing still justifiable for medical research?",
        "Should human cloning be banned globally?",
        "Is life extension technology an ethical pursuit?",
        "Should the US lead on global pandemic preparedness?",
        "Is scientific consensus being undermined by politics?",
        "Should embryonic stem cell research be federally funded?",
        "Is the precautionary principle slowing scientific progress?",

        // Agriculture & Food
        "Should the US subsidize organic farming over conventional?",
        "Is the farm bill benefiting large corporations over small farmers?",
        "Should SNAP benefits be expanded or reformed?",
        "Is lab-grown meat the future of food production?",
        "Should the US ban certain pesticides used in agriculture?",
        "Is food insecurity a solvable problem in America?",
        "Should school lunch programs be universal and free?",
        "Is the US food system contributing to the obesity epidemic?",
        "Should the government regulate food advertising to children?",
        "Is regenerative agriculture scalable enough to feed the nation?",

        // Energy
        "Should the US build more nuclear power plants?",
        "Is energy independence achievable through renewables alone?",
        "Should the US export more natural gas to allies?",
        "Is the Keystone XL pipeline cancellation good policy?",
        "Should the US invest in fusion energy research?",
        "Is the electric grid infrastructure ready for climate change?",
        "Should offshore drilling be permanently banned?",
        "Is hydrogen fuel a viable alternative to batteries?",
        "Should utility companies be publicly owned?",
        "Is the US transitioning to clean energy fast enough?",

        // Governance & Democracy
        "Should the US adopt a parliamentary system of government?",
        "Is executive order overuse undermining Congressional authority?",
        "Should the Senate be reformed or abolished?",
        "Is federalism still the best system for the United States?",
        "Should there be mandatory voting in US elections?",
        "Is lobbying a form of corruption?",
        "Should term limits apply to all members of Congress?",
        "Is the US Constitution due for major amendments?",
        "Should the US adopt a national popular vote for president?",
        "Is government too big or too small in America?",
    };
}
