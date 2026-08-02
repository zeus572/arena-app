/**
 * A static library of quotes from public figures, past and recent, on the themes this
 * product is about: self-government, disagreement, evidence, and the duty of paying
 * attention. Used in three places — the home feature rotator, the Shorts feed, and a
 * quote-of-the-day line in the footer.
 *
 * WHY THIS IS BUNDLED RATHER THAN FETCHED
 * Every other editorial source in this app (briefings, budget facts, quizzes, daily
 * games) comes from `backend-civic`. Quotes deliberately don't:
 *  - the footer renders on every page, and a network round-trip for one line of
 *    chrome is a bad trade;
 *  - `FeatureRotator` defers its random-start pick until every async source settles,
 *    so a fetched pool would mean one more load flag gating first paint;
 *  - the library is inert. It has no per-user state, no moderation queue, and it
 *    changes when someone edits this file, not when the world changes.
 * The precedent is `taxModel/engine/stateProfiles.ts`, which is bundled for the same
 * reason. Move this behind an endpoint only if quotes ever need to be edited without
 * a deploy.
 *
 * ON ACCURACY
 * Famous quotations are the single most misattributed category of text on the
 * internet, and a civics product that puts words in a real person's mouth has done
 * the exact harm it exists to prevent. So every entry carries a `source` naming the
 * speech, opinion, letter, or book it comes from — not to display prominently, but so
 * any line here can be checked against a primary document. Lines that circulate widely
 * but trace to no primary source (the "eternal vigilance" Jefferson, the "triumph of
 * evil" Burke, the Voltaire that is actually Evelyn Beatrice Hall) are deliberately
 * absent. When adding a quote: find the document first, then add the quote.
 *
 * ON BALANCE
 * The library spans the political spectrum on purpose — Eisenhower and Humphrey,
 * Buckley and Baldwin, Scalia and Ginsburg. A reader who can tell which way this app
 * leans from its quote rotation is a reader we've failed. Keep it that way.
 */

export type QuoteTheme =
  | "Self-government"
  | "Liberty"
  | "Justice"
  | "Truth"
  | "Dissent"
  | "Disagreement"
  | "Power"
  | "Participation"
  | "Common ground"
  | "Courage"
  | "Hard choices";

export type CivicQuote = {
  /** Stable slug — safe to use as a React key or in a URL. */
  id: string;
  text: string;
  speaker: string;
  /** Who they were when they said it, which is usually the part that gives it weight. */
  context: string;
  /** Display string, not a number — the library reaches back before year 1. */
  year: string;
  /** The primary document this can be checked against. See "ON ACCURACY" above. */
  source: string;
  theme: QuoteTheme;
};

