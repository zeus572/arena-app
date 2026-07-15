import { Link } from "react-router-dom";
import LegalDoc from "./LegalDoc";
import {
  LEGAL_ENTITY,
  LEGAL_ADDRESS,
  LEGAL_CONTACT_EMAIL,
  MINIMUM_AGE,
} from "@/lib/legal";

export default function Privacy() {
  return (
    <LegalDoc
      testid="privacy-page"
      kicker="Privacy"
      title="Privacy Policy"
      lead={
        <>
          This policy explains what {LEGAL_ENTITY} (“we,” “us”) collects when you
          use Civersify and the Debate Arena, why we collect it, and the choices
          you have. We try to collect as little as we can to run the product.
        </>
      }
      sections={[
        {
          heading: "Information you give us",
          body: (
            <>
              <p>When you create an account we collect:</p>
              <ul className="list-disc pl-5">
                <li>
                  <strong>Email address</strong> — to sign you in, verify your
                  account, and send transactional messages (verification and
                  password reset).
                </li>
                <li>
                  <strong>Date of birth</strong> — to confirm you meet our
                  minimum age of {MINIMUM_AGE} and to tailor age-appropriate
                  experiences. We store your date of birth; we do not display it
                  publicly.
                </li>
                <li>
                  <strong>ZIP code</strong> (Civersify) — to surface the races
                  and local stories relevant to where you live. You can change or
                  remove this in your profile.
                </li>
                <li>
                  <strong>Display name and profile choices</strong> — shown with
                  your contributions.
                </li>
              </ul>
            </>
          ),
        },
        {
          heading: "Information we collect automatically",
          body: (
            <p>
              We collect basic, privacy-light usage analytics (for example, which
              pages are viewed and aggregate performance and error data) to keep
              the service working and understand what’s useful. We aim to keep
              this minimal and do not sell it. See{" "}
              <em>Cookies &amp; analytics</em> below for your choices.
            </p>
          ),
        },
        {
          heading: "Children under " + MINIMUM_AGE,
          body: (
            <p>
              Civersify and the Debate Arena are not directed to children under{" "}
              {MINIMUM_AGE}, and we do not knowingly create accounts for or
              collect personal information from them. We ask for date of birth at
              sign-up and block accounts for anyone under {MINIMUM_AGE}. If you
              believe a child under {MINIMUM_AGE} has provided us personal
              information, contact us at {LEGAL_CONTACT_EMAIL} and we will delete
              it.
            </p>
          ),
        },
        {
          heading: "How we use information",
          body: (
            <p>
              We use the information above to operate and secure the service,
              authenticate you, personalize civic content, enforce our age
              requirement, communicate about your account, and improve the
              product. We do not sell your personal information.
            </p>
          ),
        },
        {
          heading: "Cookies & analytics",
          body: (
            <p>
              We use a small number of essential cookies to keep you signed in,
              and privacy-light analytics to measure usage. Where analytics would
              set non-essential cookies, we ask for your consent first via the
              banner shown on your first visit; you can decline and still use the
              product. Most browsers also let you block or delete cookies.
            </p>
          ),
        },
        {
          heading: "Sharing",
          body: (
            <p>
              We share information only with service providers that help us run
              the product (for example, cloud hosting, email delivery, and
              analytics), bound to protect it, and where required by law. We may
              share aggregated or de-identified information that cannot reasonably
              identify you.
            </p>
          ),
        },
        {
          heading: "Your choices and rights",
          body: (
            <p>
              You can edit your profile, change or remove your ZIP code, and
              request deletion of your account by contacting{" "}
              {LEGAL_CONTACT_EMAIL}. Depending on where you live, you may have
              additional rights to access, correct, or delete your personal
              information; we honor applicable requests.
            </p>
          ),
        },
        {
          heading: "Data retention & security",
          body: (
            <p>
              We keep personal information for as long as your account is active
              or as needed to provide the service and meet legal obligations, then
              delete or de-identify it. We use reasonable technical and
              organizational measures to protect it, but no method of transmission
              or storage is perfectly secure.
            </p>
          ),
        },
        {
          heading: "Changes to this policy",
          body: (
            <p>
              We may update this policy as the product evolves. When we make
              material changes we will update the effective date above and, where
              appropriate, notify you.
            </p>
          ),
        },
        {
          heading: "Contact",
          body: (
            <p>
              Questions? Email {LEGAL_CONTACT_EMAIL} or write to {LEGAL_ENTITY},{" "}
              {LEGAL_ADDRESS}. See also our{" "}
              <Link to="/terms" className="text-[var(--accent)] underline">
                Terms of Service
              </Link>{" "}
              and{" "}
              <Link to="/eula" className="text-[var(--accent)] underline">
                End User License Agreement
              </Link>
              .
            </p>
          ),
        },
      ]}
    />
  );
}