export const CIVIC_QUOTES: CivicQuote[] = [
  // ── Founding era ────────────────────────────────────────────────────────────
  {
    id: "franklin-republic-1787",
    text: "A republic, if you can keep it.",
    speaker: "Benjamin Franklin",
    context: "Delegate to the Constitutional Convention",
    year: "1787",
    source: "Recorded in the diary of James McHenry",
    theme: "Self-government",
  },
  {
    id: "madison-angels-1788",
    text: "If men were angels, no government would be necessary.",
    speaker: "James Madison",
    context: "Writing as Publius",
    year: "1788",
    source: "The Federalist No. 51",
    theme: "Power",
  },
  {
    id: "madison-popular-information-1822",
    text: "A popular Government, without popular information, or the means of acquiring it, is but a Prologue to a Farce or a Tragedy.",
    speaker: "James Madison",
    context: "Fourth President of the United States",
    year: "1822",
    source: "Letter to W. T. Barry",
    theme: "Truth",
  },
  {
    id: "madison-abuse-of-power-1788",
    text: "Liberty may be endangered by the abuse of liberty, but also by the abuse of power.",
    speaker: "James Madison",
    context: "Writing as Publius",
    year: "1788",
    source: "The Federalist No. 63",
    theme: "Liberty",
  },
  {
    id: "jefferson-inform-discretion-1820",
    text: "I know no safe depository of the ultimate powers of the society but the people themselves; and if we think them not enlightened enough to exercise their control with a wholesome discretion, the remedy is not to take it from them, but to inform their discretion by education.",
    speaker: "Thomas Jefferson",
    context: "Third President of the United States",
    year: "1820",
    source: "Letter to William Charles Jarvis",
    theme: "Self-government",
  },
  {
    id: "jefferson-difference-of-opinion-1815",
    text: "Difference of opinion leads to inquiry, and inquiry to truth.",
    speaker: "Thomas Jefferson",
    context: "Third President of the United States",
    year: "1815",
    source: "Letter to P. H. Wendover",
    theme: "Disagreement",
  },
  {
    id: "adams-facts-stubborn-1770",
    text: "Facts are stubborn things; and whatever may be our wishes, our inclinations, or the dictates of our passions, they cannot alter the state of facts and evidence.",
    speaker: "John Adams",
    context: "Defense counsel at the Boston Massacre trial",
    year: "1770",
    source: "Closing argument, Rex v. Wemms",
    theme: "Truth",
  },
  {
    id: "abigail-adams-remember-the-ladies-1776",
    text: "Remember the ladies, and be more generous and favorable to them than your ancestors. Do not put such unlimited power into the hands of the husbands.",
    speaker: "Abigail Adams",
    context: "Writing to her husband at the Continental Congress",
    year: "1776",
    source: "Letter to John Adams",
    theme: "Justice",
  },
  {
    id: "washington-faction-1796",
    text: "The alternate domination of one faction over another, sharpened by the spirit of revenge natural to party dissension, is itself a frightful despotism.",
    speaker: "George Washington",
    context: "First President of the United States",
    year: "1796",
    source: "Farewell Address",
    theme: "Power",
  },
  {
    id: "paine-guard-your-enemy-1795",
    text: "He that would make his own liberty secure, must guard even his enemy from oppression; for if he violates this duty, he establishes a precedent that will reach to himself.",
    speaker: "Thomas Paine",
    context: "Pamphleteer of the American Revolution",
    year: "1795",
    source: "Dissertation on First Principles of Government",
    theme: "Liberty",
  },
  {
    id: "franklin-liberty-safety-1755",
    text: "Those who would give up essential Liberty, to purchase a little temporary Safety, deserve neither Liberty nor Safety.",
    speaker: "Benjamin Franklin",
    context: "Writing for the Pennsylvania Assembly",
    year: "1755",
    source: "Reply to the Governor",
    theme: "Liberty",
  },
  {
    id: "hamilton-people-govern-1788",
    text: "Here, sir, the people govern.",
    speaker: "Alexander Hamilton",
    context: "Delegate to the New York ratifying convention",
    year: "1788",
    source: "Speech on the proposed Constitution",
    theme: "Self-government",
  },
  {
    id: "burke-representative-judgment-1774",
    text: "Your representative owes you, not his industry only, but his judgment; and he betrays instead of serving you if he sacrifices it to your opinion.",
    speaker: "Edmund Burke",
    context: "Member of Parliament for Bristol",
    year: "1774",
    source: "Speech to the Electors of Bristol",
    theme: "Self-government",
  },

  // ── Classical ───────────────────────────────────────────────────────────────
  {
    id: "pericles-no-business-here",
    text: "We do not say that a man who takes no interest in politics is a man who minds his own business; we say that he has no business here at all.",
    speaker: "Pericles",
    context: "Athenian statesman, as recorded by Thucydides",
    year: "c. 431 BC",
    source: "Funeral Oration, History of the Peloponnesian War",
    theme: "Participation",
  },
  {
    id: "aristotle-political-animal",
    text: "Man is by nature a political animal.",
    speaker: "Aristotle",
    context: "Greek philosopher",
    year: "c. 350 BC",
    source: "Politics, Book I",
    theme: "Participation",
  },
  {
    id: "cicero-welfare-of-the-people",
    text: "The welfare of the people shall be the highest law.",
    speaker: "Cicero",
    context: "Roman senator and orator",
    year: "c. 51 BC",
    source: "De Legibus, Book III",
    theme: "Justice",
  },

  // ── The long nineteenth century ─────────────────────────────────────────────
  {
    id: "lincoln-house-divided-1858",
    text: "A house divided against itself cannot stand.",
    speaker: "Abraham Lincoln",
    context: "Candidate for the U.S. Senate from Illinois",
    year: "1858",
    source: "Speech to the Illinois Republican State Convention",
    theme: "Common ground",
  },
  {
    id: "lincoln-public-sentiment-1858",
    text: "Public sentiment is everything. With public sentiment, nothing can fail; without it nothing can succeed.",
    speaker: "Abraham Lincoln",
    context: "Candidate for the U.S. Senate from Illinois",
    year: "1858",
    source: "First Lincoln–Douglas debate, Ottawa, Illinois",
    theme: "Participation",
  },
  {
    id: "lincoln-malice-toward-none-1865",
    text: "With malice toward none, with charity for all, with firmness in the right as God gives us to see the right.",
    speaker: "Abraham Lincoln",
    context: "Sixteenth President of the United States",
    year: "1865",
    source: "Second Inaugural Address",
    theme: "Common ground",
  },
  {
    id: "lincoln-gettysburg-1863",
    text: "…that government of the people, by the people, for the people, shall not perish from the earth.",
    speaker: "Abraham Lincoln",
    context: "Sixteenth President of the United States",
    year: "1863",
    source: "The Gettysburg Address",
    theme: "Self-government",
  },
  {
    id: "douglass-power-concedes-1857",
    text: "Power concedes nothing without a demand. It never did and it never will.",
    speaker: "Frederick Douglass",
    context: "Abolitionist, formerly enslaved",
    year: "1857",
    source: "West India Emancipation speech, Canandaigua, New York",
    theme: "Power",
  },
  {
    id: "anthony-we-the-whole-people-1873",
    text: "It was we, the people; not we, the white male citizens; nor yet we, the male citizens; but we, the whole people, who formed the Union.",
    speaker: "Susan B. Anthony",
    context: "On trial for the crime of voting",
    year: "1873",
    source: "Is It a Crime for a Citizen of the United States to Vote?",
    theme: "Justice",
  },
  {
    id: "stanton-sentiments-1848",
    text: "We hold these truths to be self-evident: that all men and women are created equal.",
    speaker: "Elizabeth Cady Stanton",
    context: "Organizer of the Seneca Falls Convention",
    year: "1848",
    source: "Declaration of Sentiments",
    theme: "Justice",
  },
  {
    id: "mill-knows-only-his-own-side-1859",
    text: "He who knows only his own side of the case knows little of that.",
    speaker: "John Stuart Mill",
    context: "Philosopher, later Member of Parliament",
    year: "1859",
    source: "On Liberty, Chapter II",
    theme: "Disagreement",
  },
  {
    id: "mill-silencing-1859",
    text: "The peculiar evil of silencing the expression of an opinion is that it is robbing the human race; posterity as well as the existing generation.",
    speaker: "John Stuart Mill",
    context: "Philosopher, later Member of Parliament",
    year: "1859",
    source: "On Liberty, Chapter II",
    theme: "Dissent",
  },
  {
    id: "tocqueville-associations-1840",
    text: "Americans of all ages, all conditions, and all dispositions constantly form associations.",
    speaker: "Alexis de Tocqueville",
    context: "French observer of American democracy",
    year: "1840",
    source: "Democracy in America, Volume II",
    theme: "Participation",
  },
  {
    id: "wells-light-of-truth-1892",
    text: "The way to right wrongs is to turn the light of truth upon them.",
    speaker: "Ida B. Wells",
    context: "Journalist and anti-lynching campaigner",
    year: "1892",
    source: "Southern Horrors: Lynch Law in All Its Phases",
    theme: "Truth",
  },
  {
    id: "lazarus-new-colossus-1883",
    text: "Give me your tired, your poor, your huddled masses yearning to breathe free.",
    speaker: "Emma Lazarus",
    context: "Poet",
    year: "1883",
    source: "The New Colossus",
    theme: "Justice",
  },
  {
    id: "dubois-cost-of-liberty-1909",
    text: "The cost of liberty is less than the price of repression.",
    speaker: "W. E. B. Du Bois",
    context: "Scholar and co-founder of the NAACP",
    year: "1909",
    source: "John Brown",
    theme: "Liberty",
  },
  {
    id: "booker-washington-hold-a-man-down-1911",
    text: "You can't hold a man down without staying down with him.",
    speaker: "Booker T. Washington",
    context: "Educator and founder of the Tuskegee Institute",
    year: "1911",
    source: "My Larger Education",
    theme: "Justice",
  },

  // ── Progressive era and the interwar years ──────────────────────────────────
  {
    id: "roosevelt-t-the-arena-1910",
    text: "It is not the critic who counts. The credit belongs to the man who is actually in the arena, whose face is marred by dust and sweat and blood.",
    speaker: "Theodore Roosevelt",
    context: "Former President of the United States",
    year: "1910",
    source: "Citizenship in a Republic, delivered at the Sorbonne",
    theme: "Participation",
  },
  {
    id: "roosevelt-t-criticism-1918",
    text: "To announce that there must be no criticism of the President, or that we are to stand by the President right or wrong, is not only unpatriotic and servile, but is morally treasonable to the American public.",
    speaker: "Theodore Roosevelt",
    context: "Former President of the United States",
    year: "1918",
    source: "Editorial in the Kansas City Star",
    theme: "Dissent",
  },
  {
    id: "wilson-limitation-of-power-1912",
    text: "The history of liberty is a history of the limitation of governmental power, not the increase of it.",
    speaker: "Woodrow Wilson",
    context: "Governor of New Jersey and presidential candidate",
    year: "1912",
    source: "Address to the New York Press Club",
    theme: "Liberty",
  },
  {
    id: "brandeis-sunlight-1914",
    text: "Sunlight is said to be the best of disinfectants; electric light the most efficient policeman.",
    speaker: "Louis Brandeis",
    context: "Attorney, later Justice of the Supreme Court",
    year: "1914",
    source: "Other People's Money and How the Bankers Use It",
    theme: "Truth",
  },
  {
    id: "brandeis-inert-people-1927",
    text: "Those who won our independence believed that the greatest menace to freedom is an inert people; that public discussion is a political duty.",
    speaker: "Louis Brandeis",
    context: "Justice of the Supreme Court",
    year: "1927",
    source: "Concurrence, Whitney v. California",
    theme: "Participation",
  },
  {
    id: "brandeis-men-of-zeal-1928",
    text: "The greatest dangers to liberty lurk in insidious encroachment by men of zeal, well-meaning but without understanding.",
    speaker: "Louis Brandeis",
    context: "Justice of the Supreme Court",
    year: "1928",
    source: "Dissent, Olmstead v. United States",
    theme: "Liberty",
  },
  {
    id: "holmes-competition-of-the-market-1919",
    text: "The best test of truth is the power of the thought to get itself accepted in the competition of the market.",
    speaker: "Oliver Wendell Holmes Jr.",
    context: "Justice of the Supreme Court",
    year: "1919",
    source: "Dissent, Abrams v. United States",
    theme: "Truth",
  },
  {
    id: "addams-more-democracy-1910",
    text: "The cure for the ills of Democracy is more Democracy.",
    speaker: "Jane Addams",
    context: "Founder of Hull House",
    year: "1910",
    source: "Twenty Years at Hull-House",
    theme: "Self-government",
  },
  {
    id: "mencken-good-and-hard-1916",
    text: "Democracy is the theory that the common people know what they want, and deserve to get it good and hard.",
    speaker: "H. L. Mencken",
    context: "Newspaperman and critic",
    year: "1916",
    source: "A Little Book in C Major",
    theme: "Self-government",
  },
  {
    id: "coolidge-kill-bad-bills-1914",
    text: "It is much more important to kill bad bills than to pass good ones.",
    speaker: "Calvin Coolidge",
    context: "President of the Massachusetts Senate, later President",
    year: "1914",
    source: "Have Faith in Massachusetts",
    theme: "Hard choices",
  },
  {
    id: "russell-fools-and-fanatics-1933",
    text: "The whole problem with the world is that fools and fanatics are always so certain of themselves, and wiser people so full of doubts.",
    speaker: "Bertrand Russell",
    context: "Philosopher and mathematician",
    year: "1933",
    source: "The Triumph of Stupidity",
    theme: "Disagreement",
  },
  {
    id: "fdr-fear-itself-1933",
    text: "The only thing we have to fear is fear itself — nameless, unreasoning, unjustified terror.",
    speaker: "Franklin D. Roosevelt",
    context: "Thirty-second President of the United States",
    year: "1933",
    source: "First Inaugural Address",
    theme: "Courage",
  },
  {
    id: "fdr-four-freedoms-1941",
    text: "We look forward to a world founded upon four essential human freedoms: freedom of speech, freedom of worship, freedom from want, freedom from fear.",
    speaker: "Franklin D. Roosevelt",
    context: "Thirty-second President of the United States",
    year: "1941",
    source: "Annual Message to Congress (the Four Freedoms)",
    theme: "Liberty",
  },
  {
    id: "hoover-older-men-declare-war-1944",
    text: "Older men declare war. But it is youth that must fight and die.",
    speaker: "Herbert Hoover",
    context: "Former President of the United States",
    year: "1944",
    source: "Address to the Republican National Convention",
    theme: "Power",
  },

  // ── The mid-century courts and the fight over what a state may compel ───────
  {
    id: "jackson-fixed-star-1943",
    text: "If there is any fixed star in our constitutional constellation, it is that no official, high or petty, can prescribe what shall be orthodox in politics, nationalism, religion, or other matters of opinion.",
    speaker: "Robert H. Jackson",
    context: "Justice of the Supreme Court",
    year: "1943",
    source: "Opinion of the Court, West Virginia v. Barnette",
    theme: "Dissent",
  },
  {
    id: "jackson-not-final-because-infallible-1953",
    text: "We are not final because we are infallible, but we are infallible only because we are final.",
    speaker: "Robert H. Jackson",
    context: "Justice of the Supreme Court",
    year: "1953",
    source: "Concurrence, Brown v. Allen",
    theme: "Power",
  },
  {
    id: "frankfurter-procedural-safeguards-1943",
    text: "The history of liberty has largely been the history of the observance of procedural safeguards.",
    speaker: "Felix Frankfurter",
    context: "Justice of the Supreme Court",
    year: "1943",
    source: "Opinion of the Court, McNabb v. United States",
    theme: "Liberty",
  },
  {
    id: "hand-not-too-sure-1944",
    text: "The spirit of liberty is the spirit which is not too sure that it is right.",
    speaker: "Learned Hand",
    context: "Judge of the U.S. Court of Appeals",
    year: "1944",
    source: "The Spirit of Liberty, Central Park, New York",
    theme: "Disagreement",
  },
  {
    id: "hand-liberty-lies-in-hearts-1944",
    text: "Liberty lies in the hearts of men and women; when it dies there, no constitution, no law, no court can save it.",
    speaker: "Learned Hand",
    context: "Judge of the U.S. Court of Appeals",
    year: "1944",
    source: "The Spirit of Liberty, Central Park, New York",
    theme: "Liberty",
  },
  {
    id: "warren-separate-but-equal-1954",
    text: "In the field of public education the doctrine of 'separate but equal' has no place.",
    speaker: "Earl Warren",
    context: "Chief Justice of the United States",
    year: "1954",
    source: "Opinion of the Court, Brown v. Board of Education",
    theme: "Justice",
  },
  {
    id: "black-free-press-1971",
    text: "The Founding Fathers gave the free press the protection it must have to fulfill its essential role in our democracy.",
    speaker: "Hugo Black",
    context: "Justice of the Supreme Court",
    year: "1971",
    source: "Concurrence, New York Times Co. v. United States",
    theme: "Truth",
  },

  // ── Mid-century argument about facts, power, and language ──────────────────
  {
    id: "orwell-right-to-tell-people-1945",
    text: "If liberty means anything at all, it means the right to tell people what they do not want to hear.",
    speaker: "George Orwell",
    context: "Novelist and essayist",
    year: "1945",
    source: "Unpublished preface to Animal Farm",
    theme: "Dissent",
  },
  {
    id: "orwell-insincerity-1946",
    text: "The great enemy of clear language is insincerity.",
    speaker: "George Orwell",
    context: "Novelist and essayist",
    year: "1946",
    source: "Politics and the English Language",
    theme: "Truth",
  },
  {
    id: "arendt-fact-and-fiction-1951",
    text: "The ideal subject of totalitarian rule is not the convinced Nazi or the convinced Communist, but people for whom the distinction between fact and fiction and the distinction between true and false no longer exist.",
    speaker: "Hannah Arendt",
    context: "Political theorist",
    year: "1951",
    source: "The Origins of Totalitarianism",
    theme: "Truth",
  },
  {
    id: "popper-tolerance-1945",
    text: "Unlimited tolerance must lead to the disappearance of tolerance.",
    speaker: "Karl Popper",
    context: "Philosopher of science",
    year: "1945",
    source: "The Open Society and Its Enemies",
    theme: "Disagreement",
  },
  {
    id: "berlin-freedom-for-the-wolves-1969",
    text: "Freedom for the wolves has often meant death to the sheep.",
    speaker: "Isaiah Berlin",
    context: "Philosopher of political ideas",
    year: "1969",
    source: "Four Essays on Liberty",
    theme: "Liberty",
  },
  {
    id: "churchill-worst-form-1947",
    text: "Democracy is the worst form of Government except for all those other forms that have been tried from time to time.",
    speaker: "Winston Churchill",
    context: "Member of Parliament, former Prime Minister",
    year: "1947",
    source: "Speech in the House of Commons",
    theme: "Self-government",
  },
  {
    id: "wiesel-indifference-1986",
    text: "The opposite of love is not hate, it's indifference.",
    speaker: "Elie Wiesel",
    context: "Writer and survivor of Auschwitz",
    year: "1986",
    source: "Interview, U.S. News & World Report",
    theme: "Participation",
  },

  // ── American politics, 1945–1980 ────────────────────────────────────────────
  {
    id: "truman-buck-stops-here-1949",
    text: "The buck stops here.",
    speaker: "Harry S. Truman",
    context: "Thirty-third President of the United States",
    year: "1949",
    source: "Sign on his desk in the Oval Office",
    theme: "Power",
  },
  {
    id: "smith-four-horsemen-1950",
    text: "I don't want to see the Republican Party ride to political victory on the Four Horsemen of Calumny — fear, ignorance, bigotry, and smear.",
    speaker: "Margaret Chase Smith",
    context: "U.S. Senator from Maine",
    year: "1950",
    source: "Declaration of Conscience, U.S. Senate",
    theme: "Courage",
  },
  {
    id: "stevenson-safe-to-be-unpopular-1952",
    text: "A free society is a society where it is safe to be unpopular.",
    speaker: "Adlai Stevenson",
    context: "Governor of Illinois and presidential nominee",
    year: "1952",
    source: "Campaign speech, Detroit",
    theme: "Dissent",
  },
  {
    id: "eisenhower-theft-1953",
    text: "Every gun that is made, every warship launched, every rocket fired signifies, in the final sense, a theft from those who hunger and are not fed, those who are cold and are not clothed.",
    speaker: "Dwight D. Eisenhower",
    context: "Thirty-fourth President of the United States",
    year: "1953",
    source: "The Chance for Peace, American Society of Newspaper Editors",
    theme: "Hard choices",
  },
  {
    id: "eisenhower-military-industrial-1961",
    text: "In the councils of government, we must guard against the acquisition of unwarranted influence, whether sought or unsought, by the military-industrial complex.",
    speaker: "Dwight D. Eisenhower",
    context: "Thirty-fourth President of the United States",
    year: "1961",
    source: "Farewell Address to the Nation",
    theme: "Power",
  },
  {
    id: "jfk-ask-not-1961",
    text: "Ask not what your country can do for you — ask what you can do for your country.",
    speaker: "John F. Kennedy",
    context: "Thirty-fifth President of the United States",
    year: "1961",
    source: "Inaugural Address",
    theme: "Participation",
  },
  {
    id: "jfk-never-fear-to-negotiate-1961",
    text: "Let us never negotiate out of fear. But let us never fear to negotiate.",
    speaker: "John F. Kennedy",
    context: "Thirty-fifth President of the United States",
    year: "1961",
    source: "Inaugural Address",
    theme: "Common ground",
  },
  {
    id: "galbraith-disastrous-and-unpalatable-1962",
    text: "Politics is not the art of the possible. It consists in choosing between the disastrous and the unpalatable.",
    speaker: "John Kenneth Galbraith",
    context: "Economist and U.S. Ambassador to India",
    year: "1962",
    source: "Letter to President Kennedy",
    theme: "Hard choices",
  },
  {
    id: "buckley-boston-directory-1963",
    text: "I would rather be governed by the first 2,000 people in the Boston telephone directory than by the 2,000 people on the faculty of Harvard University.",
    speaker: "William F. Buckley Jr.",
    context: "Founder of National Review",
    year: "1963",
    source: "Rumbles Left and Right",
    theme: "Self-government",
  },
  {
    id: "carson-right-to-know-1962",
    text: "The obligation to endure gives us the right to know.",
    speaker: "Rachel Carson",
    context: "Marine biologist and author",
    year: "1962",
    source: "Silent Spring",
    theme: "Truth",
  },
  {
    id: "lbj-we-shall-overcome-1965",
    text: "It is not just Negroes, but really it is all of us, who must overcome the crippling legacy of bigotry and injustice. And we shall overcome.",
    speaker: "Lyndon B. Johnson",
    context: "Thirty-sixth President of the United States",
    year: "1965",
    source: "Special Message to Congress on voting rights",
    theme: "Justice",
  },
  {
    id: "rfk-ripple-of-hope-1966",
    text: "Each time a man stands up for an ideal, or acts to improve the lot of others, or strikes out against injustice, he sends forth a tiny ripple of hope.",
    speaker: "Robert F. Kennedy",
    context: "U.S. Senator from New York",
    year: "1966",
    source: "Day of Affirmation Address, University of Cape Town",
    theme: "Participation",
  },
  {
    id: "humphrey-moral-test-1977",
    text: "The moral test of government is how it treats those who are in the dawn of life, the children; those who are in the twilight of life, the elderly; and those who are in the shadows of life, the sick, the needy, and the handicapped.",
    speaker: "Hubert H. Humphrey",
    context: "Former Vice President and U.S. Senator",
    year: "1977",
    source: "Remarks at the dedication of the Humphrey Building",
    theme: "Justice",
  },
  {
    id: "rayburn-carpenter",
    text: "Any jackass can kick down a barn, but it takes a good carpenter to build one.",
    speaker: "Sam Rayburn",
    context: "Speaker of the U.S. House of Representatives",
    year: "c. 1950",
    source: "Maxim recalled by House colleagues",
    theme: "Hard choices",
  },
  {
    id: "oneill-all-politics-is-local",
    text: "All politics is local.",
    speaker: "Thomas P. 'Tip' O'Neill Jr.",
    context: "Speaker of the U.S. House of Representatives",
    year: "1982",
    source: "Campaign maxim he made famous",
    theme: "Participation",
  },

  // ── Civil rights and the movements that followed ────────────────────────────
  {
    id: "king-injustice-anywhere-1963",
    text: "Injustice anywhere is a threat to justice everywhere.",
    speaker: "Martin Luther King Jr.",
    context: "President of the Southern Christian Leadership Conference",
    year: "1963",
    source: "Letter from Birmingham Jail",
    theme: "Justice",
  },
  {
    id: "king-moral-arc-1965",
    text: "The arc of the moral universe is long, but it bends toward justice.",
    speaker: "Martin Luther King Jr.",
    context: "President of the Southern Christian Leadership Conference",
    year: "1965",
    source: "Address at the conclusion of the Selma to Montgomery march",
    theme: "Justice",
  },
  {
    id: "king-silent-about-things-that-matter-1965",
    text: "Our lives begin to end the day we become silent about things that matter.",
    speaker: "Martin Luther King Jr.",
    context: "President of the Southern Christian Leadership Conference",
    year: "1965",
    source: "Speech in Selma, Alabama",
    theme: "Courage",
  },
  {
    id: "baldwin-insist-on-the-right-to-criticize-1955",
    text: "I love America more than any other country in the world and, exactly for this reason, I insist on the right to criticize her perpetually.",
    speaker: "James Baldwin",
    context: "Novelist and essayist",
    year: "1955",
    source: "Notes of a Native Son",
    theme: "Dissent",
  },
  {
    id: "baldwin-nothing-changed-until-faced-1962",
    text: "Not everything that is faced can be changed, but nothing can be changed until it is faced.",
    speaker: "James Baldwin",
    context: "Novelist and essayist",
    year: "1962",
    source: "As Much Truth As One Can Bear, The New York Times",
    theme: "Truth",
  },
  {
    id: "malcolm-x-for-truth-1965",
    text: "I'm for truth, no matter who tells it. I'm for justice, no matter who it is for or against.",
    speaker: "Malcolm X",
    context: "Minister and human rights activist",
    year: "1965",
    source: "Recorded interview",
    theme: "Truth",
  },
  {
    id: "hamer-sick-and-tired-1964",
    text: "I'm sick and tired of being sick and tired.",
    speaker: "Fannie Lou Hamer",
    context: "Vice-chair of the Mississippi Freedom Democratic Party",
    year: "1964",
    source: "Speech in Harlem, New York",
    theme: "Courage",
  },
  {
    id: "rustin-angelic-troublemakers",
    text: "We need in every community a group of angelic troublemakers.",
    speaker: "Bayard Rustin",
    context: "Chief organizer of the March on Washington",
    year: "1963",
    source: "Remarks on nonviolent organizing",
    theme: "Courage",
  },
  {
    id: "parks-mind-made-up-1992",
    text: "I have learned over the years that when one's mind is made up, this diminishes fear.",
    speaker: "Rosa Parks",
    context: "Civil rights activist",
    year: "1992",
    source: "Rosa Parks: My Story",
    theme: "Courage",
  },
  {
    id: "chisholm-folding-chair-1972",
    text: "If they don't give you a seat at the table, bring a folding chair.",
    speaker: "Shirley Chisholm",
    context: "First Black woman elected to the U.S. Congress",
    year: "1972",
    source: "Remark during her presidential campaign",
    theme: "Participation",
  },
  {
    id: "jordan-faith-in-the-constitution-1974",
    text: "My faith in the Constitution is whole; it is complete; it is total.",
    speaker: "Barbara Jordan",
    context: "U.S. Representative from Texas",
    year: "1974",
    source: "Statement to the House Judiciary Committee on impeachment",
    theme: "Courage",
  },
  {
    id: "jordan-national-community-1976",
    text: "We are a people in search of a national community.",
    speaker: "Barbara Jordan",
    context: "U.S. Representative from Texas",
    year: "1976",
    source: "Keynote address, Democratic National Convention",
    theme: "Common ground",
  },
  {
    id: "marshall-defective-from-the-start-1987",
    text: "The government they devised was defective from the start, requiring several amendments, a civil war, and momentous social transformation to attain the system of constitutional government we hold as fundamental today.",
    speaker: "Thurgood Marshall",
    context: "Justice of the Supreme Court",
    year: "1987",
    source: "Remarks on the Bicentennial of the Constitution",
    theme: "Justice",
  },
  {
    id: "milk-hope-will-never-be-silent-1978",
    text: "Hope will never be silent.",
    speaker: "Harvey Milk",
    context: "San Francisco City Supervisor",
    year: "1978",
    source: "Speech at Gay Freedom Day, San Francisco",
    theme: "Courage",
  },
  {
    id: "chavez-community-1984",
    text: "We cannot seek achievement for ourselves and forget about progress and prosperity for our community.",
    speaker: "Cesar Chavez",
    context: "Co-founder of the United Farm Workers",
    year: "1984",
    source: "Address to the Commonwealth Club of California",
    theme: "Common ground",
  },
  {
    id: "lewis-good-trouble-2020",
    text: "Get in good trouble, necessary trouble, and help redeem the soul of America.",
    speaker: "John Lewis",
    context: "U.S. Representative from Georgia",
    year: "2020",
    source: "Final essay, published in The New York Times",
    theme: "Courage",
  },

  // ── Voices from outside the United States ───────────────────────────────────
  {
    id: "havel-hope-1986",
    text: "Hope is not the conviction that something will turn out well, but the certainty that something makes sense, regardless of how it turns out.",
    speaker: "Václav Havel",
    context: "Dissident playwright, later President of Czechoslovakia",
    year: "1986",
    source: "Disturbing the Peace",
    theme: "Courage",
  },
  {
    id: "mandela-education-2003",
    text: "Education is the most powerful weapon which you can use to change the world.",
    speaker: "Nelson Mandela",
    context: "Former President of South Africa",
    year: "2003",
    source: "Speech at the University of the Witwatersrand",
    theme: "Participation",
  },
  {
    id: "tutu-neutral-1984",
    text: "If you are neutral in situations of injustice, you have chosen the side of the oppressor.",
    speaker: "Desmond Tutu",
    context: "Anglican archbishop and anti-apartheid leader",
    year: "1984",
    source: "Remarks on apartheid",
    theme: "Courage",
  },
  {
    id: "sen-famine-1999",
    text: "No famine has ever taken place in the history of the world in a functioning democracy.",
    speaker: "Amartya Sen",
    context: "Economist and Nobel laureate",
    year: "1999",
    source: "Development as Freedom",
    theme: "Self-government",
  },

  // ── Attention, evidence, and the information age ────────────────────────────
  {
    id: "postman-orwell-and-huxley-1985",
    text: "What Orwell feared were those who would ban books. What Huxley feared was that there would be no reason to ban a book, for there would be no one who wanted to read one.",
    speaker: "Neil Postman",
    context: "Media theorist",
    year: "1985",
    source: "Amusing Ourselves to Death",
    theme: "Truth",
  },
  {
    id: "sagan-prescription-for-disaster-1995",
    text: "We've arranged a global civilization in which most crucial elements profoundly depend on science and technology. We have also arranged things so that almost no one understands science and technology. This is a prescription for disaster.",
    speaker: "Carl Sagan",
    context: "Astronomer and science writer",
    year: "1995",
    source: "The Demon-Haunted World",
    theme: "Truth",
  },
  {
    id: "moynihan-own-facts",
    text: "Everyone is entitled to his own opinion, but not to his own facts.",
    speaker: "Daniel Patrick Moynihan",
    context: "U.S. Senator from New York",
    year: "1983",
    source: "Maxim he repeated throughout his Senate career",
    theme: "Truth",
  },
  {
    id: "powell-bad-news-isnt-wine",
    text: "Bad news isn't wine. It doesn't improve with age.",
    speaker: "Colin Powell",
    context: "Chairman of the Joint Chiefs of Staff, later Secretary of State",
    year: "1989",
    source: "From his list of leadership rules",
    theme: "Truth",
  },

  // ── Trade-offs and the limits of policy ─────────────────────────────────────
  {
    id: "sowell-no-solutions-only-tradeoffs-1987",
    text: "There are no solutions. There are only trade-offs.",
    speaker: "Thomas Sowell",
    context: "Economist and social theorist",
    year: "1987",
    source: "A Conflict of Visions",
    theme: "Hard choices",
  },
  {
    id: "friedman-temporary-program",
    text: "Nothing is so permanent as a temporary government program.",
    speaker: "Milton Friedman",
    context: "Economist and Nobel laureate",
    year: "1984",
    source: "Maxim he repeated in lectures and columns",
    theme: "Hard choices",
  },

  // ── Recent decades ──────────────────────────────────────────────────────────
  {
    id: "reagan-one-generation-away-1961",
    text: "Freedom is never more than one generation away from extinction.",
    speaker: "Ronald Reagan",
    context: "Public speaker, later Fortieth President",
    year: "1961",
    source: "Address, 'Encroaching Control'",
    theme: "Liberty",
  },
  {
    id: "reagan-trust-but-verify-1987",
    text: "Trust, but verify.",
    speaker: "Ronald Reagan",
    context: "Fortieth President of the United States",
    year: "1987",
    source: "Remarks at the INF Treaty signing",
    theme: "Truth",
  },
  {
    id: "ford-truth-is-the-glue-1974",
    text: "Truth is the glue that holds government together.",
    speaker: "Gerald R. Ford",
    context: "Thirty-eighth President of the United States",
    year: "1974",
    source: "Remarks on taking the oath of office",
    theme: "Truth",
  },
  {
    id: "carter-beautiful-mosaic-1976",
    text: "We become not a melting pot but a beautiful mosaic.",
    speaker: "Jimmy Carter",
    context: "Thirty-ninth President of the United States",
    year: "1976",
    source: "Campaign remarks",
    theme: "Common ground",
  },
  {
    id: "clinton-nothing-wrong-with-america-1993",
    text: "There is nothing wrong with America that cannot be cured by what is right with America.",
    speaker: "Bill Clinton",
    context: "Forty-second President of the United States",
    year: "1993",
    source: "First Inaugural Address",
    theme: "Common ground",
  },
  {
    id: "oconnor-blank-check-2004",
    text: "A state of war is not a blank check for the President when it comes to the rights of the Nation's citizens.",
    speaker: "Sandra Day O'Connor",
    context: "Justice of the Supreme Court",
    year: "2004",
    source: "Plurality opinion, Hamdi v. Rumsfeld",
    theme: "Power",
  },
  {
    id: "oconnor-we-must-learn-democracy-2012",
    text: "We don't inherit our democracy. We must learn it.",
    speaker: "Sandra Day O'Connor",
    context: "Retired Justice and founder of iCivics",
    year: "2012",
    source: "Remarks on civic education",
    theme: "Self-government",
  },
  {
    id: "bush-face-of-terror-2001",
    text: "The face of terror is not the true faith of Islam. That's not what Islam is all about.",
    speaker: "George W. Bush",
    context: "Forty-third President of the United States",
    year: "2001",
    source: "Remarks at the Islamic Center of Washington",
    theme: "Common ground",
  },
  {
    id: "bush-outright-fabrication-2017",
    text: "Bigotry seems emboldened. Our politics seems more vulnerable to conspiracy theories and outright fabrication.",
    speaker: "George W. Bush",
    context: "Former President of the United States",
    year: "2017",
    source: "Remarks at the Bush Institute, New York",
    theme: "Truth",
  },
  {
    id: "obama-not-a-liberal-america-2004",
    text: "There's not a liberal America and a conservative America; there's the United States of America.",
    speaker: "Barack Obama",
    context: "Candidate for the U.S. Senate from Illinois",
    year: "2004",
    source: "Keynote address, Democratic National Convention",
    theme: "Common ground",
  },
  {
    id: "obama-solidarity-2017",
    text: "Democracy does require a basic sense of solidarity — the idea that for all our outward differences, we're all in this together.",
    speaker: "Barack Obama",
    context: "Forty-fourth President of the United States",
    year: "2017",
    source: "Farewell Address, Chicago",
    theme: "Common ground",
  },
  {
    id: "michelle-obama-go-high-2016",
    text: "When they go low, we go high.",
    speaker: "Michelle Obama",
    context: "First Lady of the United States",
    year: "2016",
    source: "Address, Democratic National Convention",
    theme: "Courage",
  },
  {
    id: "mccain-regular-order-2017",
    text: "Let's trust each other. Let's return to regular order.",
    speaker: "John McCain",
    context: "U.S. Senator from Arizona",
    year: "2017",
    source: "Speech on the Senate floor",
    theme: "Common ground",
  },
  {
    id: "mccain-tribal-rivalries-2018",
    text: "We weaken our greatness when we confuse our patriotism with tribal rivalries.",
    speaker: "John McCain",
    context: "U.S. Senator from Arizona",
    year: "2018",
    source: "Farewell statement",
    theme: "Disagreement",
  },
  {
    id: "scalia-legal-document-2005",
    text: "The Constitution is not a living organism; it is a legal document. It says something, and doesn't say other things.",
    speaker: "Antonin Scalia",
    context: "Justice of the Supreme Court",
    year: "2005",
    source: "Remarks at the Woodrow Wilson Center",
    theme: "Power",
  },
  {
    id: "ginsburg-dissents-speak-to-a-future-age-2002",
    text: "Dissents speak to a future age.",
    speaker: "Ruth Bader Ginsburg",
    context: "Justice of the Supreme Court",
    year: "2002",
    source: "Interview with NPR",
    theme: "Dissent",
  },
  {
    id: "ginsburg-lead-others-to-join-2015",
    text: "Fight for the things that you care about, but do it in a way that will lead others to join you.",
    speaker: "Ruth Bader Ginsburg",
    context: "Justice of the Supreme Court",
    year: "2015",
    source: "Remarks at Radcliffe Day, Harvard",
    theme: "Common ground",
  },
  {
    id: "roberts-umpires-2005",
    text: "Judges are like umpires. Umpires don't make the rules; they apply them.",
    speaker: "John G. Roberts Jr.",
    context: "Nominee for Chief Justice of the United States",
    year: "2005",
    source: "Opening statement, Senate confirmation hearing",
    theme: "Power",
  },
  {
    id: "kagan-all-textualists-now-2015",
    text: "We're all textualists now.",
    speaker: "Elena Kagan",
    context: "Justice of the Supreme Court",
    year: "2015",
    source: "The Scalia Lecture, Harvard Law School",
    theme: "Disagreement",
  },
  {
    id: "kennedy-equal-dignity-2015",
    text: "They ask for equal dignity in the eyes of the law. The Constitution grants them that right.",
    speaker: "Anthony M. Kennedy",
    context: "Justice of the Supreme Court",
    year: "2015",
    source: "Opinion of the Court, Obergefell v. Hodges",
    theme: "Justice",
  },
];

// ─────────────────────────────────────────────────────────────────────────────
// Selection
//
// Every surface reads the library through a stride walk rather than a random pick.
// Stepping by a number coprime with the library's length visits all N quotes before
// repeating any of them, so "fresh quotes frequently" is a property of the traversal
// instead of a hope about a random number generator. Random picking over 120 entries
// gives a visible repeat roughly every dozen draws (birthday problem); a stride walk
// gives none for a full cycle.
// ─────────────────────────────────────────────────────────────────────────────

function gcd(a: number, b: number): number {
  while (b) [a, b] = [b, a % b];
  return a;
}

/**
 * The largest-ish step that still visits every quote exactly once per cycle. Starting
 * the search near len/3 keeps consecutive quotes far apart in the array, so a reader
 * who sees two in a row doesn't get two Madisons. Computed from the live length, so
 * adding or removing quotes can't silently break the full-cycle guarantee.
 */
const STRIDE = (() => {
  const len = CIVIC_QUOTES.length;
  for (let s = Math.floor(len / 3); s < len; s++) if (gcd(s, len) === 1) return s;
  return 1;
})();

/** The quote `steps` places along the walk from `start`. Wraps; negative-safe. */
function walk(start: number, steps: number): CivicQuote {
  const len = CIVIC_QUOTES.length;
  const i = (((start + steps * STRIDE) % len) + len) % len;
  return CIVIC_QUOTES[i];
}

/**
 * Where this page load starts its rotation. Drawn once per session so the home feels
 * different on each visit but holds still while you're reading it — the same reason
 * the Shorts mixer seeds its RNG once instead of calling Math.random per card.
 */
const SESSION_START = Math.floor(Math.random() * CIVIC_QUOTES.length);

/**
 * One quote per calendar day, stable for that day in the reader's own timezone.
 * The footer renders on every page, so this must not change as you navigate — a
 * footer that reshuffles under you reads as a bug, not as freshness.
 *
 * `maxLength` walks forward to the next quote that fits rather than truncating:
 * a clipped quotation is a misquotation. The footer sets it because it has one line.
 */
export function quoteOfDay(opts: { date?: Date; maxLength?: number } = {}): CivicQuote {
  const { date = new Date(), maxLength } = opts;
  // Local Y/M/D through Date.UTC gives a day number that ticks over at the reader's
  // midnight, not UTC's.
  const day = Math.floor(
    Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()) / 86_400_000,
  );
  if (!maxLength) return walk(0, day);

  // Bounded by the library size: if nothing fits we return the day's quote anyway
  // rather than looping, and the surface deals with it.
  for (let i = 0; i < CIVIC_QUOTES.length; i++) {
    const q = walk(0, day + i);
    if (q.text.length <= maxLength) return q;
  }
  return walk(0, day);
}

/**
 * The quote for rotation slot `seed`, offset by this session's start. Consecutive
 * seeds give consecutive walk steps, so each time the carousel comes back around to
 * the quote card it shows a new one.
 */
export function rotatingQuote(seed: number, start = SESSION_START): CivicQuote {
  return walk(start, seed);
}

/**
 * `count` distinct quotes for a feed pool, starting somewhere different each session.
 * Capped at the library size so a caller can't ask for repeats.
 */
export function sessionQuotes(count: number, start = SESSION_START): CivicQuote[] {
  const n = Math.min(count, CIVIC_QUOTES.length);
  return Array.from({ length: n }, (_, i) => walk(start, i));
}
